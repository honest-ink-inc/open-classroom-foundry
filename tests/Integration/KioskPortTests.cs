using System.Drawing.Printing;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;
using Foundry.Rendering;

namespace Foundry.Tests.Integration;

/// <summary>
/// The kiosk hardware ports, tested as far as a headless session honestly can:
/// camera enumeration only (a test that photographs the developer's room is a
/// trespass, not a test), and the raster printer's testable core against
/// Microsoft Print to PDF in print-to-file mode when it exists. Everything
/// deeper is hardware-bench work (plan §12).
/// </summary>
public class KioskPortTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Camera_enumeration_answers_without_throwing_devices_or_not()
    {
        var names = FlashCapCameraSource.EnumerateCameraNames();

        Assert.NotNull(names);
    }

    [Fact]
    public async Task The_raster_core_prints_a_real_pdf_to_file_when_the_inbox_printer_exists()
    {
        if (EdgePdfExporter.FindEdge() is null)
        {
            return;
        }

        var settings = new PrinterSettings { PrinterName = "Microsoft Print to PDF" };
        if (!settings.IsValid)
        {
            return; // No in-box PDF printer here; the hardware bench covers it.
        }

        var work = Path.Combine(Path.GetTempPath(), "ocf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var artifact = ApprovalGate.Approve(
                DraftArtifact.New(new ArtifactDocument(
                    [new Heading(1, "Print test"), new Paragraph("One page, aspect-fit.")]), DataLane.Green),
                "teacher@example.org", [], SomeInstant);

            var sourcePdf = Path.Combine(work, "source.pdf");
            await new EdgePdfExporter(new AccessibleHtmlRenderer()).ExportAsync(
                artifact, new Contracts.ExportRequest(Contracts.RenderTarget.PrintPdf, sourcePdf), CancellationToken.None);

            var printedPdf = Path.Combine(work, "printed.pdf");
            settings.PrintToFile = true;
            settings.PrintFileName = printedPdf;

            await WindowsPdfPrinter.PrintPdfAsync(sourcePdf, settings, duplex: false, CancellationToken.None);

            var bytes = await File.ReadAllBytesAsync(printedPdf);
            Assert.True(bytes.Length > 1000, "The printed file is implausibly small.");
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }
}
