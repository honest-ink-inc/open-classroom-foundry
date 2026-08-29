// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// The Press Room catalog (second forge menu, item 1): every press engine
// described as typed parameters so any surface can generate its form. This IS
// the governing invariant made into data — a press whose input cannot be
// expressed here does not belong in this module. Labels are neutral English;
// chrome localization wraps them at the surface.

public abstract record PressParameter(string Key, string Label);

/// <summary>A bounded number; DecimalPlaces 0 means an integer.</summary>
public sealed record NumberParameter(string Key, string Label, double Minimum, double Maximum, double Default, int DecimalPlaces = 0) : PressParameter(Key, Label);

public sealed record ChoiceParameter(string Key, string Label, IReadOnlyList<string> Options, string Default) : PressParameter(Key, Label);

public sealed record ToggleParameter(string Key, string Label, bool Default) : PressParameter(Key, Label);

/// <summary>A teacher-typed list, one entry per line — data placed verbatim, never interpreted.</summary>
public sealed record LinesParameter(string Key, string Label, string DefaultText, bool Optional = false) : PressParameter(Key, Label);

/// <summary>A short teacher-typed value on one line (a comma-separated number list, a seed).</summary>
public sealed record TextParameter(string Key, string Label, string Default) : PressParameter(Key, Label);

/// <summary>The values a surface collected, keyed by parameter, every value a string; typed access parses loudly.</summary>
public sealed class PressInputs(IReadOnlyDictionary<string, string> values)
{
    public string Text(string key) => values.TryGetValue(key, out var value)
        ? value
        : throw new ArgumentException($"Missing parameter '{key}'.", nameof(key));

