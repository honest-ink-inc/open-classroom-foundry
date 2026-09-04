using System.Reflection;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Simulated;
using Foundry.Infrastructure.Windows;
using Foundry.Rendering;

namespace Foundry.Tests.Integration;

public sealed class ArtifactSinkAuthorizationTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    private static ApprovedArtifact Approved(DataLane lane, string text = "Synthetic authorization fixture.")
        => ApprovalGate.Approve(
            DraftArtifact.New(new ArtifactDocument([new Paragraph(text)]), lane),
            "teacher@example.org",
            [],
            SomeInstant);

    private static AmberSinkAuthorization TestAuthorization(
        ApprovedArtifact artifact,
        AmberSinkPermission permissions)
    {
        var constructor = Assert.Single(
            typeof(AmberSinkAuthorization).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance));
        return Assert.IsType<AmberSinkAuthorization>(constructor.Invoke([artifact, permissions]));
    }

    [Fact]
    public async Task Green_output_keeps_the_existing_capability_free_path()
    {
        var artifact = Approved(DataLane.Green);
        var renderer = new AccessibleHtmlRenderer();
        var exporter = new RecordingExporter();
        var printer = new VirtualPrintSink();

        var rendered = await renderer.RenderAsync(
            artifact,
            new RenderRequest(RenderTarget.AccessibleHtml),
            CancellationToken.None);
        await exporter.ExportAsync(
            artifact,
            new ExportRequest(RenderTarget.PrintPdf, "synthetic.pdf"),
            CancellationToken.None);
        await printer.PrintAsync(
            artifact,
            new PrintRequest("virtual", Duplex: false, Copies: 1),
            CancellationToken.None);

        Assert.NotEmpty(rendered.Content.ToArray());
        Assert.Single(exporter.Exports);
        Assert.Single(printer.Jobs);
    }

    [Fact]
    public async Task Amber_output_without_a_capability_fails_before_any_sink_effect()
    {
        var artifact = Approved(DataLane.Amber);
        var exporter = new RecordingExporter();
        var printer = new VirtualPrintSink();

        var renderFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer().RenderAsync(
                artifact,
                new RenderRequest(RenderTarget.AccessibleHtml),
                CancellationToken.None));
        var exportFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            exporter.ExportAsync(
                artifact,
                new ExportRequest(RenderTarget.PrintPdf, "synthetic.pdf"),
                CancellationToken.None));
        var printFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            printer.PrintAsync(
                artifact,
                new PrintRequest("virtual", Duplex: false, Copies: 1),
                CancellationToken.None));

        Assert.Contains("request-bound", renderFailure.Message, StringComparison.Ordinal);
        Assert.Contains("request-bound", exportFailure.Message, StringComparison.Ordinal);
        Assert.Contains("request-bound", printFailure.Message, StringComparison.Ordinal);
        Assert.Empty(exporter.Exports);
        Assert.Empty(printer.Jobs);
    }

    [Fact]
    public async Task Alternate_public_render_entry_points_cannot_bypass_amber_authorization()
    {
        var artifact = Approved(DataLane.Amber);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AccessibleHtmlRenderer.RenderPortableSnapshotAsync(
                artifact,
                new RenderRequest(RenderTarget.AccessibleHtml),
                CancellationToken.None));
        Assert.Throws<InvalidOperationException>(() =>
            VectorPdfWriter.Write(artifact, RenderAudience.Learner));
        Assert.Throws<InvalidOperationException>(() =>
            VectorPdfWriter.WriteImposed(
                artifact,
                [(1, 2)],
                RenderAudience.Learner));
    }

    [Fact]
    public async Task A_reflection_manufactured_amber_token_is_inert_in_this_build()
    {
        var artifact = Approved(DataLane.Amber, "First synthetic artifact.");
        var other = Approved(DataLane.Amber, "Second synthetic artifact.");
        var renderOnly = TestAuthorization(
            artifact,
            AmberSinkPermission.Render);

        var renderFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer().RenderAsync(
                artifact,
                new RenderRequest(RenderTarget.AccessibleHtml),
                CancellationToken.None,
                renderOnly));
        var exportFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RecordingExporter().ExportAsync(
                artifact,
                new ExportRequest(RenderTarget.PrintPdf, "synthetic.pdf"),
                CancellationToken.None,
                renderOnly));
        var otherFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer().RenderAsync(
                other,
                new RenderRequest(RenderTarget.AccessibleHtml),
                CancellationToken.None,
                renderOnly));

        Assert.Contains("request-bound", renderFailure.Message, StringComparison.Ordinal);
        Assert.Contains("request-bound", exportFailure.Message, StringComparison.Ordinal);
        Assert.Contains("request-bound", otherFailure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Even_reflection_manufactured_compound_permissions_cannot_reach_virtual_sinks()
    {
        var artifact = Approved(DataLane.Amber);
        var authorization = TestAuthorization(
            artifact,
            AmberSinkPermission.Export | AmberSinkPermission.Print);
        var exporter = new RecordingExporter();
        var printer = new VirtualPrintSink();

        await Assert.ThrowsAsync<InvalidOperationException>(() => exporter.ExportAsync(
            artifact,
            new ExportRequest(RenderTarget.PrintPdf, "synthetic.pdf"),
            CancellationToken.None,
            authorization));
        await Assert.ThrowsAsync<InvalidOperationException>(() => printer.PrintAsync(
            artifact,
            new PrintRequest("virtual", Duplex: false, Copies: 1),
            CancellationToken.None,
            authorization));

        Assert.Empty(exporter.Exports);
        Assert.Empty(printer.Jobs);
    }

    [Fact]
    public void Export_authority_cannot_delegate_while_amber_is_nonoperative()
    {
        var artifact = Approved(DataLane.Amber, "Export source.");
        var other = Approved(DataLane.Amber, "Other source.");
        var exportOnly = TestAuthorization(artifact, AmberSinkPermission.Export);

        Assert.Throws<InvalidOperationException>(() =>
            ArtifactSinkAuthorizationGate.DelegateRenderWithinExport(artifact, exportOnly));
        Assert.Throws<InvalidOperationException>(() =>
            ArtifactSinkAuthorizationGate.DelegateRenderWithinExport(other, exportOnly));
    }

    [Fact]
    public void Print_authority_cannot_delegate_while_amber_is_nonoperative()
    {
        var artifact = Approved(DataLane.Amber, "Print source.");
        var other = Approved(DataLane.Amber, "Other source.");
        var printOnly = TestAuthorization(artifact, AmberSinkPermission.Print);

        Assert.Throws<InvalidOperationException>(() =>
            ArtifactSinkAuthorizationGate.DelegateRenderWithinPrint(artifact, printOnly));
        Assert.Throws<InvalidOperationException>(() =>
            ArtifactSinkAuthorizationGate.DelegateRenderWithinPrint(other, printOnly));
    }

    [Fact]
    public async Task Amber_print_is_refused_before_the_renderer_receives_any_authority()
    {
        var artifact = Approved(DataLane.Amber, "Print renderer confinement.");
        var printOnly = TestAuthorization(artifact, AmberSinkPermission.Print);
        var renderer = new CapturingFailingRenderer();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new WindowsPdfPrinter(renderer).PrintAsync(
                artifact,
                new PrintRequest("", Duplex: false, Copies: 1),
                CancellationToken.None,
                printOnly));

        Assert.Null(renderer.Authorization);
    }

    [Fact]
    public async Task Windows_sinks_refuse_amber_before_discovery_or_temporary_output()
    {
        var artifact = Approved(DataLane.Amber);
        var destination = Path.Combine(
            Path.GetTempPath(),
            $"honest-ink-amber-refusal-{Guid.NewGuid():N}.pdf");
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new EdgePdfExporter(new AccessibleHtmlRenderer()).ExportAsync(
                    artifact,
                    new ExportRequest(RenderTarget.PrintPdf, destination),
                    CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new WindowsPdfPrinter(new AccessibleHtmlRenderer()).PrintAsync(
                    artifact,
                    new PrintRequest("", Duplex: false, Copies: 1),
                    CancellationToken.None));

            Assert.False(File.Exists(destination));
        }
        finally
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }
        }
    }

    private sealed class CapturingFailingRenderer : IRenderer
    {
        internal AmberSinkAuthorization? Authorization { get; private set; }

        public Task<RenderedOutput> RenderAsync(
            ApprovedArtifact artifact,
            RenderRequest request,
            CancellationToken cancellationToken,
            AmberSinkAuthorization? amberAuthorization = null)
        {
            Authorization = amberAuthorization;
            throw new InvalidOperationException("Synthetic stop after capability capture.");
        }
    }
}
