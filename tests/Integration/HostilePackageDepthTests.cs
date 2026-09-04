using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
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
    private readonly JsonAssetCatalog _catalog;

    public HostilePackageDepthTests()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "OpenClassroomFoundry.slnx")))
        {
            repo = repo.Parent;
        }

        _catalog = new JsonAssetCatalog(Path.Combine(repo!.FullName, "assets", "symbols"));
        _store = new OcfprojProjectStore(_root, new AccessibleHtmlRenderer(), _catalog);

        var document = AllAboardBuilders.TaskStrip(
            "Watering the class plants",
            [
                new StepSpec("Pick up the can.", new AssetId("agency.help.v1")),
                new StepSpec("Fill it to the line."),
                new StepSpec("Water each plant once."),
            ],
            _catalog);
        var reviewedAssets = ExactAssetCatalogSnapshot.CaptureForReview(document, _catalog);
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green),
            "teacher@example.org",
            [],
            SomeInstant,
            reviewedAssets.Bindings);
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
    public async Task A_private_project_can_retain_non_open_nonredistributable_provenance()
    {
        MutateProvenance(
            "private-rights",
            provenance => provenance with
            {
                License = "LicenseRef-teacher-local",
                Redistributable = false,
            });

        var loaded = await _store.LoadProjectAsync("private-rights", CancellationToken.None);

        var assets = Assert.IsType<IAssetCatalog>(loaded.Assets, exactMatch: false);
        var provenance = assets.Find(new AssetId("agency.help.v1"));
        Assert.NotNull(provenance);
        Assert.Equal("LicenseRef-teacher-local", provenance.License);
        Assert.False(provenance.Redistributable);
    }

    [Fact]
    public async Task A_private_project_cannot_forge_redistribution_for_a_non_open_license()
    {
        MutateProvenance(
            "forged-private-rights",
            provenance => provenance with
            {
                License = "LicenseRef-teacher-local",
                Redistributable = true,
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("forged-private-rights", CancellationToken.None));

        Assert.Contains("license and redistribution", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Package_provenance_required_text_uses_the_catalog_safety_policy()
    {
        (string Hint, Func<AssetProvenance, AssetProvenance> Mutate)[] hostileCases =
        [
            ("invisible-required-provenance", provenance => provenance with { Source = "\u200B" }),
            ("directional-required-provenance", provenance => provenance with { Creator = "Synthetic\u202Ecreator" }),
            ("punctuation-required-provenance", provenance => provenance with { License = "---" }),
            ("oversized-mime-provenance", provenance => provenance with { MimeType = new string('m', 65) }),
        ];

        foreach (var (hint, mutate) in hostileCases)
        {
            MutateProvenance(hint, mutate);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _store.LoadProjectAsync(hint, CancellationToken.None));

            Assert.Contains("provenance record has invalid", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Every_optional_package_provenance_field_uses_the_catalog_safety_policy()
    {
        (string Hint, Func<AssetProvenance, AssetProvenance> Mutate)[] hostileCases =
        [
            ("oversized-ambiguity-provenance", provenance => provenance with
            {
                AmbiguityNotes = new string('a', 2049),
            }),
            ("control-attribution-provenance", provenance => provenance with
            {
                RequiredAttribution = "Synthetic credit\nsecond line",
            }),
            ("format-modification-provenance", provenance => provenance with
            {
                Modifications = "Synthetic\u2066modification",
            }),
        ];

        foreach (var (hint, mutate) in hostileCases)
        {
            MutateProvenance(hint, mutate);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _store.LoadProjectAsync(hint, CancellationToken.None));

            Assert.Contains("provenance record has invalid", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Safe_optional_package_provenance_survives_load_and_resave_exactly()
    {
        MutateProvenance(
            "safe-optional-provenance",
            provenance => provenance with
            {
                AmbiguityNotes = "May resemble another synthetic classroom marker.",
                RequiredAttribution = "Synthetic attribution: Example Artist.",
                Modifications = "Re-encoded as a passive SVG for this synthetic fixture.",
            });

        var loaded = await _store.LoadProjectAsync("safe-optional-provenance", CancellationToken.None);
        var loadedAssets = Assert.IsType<IAssetCatalog>(loaded.Assets, exactMatch: false);
        var loadedProvenance = Assert.IsType<AssetProvenance>(
            loadedAssets.Find(new AssetId("agency.help.v1")));

        var reopenedApproval = ApprovalGate.Approve(
            DraftArtifact.New(loaded.Document, DataLane.Green),
            "teacher@example.org",
            DocumentValidator.Validate(loaded.Document),
            SomeInstant,
            ExactAssetCatalogSnapshot.CaptureForReview(loaded.Document, loadedAssets).Bindings);
        var resavedStore = new OcfprojProjectStore(
            Path.Combine(_root, "safe-optional-resaved"),
            new AccessibleHtmlRenderer(),
            loadedAssets);
        await resavedStore.SaveGreenProjectAsync(
            reopenedApproval,
            new ProjectSaveRequest(
                "resaved",
                loaded.Manifest.ModuleId,
                loaded.Manifest.RecipeId,
                loaded.Manifest.RecipeVersion,
                SomeInstant),
            CancellationToken.None);

        var reopened = await resavedStore.LoadProjectAsync("resaved", CancellationToken.None);
        var reopenedAssets = Assert.IsType<IAssetCatalog>(reopened.Assets, exactMatch: false);
        var reopenedProvenance = Assert.IsType<AssetProvenance>(
            reopenedAssets.Find(new AssetId("agency.help.v1")));

        Assert.Equal(loadedProvenance, reopenedProvenance);
    }

    [Fact]
    public async Task A_structurally_valid_png_preview_is_admitted()
    {
        var path = CopyValid("valid-preview");
        ReplaceEntryAt(path, "previews/page-1.png", BuildPng(1, 1));

        var loaded = await _store.LoadProjectAsync("valid-preview", CancellationToken.None);

        Assert.Equal(DataLane.Green, loaded.Manifest.DataLane);
    }

    [Fact]
    public async Task A_signature_only_png_preview_is_refused()
    {
        var path = CopyValid("signature-preview");
        byte[] signatureOnly = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        ReplaceEntryAt(path, "previews/page-1.png", signatureOnly);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("signature-preview", CancellationToken.None));

        Assert.Contains("not a bounded PNG image", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_crc_corrupt_png_preview_is_refused()
    {
        var path = CopyValid("crc-preview");
        var corrupt = BuildPng(1, 1);
        corrupt[^1] ^= 0x01;
        ReplaceEntryAt(path, "previews/page-1.png", corrupt);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("crc-preview", CancellationToken.None));

        Assert.Contains("not a bounded PNG image", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_excessive_dimension_png_preview_is_refused()
    {
        var path = CopyValid("dimension-preview");
        ReplaceEntryAt(path, "previews/page-1.png", BuildPng(16_384, 16_384));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("dimension-preview", CancellationToken.None));

        Assert.Contains("not a bounded PNG image", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_hash_consistent_active_svg_asset_is_still_refused()
    {
        var path = CopyValid("active-svg");
        var active = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();
        AssetProvenance provenance;
        using (var archive = ZipFile.OpenRead(path))
        using (var stream = archive.GetEntry("provenance/agency.help.v1.json")!.Open())
        {
            provenance = JsonSerializer.Deserialize<AssetProvenance>(stream, Json)!;
        }

        ReplaceEntryAt(path, "assets/help.svg", active);
        ReplaceEntryAt(
            path,
            "provenance/agency.help.v1.json",
            JsonSerializer.SerializeToUtf8Bytes(
                provenance with { Sha256 = Convert.ToHexString(SHA256.HashData(active)) },
                Json));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("active-svg", CancellationToken.None));
        Assert.Contains("supported, self-contained", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_hash_consistent_signature_only_png_asset_is_refused()
    {
        var path = CopyValid("truncated-png");
        byte[] truncated = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        AssetProvenance provenance;
        using (var archive = ZipFile.OpenRead(path))
        using (var stream = archive.GetEntry("provenance/agency.help.v1.json")!.Open())
        {
            provenance = JsonSerializer.Deserialize<AssetProvenance>(stream, Json)!;
        }

        ReplaceEntryAt(path, "assets/help.svg", truncated);
        ReplaceEntryAt(
            path,
            "provenance/agency.help.v1.json",
            JsonSerializer.SerializeToUtf8Bytes(
                provenance with
                {
                    MimeType = "image/png",
                    Sha256 = Convert.ToHexString(SHA256.HashData(truncated)),
                },
                Json));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("truncated-png", CancellationToken.None));
        Assert.Contains("supported, self-contained", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_package_cannot_amplify_one_asset_through_repeated_references()
    {
        var repeated = new ArtifactDocument(
            [.. Enumerable.Range(1, 513)
                .Select(index => (DocumentNode)new ImageReference(
                    new AssetId("agency.help.v1"),
                    $"Synthetic repeated symbol {index}"))],
            "en");
        ReplaceEntry(
            "repeated-asset",
            "artifact.json",
            JsonSerializer.SerializeToUtf8Bytes(repeated, Json));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("repeated-asset", CancellationToken.None));

        Assert.Contains("image-reference limit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_package_cannot_exceed_the_document_node_bound()
    {
        var oversized = new ArtifactDocument(
            [.. Enumerable.Range(1, 4097)
                .Select(index => (DocumentNode)new Paragraph($"Synthetic bounded paragraph {index}"))],
            "en");
        ReplaceEntry(
            "too-many-nodes",
            "artifact.json",
            JsonSerializer.SerializeToUtf8Bytes(oversized, Json));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("too-many-nodes", CancellationToken.None));

        Assert.Contains("node limit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_package_cannot_exceed_the_cumulative_embedded_derivative_budget()
    {
        var path = CopyValid("asset-derivative-budget");
        var id = new AssetId("agency.help.v1");
        var document = new ArtifactDocument(
            [.. Enumerable.Range(1, 28)
                .Select(index => (DocumentNode)new ImageReference(id, $"Synthetic large symbol {index}"))],
            "en");
        var approved = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green),
            "teacher@example.org",
            [],
            SomeInstant,
            ExactAssetCatalogSnapshot.CaptureForReview(document, _catalog).Bindings);
        var snapshot = await AccessibleHtmlRenderer.RenderPortableSnapshotAsync(
            approved,
            new RenderRequest(RenderTarget.AccessibleHtml, RenderAudience.Learner),
            CancellationToken.None);
        var largeSvg = Encoding.UTF8.GetBytes(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"><desc>{new string(' ', 900_000)}</desc></svg>");
        AssetProvenance provenance;
        using (var archive = ZipFile.OpenRead(path))
        using (var stream = archive.GetEntry("provenance/agency.help.v1.json")!.Open())
        {
            provenance = JsonSerializer.Deserialize<AssetProvenance>(stream, Json)!;
        }

        ReplaceEntryAt(path, "artifact.json", JsonSerializer.SerializeToUtf8Bytes(document, Json));
        ReplaceEntryAt(path, "snapshot.html", snapshot.Content.ToArray());
        ReplaceEntryAt(path, "assets/help.svg", largeSvg);
        ReplaceEntryAt(
            path,
            "provenance/agency.help.v1.json",
            JsonSerializer.SerializeToUtf8Bytes(
                provenance with { Sha256 = Convert.ToHexString(SHA256.HashData(largeSvg)) },
                Json));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("asset-derivative-budget", CancellationToken.None));

        Assert.Contains("embedded-derivative budget", exception.Message, StringComparison.Ordinal);
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

    private void MutateProvenance(string hint, Func<AssetProvenance, AssetProvenance> mutate)
    {
        var path = CopyValid(hint);
        AssetProvenance provenance;
        using (var archive = ZipFile.OpenRead(path))
        using (var stream = archive.GetEntry("provenance/agency.help.v1.json")!.Open())
        {
            provenance = JsonSerializer.Deserialize<AssetProvenance>(stream, Json)!;
        }

        ReplaceEntryAt(
            path,
            "provenance/agency.help.v1.json",
            JsonSerializer.SerializeToUtf8Bytes(mutate(provenance), Json));
    }

    private void ReplaceEntry(string hint, string entryName, byte[] content)
        => ReplaceEntryAt(CopyValid(hint), entryName, content);

    private static void ReplaceEntryAt(string path, string entryName, byte[] content)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        archive.GetEntry(entryName)?.Delete();
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static byte[] BuildPng(int width, int height)
    {
        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header[..4], (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.Slice(4, 4), (uint)height);
        header[8] = 8;
        header[9] = 6;
        WritePngChunk(output, "IHDR"u8, header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write([0, 0, 0, 0, 0]);
        }

        WritePngChunk(output, "IDAT"u8, compressed.ToArray());
        WritePngChunk(output, "IEND"u8, []);
        return output.ToArray();
    }

    private static void WritePngChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        output.Write(length);
        output.Write(type);
        output.Write(data);

        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, PngCrc(type, data));
        output.Write(crc);
    }

    private static uint PngCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in type)
        {
            crc = UpdatePngCrc(crc, value);
        }

        foreach (var value in data)
        {
            crc = UpdatePngCrc(crc, value);
        }

        return ~crc;
    }

    private static uint UpdatePngCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }

        return crc;
    }
}
