// SPDX-License-Identifier: GPL-3.0-or-later
// Generates the canonical accessible sample outputs for the 0.1-alpha evidence
// bundle (universal Definition of Done: "canonical accessible sample outputs").
// Deterministic: fixed approval instant, fixed content, the shipped libre pack.
// With --seeded it instead produces the seeded-error review packets for the
// pilot kit (plan §14: "Teachers must detect defined seeded errors before
// pilot"): print-ready task strips, some carrying one planted defect each that
// passes every machine gate and only a practicing teacher can catch.
//
// The packets are READ FROM A FILE, never written here. They were once a literal
// array in this file, under a comment asserting the packet-to-defect mapping
// "lives only in the facilitator key" — which was false, because semantic
// defects written in plain language are legible to anyone reading the source.
// A blind study cannot define its seeds in a repository meant to be public
// (SeededPacketFile carries the full reasoning).
// Usage: SampleGenerator <repoRoot> <outputDirectory> [--seeded <definitions.json>]

using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.AllAboard;
using Foundry.Modules.DeterministicPress;
using Foundry.Rendering;
using Foundry.Storage;
using Foundry.Tools.SampleGenerator;

if (args.Length is not (2 or 4) || (args.Length == 4 && args[2] != "--seeded"))
{
    Console.Error.WriteLine("Usage: SampleGenerator <repoRoot> <outputDirectory> [--seeded <definitions.json>]");
    return 1;
}

var repoRoot = args[0];
var outputDirectory = args[1];
Directory.CreateDirectory(outputDirectory);

var approvedAt = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
var catalog = new JsonAssetCatalog(Path.Combine(repoRoot, "assets", "symbols"));
var renderer = new AccessibleHtmlRenderer();
var store = new OcfprojProjectStore(outputDirectory, renderer, catalog);

if (args.Length == 4)
{
    // The definitions are the facilitator's and live outside this repository;
    // only an obviously-fictional example is committed. Refusals are loud and
    // readable because this file is hand-edited, often shortly before a session.
    SeededPacketSet set;
    try
    {
        set = SeededPacketFile.Load(args[3]);
    }
    catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException or ArgumentException)
    {
        // The facilitator hand-edits this file, often minutes before a
        // session. They get the sentence, never the stack.
        Console.Error.WriteLine(exception.Message);
        return 1;
    }

    foreach (var packet in set.Packets)
    {
        var document = AllAboardBuilders.TaskStrip(
            packet.Title, SeededPacketFile.ToStepSpecs(packet), catalog, targetLocale: packet.TargetLocale);
        var approved = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green),
            "sample-teacher@example.org",
            DocumentValidator.Validate(document),
            approvedAt);
        var print = await renderer.RenderAsync(
            approved, new RenderRequest(RenderTarget.PrintHtml, RenderAudience.Learner), CancellationToken.None);
        await File.WriteAllBytesAsync(
            Path.Combine(outputDirectory, SeededPacketFile.FileNameFor(packet)), print.Content.ToArray());
    }

    Console.WriteLine($"{set.Packets.Count} seeded review packets written to {outputDirectory} - the key is the facilitator's, never this tool's.");
    return 0;
}

// Sample 1: a bilingual task strip with symbols.
var strip = AllAboardBuilders.TaskStrip(
    "Watering the class plants",
    [
        new StepSpec("Pick up the watering can.", new AssetId("agency.help.v1"), "Toma la regadera."),
        new StepSpec("Fill it to the line.", TargetText: "Llénala hasta la línea."),
        new StepSpec("Water each plant once.", TargetText: "Riega cada planta una vez."),
        new StepSpec("Put the can back.", new AssetId("agency.finished.v1"), "Devuelve la regadera a su lugar."),
    ],
    catalog,
    sourceLocale: "en",
    targetLocale: "es");

var approvedStrip = ApprovalGate.Approve(
    DraftArtifact.New(strip, DataLane.Green), "sample-teacher@example.org", DocumentValidator.Validate(strip), approvedAt);

await WriteRenderAsync(approvedStrip, "task-strip-bilingual.learner.html", RenderAudience.Learner);
await WriteRenderAsync(approvedStrip, "task-strip-bilingual.teacher.html", RenderAudience.Teacher);
await store.SaveGreenProjectAsync(
    approvedStrip,
    new ProjectSaveRequest("task-strip-bilingual", "all-aboard", "all-aboard.task-strip", "0.1.0", approvedAt),
    CancellationToken.None);

// Sample 2: the full agency deck.
var deck = AllAboardBuilders.AgencyCards([.. catalog.All.Select(a => a.Id)], catalog);
var approvedDeck = ApprovalGate.Approve(
    DraftArtifact.New(deck, DataLane.Green), "sample-teacher@example.org", DocumentValidator.Validate(deck), approvedAt);

await WriteRenderAsync(approvedDeck, "agency-cards.learner.html", RenderAudience.Learner);

// Sample 3: First/Then, print-ready.
var firstThen = AllAboardBuilders.FirstThen(
    new CardSpec("Math journal"), new CardSpec("Ten minutes of blocks"), catalog);
