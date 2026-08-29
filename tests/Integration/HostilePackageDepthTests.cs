using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.AllAboard;
using Foundry.Rendering;
using Foundry.Storage;

namespace Foundry.Tests.Integration;

/// <summary>
/// The hostile-package depth pass (handover 2026-08-29, forge item 6): the
/// reader is fuzzed with truncated central directories, colliding entry names,
/// and packages whose manifest disagrees with the engine, the lane contract,
/// or the package's own contents. Every mutation must fail loudly — a package
/// that cannot be trusted end to end is not a project, it is an attack or an
/// accident, and both get the same refusal.
/// </summary>
public class HostilePackageDepthTests : IDisposable
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ocf-hostile", Guid.NewGuid().ToString("N"));
    private readonly OcfprojProjectStore _store;

    public HostilePackageDepthTests()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "OpenClassroomFoundry.slnx")))
        {
            repo = repo.Parent;
        }

        var catalog = new JsonAssetCatalog(Path.Combine(repo!.FullName, "assets", "symbols"));
        _store = new OcfprojProjectStore(_root, new AccessibleHtmlRenderer(), catalog);

        var document = AllAboardBuilders.TaskStrip(
            "Watering the class plants",
            [
                new StepSpec("Pick up the can.", new AssetId("agency.help.v1")),
                new StepSpec("Fill it to the line."),
                new StepSpec("Water each plant once."),
            ],
            catalog);
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green), "teacher@example.org", [], SomeInstant);
        _store.SaveGreenProjectAsync(
            artifact,
            new ProjectSaveRequest("valid", "all-aboard", "all-aboard.task-strip", "0.1.0", SomeInstant),
            CancellationToken.None).GetAwaiter().GetResult();
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

    [Fact]
    public async Task Truncated_central_directories_fail_loudly_at_every_cut_point()
    {
        var valid = await File.ReadAllBytesAsync(_store.PathFor("valid"));

        // The central directory and its end record live at the tail of a zip:
        // every one of these cuts destroys or damages it. 1% leaves bare magic
        // bytes; length-minus-four slices into the end-of-central-directory
        // record itself.
        int[] keepLengths =
        [
            valid.Length / 100,
            valid.Length / 4,
            valid.Length / 2,
            valid.Length * 3 / 4,
            valid.Length * 95 / 100,
            valid.Length - 4,
        ];

        foreach (var keep in keepLengths)
        {
            await File.WriteAllBytesAsync(_store.PathFor("truncated"), valid[..keep]);
            await Assert.ThrowsAnyAsync<Exception>(
                () => _store.LoadProjectAsync("truncated", CancellationToken.None));
        }
    }

    [Fact]
    public async Task Colliding_entry_names_are_refused_as_a_smuggling_vector()
    {
        // Two manifest entries: a scanner reads one, the app reads the other.
        var duplicated = CopyValid("duplicated");
        using (var archive = ZipFile.Open(duplicated, ZipArchiveMode.Update))
        {
            var second = archive.CreateEntry("manifest.json");
            await using var stream = second.Open();
            stream.Write("{}"u8);
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("duplicated", CancellationToken.None));
        Assert.Contains("colliding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Names_colliding_only_by_case_are_refused_too()
    {
        // GetEntry is case-sensitive, so MANIFEST.JSON slips past exact-name
        // reads while a case-insensitive filesystem or tool sees a duplicate.
        var cased = CopyValid("cased");
        using (var archive = ZipFile.Open(cased, ZipArchiveMode.Update))
        {
            var upper = archive.CreateEntry("MANIFEST.JSON");
            await using var stream = upper.Open();
            stream.Write("{}"u8);
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("cased", CancellationToken.None));
        Assert.Contains("colliding", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_manifest_claiming_a_lane_above_green_is_refused()
    {
        MutateManifest("amber-claim", manifest => manifest with { DataLane = DataLane.Amber });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("amber-claim", CancellationToken.None));
        Assert.Contains("Green", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_schema_version_is_refused_not_guessed_at()
    {
        MutateManifest("future-schema", manifest => manifest with { SchemaVersion = "999" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("future-schema", CancellationToken.None));
        Assert.Contains("schema version", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_manifest_declaring_assets_the_package_does_not_carry_is_refused()
    {
        MutateManifest("ghost-asset", manifest => manifest with
        {
            AssetIds = [.. manifest.AssetIds, "ghost.asset.v1"],
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("ghost-asset", CancellationToken.None));
        Assert.Contains("disagree", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_package_missing_a_declared_provenance_record_is_refused()
    {
        var stripped = CopyValid("stripped");
        using (var archive = ZipFile.Open(stripped, ZipArchiveMode.Update))
        {
            archive.GetEntry("provenance/agency.help.v1.json")!.Delete();
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("stripped", CancellationToken.None));
        Assert.Contains("disagree", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tampered_artifact_that_fails_structural_validation_is_refused()
    {
        // Level-9 headings and blank text never leave the approval gate; a
        // package holding them was edited after the fact.
        var hostileDocument = new ArtifactDocument([new Heading(9, "   ")]);
        ReplaceEntry("tampered-artifact", "artifact.json",
            JsonSerializer.SerializeToUtf8Bytes(hostileDocument, Json));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("tampered-artifact", CancellationToken.None));
        Assert.Contains("tampered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_untouched_package_still_loads_after_all_that_hostility()
    {
        var loaded = await _store.LoadProjectAsync("valid", CancellationToken.None);

        Assert.Equal(DataLane.Green, loaded.Manifest.DataLane);
        Assert.Equal(EngineIdentity.ProjectSchemaVersion, loaded.Manifest.SchemaVersion);
    }

    private string CopyValid(string hint)
    {
        var destination = _store.PathFor(hint);
        File.Copy(_store.PathFor("valid"), destination);
        return destination;
    }

    private void MutateManifest(string hint, Func<ProjectManifest, ProjectManifest> mutate)
    {
        var path = CopyValid(hint);
        ProjectManifest manifest;
        using (var archive = ZipFile.OpenRead(path))
        using (var reader = new StreamReader(archive.GetEntry("manifest.json")!.Open()))
        {
            manifest = JsonSerializer.Deserialize<ProjectManifest>(reader.ReadToEnd(), Json)!;
        }

        ReplaceEntryAt(path, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(mutate(manifest), Json));
    }

    private void ReplaceEntry(string hint, string entryName, byte[] content)
        => ReplaceEntryAt(CopyValid(hint), entryName, content);

    private static void ReplaceEntryAt(string path, string entryName, byte[] content)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        archive.GetEntry(entryName)!.Delete();
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        stream.Write(content);
    }
}
