// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

public sealed record SheetPlan(int Number, int FrontLeft, int FrontRight, int BackLeft, int BackRight);

public sealed record ImpositionPlan(int ContentPages, int TotalPages, int BlankPagesAdded, IReadOnlyList<SheetPlan> Sheets);

/// <summary>
/// The Booklet Binder's saddle-stitch arithmetic (spec §5.3): the signature math
/// teachers do wrong at the copier, done right and proven for every page count.
/// PDF re-imposition arrives with the print pipeline; the imposition guide below
/// is usable at a copier today.
/// </summary>
public static class BookletImposition
{
    public static ImpositionPlan Compute(int contentPages)
    {
        if (contentPages < 1)
        {
            throw new ArgumentException("A booklet needs at least one page.", nameof(contentPages));
        }

        var total = (contentPages + 3) / 4 * 4;
        var sheets = new List<SheetPlan>(total / 4);

        for (var k = 0; k < total / 4; k++)
        {
            sheets.Add(new SheetPlan(
                Number: k + 1,
                FrontLeft: total - 2 * k,
                FrontRight: 1 + 2 * k,
                BackLeft: 2 + 2 * k,
                BackRight: total - 1 - 2 * k));
        }

        return new ImpositionPlan(contentPages, total, total - contentPages, sheets);
    }

    /// <summary>
    /// Reading order after printing duplex (short-edge flip), stacking the sheets in
    /// order, and folding the stack in half. A correct plan reconstructs 1..TotalPages.
    /// </summary>
    public static IReadOnlyList<int> FoldedReadingOrder(ImpositionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var order = new List<int>(plan.TotalPages);
        foreach (var sheet in plan.Sheets)
        {
            order.Add(sheet.FrontRight);
            order.Add(sheet.BackLeft);
        }

        foreach (var sheet in plan.Sheets.Reverse())
        {
            order.Add(sheet.BackRight);
            order.Add(sheet.FrontLeft);
        }

        return order;
    }

    /// <summary>
    /// The plan flattened to printable side order — front, back, front, back —
    /// for a two-up imposer (the vector PDF press): the signature arithmetic
    /// stays here where its proofs live; placement stays with the renderer.
    /// </summary>
    public static IReadOnlyList<(int Left, int Right)> PdfSides(ImpositionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return [.. plan.Sheets.SelectMany(s => new[] { (s.FrontLeft, s.FrontRight), (s.BackLeft, s.BackRight) })];
    }

    /// <summary>The teacher-facing imposition guide: which page goes where, sheet by sheet.</summary>
    public static ArtifactDocument Guide(ImpositionPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var rows = plan.Sheets
            .Select(IReadOnlyList<string> (s) =>
            [
                s.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Page(s.FrontLeft, plan.ContentPages),
                Page(s.FrontRight, plan.ContentPages),
                Page(s.BackLeft, plan.ContentPages),
                Page(s.BackRight, plan.ContentPages),
            ])
            .ToList();

        var paddingNote = plan.BlankPagesAdded == 0
            ? $"{plan.ContentPages} pages fill the signature exactly."
            : $"{plan.ContentPages} content pages are padded with {plan.BlankPagesAdded} blank page(s) to reach {plan.TotalPages} — the blanks land at the end, never in the middle of the reading.";

        return new ArtifactDocument(
        [
            new Heading(1, $"Saddle-stitch booklet: {plan.ContentPages} pages"),
            new Paragraph(paddingNote),
            new TableNode(
                ["Sheet", "Front left", "Front right", "Back left", "Back right"],
                rows),
            new OrderedSteps(
            [
                "Print the sheets double-sided, flipping on the SHORT edge.",
                "Keep the sheets in printed order.",
                "Fold the whole stack in half.",
                "Staple twice on the fold.",
            ]),
        ]);
    }

    private static string Page(int page, int contentPages)
        => page > contentPages ? "blank" : page.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>The presses' recipe identities: parameters in, paper out, no provider anywhere.</summary>
public static class DeterministicPressRecipes
{
    private static readonly IReadOnlyList<string> Prohibited =
    [
        "prose input of any kind - presses take parameters, never prose",
        "model involvement of any kind",
    ];

    public static RecipeManifest Blankforms { get; } = Manifest("press.blankforms", "Print-perfect classroom classics from exact parameters: graph paper, grids, number lines, ten-frames, hundred charts, dot and isometric dot paper, clock faces, staves.");

    public static RecipeManifest Flashcards { get; } = Manifest("press.flashcards", "Registration-safe double-sided flashcards from a teacher's term/answer list.");

    public static RecipeManifest BookletGuide { get; } = Manifest("press.booklet-guide", "Saddle-stitch imposition guide: correct signature arithmetic for any page count.");

    public static RecipeManifest Manipulatives { get; } = Manifest("press.manipulatives", "Cardstock math manipulatives with exact proportions: fraction strips and circles, dice and box nets, spinner faces, algebra tiles, base-ten blocks, tangrams.");

    public static RecipeManifest Foldables { get; } = Manifest("press.foldables", "Interactive-notebook foldables with the solid-cut, dashed-fold line language and a printed legend.");

    public static RecipeManifest BigPrint { get; } = Manifest("press.big-print", "Tile any vector sheet into a wall display at 100 percent scale with overlap strips and alignment marks.");

    public static RecipeManifest Handwriting { get; } = Manifest("press.handwriting", "Three-line handwriting practice rows with dashed midlines and optional model words (Latin script first).");

    public static RecipeManifest Labels { get; } = Manifest("press.labels", "Consistent classroom label series, optionally bilingual, on dimensionally described sheets.");

