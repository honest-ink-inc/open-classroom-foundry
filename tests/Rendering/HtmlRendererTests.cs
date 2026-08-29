using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;
using Xunit;

namespace Foundry.Tests.Rendering;

public class HtmlRendererTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static ApprovedArtifact Approve(ArtifactDocument document)
        => ApprovalGate.Approve(DraftArtifact.New(document, DataLane.Green), "teacher@example.org", [], SomeInstant);

    private static async Task<string> RenderAsync(ArtifactDocument document, RenderRequest request)
    {
        var output = await new AccessibleHtmlRenderer().RenderAsync(Approve(document), request, CancellationToken.None);
        return Encoding.UTF8.GetString(output.Content.Span);
    }

    [Fact]
    public async Task Hostile_content_is_escaped_never_executed()
    {
        var html = await RenderAsync(
            new ArtifactDocument([new Paragraph("<script>alert('x')</script> & <img src=x onerror=y>")]),
            new RenderRequest(RenderTarget.AccessibleHtml));

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("onerror=y>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Teacher_only_content_never_reaches_a_learner_rendering()
    {
        var document = new ArtifactDocument(
        [
            new Paragraph("Water each plant once."),
            new TeacherOnlyNotice("Fade the visual prompt after two independent successes."),
            new EvidenceLink("Steps match the staged photo.", "capture-1, region 2"),
        ]);

        var learner = await RenderAsync(document, new RenderRequest(RenderTarget.AccessibleHtml, RenderAudience.Learner));
        var teacher = await RenderAsync(document, new RenderRequest(RenderTarget.AccessibleHtml, RenderAudience.Teacher));

        Assert.DoesNotContain("Fade the visual prompt", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("capture-1", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("Approved by", learner, StringComparison.Ordinal);

        Assert.Contains("Fade the visual prompt", teacher, StringComparison.Ordinal);
        Assert.Contains("capture-1", teacher, StringComparison.Ordinal);
        Assert.Contains("Approved by teacher@example.org", teacher, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bilingual_pairs_carry_language_and_direction_semantics()
    {
        var html = await RenderAsync(
            new ArtifactDocument(
                [new BilingualPair("Raise your hand.", "ارفع يدك.", "en", "ar")],
                "en"),
            new RenderRequest(RenderTarget.AccessibleHtml));

        Assert.Contains("<p lang=\"en\" dir=\"auto\">Raise your hand.</p>", html, StringComparison.Ordinal);
        Assert.Contains("lang=\"ar\" dir=\"auto\"", html, StringComparison.Ordinal);
        Assert.Contains("ارفع يدك.", html, StringComparison.Ordinal);
        Assert.True(
            html.IndexOf("lang=\"en\"", StringComparison.Ordinal) < html.IndexOf("lang=\"ar\"", StringComparison.Ordinal),
            "The source language precedes the target in reading order.");
    }

    [Fact]
    public async Task Structure_is_semantic_headings_lists_and_scoped_table_headers()
    {
        var html = await RenderAsync(
            new ArtifactDocument(
            [
                new Heading(1, "Watering the class plants"),
                new OrderedSteps(["Pick up the can.", "Fill to the line."]),
                new TableNode(["Plant", "Days"], [["Fern", "2"], ["Cactus", "14"]]),
            ], "en"),
            new RenderRequest(RenderTarget.AccessibleHtml));

        Assert.Contains("<html lang=\"en\">", html, StringComparison.Ordinal);
        Assert.Contains("<title>Watering the class plants</title>", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Watering the class plants</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<ol class=\"steps\">", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Plant</th>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Image_placeholders_are_explicit_never_silent_gaps()
    {
        var html = await RenderAsync(
            new ArtifactDocument([new ImageReference(new AssetId("symbols.stop.v1"), "A red stop sign")]),
            new RenderRequest(RenderTarget.AccessibleHtml));

        Assert.Contains("data-asset-id=\"symbols.stop.v1\"", html, StringComparison.Ordinal);
        Assert.Contains("A red stop sign", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rendering_is_deterministic_byte_for_byte()
    {
        var document = new ArtifactDocument([new Heading(1, "Ten-frames"), new Paragraph("Cut along the lines.")]);
        var artifact = Approve(document);
        var renderer = new AccessibleHtmlRenderer();

        var first = await renderer.RenderAsync(artifact, new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None);
        var second = await renderer.RenderAsync(artifact, new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None);

        Assert.Equal(first.Content.ToArray(), second.Content.ToArray());
    }

    [Fact]
    public async Task Print_html_adds_the_paper_stylesheet()
    {
        var html = await RenderAsync(
            new ArtifactDocument([new Paragraph("Cut along the lines.")]),
            new RenderRequest(RenderTarget.PrintHtml));

        Assert.Contains("@page", html, StringComparison.Ordinal);
        Assert.Contains("break-inside: avoid", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unsupported_targets_refuse_rather_than_pretend()
    {
        var renderer = new AccessibleHtmlRenderer();
        var artifact = Approve(new ArtifactDocument([new Paragraph("x")]));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => renderer.RenderAsync(artifact, new RenderRequest(RenderTarget.PrintPdf), CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_is_respected()
    {
        var renderer = new AccessibleHtmlRenderer();
        var artifact = Approve(new ArtifactDocument([new Paragraph("x")]));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => renderer.RenderAsync(artifact, new RenderRequest(RenderTarget.AccessibleHtml), cancelled.Token));
    }
}
