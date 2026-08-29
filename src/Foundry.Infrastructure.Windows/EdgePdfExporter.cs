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
public sealed class EdgePdfExporter(IRenderer renderer) : IExporter
{
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

        var rendered = await renderer.RenderAsync(
            artifact, new RenderRequest(RenderTarget.PrintHtml), cancellationToken).ConfigureAwait(false);

        var tempDirectory = Path.Combine(Path.GetTempPath(), EngineIdentity.InternalId, "print");
        Directory.CreateDirectory(tempDirectory);
        var tempHtml = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.html");

        try
        {
            await File.WriteAllBytesAsync(tempHtml, rendered.Content.ToArray(), cancellationToken).ConfigureAwait(false);

            var destination = Path.GetFullPath(request.DestinationHint);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = edge,
                Arguments = $"--headless --disable-gpu --no-first-run --print-to-pdf=\"{destination}\" \"file:///{tempHtml.Replace('\\', '/')}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            }) ?? throw new InvalidOperationException("Edge failed to start.");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

            if (!File.Exists(destination))
            {
                throw new InvalidOperationException("Edge exited without producing the PDF.");
            }
        }
        finally
        {
            try
            {
                File.Delete(tempHtml);
            }
            catch (IOException)
            {
                // Best effort; the residue suite audits this boundary.
            }
        }
    }

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
