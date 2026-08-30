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
            DraftArtifact.New(document, DataLane.Green, ArtifactPurpose.ClassroomSupport),
            "teacher@example.org",
            [],
            SomeInstant);
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
        Assert.Equal(ArtifactPurpose.ClassroomSupport, loaded.Manifest.Purpose);
        Assert.Equal(["agency.help.v1", "agency.finished.v1"], loaded.Manifest.AssetIds);
    }

    [Fact]
    public async Task A_reopened_project_keeps_its_validated_assets_for_render_and_resave()
    {
        await _store.SaveGreenProjectAsync(ApprovedStrip(), Request("portable-assets"), CancellationToken.None);
        var loaded = await _store.LoadProjectAsync("portable-assets", CancellationToken.None);

        Assert.NotNull(loaded.Assets);
        Assert.NotNull(loaded.Assets.Find(new AssetId("agency.help.v1")));
        Assert.True(loaded.Assets.TryGetContent(new AssetId("agency.help.v1"), out var originalBytes, out var mimeType));
        Assert.Equal("image/svg+xml", mimeType);

        var reopenedApproval = ApprovalGate.Approve(
            DraftArtifact.New(loaded.Document, DataLane.Green),
            "teacher@example.org",
            DocumentValidator.Validate(loaded.Document),
            SomeInstant);
        var rendered = await new AccessibleHtmlRenderer(loaded.Assets).RenderAsync(
            reopenedApproval,
            new RenderRequest(RenderTarget.AccessibleHtml),
            CancellationToken.None);
        Assert.Contains(
            "<img src=\"data:image/svg+xml;base64,",
            Encoding.UTF8.GetString(rendered.Content.Span),
            StringComparison.Ordinal);

        var secondRoot = Path.Combine(_root, "resaved");
        var secondStore = new OcfprojProjectStore(
            secondRoot,
            new AccessibleHtmlRenderer(),
            loaded.Assets);
        await secondStore.SaveGreenProjectAsync(
            reopenedApproval,
            Request("portable-assets-resaved"),
            CancellationToken.None);
        var reopenedAgain = await secondStore.LoadProjectAsync("portable-assets-resaved", CancellationToken.None);

        Assert.NotNull(reopenedAgain.Assets);
        Assert.True(reopenedAgain.Assets.TryGetContent(
            new AssetId("agency.help.v1"),
            out var resavedBytes,
            out var resavedMimeType));
        Assert.Equal(mimeType, resavedMimeType);
        Assert.Equal(originalBytes.ToArray(), resavedBytes.ToArray());
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
    public async Task A_contextual_save_writes_exact_validation_and_uses_the_bound_render_profile()
    {
        var document = new ArtifactDocument(
        [
            new Heading(1, "Synthetic contextual project"),
            new TeacherOnlyNotice("Synthetic teacher-only proof"),
        ], "en");
        var warning = ValidationIssue.Warning("synthetic.notice", "Synthetic reviewed notice.");
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green, ArtifactPurpose.ClassroomSupport),
            "teacher@example.org",
            [warning],
            SomeInstant);
        var validation = ProjectValidationEnvelope.Exact(
            artifact,
            "all-aboard.task-strip",
            "0.1.0");
        var profile = ProjectRenderProfile.For(
            artifact,
            RenderAudience.Teacher,
            175,
            targetLanguageFirst: true);
        var request = Request("contextual") with
        {
            Validation = validation,
            RenderProfile = profile,
        };

        await _store.SaveGreenProjectAsync(artifact, request, CancellationToken.None);

        using (var archive = ZipFile.OpenRead(_store.PathFor("contextual")))
        {
            Assert.NotNull(archive.GetEntry("validation.json"));
            Assert.NotNull(archive.GetEntry("render-profile.json"));
            using var snapshotReader = new StreamReader(archive.GetEntry("snapshot.html")!.Open(), Encoding.UTF8);
            var snapshot = snapshotReader.ReadToEnd();
            Assert.Contains("body { font-size: 175%; }", snapshot, StringComparison.Ordinal);
            Assert.DoesNotContain("Synthetic teacher-only proof", snapshot, StringComparison.Ordinal);
            Assert.DoesNotContain("teacher@example.org", snapshot, StringComparison.Ordinal);
        }

        var loaded = await _store.LoadProjectAsync("contextual", CancellationToken.None);
        Assert.NotNull(loaded.Validation);
        Assert.Equal(validation.SchemaVersion, loaded.Validation.SchemaVersion);
        Assert.Equal(validation.Kind, loaded.Validation.Kind);
        Assert.Equal(validation.RecipeId, loaded.Validation.RecipeId);
        Assert.Equal(validation.RecipeVersion, loaded.Validation.RecipeVersion);
        Assert.Equal(validation.Lane, loaded.Validation.Lane);
        Assert.Equal(validation.Purpose, loaded.Validation.Purpose);
        Assert.Equal(validation.ArtifactSha256, loaded.Validation.ArtifactSha256);
        Assert.Equal(validation.UntrustedNoticeCodes, loaded.Validation.UntrustedNoticeCodes);
        Assert.Equal(profile, loaded.RenderProfile);
        Assert.Equal(["synthetic.notice"], loaded.Validation.UntrustedNoticeCodes);
    }

    [Fact]
    public async Task Save_context_is_all_or_nothing_and_must_bind_to_the_exact_artifact()
    {
        var artifact = ApprovedStrip();
        var validation = ProjectValidationEnvelope.Exact(
            artifact,
            "all-aboard.task-strip",
            "0.1.0");
        var profile = ProjectRenderProfile.For(artifact);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.SaveGreenProjectAsync(
            artifact,
            Request("half-context") with { Validation = validation },
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _store.SaveGreenProjectAsync(
            artifact,
            Request("wrong-recipe") with
            {
                Validation = validation with { RecipeId = "different.recipe" },
                RenderProfile = profile,
            },
            CancellationToken.None));

        Assert.False(File.Exists(_store.PathFor("half-context")));
        Assert.False(File.Exists(_store.PathFor("wrong-recipe")));
    }

    [Fact]
    public void Snapshot_validation_accepts_inline_vector_markup_and_literal_url_text()
    {
        var snapshot =
            """
            <!DOCTYPE html>
            <html lang="en"><head><meta charset="utf-8"><title>Vector</title>
            <style>body { font-family: system-ui, sans-serif; }</style></head>
            <body><p>Read https://example.invalid and the literal text src= before class.</p>
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10" width="10mm" height="10mm">
            <rect x="0" y="0" width="10" height="10" fill="none" stroke="#000"/>
            <text x="5" y="5">https://example.invalid is inert text</text></svg></body></html>
            """;

        OcfprojPackageValidator.ValidateSnapshotBytes(Encoding.UTF8.GetBytes(snapshot));
    }

    [Theory]
    [InlineData("<p src=\"https://example.invalid/pixel\">external source</p>")]
    [InlineData("<p onclick=\"alert(1)\">event handler</p>")]
    [InlineData("<p style=\"background: red\">inline style</p>")]
    [InlineData("<p href=\"https://example.invalid\">external link</p>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect fill=\"url(https://example.invalid/a.svg)\"/></svg>")]
    [InlineData("<style>body { background: url(https://example.invalid/a.png); }</style>")]
    [InlineData("<meta http-equiv=\"refresh\" content=\"0; url=https://example.invalid\">")]
    [InlineData("<iframe title=\"active\"></iframe>")]
    public void Snapshot_validation_rejects_active_tags_external_attributes_and_style_sources(string payload)
    {
        var snapshot = $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Hostile</title></head><body>{payload}</body></html>";

        Assert.ThrowsAny<InvalidOperationException>(
            () => OcfprojPackageValidator.ValidateSnapshotBytes(Encoding.UTF8.GetBytes(snapshot)));
    }

    [Fact]
    public async Task A_safe_but_unrelated_snapshot_is_refused_by_exact_semantic_correspondence()
    {
        await _store.SaveGreenProjectAsync(ApprovedStrip(), Request("snapshot-substitution"), CancellationToken.None);
        var unrelated =
            "<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n<title>Unrelated</title>\n<style>body { font-family: system-ui; }</style>\n</head>\n<body>\n<h1>A different safe artifact</h1>\n</body>\n</html>\n";
        var unrelatedBytes = Encoding.UTF8.GetBytes(unrelated);
        OcfprojPackageValidator.ValidateSnapshotBytes(unrelatedBytes);

        var path = _store.PathFor("snapshot-substitution");
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update))
        {
            archive.GetEntry("snapshot.html")!.Delete();
            var replacement = archive.CreateEntry("snapshot.html", CompressionLevel.Optimal);
            replacement.LastWriteTime = SomeInstant;
            await using var entry = replacement.Open();
            await entry.WriteAsync(unrelatedBytes);
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("snapshot-substitution", CancellationToken.None));
        Assert.Contains("does not correspond exactly", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_snapshot_renderer_version_is_refused_instead_of_guessed()
    {
        await _store.SaveGreenProjectAsync(ApprovedStrip(), Request("unknown-renderer"), CancellationToken.None);
        var path = _store.PathFor("unknown-renderer");
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update))
        {
            var manifestEntry = archive.GetEntry("manifest.json")!;
            string manifest;
            using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
            {
                manifest = reader.ReadToEnd();
            }

            var changed = manifest.Replace(
                $"\"engineVersion\": \"{EngineIdentity.EngineVersion}\"",
                "\"engineVersion\": \"unadmitted-synthetic-version\"",
                StringComparison.Ordinal);
            Assert.NotEqual(manifest, changed);
            manifestEntry.Delete();
            var replacement = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            replacement.LastWriteTime = SomeInstant;
            await using var output = replacement.Open();
            await output.WriteAsync(Encoding.UTF8.GetBytes(changed));
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("unknown-renderer", CancellationToken.None));
        Assert.Contains("no admitted exact renderer version", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_staged_candidate_cannot_damage_the_prior_valid_package()
    {
        await _store.SaveGreenProjectAsync(ApprovedStrip(), Request("atomic-survival"), CancellationToken.None);
        var path = _store.PathFor("atomic-survival");
        var priorBytes = await File.ReadAllBytesAsync(path);
        var replacement = ApprovalGate.Approve(
            DraftArtifact.New(
                new ArtifactDocument([new Heading(1, "Replacement that must not land")], "en"),
                DataLane.Green,
                ArtifactPurpose.ClassroomSupport),
            "teacher@example.org",
            [],
            SomeInstant);
        var invalidCandidateStore = new OcfprojProjectStore(
            _root,
            new SafeButUnrelatedSnapshotRenderer(),
            _catalog);

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => invalidCandidateStore.SaveGreenProjectAsync(
                replacement,
                Request("atomic-survival"),
                CancellationToken.None));

        Assert.Contains("does not correspond exactly", exception.Message, StringComparison.Ordinal);
        Assert.Equal(priorBytes, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.stage", SearchOption.TopDirectoryOnly));
        var reopened = await _store.LoadProjectAsync("atomic-survival", CancellationToken.None);
        Assert.Equal("Watering the class plants", Assert.IsType<Heading>(reopened.Document.Nodes[0]).Text);
    }

    [Fact]
    public async Task A_canceled_staged_save_cannot_damage_the_prior_valid_package()
    {
        await _store.SaveGreenProjectAsync(ApprovedStrip(), Request("atomic-cancellation"), CancellationToken.None);
        var path = _store.PathFor("atomic-cancellation");
        var priorBytes = await File.ReadAllBytesAsync(path);
        var replacement = ApprovalGate.Approve(
            DraftArtifact.New(
                new ArtifactDocument([new Heading(1, "Canceled replacement")], "en"),
                DataLane.Green,
                ArtifactPurpose.ClassroomSupport),
            "teacher@example.org",
            [],
            SomeInstant);
        using var cancellation = new CancellationTokenSource();
        var canceledStore = new OcfprojProjectStore(
            _root,
            new CancelAfterRenderingRenderer(cancellation),
            _catalog);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => canceledStore.SaveGreenProjectAsync(
                replacement,
                Request("atomic-cancellation"),
                cancellation.Token));

        Assert.Equal(priorBytes, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.stage", SearchOption.TopDirectoryOnly));
        var reopened = await _store.LoadProjectAsync("atomic-cancellation", CancellationToken.None);
        Assert.Equal("Watering the class plants", Assert.IsType<Heading>(reopened.Document.Nodes[0]).Text);
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
    public async Task Hostile_entry_names_are_refused_and_nothing_is_ever_extracted()
    {
        // A package with a zip-slip entry beside valid content is rejected by
        // the exact topology validator before anything can be extracted.
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
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _store.LoadProjectAsync("hostile", CancellationToken.None));

        Assert.Contains("unsafe", exception.Message, StringComparison.Ordinal);
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
    public async Task Entry_stamps_come_from_the_save_instant_not_the_clock()
    {
        // The cause that hid: two fast runs share a DOS two-second window and
        // look identical, so this pins the stamp itself rather than comparing
        // runs. Asserted on .DateTime, which is timezone-independent here
        // because the writer stamps from UTC.
        await _store.SaveGreenProjectAsync(ApprovedStrip(), Request("stamped"), CancellationToken.None);

        using var archive = ZipFile.OpenRead(_store.PathFor("stamped"));
        Assert.NotEmpty(archive.Entries);
        foreach (var entry in archive.Entries)
        {
            Assert.Equal(SomeInstant.UtcDateTime, entry.LastWriteTime.DateTime);
        }
    }

    [Fact]
    public async Task The_project_id_is_derived_from_the_save_not_from_chance()
    {
        // The cause that actually differed across sample runs: Guid.NewGuid().
        await _store.SaveGreenProjectAsync(ApprovedStrip(), Request("derived"), CancellationToken.None);
        var first = (await _store.LoadProjectAsync("derived", CancellationToken.None)).Manifest.ProjectId;

        await _store.SaveGreenProjectAsync(ApprovedStrip(), Request("derived"), CancellationToken.None);
        var second = (await _store.LoadProjectAsync("derived", CancellationToken.None)).Manifest.ProjectId;

        Assert.Equal(first, second);
        Assert.NotEqual(Guid.Empty, first);

        // Different projects still get different ids: the id identifies the
        // project, it does not merely repeat.
        await _store.SaveGreenProjectAsync(ApprovedStrip(), Request("derived-elsewhere"), CancellationToken.None);
        var other = (await _store.LoadProjectAsync("derived-elsewhere", CancellationToken.None)).Manifest.ProjectId;
        Assert.NotEqual(first, other);
    }

    [Fact]
    public async Task Two_identical_saves_are_byte_identical()
    {
        // The whole claim the CI determinism gate makes, stated here as
        // arithmetic: the writer is a pure function of its inputs. This test
        // standing green is what permits the gate's exclusion to be deleted.
        var artifact = ApprovedStrip();

        await _store.SaveGreenProjectAsync(artifact, Request("twice"), CancellationToken.None);
        var first = await File.ReadAllBytesAsync(_store.PathFor("twice"));

        await _store.SaveGreenProjectAsync(artifact, Request("twice"), CancellationToken.None);
        var second = await File.ReadAllBytesAsync(_store.PathFor("twice"));

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task A_request_with_no_save_instant_writes_the_zip_epoch_rather_than_throwing()
    {
        // ProjectSaveRequest.SavedAtUtc defaults to DateTimeOffset.MinValue,
        // which no DOS timestamp can hold; the clamp keeps the save quiet.
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(new ArtifactDocument([new Heading(1, "Undated")]), DataLane.Green),
            "teacher@example.org", [], SomeInstant);

        await _store.SaveGreenProjectAsync(
            artifact,
            Request("undated") with { SavedAtUtc = default },
            CancellationToken.None);

        using var archive = ZipFile.OpenRead(_store.PathFor("undated"));
        Assert.Equal(new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), archive.Entries[0].LastWriteTime.DateTime);
    }

    [Fact]
    public void Destination_hints_cannot_smuggle_paths()
    {
        Assert.EndsWith("watering-plants.ocfproj", _store.PathFor("..\\..\\watering-plants"), StringComparison.Ordinal);
        Assert.DoesNotContain("..", Path.GetFileName(_store.PathFor("..\\evil")), StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => _store.PathFor("..."));
    }

    private sealed class SafeButUnrelatedSnapshotRenderer : IRenderer
    {
        public Task<RenderedOutput> RenderAsync(
            ApprovedArtifact artifact,
            RenderRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            const string unrelated =
                "<!DOCTYPE html>\n<html lang=\"en\"><head><meta charset=\"utf-8\"><title>Safe</title><style>body { font-family: system-ui; }</style></head><body><h1>Safe but unrelated</h1></body></html>\n";
            return Task.FromResult(new RenderedOutput(
                request.Target,
                Encoding.UTF8.GetBytes(unrelated),
                "text/html"));
        }
    }

    private sealed class CancelAfterRenderingRenderer(CancellationTokenSource cancellation) : IRenderer
    {
        public async Task<RenderedOutput> RenderAsync(
            ApprovedArtifact artifact,
            RenderRequest request,
            CancellationToken cancellationToken)
        {
            var output = await new AccessibleHtmlRenderer().RenderAsync(
                artifact,
                request,
                cancellationToken);
            await cancellation.CancelAsync();
            return output;
        }
    }
}
