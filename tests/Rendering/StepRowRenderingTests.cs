using System.Security.Cryptography;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;

namespace Foundry.Tests.Rendering;

public class StepRowRenderingTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static async Task<string> RenderAsync(ArtifactDocument document, RenderRequest? request = null)
    {
        var catalog = new FixtureAssetCatalog();
        var reviewedAssets = ExactAssetCatalogSnapshot.CaptureForReview(document, catalog);
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green),
            "teacher@example.org",
            [],
            SomeInstant,
            reviewedAssets.Bindings);
        var output = await new AccessibleHtmlRenderer(reviewedAssets).RenderAsync(
            artifact, request ?? new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None);
        return Encoding.UTF8.GetString(output.Content.Span);
    }

    [Fact]
    public async Task A_symbol_renders_inside_its_steps_list_item()
    {
        var html = await RenderAsync(new ArtifactDocument(
        [
            new StepRow("Stop at the door.", new ImageReference(new AssetId("agency.stop.v1"), "An octagon outline")),
            new StepRow("Ask for help."),
        ]));

        // RC-3 on the page: figure and text share one <li>.
        var listItem = html[html.IndexOf("<li>", StringComparison.Ordinal)..html.IndexOf("</li>", StringComparison.Ordinal)];
        Assert.Contains("<img src=\"data:image/svg+xml;base64,", listItem, StringComparison.Ordinal);
        Assert.Contains("Stop at the door.", listItem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Numbering_continues_across_page_breaks()
    {
        var html = await RenderAsync(new ArtifactDocument(
        [
            new StepRow("One."), new StepRow("Two."), new StepRow("Three."),
            new PageBreak(),
            new StepRow("Four."), new StepRow("Five."),
        ]));

        Assert.Contains("<ol class=\"steps\">", html, StringComparison.Ordinal);
        Assert.Contains("<ol class=\"steps\" start=\"4\">", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"page-break\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bilingual_rows_carry_language_semantics_and_honor_target_first()
    {
        var document = new ArtifactDocument(
            [new StepRow("Line up.", null, "Haz fila.", "en", "es")], "en");

        var sourceFirst = await RenderAsync(document);
        var targetFirst = await RenderAsync(document,
            new RenderRequest(RenderTarget.AccessibleHtml, TargetLanguageFirst: true));

        Assert.Contains("<p lang=\"es\" dir=\"auto\">Haz fila.</p>", sourceFirst, StringComparison.Ordinal);
        Assert.True(
            sourceFirst.IndexOf("Line up.", StringComparison.Ordinal) < sourceFirst.IndexOf("Haz fila.", StringComparison.Ordinal));
        Assert.True(
            targetFirst.IndexOf("Haz fila.", StringComparison.Ordinal) < targetFirst.IndexOf("Line up.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Large_print_is_a_render_dial_not_a_content_change()
    {
        var document = new ArtifactDocument([new StepRow("Cut along the lines.")]);

        var large = await RenderAsync(document,
            new RenderRequest(RenderTarget.PrintHtml, TextScalePercent: 160));
        var ordinary = await RenderAsync(document, new RenderRequest(RenderTarget.PrintHtml));

        Assert.Contains("body { font-size: 160%; }", large, StringComparison.Ordinal);
        Assert.DoesNotContain("font-size: 160%", ordinary, StringComparison.Ordinal);
        Assert.Contains("Cut along the lines.", large, StringComparison.Ordinal);
    }

    private sealed class FixtureAssetCatalog : IAssetCatalog
    {
        private static readonly byte[] Content = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"10\"/></svg>");
        private static readonly string ContentHash = Convert.ToHexString(SHA256.HashData(Content));

        public IReadOnlyList<AssetProvenance> All => [];

        public AssetProvenance? Find(AssetId id)
            => new(
                id,
                $"concept.{id.Value}",
                "1.0.0",
                "fixture.svg",
                "image/svg+xml",
                "synthetic test",
                "synthetic test",
                "CC0-1.0",
                ContentHash,
                "Synthetic fixture",
                "Synthetic fixture",
                Redistributable: true);

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            content = Content;
            mimeType = "image/svg+xml";
            return true;
        }
    }
}