    public int Whole(string key) => int.TryParse(Text(key), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
        ? parsed
        : throw new ArgumentException($"'{Text(key)}' is not a whole number for '{key}'.", nameof(key));

    public double Mm(string key) => double.TryParse(Text(key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
        ? parsed
        : throw new ArgumentException($"'{Text(key)}' is not a number for '{key}'.", nameof(key));

    public bool Bool(string key) => Text(key) == "true";

    public PageSize Page() => Text(PressRoomCatalog.PageKey) switch
    {
        "A4" => PageSize.A4,
        "Letter landscape" => PageSize.LetterLandscape,
        "A4 landscape" => PageSize.A4Landscape,
        _ => PageSize.Letter,
    };

    public IReadOnlyList<string> Lines(string key) =>
        [.. Text(key).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)];

    /// <summary>Code-bearing lines, verbatim: leading whitespace preserved (indentation IS content), tabs widened to four spaces, blank lines dropped.</summary>
    public IReadOnlyList<string> RawLines(string key) =>
        [.. Text(key).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Select(l => l.Replace("\t", "    ", StringComparison.Ordinal).TrimEnd())
            .Where(l => l.Length > 0)];

    public IReadOnlyList<int> IntList(string key) =>
        [.. Text(key).Split(',').Select(part => int.TryParse(part.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ArgumentException($"'{part.Trim()}' is not a whole number in '{key}'.", nameof(key)))];

    /// <summary>Lines of "left | right"; the right side optional when the press allows it.</summary>
    public IReadOnlyList<(string Left, string? Right)> SplitLines(string key) =>
        [.. Lines(key).Select(line =>
        {
            var parts = line.Split('|', 2);
            return (parts[0].Trim(), parts.Length == 2 ? parts[1].Trim() : null);
        })];
}

public sealed record PressDefinition(
    string Id,
    string Title,
    RecipeManifest Recipe,
    IReadOnlyList<PressParameter> Parameters,
    Func<PressInputs, ArtifactDocument> Build);

public static class PressRoomCatalog
{
    public const string PageKey = "page";

    /// <summary>Constitution 14: the declared time-to-artifact budget, displayed per recipe and measured in pilots.</summary>
    public const int BudgetMinutes = 3;

    private static ChoiceParameter Page(string defaultOption = "Letter")
        => new(PageKey, "Page size", ["Letter", "A4", "Letter landscape", "A4 landscape"], defaultOption);

    private static NumberParameter Seed() => new("seed", "Seed (same seed, same pages)", 1, 99999999, 20260908);

    public static IReadOnlyList<PressDefinition> All { get; } =
    [
        new("calibration-proof", "Calibration & proof sheet", DeterministicPressRecipes.Calibration,
            [Page(), new NumberParameter("margin", "Margin (mm)", 5, 25, 12)],
            inputs => CalibrationPress.ProofPage(inputs.Page(), inputs.Mm("margin"))),

        new("graph-paper", "Graph paper", DeterministicPressRecipes.Blankforms,
            [Page(), new NumberParameter("pitch", "Square size (mm)", 2, 25, 5, 1), new NumberParameter("major", "Heavier line every Nth (0 for none)", 0, 10, 5)],
            inputs => BlankformsPress.GraphPaper(inputs.Page(), inputs.Mm("pitch"), majorEvery: inputs.Whole("major"))),

        new("coordinate-grid", "Coordinate grid", DeterministicPressRecipes.Blankforms,
            [Page(), new NumberParameter("pitch", "Square size (mm)", 2, 25, 10, 1), new ChoiceParameter("quadrants", "Quadrants", ["Four", "First"], "Four")],
            inputs => BlankformsPress.CoordinateGrid(inputs.Page(), inputs.Mm("pitch"),
                quadrants: inputs.Text("quadrants") == "First" ? GridQuadrants.First : GridQuadrants.Four)),

        new("number-line", "Number line", DeterministicPressRecipes.Blankforms,
            [new NumberParameter("from", "From", -100, 100, 0), new NumberParameter("to", "To", -100, 200, 20), new NumberParameter("subdivisions", "Subdivisions per unit", 1, 10, 1), Page()],
            inputs => BlankformsPress.NumberLine(inputs.Whole("from"), inputs.Whole("to"), inputs.Page(), subdivisions: inputs.Whole("subdivisions"))),

        new("ten-frames", "Ten-frames", DeterministicPressRecipes.Blankforms,
            [new NumberParameter("frames", "Frames", 1, 6, 2), new NumberParameter("cell", "Cell size (mm)", 12, 30, 22, 1), Page()],
            inputs => BlankformsPress.TenFrames(inputs.Whole("frames"), inputs.Mm("cell"), inputs.Page())),

        new("clock-face", "Clock face", DeterministicPressRecipes.Blankforms,
            [new NumberParameter("radius", "Radius (mm)", 30, 90, 70, 1), new ToggleParameter("numerals", "Numerals", true), new ToggleParameter("ticks", "Minute ticks", true), Page()],
            inputs => BlankformsPress.ClockFace(inputs.Mm("radius"), inputs.Bool("numerals"), inputs.Bool("ticks"), inputs.Page())),

        new("music-staves", "Music staves", DeterministicPressRecipes.Blankforms,
            [new NumberParameter("staves", "Staves", 1, 12, 8), Page()],
            inputs => BlankformsPress.MusicStaves(inputs.Whole("staves"), inputs.Page())),

        new("hundred-chart", "Hundred chart", DeterministicPressRecipes.Blankforms,
            [new NumberParameter("start", "Start at", 0, 900, 1), new ToggleParameter("labeled", "Numbers filled in", true), Page()],
            inputs => BlankformsPress.HundredChart(inputs.Whole("start"), size: inputs.Page(), labeled: inputs.Bool("labeled"))),

        new("dot-paper", "Dot paper", DeterministicPressRecipes.Blankforms,
            [new NumberParameter("pitch", "Dot spacing (mm)", 5, 25, 10, 1), Page()],
            inputs => BlankformsPress.DotPaper(inputs.Mm("pitch"), size: inputs.Page())),

        new("isometric-dot-paper", "Isometric dot paper", DeterministicPressRecipes.Blankforms,
            [new NumberParameter("pitch", "Dot spacing (mm)", 5, 25, 10, 1), Page()],
            inputs => BlankformsPress.IsometricDotPaper(inputs.Mm("pitch"), size: inputs.Page())),

        new("cornell-notes", "Cornell notes page", DeterministicPressRecipes.Blankforms,
            [Page()],
            inputs => BlankformsPress.CornellNotes(inputs.Page())),

        new("month-calendar", "Month calendar", DeterministicPressRecipes.Blankforms,
            [new LinesParameter("days", "Weekday labels (seven lines)", "Mon\nTue\nWed\nThu\nFri\nSat\nSun"), new NumberParameter("weeks", "Week rows", 4, 6, 5), Page()],
            inputs => BlankformsPress.MonthCalendar(inputs.Lines("days"), inputs.Whole("weeks"), inputs.Page())),

        new("lab-table", "Lab data table", DeterministicPressRecipes.Blankforms,
            [new LinesParameter("columns", "Column headers (one per line)", "Trial\nMass (g)\nTime (s)"), new NumberParameter("rows", "Data rows", 2, 20, 8), Page()],
            inputs => BlankformsPress.LabTable(inputs.Lines("columns"), inputs.Whole("rows"), inputs.Page())),

        new("handwriting-rows", "Handwriting practice rows", DeterministicPressRecipes.Handwriting,
            [new NumberParameter("rows", "Rows", 1, 12, 8), new NumberParameter("xheight", "x-height (mm)", 4, 12, 8, 1), new LinesParameter("words", "Model words (one per line; blank for none)", "", Optional: true), Page()],
            inputs => HandwritingFoundry.PracticeRows(inputs.Whole("rows"), inputs.Mm("xheight"),
                inputs.Lines("words") is { Count: > 0 } words ? words : null, inputs.Page())),

        new("fraction-strips", "Fraction strips", DeterministicPressRecipes.Manipulatives,
            [new TextParameter("denominators", "Denominators (comma-separated, 2-16)", "2, 3, 4, 6, 8"), new ToggleParameter("labeled", "Labels printed", true), Page()],
            inputs => ManipulativeMint.FractionStrips(inputs.IntList("denominators"), inputs.Page(), labeled: inputs.Bool("labeled"))),

        new("fraction-circles", "Fraction circles", DeterministicPressRecipes.Manipulatives,
            [new TextParameter("denominators", "Denominators (comma-separated, 2-16)", "2, 3, 4, 6, 8"), new ToggleParameter("labeled", "Labels printed", true), Page()],
            inputs => ManipulativeMint.FractionCircles(inputs.IntList("denominators"), size: inputs.Page(), labeled: inputs.Bool("labeled"))),

        new("dice-net", "Die net", DeterministicPressRecipes.Manipulatives,
            [new NumberParameter("edge", "Edge (mm)", 20, 60, 30, 1), Page()],
            inputs => ManipulativeMint.DiceNet(inputs.Mm("edge"), inputs.Page())),

        new("box-net", "Box net", DeterministicPressRecipes.Manipulatives,
            [new NumberParameter("length", "Length (mm)", 20, 90, 55, 1), new NumberParameter("depth", "Depth (mm)", 15, 60, 35, 1), new NumberParameter("height", "Height (mm)", 15, 60, 30, 1), Page()],
            inputs => ManipulativeMint.BoxNet(inputs.Mm("length"), inputs.Mm("depth"), inputs.Mm("height"), inputs.Page())),

        new("spinner-face", "Spinner face", DeterministicPressRecipes.Manipulatives,
            [new NumberParameter("sectors", "Sectors", 2, 12, 4), new LinesParameter("labels", "Sector labels (one per line; blank for none)", "", Optional: true), Page()],
            inputs => ManipulativeMint.SpinnerFace(inputs.Whole("sectors"),
                inputs.Lines("labels") is { Count: > 0 } labels ? labels : null, size: inputs.Page())),

        new("flap-book", "Flap book", DeterministicPressRecipes.Foldables,
            [new NumberParameter("flaps", "Flaps", 2, 8, 4), new LinesParameter("labels", "Flap labels (one per line; blank for none)", "", Optional: true), Page()],
            inputs => FoldablesFoundry.FlapBook(inputs.Whole("flaps"),
                inputs.Lines("labels") is { Count: > 0 } labels ? labels : null, inputs.Page())),

        new("label-sheets", "Classroom labels", DeterministicPressRecipes.Labels,
            [new LinesParameter("labels", "Labels, one per line (add | second line for bilingual)", "Scissors | Tijeras\nGlue\nMarkers"), Page()],
            inputs => LabelLathe.Sheets(
                [.. inputs.SplitLines("labels").Select(pair => new LabelSpec(pair.Left, pair.Right))], inputs.Page())),

        new("flashcards", "Flashcards", DeterministicPressRecipes.Flashcards,
            [new LinesParameter("pairs", "Cards, one per line as term | answer", "photosynthesis | how plants make food from light\nhabitat | where an organism lives")],
            inputs => FlashcardFlywheel.Build(
                [.. inputs.SplitLines("pairs").Select(pair => new FlashcardPair(pair.Left, pair.Right ?? ""))]).Document),

        new("booklet-guide", "Saddle-stitch booklet guide", DeterministicPressRecipes.BookletGuide,
            [new NumberParameter("pages", "Content pages", 1, 64, 8)],
            inputs => BookletImposition.Guide(BookletImposition.Compute(inputs.Whole("pages")))),

        new("bingo-cards", "Bingo cards", DeterministicPressRecipes.Puzzles,
            [new LinesParameter("entries", "Entries, one per line (at least 24)", DefaultBingoEntries), new NumberParameter("cards", "Cards", 1, 40, 4), Seed(), new ToggleParameter("free", "Free center", true), Page()],
            inputs => PuzzlePress.BingoBoards(inputs.Lines("entries"), inputs.Whole("cards"), inputs.Whole("seed"), inputs.Bool("free"), size: inputs.Page())),

        new("word-search", "Word search", DeterministicPressRecipes.Puzzles,
            [new LinesParameter("words", "Words to hide, one per line", "fraction\ndecimal\npercent\nratio\ngraph\nsum"), Seed(), new NumberParameter("grid", "Grid size", 6, 20, 12), new ToggleParameter("diagonals", "Diagonals", true), new ToggleParameter("backwards", "Backwards words", false), Page()],
            inputs => PuzzlePress.WordSearch(inputs.Lines("words"), inputs.Whole("seed"), inputs.Whole("grid"),
                inputs.Bool("diagonals"), inputs.Bool("backwards"), size: inputs.Page())),

        new("grouping-cards", "Grouping cards", DeterministicPressRecipes.Grouping,
            [new LinesParameter("roster", "Roster labels, one per line - synthetic or first-name-free only", "Star 1\nStar 2\nStar 3\nStar 4\nStar 5\nStar 6\nStar 7\nStar 8"), new NumberParameter("size", "Group size", 2, 10, 4), Seed(), Page()],
            inputs => GroupingDeck.Cards(inputs.Lines("roster"), inputs.Whole("size"), inputs.Whole("seed"), inputs.Page())),

        // The computational-thinking studio (menu 2, item 4).
        new("parsons-puzzle", "Parsons line-ordering puzzle", DeterministicPressRecipes.Computational,
            [new TextParameter("prompt", "Prompt printed at the top", "Number the lines in the correct order."),
             new LinesParameter("solution", "Working solution, one line each, in the CORRECT order (indentation is kept)", "total = 0\nfor n in [1, 2, 3]:\n    total = total + n\nprint(total)"),
             new LinesParameter("distractors", "Distractor lines (teacher-authored; blank for none)", "", Optional: true),
             Seed(), Page()],
            inputs => ParsonsPress.Puzzle(inputs.Text("prompt"), inputs.RawLines("solution"), inputs.RawLines("distractors"), inputs.Whole("seed"), inputs.Page())),

        new("trace-table", "Trace-table sheet", DeterministicPressRecipes.Computational,
            [new TextParameter("prompt", "Prompt printed at the top", "Trace each variable line by line, then predict the output."),
             new LinesParameter("code", "Code, one line each (kept exactly as typed)", "x = 2\ny = 5\nwhile x < y:\n    x = x + 2\nprint(x)"),
             new TextParameter("variables", "Variables (comma-separated)", "x, y"),
             new NumberParameter("rows", "Trace rows", 3, 14, 6), Page()],
            inputs => TraceTableTutor.Sheet(inputs.Text("prompt"), inputs.RawLines("code"),
                [.. inputs.Text("variables").Split(',').Select(v => v.Trim()).Where(v => v.Length > 0)],
                inputs.Whole("rows"), inputs.Page())),

        new("algorithm-cards", "Unplugged algorithm cards", DeterministicPressRecipes.Computational,
            [new LinesParameter("actions", "Action cards, one per line", "Stand up\nPush your chair in\nWalk to the door\nLine up quietly"),
             new LinesParameter("controls", "Control cards, one per line (any language; blank for none)", "Start\nStop\nRepeat ×2\nIf yes\nIf no", Optional: true),
             Page()],
            inputs => AlgorithmAtelier.ActionCards(inputs.Lines("actions"), inputs.Lines("controls"), inputs.Page())),

        new("rubber-duck-deck", "Rubber-duck debugging cards", DeterministicPressRecipes.Computational,
            [new LinesParameter("prompts", "Prompt cards, one per line (edit freely)",
                "Say what the program should do.\nRead your code aloud, one line at a time.\nSay what each line actually does.\nFind the first line where the two stories differ.\nSay what you expected there, and what happened.\nChange ONE thing, then read aloud again.\nStill stuck? Now you know exactly what to ask."),
             Page()],
            inputs => AlgorithmAtelier.PromptCards(inputs.Lines("prompts"), inputs.Page())),

        // Retrieval Grid Generator (menu 2, item 5).
        new("retrieval-grids", "Retrieval grids", DeterministicPressRecipes.Retrieval,
            [new LinesParameter("questions", "Question bank, one per line (used verbatim)",
                "Name the three data lanes\nWhat does Gate B decide?\n7 × 8\nDefine perimeter\nWhat is a variable?\nSpell necessary\nName one primary source\nWhat is the water cycle's first step?\n12 ÷ 4\nWhat does an author's claim need?\nName the largest planet\nWhat is a habitat?"),
             new NumberParameter("grids", "Grids", 1, 6, 3),
             new NumberParameter("rows", "Rows", 2, 4, 3),
             new NumberParameter("columns", "Columns", 2, 4, 3),
             Seed(), Page()],
            inputs => RetrievalGrid.Grids(inputs.Lines("questions"), inputs.Whole("grids"),
                inputs.Whole("rows"), inputs.Whole("columns"), inputs.Whole("seed"), inputs.Page())),

        // Field Journal Forge (menu 2, item 6).
        new("observation-frame", "Field observation frame", DeterministicPressRecipes.FieldJournal,
            [new LinesParameter("prompts", "Prompts, one per line", "What I see\nWhat I hear\nWhat I wonder"),
             new NumberParameter("sketch", "Sketch box height (mm)", 40, 160, 110), Page()],
            inputs => FieldJournalForge.ObservationFrame(inputs.Lines("prompts"), inputs.Mm("sketch"), inputs.Page())),

        new("specimen-labels", "Specimen labels", DeterministicPressRecipes.FieldJournal,
            [new LinesParameter("fields", "Write-in fields, one per line", "Name\nDate\nLocation\nNotes"), Page()],
            inputs => FieldJournalForge.SpecimenLabels(inputs.Lines("fields"), inputs.Page())),

        new("field-log", "Weather and phenology log", DeterministicPressRecipes.FieldJournal,
            [new LinesParameter("columns", "Column headers (one per line)", "Date\nWeather\nTemperature\nWhat changed"),
             new NumberParameter("rows", "Data rows", 2, 20, 12), Page()],
            inputs => BlankformsPress.LabTable(inputs.Lines("columns"), inputs.Whole("rows"), inputs.Page())),

        new("site-map", "Site-map page", DeterministicPressRecipes.FieldJournal,
            [new NumberParameter("pitch", "Grid square (mm)", 5, 25, 10, 1),
             new NumberParameter("meters", "Meters per square", 1, 1000, 1), Page()],
            inputs => FieldJournalForge.SiteMapPage(inputs.Mm("pitch"), inputs.Mm("meters"), inputs.Page())),

        // The Manipulative Mint's third strike (menu 2, item 7).
        new("algebra-tiles", "Algebra tiles", DeterministicPressRecipes.Manipulatives,
            [new NumberParameter("unit", "Unit (mm)", 8, 25, 12, 1),
             new NumberParameter("x", "x length (mm — never a whole number of units)", 20, 80, 45, 1),
             new NumberParameter("xsq", "x-squared tiles", 0, 4, 2),
             new NumberParameter("xs", "x tiles", 0, 12, 6),
             new NumberParameter("units", "Unit tiles", 0, 30, 10),
             new ToggleParameter("labeled", "Labels printed", true), Page()],
            inputs => ManipulativeMint.AlgebraTiles(inputs.Mm("unit"), inputs.Mm("x"),
                inputs.Whole("xsq"), inputs.Whole("xs"), inputs.Whole("units"), inputs.Bool("labeled"), inputs.Page())),

        new("base-ten-blocks", "Base-ten blocks", DeterministicPressRecipes.Manipulatives,
            [new NumberParameter("unit", "Unit (mm)", 6, 15, 10, 1),
             new NumberParameter("flats", "Flats", 0, 2, 1),
             new NumberParameter("rods", "Rods", 0, 10, 4),
             new NumberParameter("units", "Units", 0, 30, 12), Page()],
            inputs => ManipulativeMint.BaseTenBlocks(inputs.Mm("unit"),
                inputs.Whole("flats"), inputs.Whole("rods"), inputs.Whole("units"), inputs.Page())),

        new("tangram", "Tangram square", DeterministicPressRecipes.Manipulatives,
            [new NumberParameter("side", "Side (mm)", 80, 165, 160, 1), Page()],
            inputs => ManipulativeMint.Tangram(inputs.Mm("side"), inputs.Page())),

        // The math scaffold presses (menu 3, item 4).
        new("worked-example-fader", "Worked example, faded", DeterministicPressRecipes.MathScaffolds,
            [new TextParameter("problem", "The problem, as you would write it", "Solve: 3(x + 4) = 27"),
             new LinesParameter("steps", "Your worked steps, one per line, in order", "3x + 12 = 27\n3x = 27 - 12\n3x = 15\nx = 15 / 3\nx = 5"),
             new NumberParameter("fades", "Faded practice sheets", 1, 4, 3),
             new TextParameter("check", "Self-check line", "Check: substitute your answer back into the problem."),
             Page()],
            inputs => WorkedExampleFader.Sheets(inputs.Text("problem"), inputs.RawLines("steps"),
                inputs.Whole("fades"), inputs.Text("check"), inputs.Page())),

        new("estimation-first", "Estimation-first problems", DeterministicPressRecipes.MathScaffolds,
            [new LinesParameter("problems", "Problems, one per line", "487 + 316\n72 × 9\n1,205 − 388"),
             new TextParameter("label1", "First section label", "My estimate"),
             new TextParameter("label2", "Second section label", "A reasonable range (low - high)"),
             new TextParameter("label3", "Third section label", "Exact answer"),
             new TextParameter("label4", "Fourth section label", "How close was my estimate?"),
             Page()],
            inputs => EstimationFirst.Sheets(inputs.Lines("problems"),
                [inputs.Text("label1"), inputs.Text("label2"), inputs.Text("label3"), inputs.Text("label4")],
                inputs.Page())),

        // The history presses (menu 3, item 5).
        new("timeline", "Timeline", DeterministicPressRecipes.History,
            [new LinesParameter("events", "Events, one per line as year | label (or start-end | label)", "1957 | Sputnik\n1961 | First human in orbit\n1969 | Moon landing\n1972-1975 | Final Apollo era"),
             new NumberParameter("from", "From year", -3000, 2100, 1950),
             new NumberParameter("to", "To year", -3000, 2100, 1980),
             Page("Letter landscape")],
            inputs => TimelineWeaver.Sheet(TimelineWeaver.Parse(inputs.SplitLines("events")),
                inputs.Whole("from"), inputs.Whole("to"), inputs.Page())),

        // The Chart Press (menu 4, item 2): atlas #88's deterministic heart.
        // Default data keeps the invariant visible: Sun is exactly twice Shade.
        new("bar-chart", "Bar chart", DeterministicPressRecipes.Charts,
            [new TextParameter("title", "Chart title", "Bean plants after three weeks (cm)"),
             new LinesParameter("data", "Bars, one per line as label | value", "Sun | 18\nShade | 9\nWindow | 12"),
             new ChoiceParameter("orientation", "Bars run", ["Up", "Across"], "Up"),
             Page("Letter landscape")],
            inputs => ChartPress.Sheet(inputs.Text("title"),
                ChartPress.Parse(inputs.SplitLines("data")),
                inputs.Text("orientation") == "Across", inputs.Page())),

        new("synthesis-table", "Source synthesis table", DeterministicPressRecipes.History,
            [new LinesParameter("claims", "Claims, one per line", "The canal changed local trade\nWorkers came from nearby towns\nThe flood of 1889 ended the era"),
             new LinesParameter("sources", "Sources, one per line", "Newspaper\nDiary\nLedger"),
             new TextParameter("legend", "Legend", "Mark each cell: A = agrees, D = disputes, dash = silent"),
             new TextParameter("provenance", "Foot row label", "Who made this source, when, and why?"),
             Page()],
            inputs => SourceSynthesisTable.Sheet(inputs.Lines("claims"), inputs.Lines("sources"),
                inputs.Text("legend"), inputs.Text("provenance"), inputs.Page())),

        // The learner-held kit (menu 3, item 6).
        new("portfolio-passport", "Portfolio passport", DeterministicPressRecipes.LearnerHeld,
            [new LinesParameter("selection", "Selection-slip prompts, one per line", "What is it?\nWhy I chose it\nWhat it shows I can do"),
             new LinesParameter("reflection", "Reflection prompts, one per line", "Before, I...\nNow, I...\nNext, I will..."),
             new NumberParameter("contents", "Contents rows", 4, 14, 8),
             new TextParameter("pledge", "The pledge printed on every page", "This record belongs to the learner and lives on paper - never in a data system."),
             Page()],
            inputs => LearnerHeldKit.PortfolioPassport(inputs.Lines("selection"), inputs.Lines("reflection"),
                inputs.Whole("contents"), inputs.Text("pledge"), inputs.Page())),

        new("strategy-shelf", "Strategy shelf cards", DeterministicPressRecipes.LearnerHeld,
            [new LinesParameter("strategies", "Strategies offered, one per line (the learner chooses)", "Reread the sentence slowly\nBreak the problem into parts\nDraw what I know\nTake three slow breaths\nAsk: what exactly is stuck?\nCheck against an example"),
             new TextParameter("pledge", "The pledge printed on every page", "These cards are mine; I chose them."),
             Page()],
            inputs => LearnerHeldKit.StrategyShelf(inputs.Lines("strategies"), inputs.Text("pledge"), inputs.Page())),

        new("goal-post", "Goal sheet", DeterministicPressRecipes.LearnerHeld,
            [new LinesParameter("prompts", "Prompts, one per line", "My goal\nHow I will know I am getting there\nReview date and what I noticed\nEvidence I choose to keep"),
             new TextParameter("pledge", "The pledge printed on the page", "This sheet lives in my folder - never in a data system."),
             Page()],
            inputs => LearnerHeldKit.GoalPost(inputs.Lines("prompts"), inputs.Text("pledge"), inputs.Page())),

        // The rubric and criteria presses (menu 3, item 7).
        new("one-point-rubric", "One-point rubric", DeterministicPressRecipes.Rubrics,
            [new LinesParameter("criteria", "Criteria, one per line", "The claim is stated in one clear sentence\nEvery reason cites its evidence\nThe counterclaim is answered, not ignored"),
             new TextParameter("below", "Left column header", "Evidence of growing toward"),
             new TextParameter("beyond", "Right column header", "Evidence of going beyond"),
             Page()],
            inputs => RubricPresses.OnePointRubric(inputs.Lines("criteria"),
                inputs.Text("below"), inputs.Text("beyond"), inputs.Page())),

        new("success-criteria", "Success criteria checklist", DeterministicPressRecipes.Rubrics,
            [new TextParameter("objective", "The objective, in learner language", "I can explain why the seasons change."),
             new LinesParameter("criteria", "Observable criteria, one per line", "I name the tilt of the axis\nI use a diagram in my explanation\nI say what summer looks like in each hemisphere"),
             new LinesParameter("continuum", "Continuum stages, one per line", "Beginning\nMeeting\nBeyond"),
             Page()],
            inputs => RubricPresses.SuccessCriteria(inputs.Text("objective"), inputs.Lines("criteria"),
                inputs.Lines("continuum"), inputs.Page())),

        new("done-definition", "Definition of done", DeterministicPressRecipes.Rubrics,
            [new LinesParameter("checklist", "Completion checklist, one per line", "Every question is answered\nMy name and date are at the top\nI read it aloud once"),
             new LinesParameter("examples", "Looks like (one per line; blank for none)", "Full sentences\nUnits on every answer", Optional: true),
             new LinesParameter("nonexamples", "Doesn't look like (one per line; blank for none)", "One-word answers\nCrossed-out guesses", Optional: true),
             new TextParameter("final", "Final self-check line", "Final check: I compared my work to every line above."),
             Page()],
            inputs => RubricPresses.DoneDefinition(inputs.Lines("checklist"), inputs.Lines("examples"),
                inputs.Lines("nonexamples"), inputs.Text("final"), inputs.Page())),

        // Big Print Shop stays out: its input is an existing approved artifact,
        // not parameters — it joins the room when the project library picker does.
    ];

    private const string DefaultBingoEntries = "sum\ndifference\nproduct\nquotient\nfactor\nmultiple\nnumerator\ndenominator\nfraction\ndecimal\npercent\nratio\narea\nperimeter\nvolume\nangle\nvertex\nedge\nprime\neven\nodd\nsquare\ncube\nhalf";

    public static PressDefinition ById(string id)
        => All.FirstOrDefault(d => d.Id == id)
            ?? throw new ArgumentException($"No press '{id}' in the catalog.", nameof(id));

    /// <summary>Every parameter's declared default as the surface would submit it.</summary>
    public static Dictionary<string, string> Defaults(PressDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Parameters.ToDictionary(p => p.Key, p => p switch
        {
            NumberParameter number => number.Default.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ChoiceParameter choice => choice.Default,
            ToggleParameter toggle => toggle.Default ? "true" : "false",
            LinesParameter lines => lines.DefaultText,
            TextParameter text => text.Default,
            _ => throw new NotSupportedException($"Unknown parameter kind {p.GetType().Name}."),
        });
    }
}