    public static RecipeManifest Calibration { get; } = Manifest("press.calibration", "Printer calibration and proof sheet: 100 mm rulers, a margin frame, duplex ring targets, and an ink-density ramp — the instrument that clears or convicts the printer before any artifact is blamed.");

    public static RecipeManifest Puzzles { get; } = Manifest("press.puzzles", "Seeded-deterministic bingo boards and word searches from a teacher's own list: same seed, same pages, never random at print time.");

    public static RecipeManifest Grouping { get; } = Manifest(
        "press.grouping",
        "Seeded-deterministic grouping cards from a teacher-typed roster: same seed, same groups, reprintable and verifiable.",
        "real learner names — the roster takes synthetic or first-name-free labels only (atlas #137 lane correction)");

    public static RecipeManifest Computational { get; } = Manifest(
        "press.computational",
        "Parsons puzzles, trace tables, unplugged algorithm cards, and rubber-duck decks from a teacher's own code and routines.",
        "interpreting, executing, correcting, or completing the teacher's code — code is cargo, preserved exactly");

    public static RecipeManifest Retrieval { get; } = Manifest(
        "press.retrieval",
        "Seeded spaced-retrieval grids from the teacher's own question bank: questions verbatim, same seed same grids.",
        "grading, scoring, or tracking retrieval outcomes — the grids are practice paper, not measurement records");

    public static RecipeManifest FieldJournal { get; } = Manifest(
        "press.field-journal",
        "Field and nature study kits: observation frames, specimen labels, field logs, and site-map pages with true scale bars.");

    public static RecipeManifest MathScaffolds { get; } = Manifest(
        "press.math-scaffolds",
        "Worked-example fades and estimation-first scaffolds from the teacher's own problems and steps — the work verbatim, the fading exact arithmetic.",
        "solving, checking, correcting, or completing the teacher's mathematics — the steps are cargo");

    public static RecipeManifest History { get; } = Manifest(
        "press.history",
        "Proportionally true timelines and claim-by-source synthesis tables from teacher-typed events, claims, and sources.",
        "verifying, correcting, or supplying dates, claims, or sources — the history is the teacher's, placed exactly as typed");

    public static RecipeManifest LearnerHeld { get; } = Manifest(
        "press.learner-held",
        "Learner-held paper kits — portfolio passports, strategy shelves, goal sheets — the record lives in the learner's hands.",
        "entering learner reflections, goals, or portfolio contents into any data system — the paper IS the record");

    public static RecipeManifest Rubrics { get; } = Manifest(
        "press.rubrics",
        "One-point rubrics, success-criteria checklists, and definitions of done from the teacher's own criteria.",
        "inventing, weighting, or scoring criteria — the press prints the teacher's judgment, never substitutes its own");

    public static RecipeManifest Charts { get; } = Manifest(
        "press.charts",
        "Proportionally true bar charts from a teacher's own labeled values: a value twice as large is a bar exactly twice as long, gridlines at clean intervals.",
        "computing, aggregating, correcting, or interpreting the teacher's data — the numbers are the teacher's claim, drawn exactly as typed");

    public static RecipeManifest Schedules { get; } = Manifest(
        "press.schedules",
        "Bell-to-bell lesson plans whose clock arithmetic is done exactly: cumulative times, transitions counted, the closure protected at the end, overruns refused before they reach a classroom.",
        "planning, suggesting, reordering, or trimming activities — the plan is the teacher's; the press does only the clock arithmetic");

    public static RecipeManifest Fluency { get; } = Manifest(
        "press.fluency",
        "Repeated-reading rehearsal sheets from the teacher's own passage: text verbatim in large print, the teacher's phrase marks as visible breath-breaks, tally boxes, and a reflection line.",
        "altering, leveling, simplifying, or re-phrasing the passage — the text and its phrase marks are the teacher's, preserved exactly");

    public static RecipeManifest Protocols { get; } = Manifest(
        "press.protocols",
        "Concept-sort card decks, discussion role cards, and peer-feedback protocol sheets from the teacher's own examples, roles, and sentence stems — card kinds distinguished by shape, never color alone.",
        "inventing examples, roles, stems, or feedback content — the sorting judgment, the discussion, and the feedback remain the learners' and the teacher's");

    public static RecipeManifest Glossary { get; } = Manifest(
        "press.glossary",
        "Bilingual unit glossaries from the teacher's own terms, meanings, and translations — consistent terminology as aligned pairs with correct language tags.",
        "translating, correcting, or leveling the teacher's entries — the translations are the teacher's own, placed verbatim");

    public static IReadOnlyList<RecipeManifest> All { get; } =
        [Blankforms, Flashcards, BookletGuide, Manipulatives, Foldables, BigPrint, Handwriting, Labels, Calibration, Puzzles, Grouping, Computational, Retrieval, FieldJournal, MathScaffolds, History, LearnerHeld, Rubrics, Charts, Schedules, Fluency, Protocols, Glossary];

    private static RecipeManifest Manifest(string id, string purpose, params string[] alsoProhibited) => new(
        Id: id,
        Version: "0.1.0",
        License: "GPL-3.0-or-later",
        MinimumEngineVersion: EngineIdentity.EngineVersion,
        InstructionalPurpose: purpose,
        ProhibitedPurposes: [.. Prohibited, .. alsoProhibited],
        AllowedInputKinds: ["parameters", "teacher-entered-list"],
        MaximumLane: DataLane.Green,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.deterministic-press.v1",
        ValidatorIds: ["document.structural"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.PrintHtml, RenderTarget.Svg],
        Warnings: ["Verify your printer is at 100 percent scale; fit-to-page changes dimensions."],
        EvaluationSuiteVersion: "0.1");
}
