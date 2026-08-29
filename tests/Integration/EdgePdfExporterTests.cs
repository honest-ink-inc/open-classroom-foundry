using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;
using Foundry.Rendering;
using Xunit;

namespace Foundry.Tests.Integration;

public class EdgePdfExporterTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task An_approved_artifact_becomes_a_real_pdf()
    {
        if (EdgePdfExporter.FindEdge() is null)
        {
            // No Edge on this machine: the pipeline cannot be exercised here.
            // The hardware bench (plan §12) covers it where Edge exists.
            return;
        }

        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(new ArtifactDocument(
                [new Heading(1, "Ten-frame practice"), new Paragraph("Cut along the lines.")]), DataLane.Green),
            "teacher@example.org", [], SomeInstant);

        var destination = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"), "ten-frames.pdf");
        try
        {
            await new EdgePdfExporter(new AccessibleHtmlRenderer()).ExportAsync(
                artifact, new ExportRequest(RenderTarget.PrintPdf, destination), CancellationToken.None);

            var bytes = await File.ReadAllBytesAsync(destination);
            Assert.True(bytes.Length > 1000, "The PDF is implausibly small.");
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally
        {
            var directory = Path.GetDirectoryName(destination)!;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
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
}
