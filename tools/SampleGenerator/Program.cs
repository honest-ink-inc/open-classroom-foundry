// SPDX-License-Identifier: GPL-3.0-or-later
// Generates the canonical accessible sample outputs for the 0.1-alpha evidence
// bundle (universal Definition of Done: "canonical accessible sample outputs").
// Deterministic: fixed approval instant, fixed content, the shipped libre pack.
// With --seeded it instead produces the seeded-error review packets for the
// pilot kit (plan §14: "Teachers must detect defined seeded errors before
// pilot"): eight print-ready task strips, six carrying one planted defect each
// that passes every machine gate and only a practicing teacher can catch. The
// facilitator key lives beside the kit, hand-authored, never generated here.
// Usage: SampleGenerator <repoRoot> <outputDirectory> [--seeded]

using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.AllAboard;
using Foundry.Modules.DeterministicPress;
using Foundry.Rendering;
using Foundry.Storage;

if (args.Length is < 2 or > 3 || (args.Length == 3 && args[2] != "--seeded"))
{
    Console.Error.WriteLine("Usage: SampleGenerator <repoRoot> <outputDirectory> [--seeded]");
    return 1;
}

var repoRoot = args[0];
var outputDirectory = args[1];
Directory.CreateDirectory(outputDirectory);

var approvedAt = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
var catalog = new JsonAssetCatalog(Path.Combine(repoRoot, "assets", "symbols"));
var renderer = new AccessibleHtmlRenderer();
var store = new OcfprojProjectStore(outputDirectory, renderer, catalog);

if (args.Length == 3)
{
    // Packet letters carry no information about which are seeded; the mapping
    // lives only in the facilitator key. Defects here are semantic — wrong
    // order, wrong translation, wrong symbol, missing step, self-contradiction —
    // precisely the class no validator can catch, which is why the study gates
    // the pilot.
    var packets = new (char Letter, string Title, StepSpec[] Steps, string? TargetLocale)[]
    {
        ('a', "Mixing green paint",
        [
            new StepSpec("Squeeze blue paint onto the tray."),
            new StepSpec("Mix the colors together with your brush."),
            new StepSpec("Squeeze yellow paint onto the tray."),
            new StepSpec("Paint the green swatch on your paper."),
        ], null),
        ('b', "Library checkout",
        [
            new StepSpec("Choose one book from the shelf."),
            new StepSpec("Bring it to the checkout desk."),
            new StepSpec("Scan your library card."),
            new StepSpec("Put the book in your bag."),
        ], null),
        ('c', "Feeding the class fish",
        [
            new StepSpec("Take one pinch of fish food.", TargetText: "Toma una pizca de comida para peces."),
            new StepSpec("Sprinkle it into the tank once.", TargetText: "Espolvoréala en la pecera dos veces."),
            new StepSpec("Close the food container.", TargetText: "Cierra el bote de comida."),
            new StepSpec("Wash your hands.", TargetText: "Lávate las manos."),
        ], "es"),
        ('d', "Silent reading slip",
        [
            new StepSpec("Read pages 10 to 14 of your book."),
            new StepSpec("Choose your favorite sentence."),
            new StepSpec("Answer the question about page 20 on your slip."),
            new StepSpec("Put your slip in the basket."),
        ], null),
        ('e', "Morning arrival",
        [
            new StepSpec("Hang up your backpack."),
            new StepSpec("Move your name magnet to \"here\"."),
            new StepSpec("Sharpen two pencils."),
            new StepSpec("Start the warm-up on the board."),
        ], null),
        ('f', "Clay pinch pots",
        [
            new StepSpec("Get your clay and your mat.", new AssetId("agency.finished.v1")),
            new StepSpec("Pinch the clay into a pot shape."),
            new StepSpec("Raise your hand if you want help.", new AssetId("agency.help.v1")),
            new StepSpec("Put your pot on the drying shelf."),
        ], null),
        ('g', "Washing your hands",
        [
            new StepSpec("Wet your hands with warm water."),
            new StepSpec("Rub soap on your hands and count to twenty."),
            new StepSpec("Dry your hands with a paper towel."),
            new StepSpec("Throw the towel in the bin."),
        ], null),
        ('h', "Paper airplane",
        [
            new StepSpec("Fold the paper in half the long way."),
            new StepSpec("Unfold it so it lies flat."),
            new StepSpec("Fold the top corners in to the center line."),
            new StepSpec("Test-fly your plane toward the target."),
        ], null),
    };

    foreach (var (letter, title, steps, targetLocale) in packets)
    {
        var document = AllAboardBuilders.TaskStrip(title, steps, catalog, targetLocale: targetLocale);
        var approved = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green),
            "sample-teacher@example.org",
            DocumentValidator.Validate(document),
            approvedAt);
        var print = await renderer.RenderAsync(
            approved, new RenderRequest(RenderTarget.PrintHtml, RenderAudience.Learner), CancellationToken.None);
        await File.WriteAllBytesAsync(
            Path.Combine(outputDirectory, $"packet-{letter}.print.html"), print.Content.ToArray());
    }

    Console.WriteLine($"Seeded review packets written to {outputDirectory}");
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

Console.WriteLine($"Samples written to {outputDirectory}");
return 0;

async Task WriteRenderAsync(ApprovedArtifact artifact, string fileName, RenderAudience audience)
{
    var output = await renderer.RenderAsync(
        artifact, new RenderRequest(RenderTarget.AccessibleHtml, audience), CancellationToken.None);
    await File.WriteAllBytesAsync(Path.Combine(outputDirectory, fileName), output.Content.ToArray());
}
