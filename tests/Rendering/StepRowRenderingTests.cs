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
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green), "teacher@example.org", [], SomeInstant);
        var output = await new AccessibleHtmlRenderer().RenderAsync(
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
        Assert.Contains("data-asset-id=\"agency.stop.v1\"", listItem, StringComparison.Ordinal);
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
}
