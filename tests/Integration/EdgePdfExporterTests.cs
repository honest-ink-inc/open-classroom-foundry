using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;
using Foundry.Rendering;
using Xunit.Abstractions;

namespace Foundry.Tests.Integration;

public class EdgePdfExporterTests(ITestOutputHelper output)
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
        var document = new ArtifactDocument(
            [
                new Heading(1, "Synthetic asset-backed routine"),
                new StepRow(
                    "Stop at the synthetic marker.",
                    new ImageReference(symbolId, "A synthetic stop marker")),
            ]);
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green),
            "synthetic-reviewer@example.invalid",
            [],
            SomeInstant,
            ExactAssetCatalogSnapshot.CaptureForReview(document, catalog).Bindings);
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
        var clock = Stopwatch.StartNew();
        Task? wait = null;
        Exception? primaryFailure = null;
        try
        {
            output.WriteLine("Current file-waiter instrument; no Edge child is launched and no hosted cause is inferred.");
            WritePdfWaiterPhase(clock, "before-waiter-creation");
            wait = EdgePdfExporter.WaitForCompletePdfAsync(
                path,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(20),
                CancellationToken.None,
                Task.CompletedTask,
                TimeSpan.FromSeconds(1));
            WritePdfWaiterPhase(clock, "after-waiter-creation");

            // Model a completed launcher, followed by partial child output and
            // completion after the intended grace interval. Scheduling is observed,
            // not assumed to keep these continuations inside any deadline.
            WritePdfWaiterPhase(clock, "before-partial-replacement");
            await ReplaceTextWithRetryAsync(path, "%PDF-1.7\nnot finished");
            WritePdfWaiterPhase(clock, "after-partial-replacement");
            await Task.Delay(TimeSpan.FromMilliseconds(1_100));
            await AssertPartialPdfWaiterPendingAsync(wait, clock, WritePdfCheckpoint);

            WritePdfWaiterPhase(clock, "before-complete-replacement");
            await ReplaceTextWithRetryAsync(path, "%PDF-1.7\nbody\n%%EOF\n");
            WritePdfWaiterPhase(clock, "after-complete-replacement");
            await wait;
            WritePdfWaiterPhase(clock, "final-waiter-await-succeeded");
        }
        catch (Exception failure)
        {
            primaryFailure = failure;
            throw;
        }
        finally
        {
            await CleanupOwnedPdfWaiterAsync(wait, directory, primaryFailure);
        }
    }

    [Fact]
    public async Task An_incomplete_pdf_timeout_is_terminal_but_not_successful()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "incomplete.pdf");
        Directory.CreateDirectory(directory);
        var clock = Stopwatch.StartNew();
        Task? wait = null;
        Exception? primaryFailure = null;
        Exception? expectedWaiterFailure = null;
        try
        {
            output.WriteLine("Current real-waiter timeout counterexample; pre-created incomplete output, not a hosted handoff replay.");
            await File.WriteAllTextAsync(path, "%PDF-1.7\nnot finished");
            WritePdfWaiterPhase(clock, "before-waiter-creation");
            wait = EdgePdfExporter.WaitForCompletePdfAsync(
                path,
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(20),
                CancellationToken.None,
                Task.CompletedTask,
                TimeSpan.FromSeconds(1));
            WritePdfWaiterPhase(clock, "after-waiter-creation");

            var timeout = await Assert.ThrowsAsync<TimeoutException>(() => wait);
            expectedWaiterFailure = timeout;
            var status = wait.Status;
            output.WriteLine("Observed timeout: {0}: {1}", timeout.GetType().Name, timeout.Message);
            output.WriteLine("Terminal observation: elapsed_ms={0:F3}; status={1}; IsCompleted={2}; IsCompletedSuccessfully={3}",
                clock.Elapsed.TotalMilliseconds, status, wait.IsCompleted, wait.IsCompletedSuccessfully);
            Assert.Equal(TaskStatus.Faulted, status);
            Assert.True(wait.IsCompleted);
            Assert.False(wait.IsCompletedSuccessfully);
        }
        catch (Exception failure)
        {
            primaryFailure = failure;
            throw;
        }
        finally
        {
            await CleanupOwnedPdfWaiterAsync(wait, directory, primaryFailure, expectedWaiterFailure);
        }
    }

    [Theory]
    [InlineData(TaskStatus.WaitingForActivation)]
    [InlineData(TaskStatus.RanToCompletion)]
    [InlineData(TaskStatus.Faulted)]
    [InlineData(TaskStatus.Canceled)]
    public async Task Partial_pdf_checkpoint_preserves_the_observed_pending_success_fault_or_cancellation(TaskStatus status)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var syntheticFault = new IOException("Synthetic checkpoint fault.");
        var canceledToken = new CancellationToken(canceled: true);
        switch (status)
        {
            case TaskStatus.RanToCompletion:
                completion.SetResult();
                break;
            case TaskStatus.Faulted:
                completion.SetException(syntheticFault);
                break;
            case TaskStatus.Canceled:
                completion.SetCanceled(canceledToken);
                break;
        }

        var observations = new List<PdfCheckpointObservation>();
        try
        {
            output.WriteLine("Synthetic task-state instrument control; no file, native child, or hosted reproduction.");
            var failure = await Record.ExceptionAsync(() => AssertPartialPdfWaiterPendingAsync(
                completion.Task,
                Stopwatch.StartNew(),
                observation =>
                {
                    observations.Add(observation);
                    WritePdfCheckpoint(observation);
                }));

            var observed = Assert.Single(observations);
            Assert.Equal(status, observed.Status);
            Assert.True(observed.BeforeStatus >= TimeSpan.Zero);
            Assert.True(observed.AfterStatus >= observed.BeforeStatus);
            switch (status)
            {
                case TaskStatus.WaitingForActivation:
                    Assert.Null(failure);
                    break;
                case TaskStatus.RanToCompletion:
                    var assertion = Assert.IsType<Xunit.Sdk.XunitException>(failure, exactMatch: false);
                    Assert.Contains("completed successfully", assertion.Message, StringComparison.Ordinal);
                    break;
                case TaskStatus.Faulted:
                    Assert.Same(syntheticFault, failure);
                    break;
                case TaskStatus.Canceled:
                    var canceled = Assert.IsType<OperationCanceledException>(failure, exactMatch: false);
                    Assert.Equal(canceledToken, canceled.CancellationToken);
                    break;
            }
        }
        finally
        {
            completion.TrySetResult();
            try
            {
                await completion.Task;
            }
            catch (Exception failure) when (ReferenceEquals(failure, syntheticFault))
            {
                // Re-observe the exact configured fault after asserting its propagation.
            }
            catch (OperationCanceledException failure) when (
                status == TaskStatus.Canceled && failure.CancellationToken == canceledToken)
            {
                // Re-observe the exact configured canceled task; no worker remains.
            }
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

    private readonly record struct PdfCheckpointObservation(TimeSpan BeforeStatus, TaskStatus Status, TimeSpan AfterStatus);

    private static async Task AssertPartialPdfWaiterPendingAsync(
        Task wait,
        Stopwatch clock,
        Action<PdfCheckpointObservation> observe)
    {
        var beforeStatus = clock.Elapsed;
        var status = wait.Status;
        var afterStatus = clock.Elapsed;
        observe(new PdfCheckpointObservation(beforeStatus, status, afterStatus));

        // These are sequential observations, not the waiter's private deadline epoch.
        // A pending observation is not a test pass: the caller must still await this task.
        if (status is TaskStatus.RanToCompletion or TaskStatus.Faulted or TaskStatus.Canceled)
        {
            await wait;
        }

        Assert.False(status == TaskStatus.RanToCompletion, "The partial PDF waiter completed successfully at the checkpoint.");
    }

    private void WritePdfWaiterPhase(Stopwatch clock, string phase)
        => output.WriteLine("Sequential waiter observation: phase={0}; elapsed_ms={1:F3}", phase, clock.Elapsed.TotalMilliseconds);

    private void WritePdfCheckpoint(PdfCheckpointObservation observation)
        => output.WriteLine("Sequential checkpoint observations: before_status_ms={0:F3}; status={1}; after_status_ms={2:F3}; no outer-clock acceptance gate",
            observation.BeforeStatus.TotalMilliseconds, observation.Status, observation.AfterStatus.TotalMilliseconds);

    private async Task CleanupOwnedPdfWaiterAsync(
        Task? wait,
        string directory,
        Exception? primaryFailure,
        Exception? expectedWaiterFailure = null)
    {
        var cleanupFailures = new List<Exception>();
        if (wait is not null)
        {
            try
            {
                // Observe the exact owned waiter under its existing budget, including
                // an outcome reached after a failed checkpoint or replacement.
                await wait;
            }
            catch (Exception failure)
            {
                var alreadyObserved = ReferenceEquals(failure, primaryFailure) || ReferenceEquals(failure, expectedWaiterFailure);
                ReportCleanup(alreadyObserved ? "Re-observed owned waiter outcome" : "Secondary waiter cleanup failure", failure);
                if (!alreadyObserved)
                {
                    cleanupFailures.Add(failure);
                }
            }
        }

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception failure)
        {
            ReportCleanup("Secondary directory cleanup failure", failure);
            cleanupFailures.Add(failure);
        }

        if (primaryFailure is not null || cleanupFailures.Count == 0)
        {
            return;
        }

        if (cleanupFailures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(cleanupFailures[0]).Throw();
        }

        throw new AggregateException("PDF waiter cleanup failed without an earlier primary failure.", cleanupFailures);

        void ReportCleanup(string label, Exception failure)
        {
            try
            {
                output.WriteLine("{0}: {1}", label, failure);
            }
            catch (Exception reportingFailure)
            {
                // Even a diagnostic-output failure must not replace an original
                // test failure. Without one, retain it as a failing cleanup outcome.
                cleanupFailures.Add(reportingFailure);
            }
        }
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