var approvedFirstThen = ApprovalGate.Approve(
    DraftArtifact.New(firstThen, DataLane.Green), "sample-teacher@example.org", DocumentValidator.Validate(firstThen), approvedAt);

var printReady = await renderer.RenderAsync(
    approvedFirstThen, new RenderRequest(RenderTarget.PrintHtml, RenderAudience.Learner), CancellationToken.None);
await File.WriteAllBytesAsync(Path.Combine(outputDirectory, "first-then.print.html"), printReady.Content.ToArray());

// Samples 4+: the Deterministic Press — calibration instrument and second wave
// (handover 2026-08-29). All parameters, no prose; seeds fixed so the run is
// byte-identical every time.
string[] bingoEntries =
[
    "sum", "difference", "product", "quotient", "factor", "multiple",
    "numerator", "denominator", "fraction", "decimal", "percent", "ratio",
    "area", "perimeter", "volume", "angle", "vertex", "edge",
    "prime", "even", "odd", "square", "cube", "half", "quarter",
];

var pressSamples = new (string Name, ArtifactDocument Document)[]
{
    ("calibration-proof", CalibrationPress.ProofPage()),
    ("hundred-chart", BlankformsPress.HundredChart()),
    ("first-quadrant-grid", BlankformsPress.CoordinateGrid(quadrants: GridQuadrants.First)),
    ("dot-paper", BlankformsPress.DotPaper()),
    ("isometric-dot-paper", BlankformsPress.IsometricDotPaper()),
    ("fraction-circles", ManipulativeMint.FractionCircles([2, 3, 4, 6, 8])),
    ("spinner-face", ManipulativeMint.SpinnerFace(4, ["1", "2", "3", "4"])),
    ("box-net", ManipulativeMint.BoxNet()),
    ("bingo-cards", PuzzlePress.BingoBoards(bingoEntries, cards: 2, seed: 20260829)),
    ("word-search", PuzzlePress.WordSearch(
        ["fraction", "decimal", "percent", "ratio", "graph", "sum"], seed: 20260829)),
    ("grouping-deck", GroupingDeck.Cards(
        [.. Enumerable.Range(1, 22).Select(i => $"Star {i}")], groupSize: 4, seed: 20260829)),
    ("parsons-puzzle", ParsonsPress.Puzzle(
        "Number the lines in the correct order.",
        ["total = 0", "for n in [1, 2, 3]:", "    total = total + n", "print(total)"],
        ["    total = 0"], seed: 20260829)),
    ("trace-table", TraceTableTutor.Sheet(
        "Trace each variable line by line, then predict the output.",
        ["x = 2", "y = 5", "while x < y:", "    x = x + 2", "print(x)"], ["x", "y"], traceRows: 6)),
    ("retrieval-grids", RetrievalGrid.Grids(
        [.. Enumerable.Range(1, 12).Select(i => $"Question {i}")], gridCount: 2, rows: 3, columns: 3, seed: 20260829)),
    ("observation-frame", FieldJournalForge.ObservationFrame(["What I see", "What I hear", "What I wonder"])),
    ("site-map", FieldJournalForge.SiteMapPage(pitchMm: 10, metersPerSquare: 2)),
    ("algebra-tiles", ManipulativeMint.AlgebraTiles()),
    ("base-ten-blocks", ManipulativeMint.BaseTenBlocks()),
    ("tangram", ManipulativeMint.Tangram()),
    ("worked-example-fader", WorkedExampleFader.Sheets(
        "Solve: 3(x + 4) = 27",
        ["3x + 12 = 27", "3x = 15", "x = 5"], fadeSheets: 2,
        "Check: substitute your answer back into the problem.")),
    ("timeline", TimelineWeaver.Sheet(
        TimelineWeaver.Parse([("1957", "Sputnik"), ("1969", "Moon landing"), ("1972-1975", "Final Apollo era")]),
        1950, 1980)),
    ("bar-chart", ChartPress.Sheet(
        "Bean plants after three weeks (cm)",
        ChartPress.Parse([("Sun", "18"), ("Shade", "9"), ("Window", "12")]))),
    ("class-set-word-search", ClassSets.Compose(
        PressRoomCatalog.ById("word-search"),
        PressRoomCatalog.Defaults(PressRoomCatalog.ById("word-search")),
        baseSeed: 20260908, variants: 3)),
    ("glossary-garden", GlossaryGarden.Sheet(
        "Unit 3: The water cycle",
        GlossaryGarden.Parse(["evaporation | Liquid water becomes vapor. | evaporación", "condensation | Vapor becomes liquid drops. | condensación", "precipitation | Water falls as rain or snow. | precipitación"]),
        "en", "es")),
    ("concept-sort", ConceptSortStudio.Cards("Mammals",
        ["Dolphin", "Bat", "Elephant", "Whale"], ["Shark", "Penguin", "Crocodile", "Salmon"], ["Platypus", "Fossil skeleton"])),
    ("role-cards", DiscussionRoleWheel.Cards(
        [("Facilitator", "Ask each person for their view before deciding."), ("Skeptic", "Ask for the evidence behind each claim."), ("Summarizer", "Restate the group's thinking in your own words."), ("Recorder", "Write what the group agrees and where it splits.")],
        "Rotate one role clockwise each round - every voice gets every job.")),
    ("peer-feedback", PeerFeedbackBuilder.Sheet(
        "Peer feedback protocol",
        "Every comment points at the work - name the line, the step, or the spot.",
        ["One strength I noticed is...", "A question I have is...", "One specific suggestion is..."],
        "The author decides: what I will use, what I will set aside.")),
    ("bug-zoo", BugZoo.Sheet(
        "This program should print 6 - but it does not.",
        ["total = 0", "for n in [1, 2, 3]:", "    total = n", "print(total)"],
        ["Diagnose - what exactly goes wrong, and on which line?", "Repair - write the corrected line or lines.", "Explain - why is the buggy version convincing?"],
        "Assignment replaces; learners expect accumulation without writing total + n.")),
    ("fluency-rehearsal", FluencyRehearsal.Sheet(
        "The Little Boat",
        ["The little boat | rocked gently | on the bright water,", "and the river | carried it | all the way home."],
        readings: 3, "After my last reading, I noticed...")),
    ("bell-to-bell", BellToBell.Plan(
        "Tuesday, period 2", "8:30",
        BellToBell.Parse([("5", "Warm-up"), ("15", "Mini-lesson"), ("20", "Guided practice"), ("8", "Share out")]),
        periodMinutes: 55, transitionMinutes: 1, "Pack up, reflect, and reset", closureMinutes: 3)),
    ("portfolio-passport", LearnerHeldKit.PortfolioPassport(
        ["What is it?", "Why I chose it"], ["Before, I...", "Now, I..."], contentsRows: 8,
        "This record belongs to the learner and lives on paper - never in a data system.")),
    ("one-point-rubric", RubricPresses.OnePointRubric(
        ["The claim is stated in one clear sentence", "Every reason cites its evidence"],
        "Evidence of growing toward", "Evidence of going beyond")),
};

