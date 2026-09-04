// SPDX-License-Identifier: GPL-3.0-or-later
using System.Drawing.Printing;
using Foundry.Contracts;
using Foundry.Domain;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Foundry.Infrastructure.Windows;

/// <summary>
/// Silent PDF printing with in-box Windows parts, ported from Writer's Kiosk
/// baseline c2b670b (PdfRasterPrinter.cs): Windows.Data.Pdf rasterizes each
/// page and System.Drawing.Printing spools the bitmaps — no window, no dialog,
/// no third-party install. As an IPrinter it accepts only ApprovedArtifact
/// (ADR-004): the artifact renders to print HTML, becomes a PDF via headless
/// Edge, prints, and its temp files die in a finally block.
/// </summary>
public sealed class WindowsPdfPrinter(IRenderer renderer) : IPrinter
{
    private const double RenderDpi = 300.0;

    public async Task PrintAsync(
        ApprovedArtifact artifact,
        PrintRequest request,
        CancellationToken cancellationToken,
        AmberSinkAuthorization? amberAuthorization = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(request);
        var renderAuthorization = ArtifactSinkAuthorizationGate.DelegateRenderWithinPrint(
            artifact,
            amberAuthorization);

        var tempDirectory = Path.Combine(Path.GetTempPath(), EngineIdentity.InternalId, "print");
        Directory.CreateDirectory(tempDirectory);
        var tempPdf = Path.Combine(tempDirectory, $"{Guid.NewGuid():N}.pdf");

        try
        {
            // Native first: the renderer serves vector-first PDF for vector
            // documents — no browser on the machine required. Documents it
            // refuses (HTML-shaped, non-WinAnsi text) fall back to Edge.
            try
            {
                var native = await renderer.RenderAsync(
                    artifact,
                    new RenderRequest(
                        RenderTarget.PrintPdf,
                        request.Audience,
                        request.TextScalePercent,
                        request.TargetLanguageFirst),
                    cancellationToken,
                    renderAuthorization).ConfigureAwait(false);
                await File.WriteAllBytesAsync(tempPdf, native.Content.ToArray(), cancellationToken).ConfigureAwait(false);
            }
            catch (NotSupportedException)
            {
                await new EdgePdfExporter(renderer).ExportWithinPrintAsync(
                    artifact,
                    new ExportRequest(
                        RenderTarget.PrintPdf,
                        tempPdf,
                        request.Audience,
                        request.TextScalePercent,
                        request.TargetLanguageFirst),
                    renderAuthorization,
                    cancellationToken).ConfigureAwait(false);
            }

            var settings = new PrinterSettings();
            if (!string.IsNullOrWhiteSpace(request.PrinterName))
            {
                settings.PrinterName = request.PrinterName;
            }

            if (!settings.IsValid)
            {
                throw new InvalidOperationException($"Printer \"{settings.PrinterName}\" is not available on this machine.");
            }

            settings.Copies = (short)Math.Clamp(request.Copies, 1, 99);
            await PrintPdfAsync(tempPdf, settings, request.Duplex, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                File.Delete(tempPdf);
            }
            catch (IOException)
            {
                // Best effort; the residue suite audits this boundary.
            }
        }
    }

    /// <summary>Testable core: caller-supplied settings may target print-to-file for hardware-free verification.</summary>
    internal static async Task PrintPdfAsync(string pdfPath, PrinterSettings settings, bool duplex, CancellationToken cancellationToken)
    {
        var pages = await RasterizeAsync(pdfPath).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrintBitmaps(pages, settings, duplex);
        }
        finally
        {
            foreach (var page in pages)
            {
                page.Dispose();
            }
        }
    }

    private static async Task<List<Bitmap>> RasterizeAsync(string pdfPath)
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(pdfPath));
        var document = await PdfDocument.LoadFromFileAsync(file);
        var pages = new List<Bitmap>();
        try
        {
            for (uint i = 0; i < document.PageCount; i++)
            {
                using var page = document.GetPage(i);
                // Page size is in points (1/72 inch); clamp so a malformed page
                // can never demand an absurd bitmap.
                var options = new PdfPageRenderOptions
                {
                    DestinationWidth = (uint)Math.Clamp(page.Size.Width * RenderDpi / 72.0, 1, 4400),
                    DestinationHeight = (uint)Math.Clamp(page.Size.Height * RenderDpi / 72.0, 1, 5700),
                };

                using var stream = new InMemoryRandomAccessStream();
                await page.RenderToStreamAsync(stream, options);
                using var netStream = stream.AsStreamForRead();
                using var buffer = new MemoryStream();
                await netStream.CopyToAsync(buffer).ConfigureAwait(false);
                buffer.Position = 0;

                // Copy out of the stream-backed bitmap: GDI+ requires the source
                // stream to outlive it otherwise.
                using var streamBacked = new Bitmap(buffer);
                pages.Add(new Bitmap(streamBacked));
            }
        }
        catch
        {
            foreach (var page in pages)
            {
                page.Dispose();
            }

            throw;
        }

        if (pages.Count == 0)
        {
            throw new InvalidOperationException("The PDF contained no pages to print.");
        }

        return pages;
    }

    private static void PrintBitmaps(List<Bitmap> pages, PrinterSettings settings, bool duplex)
    {
        using var document = new PrintDocument();
        document.PrinterSettings = settings;
        document.DocumentName = "Approved artifact";
        // StandardPrintController suppresses the on-screen progress box.
        document.PrintController = new StandardPrintController();
        if (duplex && settings.CanDuplex)
        {
            // Long-edge flip: the Flashcard Flywheel's registration doctrine.
            settings.Duplex = Duplex.Vertical;
        }

        document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

        var index = 0;
        document.PrintPage += (_, e) =>
        {
            var bitmap = pages[index];
            // Aspect-fit onto the page (units are 1/100 inch); the artifact
            // carries its own margins, so a full-page fit re-creates the PDF.
            var bounds = e.PageBounds;
            var scale = Math.Min((float)bounds.Width / bitmap.Width, (float)bounds.Height / bitmap.Height);
            var width = bitmap.Width * scale;
            var height = bitmap.Height * scale;
            e.Graphics!.DrawImage(bitmap, (bounds.Width - width) / 2f, (bounds.Height - height) / 2f, width, height);
            index++;
            e.HasMorePages = index < pages.Count;
        };

        document.Print();
    }
}
