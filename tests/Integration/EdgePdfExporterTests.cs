using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;
using Foundry.Rendering;

namespace Foundry.Tests.Integration;

public class EdgePdfExporterTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static ApprovedArtifact Artifact(string heading)
        => ApprovalGate.Approve(
            DraftArtifact.New(new ArtifactDocument(
                [new Heading(1, heading), new Paragraph("Cut along the lines.")]), DataLane.Green),
            "teacher@example.org", [], SomeInstant);

    private static async Task AssertRealPdfAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        Assert.True(bytes.Length > 1000, "The PDF is implausibly small.");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Contains("%%EOF", Encoding.ASCII.GetString(bytes), StringComparison.Ordinal);
    }

    private static async Task ReplaceTextWithRetryAsync(string path, string content)
    {
        const int attempts = 100;
        var replacement = path + $".{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(replacement, content);
        try
        {
            for (var attempt = 0; attempt < attempts; attempt++)
            {
                try
                {
                    File.Move(replacement, path, overwrite: true);
                    return;
                }
                catch (Exception exception) when (
                    (exception is IOException or UnauthorizedAccessException) && attempt + 1 < attempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10));
                }
            }
        }
        finally
        {
            File.Delete(replacement);
        }
    }

    [Fact]
    public void Producer_start_failure_has_one_stable_path_free_message()
    {
        var missingExecutable = Path.Combine(
            Path.GetTempPath(),
            "ocf-tests",
            Guid.NewGuid().ToString("N"),
            "missing-edge.exe");

        var failure = Assert.Throws<InvalidOperationException>(() =>
            EdgePdfExporter.StartLocalPdfProcess(new ProcessStartInfo(missingExecutable)
            {
                UseShellExecute = false,
            }));

        Assert.Equal("Microsoft Edge could not start the local PDF process.", failure.Message);
        Assert.DoesNotContain(missingExecutable, failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(failure.InnerException);
    }

    [Fact]
    public async Task An_approved_artifact_becomes_a_real_pdf()
    {
        if (EdgePdfExporter.FindEdge() is null)
        {
            // No Edge on this machine: the pipeline cannot be exercised here.
            // The hardware bench (plan §12) covers it where Edge exists.
            return;
        }

        var destination = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"), "ten-frames.pdf");
        try
        {
            await new EdgePdfExporter(new AccessibleHtmlRenderer()).ExportAsync(
                Artifact("Ten-frame practice"), new ExportRequest(RenderTarget.PrintPdf, destination), CancellationToken.None);

            await AssertRealPdfAsync(destination);
        }
        finally
        {
            var directory = Path.GetDirectoryName(destination);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task The_real_edge_pipeline_resolves_an_asset_and_produces_a_pdf()
    {
        if (EdgePdfExporter.FindEdge() is null)
        {
            return;
        }

        var symbolId = new AssetId("synthetic.stop.v1");
        var symbol = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\" role=\"img\"><title>Synthetic stop marker</title><rect x=\"5\" y=\"5\" width=\"90\" height=\"90\" fill=\"#fff\" stroke=\"#000\"/></svg>");
        var catalog = new OneAssetCatalog(symbolId, symbol, "image/svg+xml");
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(new ArtifactDocument(
            [
                new Heading(1, "Synthetic asset-backed routine"),
                new StepRow(
                    "Stop at the synthetic marker.",
                    new ImageReference(symbolId, "A synthetic stop marker")),
            ]), DataLane.Green),
            "synthetic-reviewer@example.invalid",
            [],
            SomeInstant);
        var destination = Path.Combine(
            Path.GetTempPath(),
            "ocf-tests",
            Guid.NewGuid().ToString("N"),
            "semantic-with-asset.pdf");

        try
        {
            await new EdgePdfExporter(new AccessibleHtmlRenderer(catalog)).ExportAsync(
                artifact,
                new ExportRequest(RenderTarget.PrintPdf, destination),
                CancellationToken.None);

            await AssertRealPdfAsync(destination);
            Assert.True(
                catalog.ContentCalls > 0,
                "The semantic PDF route never resolved its referenced asset; this test does not claim pixel-level appearance evidence.");
        }
        finally
        {
            var directory = Path.GetDirectoryName(destination);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Two_exports_complete_concurrently_with_isolated_edge_profiles()
    {
        if (EdgePdfExporter.FindEdge() is null)
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var first = Path.Combine(directory, "first.pdf");
        var second = Path.Combine(directory, "second.pdf");
        try
        {
            var exporter = new EdgePdfExporter(new AccessibleHtmlRenderer());
            await Task.WhenAll(
                exporter.ExportAsync(
                    Artifact("First concurrent export"),
                    new ExportRequest(RenderTarget.PrintPdf, first),
                    CancellationToken.None),
                exporter.ExportAsync(
                    Artifact("Second concurrent export"),
                    new ExportRequest(RenderTarget.PrintPdf, second),
                    CancellationToken.None));

            await AssertRealPdfAsync(first);
            await AssertRealPdfAsync(second);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Completion_waits_for_a_stable_pdf_header_and_end_marker()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "delayed.pdf");
        Directory.CreateDirectory(directory);
        try
        {
            await File.WriteAllTextAsync(path, "%PDF-1.7\nnot finished");
            var wait = EdgePdfExporter.WaitForCompletePdfAsync(
                path,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(20),
                CancellationToken.None);

            await Task.Delay(TimeSpan.FromMilliseconds(100));
            Assert.False(wait.IsCompleted, "A PDF without %%EOF was accepted as complete.");

            await ReplaceTextWithRetryAsync(path, "%PDF-1.7\nbody\n%%EOF\n");
            await wait;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task A_completed_launcher_gets_a_bounded_handoff_for_its_pdf_child()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "handoff.pdf");
        Directory.CreateDirectory(directory);
        try
        {
            var wait = EdgePdfExporter.WaitForCompletePdfAsync(
                path,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(20),
                CancellationToken.None,
                Task.CompletedTask,
                TimeSpan.FromSeconds(1));

            // The launcher is already gone, but its child creates a partial PDF
            // inside the grace window and completes it after that grace expires.
            await ReplaceTextWithRetryAsync(path, "%PDF-1.7\nnot finished");
            await Task.Delay(TimeSpan.FromMilliseconds(1_100));
            Assert.False(wait.IsCompleted, "A child-owned partial PDF was mistaken for launcher failure or completion.");

            await ReplaceTextWithRetryAsync(path, "%PDF-1.7\nbody\n%%EOF\n");
            await wait;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task A_completed_launcher_without_any_pdf_fails_before_the_export_timeout()
    {
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => EdgePdfExporter.WaitForCompletePdfAsync(
            Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"), "never.pdf"),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(10),
            CancellationToken.None,
            Task.CompletedTask,
            TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task Cancellation_cleanup_retries_locked_and_late_created_residue()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var jobDirectory = Path.Combine(directory, "job");
        var stagedPdf = Path.Combine(directory, ".honest-ink-staged.pdf");
        Directory.CreateDirectory(jobDirectory);
        try
        {
            Task cleanup;
            await using (var jobLock = new FileStream(
                Path.Combine(jobDirectory, "source.html"),
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                cleanup = EdgePdfExporter.CleanupResidueAsync(
                    jobDirectory,
                    stagedPdf,
                    guardAgainstLateCreation: true,
                    attempts: 40,
                    pollInterval: TimeSpan.FromMilliseconds(20));

                // A detached child can create the destination-stage file after
                // the first cleanup observation. Hold it briefly as Edge would
                // while writing, then prove the retry removes both residue roots.
                await Task.Delay(TimeSpan.FromMilliseconds(60));
                await using var stagedLock = new FileStream(
                    stagedPdf,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None);
                await stagedLock.WriteAsync("%PDF-"u8.ToArray());
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }

            await cleanup;

            Assert.False(File.Exists(stagedPdf), "The late-created staged PDF survived cancellation cleanup.");
            Assert.False(Directory.Exists(jobDirectory), "The locked per-job Edge profile survived cancellation cleanup.");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Completion_wait_respects_cancellation()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => EdgePdfExporter.WaitForCompletePdfAsync(
            Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"), "never.pdf"),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMilliseconds(20),
            cancellation.Token));
    }

    [Fact]
    public void Print_html_rendering_preserves_the_export_options()
    {
        var request = EdgePdfExporter.PrintHtmlRequest(new ExportRequest(
            RenderTarget.PrintPdf,
            "ignored-by-this-mapping.pdf",
            RenderAudience.Teacher,
            TextScalePercent: 175,
            TargetLanguageFirst: true));

        Assert.Equal(RenderTarget.PrintHtml, request.Target);
        Assert.Equal(RenderAudience.Teacher, request.Audience);
        Assert.Equal(175, request.TextScalePercent);
        Assert.True(request.TargetLanguageFirst);
    }

    [Fact]
    public async Task Other_targets_are_refused()
    {
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(new ArtifactDocument([new Paragraph("x")]), DataLane.Green),
            "teacher@example.org", [], SomeInstant);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => new EdgePdfExporter(new AccessibleHtmlRenderer()).ExportAsync(
                artifact, new ExportRequest(RenderTarget.Svg, "x.svg"), CancellationToken.None));
    }

    private sealed class OneAssetCatalog : IAssetCatalog
    {
        private readonly ReadOnlyMemory<byte> _content;
        private readonly AssetProvenance _provenance;

        internal OneAssetCatalog(AssetId id, ReadOnlyMemory<byte> content, string mimeType)
        {
            _content = content;
            _provenance = new AssetProvenance(
                id,
                "concept.synthetic.stop",
                "1.0.0",
                "synthetic-stop.svg",
                mimeType,
                "Synthetic in-memory integration fixture",
                "Honest Ink test suite",
                "CC0-1.0",
                Convert.ToHexString(SHA256.HashData(content.Span)),
                "Synthetic stop marker",
                "A synthetic stop marker",
                Redistributable: true);
        }

        internal int ContentCalls { get; private set; }

        public IReadOnlyList<AssetProvenance> All => [_provenance];

        public AssetProvenance? Find(AssetId id)
            => id == _provenance.Id ? _provenance : null;

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            ContentCalls++;
            if (id == _provenance.Id)
            {
                content = _content;
                mimeType = _provenance.MimeType;
                return true;
            }

            content = default;
            mimeType = string.Empty;
            return false;
        }
    }
}
