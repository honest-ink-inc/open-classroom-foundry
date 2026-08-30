// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using System.Text.Json.Nodes;

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
    public void Production_catalog_open_fails_before_use_when_required_provenance_is_blank()
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

            var refusal = Assert.Throws<InvalidDataException>(() => AppServices.OpenSymbolCatalog(directory));
            Assert.Contains("asset.incomplete-provenance", refusal.Message, StringComparison.Ordinal);
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
