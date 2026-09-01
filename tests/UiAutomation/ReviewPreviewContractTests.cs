// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Storage;
using Microsoft.Win32.SafeHandles;

namespace Foundry.Tests.UiAutomation;

public sealed class ReviewPreviewContractTests
{
    [Fact]
    public void Native_pdf_probe_falls_through_when_a_vector_glyph_is_not_encodable()
    {
        var document = new ArtifactDocument(
        [
            new VectorGraphic(
                100,
                100,
                [new TextLabel(50, 50, "★")],
                "A synthetic sheet with a star"),
        ]);
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green),
            "synthetic-reviewer@example.invalid",
            [],
            new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero));

        Assert.True(Rendering.VectorPdfWriter.CanWrite(document));
        Assert.Null(AppServices.TryRenderNativePdf(artifact, RenderAudience.Learner));
    }

    [Fact]
    public async Task Transactional_export_preserves_an_existing_destination_and_removes_its_stage_on_failure()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(directory, "existing.pdf");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(destination, "original exact bytes");
        try
        {
            await using (var destinationLock = new FileStream(
                destination,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None))
            {
                var failure = await Record.ExceptionAsync(() => AppServices.WriteExportBytesAsync(
                    destination,
                    "replacement bytes"u8.ToArray(),
                    CancellationToken.None));
                Assert.True(
                    failure is IOException or UnauthorizedAccessException,
                    $"Expected a filesystem promotion refusal; received {failure?.GetType().FullName ?? "no exception"}.");
            }

            Assert.Equal("original exact bytes", await File.ReadAllTextAsync(destination));
            Assert.Empty(Directory.EnumerateFiles(directory, ".honest-ink-*.stage"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Transactional_export_honors_preexisting_cancellation_without_touching_the_destination()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(directory, "existing.pdf");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(destination, "original exact bytes");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AppServices.WriteExportBytesAsync(
                destination,
                "replacement bytes"u8.ToArray(),
                cancellation.Token));

            Assert.Equal("original exact bytes", await File.ReadAllTextAsync(destination));
            Assert.Empty(Directory.EnumerateFiles(directory, ".honest-ink-*.stage"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Transactional_export_atomically_replaces_an_unlocked_destination()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(directory, "existing.pdf");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(destination, "prior exact bytes");
        try
        {
            await AppServices.WriteExportBytesAsync(
                destination,
                "approved exact bytes"u8.ToArray(),
                CancellationToken.None);

            Assert.Equal("approved exact bytes", await File.ReadAllTextAsync(destination));
            Assert.Empty(Directory.EnumerateFiles(directory, ".honest-ink-*.stage"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Transactional_export_promotes_the_handle_owned_bytes_when_a_rewrite_is_attempted()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(directory, "rewritten.pdf");
        Directory.CreateDirectory(directory);
        try
        {
            await AppServices.WriteExportBytesAsync(
                destination,
                "approved exact bytes"u8.ToArray(),
                CancellationToken.None,
                stageReady: stage =>
                {
                    var failure = Record.Exception(() =>
                        File.WriteAllBytes(stage, "substituted bytes"u8.ToArray()));
                    Assert.True(
                        failure is IOException or UnauthorizedAccessException,
                        $"Expected the held stage to refuse a rewrite; received {failure?.GetType().FullName ?? "no exception"}.");
                    return Task.CompletedTask;
                });

            Assert.Equal("approved exact bytes", await File.ReadAllTextAsync(destination));
            Assert.Empty(Directory.EnumerateFiles(directory, ".honest-ink-*.stage"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Transactional_export_promotes_the_handle_owned_bytes_when_path_replacement_is_attempted()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(directory, "replaced.pdf");
        var substitute = Path.Combine(directory, "substitute.stage");
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(substitute, "substituted bytes");
            await AppServices.WriteExportBytesAsync(
                destination,
                "approved exact bytes"u8.ToArray(),
                CancellationToken.None,
                stageReady: stage =>
                {
                    var failure = Record.Exception(() => File.Move(substitute, stage, overwrite: true));
                    Assert.True(
                        failure is IOException or UnauthorizedAccessException,
                        $"Expected the held stage to refuse path replacement; received {failure?.GetType().FullName ?? "no exception"}.");
                    return Task.CompletedTask;
                });

            Assert.Equal("approved exact bytes", await File.ReadAllTextAsync(destination));
            Assert.Equal("substituted bytes", await File.ReadAllTextAsync(substitute));
            Assert.Empty(Directory.EnumerateFiles(directory, ".honest-ink-*.stage"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Transactional_export_holds_the_parent_against_junction_substitution()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "selected");
        var displaced = Path.Combine(root, "displaced");
        var displacedRoot = $"{root}-displaced";
        var destination = Path.Combine(directory, "held-parent.pdf");
        Directory.CreateDirectory(directory);
        try
        {
            await AppServices.WriteExportBytesAsync(
                destination,
                "approved exact bytes"u8.ToArray(),
                CancellationToken.None,
                stageReady: _ =>
                {
                    // A junction substitution first has to rename or remove
                    // the selected directory. The retained directory handle
                    // withholds the delete sharing that operation requires.
                    var failure = Record.Exception(() => Directory.Move(directory, displaced));
                    Assert.True(
                        failure is IOException or UnauthorizedAccessException,
                        $"Expected the held parent to refuse displacement; received {failure?.GetType().FullName ?? "no exception"}.");
                    Assert.True(Directory.Exists(directory));
                    Assert.False(Directory.Exists(displaced));

                    var ancestorFailure = Record.Exception(() => Directory.Move(root, displacedRoot));
                    Assert.True(
                        ancestorFailure is IOException or UnauthorizedAccessException,
                        $"Expected the held namespace chain to refuse ancestor displacement; received {ancestorFailure?.GetType().FullName ?? "no exception"}.");
                    Assert.True(Directory.Exists(root));
                    Assert.False(Directory.Exists(displacedRoot));
                    return Task.CompletedTask;
                });

            Assert.Equal("approved exact bytes", await File.ReadAllTextAsync(destination));
            Assert.Empty(Directory.EnumerateFiles(directory, ".honest-ink-*.stage"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            if (Directory.Exists(displacedRoot))
            {
                Directory.Delete(displacedRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Transactional_export_rename_contract_uses_only_a_leaf_and_a_null_root_handle()
    {
        const string destinationLeaf = "held-parent.pdf";
        var renameInformation = AppServices.BuildExportRenameInformation(destinationLeaf);
        var rootDirectoryOffset = IntPtr.Size == 8 ? 8 : 4;
        var fileNameLengthOffset = rootDirectoryOffset + IntPtr.Size;
        var fileNameOffset = fileNameLengthOffset + sizeof(uint);
        var destinationBytes = Encoding.Unicode.GetBytes(destinationLeaf);

        Assert.Equal(1, renameInformation[0]);
        Assert.All(
            renameInformation.AsSpan(rootDirectoryOffset, IntPtr.Size).ToArray(),
            value => Assert.Equal(0, value));
        Assert.Equal(
            checked((uint)destinationBytes.Length),
            BitConverter.ToUInt32(renameInformation, fileNameLengthOffset));
        Assert.True(destinationBytes.SequenceEqual(
            renameInformation.AsSpan(fileNameOffset, destinationBytes.Length)));
        Assert.DoesNotContain(
            Path.DirectorySeparatorChar,
            Encoding.Unicode.GetString(renameInformation, fileNameOffset, destinationBytes.Length));
    }

    [Fact]
    public void Transactional_export_failure_cleanup_contract_accepts_only_the_owned_stage_handle()
    {
        var cleanup = typeof(AppServices).GetMethod(
            "TryMarkExportStageForDeletion",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(cleanup);
        var parameter = Assert.Single(cleanup.GetParameters());
        Assert.Equal(typeof(SafeFileHandle), parameter.ParameterType);
    }

    [Fact]
    public void Transactional_export_native_status_contract_matches_the_windows_pointer_abi()
    {
        var ioStatusBlock = typeof(AppServices).GetNestedType(
            "IoStatusBlock",
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(ioStatusBlock);
        Assert.Equal(IntPtr.Size * 2, Marshal.SizeOf(ioStatusBlock));
        Assert.Equal(IntPtr.Zero, Marshal.OffsetOf(ioStatusBlock, "Status"));
        Assert.Equal(new IntPtr(IntPtr.Size), Marshal.OffsetOf(ioStatusBlock, "Information"));

        var mapStatus = typeof(AppServices).GetMethod(
            "RtlNtStatusToDosError",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(mapStatus);
        const uint errorAccessDenied = 5;
        var mapped = Assert.IsType<uint>(mapStatus.Invoke(
            obj: null,
            [unchecked((int)0xC0000022)]));
        Assert.Equal(errorAccessDenied, mapped);
    }

    [Fact]
    public async Task Transactional_export_rejects_an_alternate_data_stream_leaf_before_creating_a_stage()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var directory = Path.Combine(root, "selected");
        var destination = Path.Combine(directory, "report.pdf:redirected");
        Directory.CreateDirectory(directory);
        try
        {
            await Assert.ThrowsAsync<IOException>(() => AppServices.WriteExportBytesAsync(
                destination,
                "approved exact bytes"u8.ToArray(),
                CancellationToken.None));

            Assert.Empty(Directory.EnumerateFiles(directory));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Preexisting_cancellation_does_not_create_a_missing_destination_parent()
    {
        var root = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var missingParent = Path.Combine(root, "missing-parent");
        var destination = Path.Combine(missingParent, "cancelled.pdf");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AppServices.WriteExportBytesAsync(
                destination,
                "replacement bytes"u8.ToArray(),
                cancellation.Token));

            Assert.False(Directory.Exists(missingParent));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Transactional_export_reports_read_only_residue_without_losing_the_primary_cancellation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var destination = Path.Combine(directory, "existing.pdf");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(destination, "original exact bytes");
        using var cancellation = new CancellationTokenSource();
        FileStream? stageInspection = null;
        try
        {
            var failure = await Assert.ThrowsAsync<IOException>(() => AppServices.WriteExportBytesAsync(
                destination,
                "replacement bytes"u8.ToArray(),
                cancellation.Token,
                stageReady: stage =>
                {
                    stageInspection = new FileStream(
                        stage,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    // Share symmetry requires a peer opened alongside a
                    // DELETE-capable writer to share delete access. A read-only
                    // attribute gives the cleanup path a deterministic residue
                    // without weakening the writer's held-handle contract.
                    File.SetAttributes(stage, FileAttributes.ReadOnly);
                    cancellation.Cancel();
                    return Task.CompletedTask;
                }));

            Assert.Contains("staged residue", failure.Message, StringComparison.Ordinal);
            var combined = Assert.IsType<AggregateException>(failure.InnerException);
            Assert.Contains(combined.InnerExceptions, exception => exception is OperationCanceledException);
            Assert.Contains(
                combined.InnerExceptions,
                exception => exception is IOException ioFailure
                    && ioFailure.Message.Contains(
                        "export.stage-delete-disposition-failed",
                        StringComparison.Ordinal));
            Assert.Equal("original exact bytes", await File.ReadAllTextAsync(destination));
            Assert.Single(Directory.EnumerateFiles(directory, ".honest-ink-*.stage"));
        }
        finally
        {
            if (stageInspection is not null)
            {
                await stageInspection.DisposeAsync();
            }

            foreach (var stage in Directory.EnumerateFiles(directory, ".honest-ink-*.stage"))
            {
                File.SetAttributes(stage, FileAttributes.Normal);
            }

            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Production_catalog_open_fails_at_the_build_identity_fence_when_manifest_content_changes()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "assets", "symbols");
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            }

            var manifestPath = Path.Combine(directory, "manifest.json");
            var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsArray();
            manifest[0]!["source"] = string.Empty;
            File.WriteAllText(manifestPath, manifest.ToJsonString());

            Assert.Contains(
                new JsonAssetCatalog(directory).VerifyIntegrity(),
                issue => issue.Code == "asset.incomplete-provenance");
            var refusal = Assert.Throws<InvalidDataException>(() => AppServices.OpenSymbolCatalog(directory));
            Assert.Contains("asset.unexpected-build-identity", refusal.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(directory, refusal.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Current_shipped_symbol_manifest_matches_the_exact_build_identity_fence()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "assets", "symbols");
        var catalog = new JsonAssetCatalog(directory);

        // This is a known-answer identity check for the already-shipped 13
        // originals. It does not assert protected review and admits no candidate.
        Assert.Equal(AppServices.ExpectedShippedSymbolManifestSha256, catalog.ManifestSha256);
        Assert.Equal(13, catalog.All.Count);
        Assert.Empty(catalog.VerifyIntegrity());
        Assert.Empty(catalog.VerifyClosedDeploymentRoot());
        Assert.IsType<JsonAssetCatalog>(AppServices.SymbolCatalog());
    }

    [Fact]
    public void Semantic_validity_cannot_turn_raw_manifest_byte_drift_into_a_shipped_catalog()
    {
        var directory = CopyShippedSymbolPack();
        try
        {
            var manifestPath = Path.Combine(directory, JsonAssetCatalog.ManifestFileName);
            File.AppendAllText(manifestPath, "\n");

            var generic = new JsonAssetCatalog(directory);
            Assert.Empty(generic.VerifyIntegrity());
            Assert.Empty(generic.VerifyClosedDeploymentRoot());
            Assert.NotEqual(AppServices.ExpectedShippedSymbolManifestSha256, generic.ManifestSha256);

            var refusal = Assert.Throws<InvalidDataException>(() => AppServices.OpenSymbolCatalog(directory));
            Assert.Contains("asset.unexpected-build-identity", refusal.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(directory, refusal.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(JsonAssetCatalog.ManifestFileName, refusal.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void A_semantically_valid_provenance_record_change_cannot_enter_the_shipped_catalog()
    {
        var directory = CopyShippedSymbolPack();
        try
        {
            var manifestPath = Path.Combine(directory, JsonAssetCatalog.ManifestFileName);
            var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsArray();
            manifest[0]!["source"] = "synthetic-current-pack-drift";
            File.WriteAllText(manifestPath, manifest.ToJsonString());

            var generic = new JsonAssetCatalog(directory);
            Assert.Empty(generic.VerifyIntegrity());
            Assert.Empty(generic.VerifyClosedDeploymentRoot());
            Assert.NotEqual(AppServices.ExpectedShippedSymbolManifestSha256, generic.ManifestSha256);

            var refusal = Assert.Throws<InvalidDataException>(() => AppServices.OpenSymbolCatalog(directory));
            Assert.Contains("asset.unexpected-build-identity", refusal.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(directory, refusal.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("synthetic-current-pack-drift", refusal.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shipped_catalog_open_refuses_top_level_and_nested_unlisted_entries_without_leaking_paths(bool nested)
    {
        var directory = CopyShippedSymbolPack();
        const string UnlistedName = "unlisted-synthetic-entry";
        try
        {
            var unlistedPath = nested
                ? Path.Combine(directory, UnlistedName, "fixture.bin")
                : Path.Combine(directory, UnlistedName + ".bin");
            Directory.CreateDirectory(Path.GetDirectoryName(unlistedPath)!);
            File.WriteAllText(unlistedPath, "synthetic unlisted bytes");

            var generic = new JsonAssetCatalog(directory);
            Assert.Empty(generic.VerifyIntegrity());
            Assert.Contains(
                generic.VerifyClosedDeploymentRoot(),
                issue => issue.Code == "asset.unexpected-deployment-entry");

            var refusal = Assert.Throws<InvalidDataException>(() => AppServices.OpenSymbolCatalog(directory));
            Assert.Contains("asset.unexpected-deployment-entry", refusal.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(directory, refusal.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(UnlistedName, refusal.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Readiness_requires_the_exact_current_marker_revision_request_and_generation()
    {
        var firstDraft = DraftArtifact.New(
            new ArtifactDocument([new Paragraph("First exact revision.")]),
            DataLane.Green);
        var firstRevision = firstDraft.Revision;
        var secondRevision = firstDraft.WithEditedDocument(
            new ArtifactDocument([new Paragraph("Second exact revision.")])).Revision;
        var request = new RenderRequest(RenderTarget.AccessibleHtml);
        var otherRequest = request with { TextScalePercent = 125 };
        var readiness = new PreviewReadinessState();

        var firstGeneration = readiness.BeginLoad();
        readiness.Expect(firstGeneration, firstRevision, request, "marker-first");
        Assert.False(readiness.IsReady);
        Assert.False(readiness.ObserveDocumentCompleted(firstRevision, request, null));
        Assert.False(readiness.ObserveDocumentCompleted(firstRevision, request, "marker-stale"));
        Assert.True(readiness.ObserveDocumentCompleted(firstRevision, request, "marker-first"));
        Assert.True(readiness.IsReadyFor(firstRevision, request));

        readiness.NavigationStarted();
        Assert.False(readiness.IsReadyFor(firstRevision, request));
        Assert.True(readiness.ObserveDocumentCompleted(firstRevision, request, "marker-first"));

        var secondGeneration = readiness.BeginLoad();
        readiness.Expect(secondGeneration, secondRevision, request, "marker-second");
        Assert.NotEqual(firstGeneration, secondGeneration);
        Assert.False(readiness.ObserveDocumentCompleted(firstRevision, request, "marker-first"));
        Assert.False(readiness.ObserveDocumentCompleted(secondRevision, otherRequest, "marker-second"));
        Assert.False(readiness.ObserveDocumentCompleted(secondRevision, request, "marker-first"));
        Assert.True(readiness.ObserveDocumentCompleted(secondRevision, request, "marker-second"));

        readiness.Fail(firstGeneration);
        Assert.True(readiness.IsReadyFor(secondRevision, request));
        readiness.Fail(secondGeneration);
        Assert.False(readiness.IsReadyFor(secondRevision, request));
    }

    [Fact]
    public void Source_comparison_is_exact_and_manual_default_states_source_unavailable()
        => Sta.Run(() =>
        {
            var supplied = new ReviewSourceContext(
                "Synthetic verified transcription",
                "First exact line.\nSecond exact line.  ");
            using var withSource = new ReviewForm(Session(
                new ReviewViewContext(
                    new RenderRequest(RenderTarget.PrintHtml),
                    supplied),
                new Paragraph("Synthetic current draft.")));
            withSource.Show();
            var tabs = ReviewSurfaceContractTests.Flatten(withSource).OfType<TabControl>().Single();
            tabs.SelectedIndex = 1;
            System.Windows.Forms.Application.DoEvents();

            var source = TextByName(withSource, "Exact source or verified transcription");
            var draft = TextByName(withSource, "Exact current semantic draft");
            Assert.True(source.ReadOnly);
            Assert.Contains("Synthetic verified transcription", source.Text, StringComparison.Ordinal);
            Assert.Contains("UTF-16 code units: 38", source.Text, StringComparison.Ordinal);
            Assert.Contains("First exact line.\nSecond exact line.  ", source.Text, StringComparison.Ordinal);
            Assert.Contains("Synthetic current draft.", draft.Text, StringComparison.Ordinal);

            using var manual = new ReviewForm(Session(
                ReviewViewContext.ManualDefault,
                new Paragraph("Synthetic manual draft.")));
            manual.Show();
            var manualTabs = ReviewSurfaceContractTests.Flatten(manual).OfType<TabControl>().Single();
            manualTabs.SelectedIndex = 1;
            System.Windows.Forms.Application.DoEvents();
            Assert.Equal(
                "Source unavailable for this manual or reopened path. No source or transcription has been fabricated.",
                TextByName(manual, "Exact source or verified transcription").Text);
        });

    [Fact]
    public void Visual_derivative_is_marked_uses_the_exact_profile_and_refreshes_after_edit()
        => Sta.Run(() =>
        {
            var context = new ReviewViewContext(new RenderRequest(
                RenderTarget.AccessibleHtml,
                RenderAudience.Teacher,
                TextScalePercent: 175,
                TargetLanguageFirst: true));
            using var form = new ReviewForm(Session(
                context,
                new Paragraph("Before exact edit."),
                new TeacherOnlyNotice("Teacher profile proof.")));
            var approve = (Button)ReviewSurfaceContractTests.ByName(form, "Approve");
            Assert.False(approve.Enabled);
            form.Show();

            var tabs = ReviewSurfaceContractTests.Flatten(form).OfType<TabControl>().Single();
            tabs.SelectedIndex = 2;
            System.Windows.Forms.Application.DoEvents();
            var browser = ReviewSurfaceContractTests.Flatten(form).OfType<WebBrowser>().Single();
            var before = AwaitDocument(browser, "Before exact edit.");
            AwaitEnabled(approve);
            Assert.Contains("UNAPPROVED DRAFT — NOT FOR USE", before, StringComparison.Ordinal);
            Assert.Contains("data-review-state=\"unapproved\"", before, StringComparison.Ordinal);
            Assert.Contains("Teacher profile proof.", before, StringComparison.Ordinal);
            Assert.Contains("body { font-size: 175%; }", before, StringComparison.Ordinal);
            Assert.False(browser.IsWebBrowserContextMenuEnabled);
            Assert.False(browser.WebBrowserShortcutsEnabled);
            Assert.False(browser.AllowWebBrowserDrop);

            var profile = ReviewSurfaceContractTests.Flatten(form).OfType<Label>()
                .Single(label => label.Text.StartsWith("Exact preview profile:", StringComparison.Ordinal));
            Assert.Contains("accessible HTML layout", profile.Text, StringComparison.Ordinal);
            Assert.Contains("Teacher copy", profile.Text, StringComparison.Ordinal);
            Assert.Contains("175 percent", profile.Text, StringComparison.Ordinal);
            Assert.Contains("target language first", profile.Text, StringComparison.Ordinal);

            tabs.SelectedIndex = 0;
            var list = (ListBox)ReviewSurfaceContractTests.ByName(form, "Draft elements");
            list.SelectedIndex = 0;
            var editor = (TextBox)ReviewSurfaceContractTests.ByName(form, "Selected element text");
            editor.Text = "After exact edit.";
            Assert.False(approve.Enabled);
            ((Button)ReviewSurfaceContractTests.ByName(form, "Apply edit")).PerformClick();
            Assert.False(approve.Enabled);

            tabs.SelectedIndex = 2;
            System.Windows.Forms.Application.DoEvents();
            var after = AwaitDocument(browser, "After exact edit.");
            AwaitEnabled(approve);
            Assert.DoesNotContain("Before exact edit.", after, StringComparison.Ordinal);
            Assert.Contains("UNAPPROVED DRAFT — NOT FOR USE", after, StringComparison.Ordinal);

            tabs.SelectedIndex = 1;
            var current = TextByName(form, "Exact current semantic draft").Text;
            Assert.Contains("After exact edit.", current, StringComparison.Ordinal);
            Assert.DoesNotContain("Before exact edit.", current, StringComparison.Ordinal);
        });

    [Fact]
    public void Gate_B_unlocks_only_after_the_actual_WebBrowser_decodes_the_reviewed_svg_symbol()
        => Sta.Run(() =>
        {
            var catalog = AppServices.SymbolCatalog();
            var context = new ReviewViewContext(
                new RenderRequest(RenderTarget.AccessibleHtml),
                assetCatalog: catalog);
            using var form = new ReviewForm(Session(
                context,
                new ImageReference(new AssetId("agency.stop.v1"), "A stop symbol")));
            var approve = (Button)ReviewSurfaceContractTests.ByName(form, "Approve");
            form.Show();

            var tabs = ReviewSurfaceContractTests.Flatten(form).OfType<TabControl>().Single();
            tabs.SelectedIndex = 2;
            System.Windows.Forms.Application.DoEvents();
            var browser = ReviewSurfaceContractTests.Flatten(form).OfType<WebBrowser>().Single();
            var html = AwaitDocument(browser, "data:image/svg+xml;base64,");
            var image = Assert.Single(browser.Document!.Images.Cast<HtmlElement>());
            Assert.True(
                ReviewForm.PreviewImagesAreDecoded(browser.Document),
                $"readyState='{image.GetAttribute("readyState")}', complete='{image.GetAttribute("complete")}', naturalWidth='{image.GetAttribute("naturalWidth")}', naturalHeight='{image.GetAttribute("naturalHeight")}', width='{image.GetAttribute("width")}', height='{image.GetAttribute("height")}', offset={image.OffsetRectangle}, dom='{image.DomElement?.GetType().FullName}'.");
            AwaitEnabled(approve);

            Assert.Contains("X-UA-Compatible", html, StringComparison.OrdinalIgnoreCase);
        });

    [Fact]
    public void Gate_B_fails_closed_for_a_broken_image()
        => Sta.Run(() =>
        {
            using var host = new Form();
            using var browser = new WebBrowser { Dock = DockStyle.Fill, ScriptErrorsSuppressed = true };
            host.Controls.Add(browser);
            host.Show();
            browser.DocumentText = "<!doctype html><html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"></head><body><img alt=\"Broken synthetic image\" src=\"data:image/svg+xml;base64,QUFBQQ==\"></body></html>";

            var deadline = DateTime.UtcNow.AddSeconds(3);
            while ((browser.Document is null || browser.Document.Images.Count == 0) && DateTime.UtcNow < deadline)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }

            Assert.NotNull(browser.Document);
            Assert.Single(browser.Document.Images.Cast<HtmlElement>());
            Assert.False(ReviewForm.PreviewImagesAreDecoded(browser.Document));
        });

    [Theory]
    [InlineData(1.0f)]
    [InlineData(1.25f)]
    public void Every_review_tab_remains_keyboard_reachable_at_the_1366_by_768_floor(float scale)
        => Sta.Run(() =>
        {
            using var form = new ReviewForm(Session(
                new ReviewViewContext(
                    new RenderRequest(RenderTarget.PrintHtml),
                    new ReviewSourceContext("Synthetic source", "Synthetic exact source.")),
                new Paragraph("Synthetic floor draft.")));
            form.StartPosition = FormStartPosition.Manual;
            form.ShowInTaskbar = false;
            form.Opacity = 0;
            if (scale != 1.0f)
            {
                form.Scale(new SizeF(scale, scale));
            }

            form.Bounds = new Rectangle(0, 0, 1366, 728);
            form.Show();
            form.PerformLayout();
            System.Windows.Forms.Application.DoEvents();

            var floor = form.RectangleToScreen(form.ClientRectangle);
            var tabs = ReviewSurfaceContractTests.Flatten(form).OfType<TabControl>().Single();
            AssertContained(tabs, floor);
            for (var index = 0; index < tabs.TabPages.Count; index++)
            {
                tabs.SelectedIndex = index;
                tabs.PerformLayout();
                System.Windows.Forms.Application.DoEvents();

                var reachable = ReviewSurfaceContractTests.Flatten(tabs.SelectedTab!)
                    .Where(control => control.Visible
                        && (control.TabStop
                            || control is TextBoxBase or ListBox or WebBrowser or ButtonBase))
                    .ToArray();
                Assert.NotEmpty(reachable);
                Assert.All(reachable, control =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(control.AccessibilityObject.Name));
                    AssertContainedOrScrollable(control, floor);
                });
            }

            foreach (var button in ReviewSurfaceContractTests.Flatten(form).OfType<Button>()
                         .Where(button => button.Visible))
            {
                AssertContained(button, floor);
            }
        });

    private static ReviewSession Session(
        ReviewViewContext context,
        params DocumentNode[] nodes)
        => new(
            DraftArtifact.New(new ArtifactDocument(nodes), DataLane.Green),
            AppServices.MachineAtReview(),
            new DefaultArtifactValidator(),
            context);

    private static string CopyShippedSymbolPack()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "assets", "symbols");
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
        }

        return directory;
    }

    private static string AwaitDocument(WebBrowser browser, string expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            System.Windows.Forms.Application.DoEvents();
            var document = browser.DocumentText;
            if (document.Contains(expected, StringComparison.Ordinal))
            {
                return document;
            }

            Thread.Sleep(10);
        }

        return browser.DocumentText;
    }

    private static void AwaitEnabled(Button button)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && !button.Enabled)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }

        Assert.True(button.Enabled, "Approval did not unlock after the exact preview completed.");
    }

    private static TextBox TextByName(Form form, string accessibleName)
        => ReviewSurfaceContractTests.Flatten(form).OfType<TextBox>().Single(
            control => control.AccessibilityObject.Name == accessibleName);

    private static void AssertContained(Control control, Rectangle floor)
        => Assert.True(
            floor.Contains(control.RectangleToScreen(control.ClientRectangle)),
            $"{control.AccessibilityObject.Name} is outside the floor.");

    private static void AssertContainedOrScrollable(Control control, Rectangle floor)
    {
        var rectangle = control.RectangleToScreen(control.ClientRectangle);
        if (floor.Contains(rectangle))
        {
            return;
        }

        Assert.Contains(
            Ancestors(control),
            parent => parent is ScrollableControl { AutoScroll: true }
                && parent.RectangleToScreen(parent.ClientRectangle).IntersectsWith(rectangle));
    }

    private static IEnumerable<Control> Ancestors(Control control)
    {
        for (var parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            yield return parent;
        }
    }

}
