using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;
using Xunit;

namespace Foundry.Tests.Rendering;

/// <summary>Bilingual and print regressions for the Days 76–90 evidence bundle.</summary>
public class BilingualRegressionTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] ExpectedOrder = ["One.", "Uno.", "Two.", "Dos.", "Three.", "Tres."];

    private static async Task<string> RenderAsync(ArtifactDocument document)
    {
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green), "teacher@example.org", [], SomeInstant);
        var output = await new AccessibleHtmlRenderer().RenderAsync(
            artifact, new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None);
        return Encoding.UTF8.GetString(output.Content.Span);
    }

    [Fact]
    public async Task A_very_long_translation_is_never_truncated()
    {
        var longTranslation = string.Concat(Enumerable.Repeat("Riega cada planta una vez y devuelve la regadera a su lugar. ", 12)).Trim();

        var html = await RenderAsync(new ArtifactDocument(
            [new BilingualPair("Water each plant once.", longTranslation, "en", "es")]));

        Assert.Contains(longTranslation, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cjk_text_with_mixed_script_numerals_survives_intact()
    {
        var html = await RenderAsync(new ArtifactDocument(
            [new BilingualPair("Step 3: line up at 8:15.", "第3步：8:15 在门口排队。", "en", "zh")]));

        Assert.Contains("第3步：8:15 在门口排队。", html, StringComparison.Ordinal);
        Assert.Contains("lang=\"zh\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Many_pairs_keep_their_pairwise_order()
    {
        var html = await RenderAsync(new ArtifactDocument(
        [
            new BilingualPair("One.", "Uno.", "en", "es"),
            new BilingualPair("Two.", "Dos.", "en", "es"),
            new BilingualPair("Three.", "Tres.", "en", "es"),
        ]));

        var positions = ExpectedOrder
            .Select(text => html.IndexOf(text, StringComparison.Ordinal))
            .ToArray();

        Assert.All(positions, p => Assert.True(p >= 0));
        Assert.Equal(positions.OrderBy(p => p), positions);
    }
}
