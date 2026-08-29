// Generates the canonical accessible sample outputs for the 0.1-alpha evidence
// bundle (universal Definition of Done: "canonical accessible sample outputs").
// Deterministic: fixed approval instant, fixed content, the shipped libre pack.
// Usage: SampleGenerator <repoRoot> <outputDirectory>

using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.AllAboard;
using Foundry.Rendering;
using Foundry.Storage;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: SampleGenerator <repoRoot> <outputDirectory>");
    return 1;
}

var repoRoot = args[0];
var outputDirectory = args[1];
Directory.CreateDirectory(outputDirectory);

var approvedAt = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
var catalog = new JsonAssetCatalog(Path.Combine(repoRoot, "assets", "symbols"));
var renderer = new AccessibleHtmlRenderer();
var store = new OcfprojProjectStore(outputDirectory, renderer, catalog);

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

Console.WriteLine($"Samples written to {outputDirectory}");
return 0;

async Task WriteRenderAsync(ApprovedArtifact artifact, string fileName, RenderAudience audience)
{
    var output = await renderer.RenderAsync(
        artifact, new RenderRequest(RenderTarget.AccessibleHtml, audience), CancellationToken.None);
    await File.WriteAllBytesAsync(Path.Combine(outputDirectory, fileName), output.Content.ToArray());
}
