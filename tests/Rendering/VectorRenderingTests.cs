using System.Text;
using System.Text.Json;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;

namespace Foundry.Tests.Rendering;

public class VectorRenderingTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static ApprovedArtifact Approve(ArtifactDocument document)
        => ApprovalGate.Approve(DraftArtifact.New(document, DataLane.Green), "teacher@example.org", [], SomeInstant);

    private static ArtifactDocument SheetWithLabel(string labelText) => new(
    [
        new VectorGraphic(100, 50,
        [
            new LineSeg(10, 10, 90, 10, 0.5),
            new RectShape(10, 20, 30, 20),
            new CircleShape(70, 30, 8),
            new TextLabel(50, 45, labelText),
        ], "A test sheet"),
    ]);

    [Fact]
    public async Task Vector_sheets_render_as_millimeter_true_inline_svg()
    {
        var output = await new AccessibleHtmlRenderer().RenderAsync(
            Approve(SheetWithLabel("50")), new RenderRequest(RenderTarget.PrintHtml), CancellationToken.None);
        var html = Encoding.UTF8.GetString(output.Content.Span);

        Assert.Contains("viewBox=\"0 0 100 50\"", html, StringComparison.Ordinal);
        Assert.Contains("width=\"100mm\" height=\"50mm\"", html, StringComparison.Ordinal);
        Assert.Contains("<line x1=\"10\" y1=\"10\" x2=\"90\" y2=\"10\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"A test sheet\"", html, StringComparison.Ordinal);
        Assert.Contains("figure.vector-sheet { break-after: page", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Labels_are_escaped_like_all_text()
    {
        var output = await new AccessibleHtmlRenderer().RenderAsync(
            Approve(SheetWithLabel("<script>alert(1)</script>")),
            new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None);
        var html = Encoding.UTF8.GetString(output.Content.Span);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_single_sheet_document_exports_as_standalone_svg()
    {
        var output = await new AccessibleHtmlRenderer().RenderAsync(
            Approve(SheetWithLabel("50")), new RenderRequest(RenderTarget.Svg), CancellationToken.None);

        Assert.Equal("image/svg+xml", output.MimeType);
        var svg = Encoding.UTF8.GetString(output.Content.Span);
        Assert.StartsWith("<svg xmlns=\"http://www.w3.org/2000/svg\"", svg, StringComparison.Ordinal);
        Assert.Contains("role=\"img\" aria-label=\"A test sheet\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Multi_sheet_documents_refuse_standalone_svg()
    {
        var document = new ArtifactDocument(
        [
            new VectorGraphic(100, 50, [new LineSeg(0, 0, 1, 1)], "One"),
            new VectorGraphic(100, 50, [new LineSeg(0, 0, 1, 1)], "Two"),
        ]);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => new AccessibleHtmlRenderer().RenderAsync(
                Approve(document), new RenderRequest(RenderTarget.Svg), CancellationToken.None));
    }

    [Fact]
    public async Task A_vector_sheet_mixed_with_other_semantic_nodes_refuses_instead_of_dropping_content()
    {
        var document = new ArtifactDocument(
        [
            new Heading(1, "Do not drop this heading"),
            new VectorGraphic(100, 50, [new LineSeg(0, 0, 1, 1)], "One sheet"),
        ]);

        var refusal = await Assert.ThrowsAsync<NotSupportedException>(
            () => new AccessibleHtmlRenderer().RenderAsync(
                Approve(document),
                new RenderRequest(RenderTarget.Svg),
                CancellationToken.None));

        Assert.Contains("no other nodes", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Vector_documents_survive_polymorphic_serialization()
    {
        var document = SheetWithLabel("50");

        var roundTripped = JsonSerializer.Deserialize<ArtifactDocument>(JsonSerializer.Serialize(document));

        Assert.Equal(JsonSerializer.Serialize(document), JsonSerializer.Serialize(roundTripped));
        Assert.IsType<VectorGraphic>(roundTripped!.Nodes[0]);
    }
}
