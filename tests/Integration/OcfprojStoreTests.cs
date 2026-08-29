using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.AllAboard;
using Foundry.Rendering;
using Foundry.Storage;

namespace Foundry.Tests.Integration;

public class OcfprojStoreTests : IDisposable
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
    private readonly JsonAssetCatalog _catalog;
    private readonly OcfprojProjectStore _store;

    public OcfprojStoreTests()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "OpenClassroomFoundry.slnx")))
        {
            repo = repo.Parent;
        }

        _catalog = new JsonAssetCatalog(Path.Combine(repo!.FullName, "assets", "symbols"));
        _store = new OcfprojProjectStore(_root, new AccessibleHtmlRenderer(), _catalog);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp cleanup is best-effort.
        }
    }

    private ApprovedArtifact ApprovedStrip()
    {
        var document = AllAboardBuilders.TaskStrip(
            "Watering the class plants",
            [
                new StepSpec("Pick up the can.", new AssetId("agency.help.v1")),
                new StepSpec("Fill to the line."),
                new StepSpec("Water each plant once.", new AssetId("agency.finished.v1")),
            ],
            _catalog);

        return ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green), "teacher@example.org", [], SomeInstant);
    }

    private static ProjectSaveRequest Request(string hint) => new(
        hint, ModuleId: "all-aboard", RecipeId: "all-aboard.task-strip", RecipeVersion: "0.1.0", SavedAtUtc: SomeInstant);

    [Fact]
    public async Task A_saved_project_reopens_with_the_identical_document()
    {
        var artifact = ApprovedStrip();

        await _store.SaveGreenProjectAsync(artifact, Request("watering-plants"), CancellationToken.None);
        var loaded = await _store.LoadProjectAsync("watering-plants", CancellationToken.None);

        var savedJson = JsonSerializer.Serialize(artifact.Revision.Document);
        var loadedJson = JsonSerializer.Serialize(loaded.Document);
        Assert.Equal(savedJson, loadedJson);

        Assert.Equal("all-aboard.task-strip", loaded.Manifest.RecipeId);
        Assert.Equal(DataLane.Green, loaded.Manifest.DataLane);
        Assert.Equal(["agency.help.v1", "agency.finished.v1"], loaded.Manifest.AssetIds);
    }

    [Fact]
    public async Task The_package_carries_assets_provenance_and_a_readable_snapshot()
    {
        await _store.SaveGreenProjectAsync(ApprovedStrip(), Request("watering-plants"), CancellationToken.None);

        using var archive = ZipFile.OpenRead(_store.PathFor("watering-plants"));
        var entries = archive.Entries.Select(e => e.FullName).ToList();

        Assert.Contains("manifest.json", entries);
        Assert.Contains("artifact.json", entries);
        Assert.Contains("snapshot.html", entries);
        Assert.Contains("assets/help.svg", entries);
        Assert.Contains("provenance/agency.help.v1.json", entries);

        using var reader = new StreamReader(archive.GetEntry("snapshot.html")!.Open(), Encoding.UTF8);
        var snapshot = reader.ReadToEnd();
        Assert.Contains("Watering the class plants", snapshot, StringComparison.Ordinal);
        Assert.Contains("<ol class=\"steps\">", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_asset_blocks_the_save_entirely()
    {
        var document = new ArtifactDocument(
        [
            new Heading(1, "Mystery"),
            new ImageReference(new AssetId("proprietary.mystery"), "A symbol of unknown origin"),
        ]);
        var artifact = ApprovalGate.Approve(DraftArtifact.New(document, DataLane.Green), "teacher@example.org", [], SomeInstant);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.SaveGreenProjectAsync(artifact, Request("mystery"), CancellationToken.None));

        Assert.False(File.Exists(_store.PathFor("mystery")));
    }

    [Fact]
    public async Task Amber_artifacts_are_refused_by_the_real_store_too()
    {
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(new ArtifactDocument([new Paragraph("Response summary")]), DataLane.Amber),
            "teacher@example.org", [], SomeInstant);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.SaveGreenProjectAsync(artifact, Request("amber"), CancellationToken.None));
    }

    [Fact]
    public async Task An_oversized_entry_is_refused_before_it_is_read()
    {
        // R2-3: a manifest claiming 65 MB is not a classroom artifact.
        Directory.CreateDirectory(_root);
        var path = _store.PathFor("bomb");
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("manifest.json");
            await using var entryStream = entry.Open();
            var zeros = new byte[1024 * 1024];
            for (var written = 0L; written <= OcfprojProjectStore.MaxEntryBytes; written += zeros.Length)
            {
                await entryStream.WriteAsync(zeros);
            }
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("bomb", CancellationToken.None));
        Assert.Contains("ceiling", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Hostile_entry_names_are_inert_because_nothing_is_ever_extracted()
    {
        // A package with a zip-slip entry beside valid content: loading reads the
        // known entries by exact name and extracts nothing to disk.
        var artifact = ApprovedStrip();
        await _store.SaveGreenProjectAsync(artifact, Request("host"), CancellationToken.None);

        var hostilePath = _store.PathFor("hostile");
        File.Copy(_store.PathFor("host"), hostilePath);
        using (var stream = new FileStream(hostilePath, FileMode.Open, FileAccess.ReadWrite))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update))
        {
            var evil = archive.CreateEntry("../../evil.txt");
            await using var entryStream = evil.Open();
            entryStream.Write("escaped"u8);
        }

        var escapeTarget = Path.GetFullPath(Path.Combine(_root, "..", "evil.txt"));
        var loaded = await _store.LoadProjectAsync("hostile", CancellationToken.None);

        Assert.Equal(DataLane.Green, loaded.Manifest.DataLane);
        Assert.False(File.Exists(escapeTarget));
    }

    [Fact]
    public async Task Corrupt_and_malformed_packages_fail_loudly_not_quietly()
    {
        Directory.CreateDirectory(_root);

        // Truncated garbage is not a package.
        await File.WriteAllBytesAsync(_store.PathFor("garbage"), [0x50, 0x4B, 0x03, 0x04, 0xFF]);
        await Assert.ThrowsAnyAsync<Exception>(() => _store.LoadProjectAsync("garbage", CancellationToken.None));

        // A valid zip with malformed manifest JSON fails with a clear error.
        var malformedPath = _store.PathFor("malformed");
        using (var stream = new FileStream(malformedPath, FileMode.Create, FileAccess.Write))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("manifest.json");
            await using var entryStream = entry.Open();
            entryStream.Write("{ not json"u8);
        }

        await Assert.ThrowsAnyAsync<Exception>(() => _store.LoadProjectAsync("malformed", CancellationToken.None));
    }

    [Fact]
    public void Destination_hints_cannot_smuggle_paths()
    {
        Assert.EndsWith("watering-plants.ocfproj", _store.PathFor("..\\..\\watering-plants"), StringComparison.Ordinal);
        Assert.DoesNotContain("..", Path.GetFileName(_store.PathFor("..\\evil")), StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => _store.PathFor("..."));
    }
}
