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

    public PageSize Page() => Text(PressRoomCatalog.PageKey) == "A4" ? PageSize.A4 : PageSize.Letter;

    public IReadOnlyList<string> Lines(string key) =>
        [.. Text(key).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Select(l => l.Trim())
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

    private static ChoiceParameter Page() => new(PageKey, "Page size", ["Letter", "A4"], "Letter");

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
