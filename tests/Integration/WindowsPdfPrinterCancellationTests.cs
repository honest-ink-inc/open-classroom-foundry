// SPDX-License-Identifier: GPL-3.0-or-later
using System.Drawing.Printing;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;

namespace Foundry.Tests.Integration;

public sealed class WindowsPdfPrinterCancellationTests
{
    [Fact]
    public async Task A_precanceled_Green_print_does_not_call_the_renderer()
    {
        var renderer = new RecordingFailingRenderer();
        var cancellationToken = new CancellationToken(canceled: true);

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new WindowsPdfPrinter(renderer).PrintAsync(
                Approved(DataLane.Green),
                new PrintRequest("", Duplex: false, Copies: 1),
                cancellationToken));

        Assert.Equal(cancellationToken, failure.CancellationToken);
        Assert.False(renderer.Called);
    }

    [Fact]
    public async Task A_precanceled_raster_print_does_not_open_the_input_pdf()
    {
        var absentPath = Path.Combine(
            Path.GetTempPath(),
            $"honest-ink-canceled-print-{Guid.NewGuid():N}.pdf");
        var cancellationToken = new CancellationToken(canceled: true);
        Assert.False(File.Exists(absentPath));

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            WindowsPdfPrinter.PrintPdfAsync(
                absentPath,
                new PrinterSettings(),
                duplex: false,
                cancellationToken));

        Assert.Equal(cancellationToken, failure.CancellationToken);
        Assert.False(File.Exists(absentPath));
    }

    [Fact]
    public async Task Cancellation_does_not_replace_the_Amber_sink_refusal()
    {
        var renderer = new RecordingFailingRenderer();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WindowsPdfPrinter(renderer).PrintAsync(
                Approved(DataLane.Amber),
                new PrintRequest("", Duplex: false, Copies: 1),
                new CancellationToken(canceled: true)));

        Assert.Contains("request-bound", failure.Message, StringComparison.Ordinal);
        Assert.False(renderer.Called);
    }

    private static ApprovedArtifact Approved(DataLane lane)
        => ApprovalGate.Approve(
            DraftArtifact.New(
                new ArtifactDocument([new Paragraph("Synthetic print cancellation fixture.")]),
                lane),
            "teacher@example.org",
            [],
            new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero));

    private sealed class RecordingFailingRenderer : IRenderer
    {
        internal bool Called { get; private set; }

        public Task<RenderedOutput> RenderAsync(
            ApprovedArtifact artifact,
            RenderRequest request,
            CancellationToken cancellationToken,
            AmberSinkAuthorization? amberAuthorization = null)
        {
            Called = true;
            throw new InvalidOperationException("Synthetic renderer must not run after cancellation.");
        }
    }
}
