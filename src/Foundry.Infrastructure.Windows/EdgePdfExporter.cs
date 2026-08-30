// SPDX-License-Identifier: GPL-3.0-or-later
using System.ComponentModel;
using System.Diagnostics;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Infrastructure.Windows;

/// <summary>
/// The paper pipeline's first real machine: print-ready HTML through headless
/// Edge into a PDF at the teacher's destination. Accepts only ApprovedArtifact
/// (ADR-004, by interface). The intermediate HTML lives in a per-job temp file
/// and is deleted in a finally block — part of the documented residue boundary,
/// which is why this exporter is Green-lane machinery until the residue suite
/// hardens it further.
/// </summary>
public sealed class EdgePdfExporter : IExporter
{
    private const int ExportTimeoutSeconds = 90;
    private const int CleanupAttempts = 40;
    private static readonly TimeSpan PdfPollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan LauncherHandoffGrace = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ProcessStopGrace = TimeSpan.FromSeconds(2);
    private readonly IRenderer _renderer;

    public EdgePdfExporter(IRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public async Task ExportAsync(ApprovedArtifact artifact, ExportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Target != RenderTarget.PrintPdf)
        {
            throw new NotSupportedException("This exporter produces print PDFs; other targets have their own paths.");
        }

        var edge = FindEdge()
            ?? throw new InvalidOperationException("Microsoft Edge was not found; the PDF pipeline requires it on this machine.");

        var rendered = await _renderer.RenderAsync(
            artifact,
            PrintHtmlRequest(request),
            cancellationToken).ConfigureAwait(false);

        var tempDirectory = Path.Combine(Path.GetTempPath(), EngineIdentity.InternalId, "print");
        Directory.CreateDirectory(tempDirectory);
        var jobDirectory = Path.Combine(tempDirectory, Guid.NewGuid().ToString("N"));
        var profileDirectory = Path.Combine(jobDirectory, "edge-profile");
        var tempHtml = Path.Combine(jobDirectory, "source.html");
        var destination = Path.GetFullPath(request.DestinationHint);
        var destinationDirectory = Path.GetDirectoryName(destination)!;
        var stagedPdf = Path.Combine(destinationDirectory, $".honest-ink-{Guid.NewGuid():N}.pdf");
        Process? process = null;
        Task? producerCompletion = null;
        var destinationCommitted = false;
        try
        {
            Directory.CreateDirectory(jobDirectory);
            Directory.CreateDirectory(profileDirectory);
            await File.WriteAllBytesAsync(tempHtml, rendered.Content.ToArray(), cancellationToken).ConfigureAwait(false);

            Directory.CreateDirectory(destinationDirectory);
            cancellationToken.ThrowIfCancellationRequested();

            process = StartLocalPdfProcess(StartInfo(edge, stagedPdf, tempHtml, profileDirectory));
            // Drain both pipes without retaining their contents. Edge's output
            // is neither trusted nor content-free, so it can be a completion
            // signal but can never enter an exception, status line, or log.
            var standardOutputDrain = DrainAsync(process.StandardOutput.BaseStream);
            var standardErrorDrain = DrainAsync(process.StandardError.BaseStream);
            producerCompletion = ObserveProcessLifetimeAsync(process, standardOutputDrain, standardErrorDrain);
            try
            {
                // Edge may hand the print job to a child and let this Process
                // object exit before the child closes the PDF. The file, not
                // the launcher lifetime, is the completion boundary.
                await WaitForCompletePdfAsync(
                    stagedPdf,
                    TimeSpan.FromSeconds(ExportTimeoutSeconds),
                    PdfPollInterval,
                    cancellationToken,
                    producerCompletion,
                    LauncherHandoffGrace).ConfigureAwait(false);
            }
            catch (PdfProducerEndedException waitFailure)
            {
                throw new InvalidOperationException(
                    $"Edge ended before producing a PDF. {DescribeEdge(process)}",
                    waitFailure);
            }
            catch (TimeoutException waitFailure)
            {
                throw new InvalidOperationException(
                    $"Edge did not produce a complete PDF within {ExportTimeoutSeconds} seconds. {DescribeEdge(process)}",
                    waitFailure);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(stagedPdf, destination, overwrite: true);
            destinationCommitted = true;
        }
        finally
        {
            var producerSettled = true;
            if (process is not null)
            {
                producerSettled = await BestEffortStopAsync(process, producerCompletion).ConfigureAwait(false);
                process.Dispose();
            }

            // Cleanup never replaces the export's primary success or failure.
            // On cancellation/failure, keep watching through a bounded handoff
            // window: a detached Edge child can create the staged PDF after an
            // initial File.Delete no-op. The profile remains job-isolated while
            // the same loop retries any locks it still owns.
            await CleanupResidueAsync(
                jobDirectory,
                stagedPdf,
                guardAgainstLateCreation: process is not null && (!destinationCommitted || !producerSettled),
                CleanupAttempts,
                PdfPollInterval).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts the local producer behind a stable, speaking failure boundary.
    /// Process-start exceptions can contain machine-specific executable paths;
    /// callers and UI status receive only this message.
    /// </summary>
    internal static Process StartLocalPdfProcess(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        try
        {
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException("The local PDF process returned no process handle.");
        }
        catch (Exception failure) when (failure is Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Microsoft Edge could not start the local PDF process.",
                failure);
        }
    }

    /// <summary>
    /// Waits for a PDF that has both boundary markers, can be opened without a
    /// writer, and has the same length on two consecutive observations.
    /// Internal so the post-process timing race is regression-tested without
    /// depending on Edge being installed.
    /// </summary>
    internal static async Task WaitForCompletePdfAsync(
        string path,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken,
        Task? producerCompletion = null,
        TimeSpan? producerHandoffGrace = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);
        var handoffGrace = producerHandoffGrace ?? LauncherHandoffGrace;
        if (producerCompletion is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(handoffGrace, TimeSpan.Zero);
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);

        long? previousCompleteLength = null;
        long? producerCompletedAt = null;
        var outputAppeared = false;
        try
        {
            while (true)
            {
                bounded.Token.ThrowIfCancellationRequested();
                outputAppeared |= File.Exists(path);
                if (TryReadCompletePdf(path, out var length))
                {
                    if (previousCompleteLength == length)
                    {
                        return;
                    }

                    previousCompleteLength = length;
                }
                else
                {
                    previousCompleteLength = null;
                }

                if (!outputAppeared && producerCompletion?.IsCompleted == true)
                {
                    producerCompletedAt ??= Stopwatch.GetTimestamp();
                    if (Stopwatch.GetElapsedTime(producerCompletedAt.Value) >= handoffGrace)
                    {
                        throw new PdfProducerEndedException();
                    }
                }

                await Task.Delay(pollInterval, bounded.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The PDF did not become complete before the export timeout.");
        }
    }

    internal static RenderRequest PrintHtmlRequest(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new RenderRequest(
            RenderTarget.PrintHtml,
            request.Audience,
            request.TextScalePercent,
            request.TargetLanguageFirst);
    }

    private static ProcessStartInfo StartInfo(string edge, string pdf, string html, string profileDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = edge,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--disable-gpu");
        startInfo.ArgumentList.Add("--disable-background-mode");
        startInfo.ArgumentList.Add("--no-first-run");
        startInfo.ArgumentList.Add("--no-default-browser-check");
        startInfo.ArgumentList.Add($"--user-data-dir={profileDirectory}");
        startInfo.ArgumentList.Add($"--print-to-pdf={pdf}");
        startInfo.ArgumentList.Add(new Uri(html).AbsoluteUri);
        return startInfo;
    }

    private static bool TryReadCompletePdf(string path, out long length)
    {
        length = 0;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            length = stream.Length;
            if (length < 10)
            {
                return false;
            }

            Span<byte> header = stackalloc byte[5];
            stream.ReadExactly(header);
            if (!header.SequenceEqual("%PDF-"u8))
            {
                return false;
            }

            var tailLength = (int)Math.Min(length, 1_024);
            var tail = new byte[tailLength];
            _ = stream.Seek(-tailLength, SeekOrigin.End);
            stream.ReadExactly(tail);
            if (tail.AsSpan().LastIndexOf("%%EOF"u8) < 0)
            {
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async Task DrainAsync(Stream source)
    {
        try
        {
            await source.CopyToAsync(Stream.Null, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // Process teardown can close a redirected pipe between reads. Its
            // contents are deliberately discarded, so closure is completion.
        }
    }

    private static async Task ObserveProcessLifetimeAsync(
        Process process,
        Task standardOutputDrain,
        Task standardErrorDrain)
    {
        try
        {
            await Task.WhenAll(
                process.WaitForExitAsync(CancellationToken.None),
                standardOutputDrain,
                standardErrorDrain).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            // The file-completion boundary remains authoritative. This task is
            // only the content-free signal for a bounded launcher handoff.
        }
    }

    private static string DescribeEdge(Process process)
    {
        try
        {
            return process.HasExited
                ? $"The Edge launcher exited with code {process.ExitCode}."
                : "The Edge launcher was still running.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            return "The Edge launcher state was unavailable.";
        }
    }

    private static async Task<bool> BestEffortStopAsync(Process process, Task? producerCompletion)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            // The process either ended between observation and kill, or the OS
            // refused the best-effort teardown. Cleanup must not mask export.
        }

        producerCompletion ??= ObserveProcessLifetimeAsync(process, Task.CompletedTask, Task.CompletedTask);
        var completed = await Task.WhenAny(
            producerCompletion,
            Task.Delay(ProcessStopGrace)).ConfigureAwait(false);
        if (completed != producerCompletion)
        {
            return false;
        }

        if (producerCompletion.IsFaulted)
        {
            _ = producerCompletion.Exception;
        }

        return true;
    }

    internal static async Task CleanupResidueAsync(
        string jobDirectory,
        string stagedPdf,
        bool guardAgainstLateCreation,
        int attempts,
        TimeSpan pollInterval)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedPdf);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pollInterval, TimeSpan.Zero);

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var stagedPdfDeleted = BestEffortDeleteFile(stagedPdf);
            var jobDirectoryDeleted = BestEffortDeleteDirectory(jobDirectory);
            if (!guardAgainstLateCreation && stagedPdfDeleted && jobDirectoryDeleted)
            {
                return;
            }

            if (attempt + 1 < attempts)
            {
                await Task.Delay(pollInterval).ConfigureAwait(false);
            }
        }
    }

    private static bool BestEffortDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool BestEffortDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed class PdfProducerEndedException()
        : InvalidOperationException("The PDF producer ended before output appeared.");

    public static string? FindEdge()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe"),
        ];

        return candidates.FirstOrDefault(File.Exists);
    }
}