foreach (var (name, document) in pressSamples)
{
    var approvedPress = ApprovalGate.Approve(
        DraftArtifact.New(document, DataLane.Green),
        "sample-teacher@example.org",
        DocumentValidator.Validate(document),
        approvedAt);
    var pressPrint = await renderer.RenderAsync(
        approvedPress, new RenderRequest(RenderTarget.PrintHtml, RenderAudience.Learner), CancellationToken.None);
    await File.WriteAllBytesAsync(
        Path.Combine(outputDirectory, $"press-{name}.print.html"), pressPrint.Content.ToArray());
}

// The vector-first PDF (second forge menu, item 3): the calibration instrument
// as deterministic PDF bytes — the print pipeline's primary paper format.
var approvedProof = ApprovalGate.Approve(
    DraftArtifact.New(CalibrationPress.ProofPage(), DataLane.Green),
    "sample-teacher@example.org", [], approvedAt);
var proofPdf = await renderer.RenderAsync(
    approvedProof, new RenderRequest(RenderTarget.PrintPdf, RenderAudience.Learner), CancellationToken.None);
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "press-calibration-proof.pdf"), proofPdf.Content.ToArray());

// The imposed booklet (third forge menu, item 1): the signature arithmetic and
// the PDF transforms composed — three retrieval grids as a saddle-stitch file.
var approvedGrids = ApprovalGate.Approve(
    DraftArtifact.New(RetrievalGrid.Grids(
        [.. Enumerable.Range(1, 12).Select(i => $"Question {i}")], gridCount: 3, rows: 3, columns: 3, seed: 20260829), DataLane.Green),
    "sample-teacher@example.org", [], approvedAt);
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "press-booklet-retrieval.pdf"),
    VectorPdfWriter.WriteImposed(
        approvedGrids,
        BookletImposition.PdfSides(BookletImposition.Compute(3)),
        RenderAudience.Teacher));

// The Studio Sampler (fourth forge menu, item 9): the whole catalog as one
// imposed saddle-stitch booklet — the catalog builds it, the imposer binds
// it, the PDF press inks it. Composition happens HERE, per the layering wall.
var sampler = StudioSampler.Catalog();
var approvedSampler = ApprovalGate.Approve(
    DraftArtifact.New(sampler, DataLane.Green),
    "sample-teacher@example.org", DocumentValidator.Validate(sampler), approvedAt);
await File.WriteAllBytesAsync(
    Path.Combine(outputDirectory, "press-studio-sampler.pdf"),
    VectorPdfWriter.WriteImposed(
        approvedSampler,
        BookletImposition.PdfSides(BookletImposition.Compute(sampler.Nodes.OfType<VectorGraphic>().Count())),
        RenderAudience.Teacher));

Console.WriteLine($"Samples written to {outputDirectory}");
return 0;

async Task WriteRenderAsync(ApprovedArtifact artifact, string fileName, RenderAudience audience)
{
    var output = await renderer.RenderAsync(
        artifact, new RenderRequest(RenderTarget.AccessibleHtml, audience), CancellationToken.None);
    await File.WriteAllBytesAsync(Path.Combine(outputDirectory, fileName), output.Content.ToArray());
}
