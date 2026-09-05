// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;
using Foundry.Rendering;
using Xunit.Abstractions;

namespace Foundry.Tests.Rendering;

public sealed class LearnerHeldKitLayoutTests(ITestOutputHelper output)
{
    private const string Pledge = "Synthetic layout proof: blank paper, no learner record.";
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new() { WriteIndented = true };

    public static TheoryData<PageSize, int> GoalCases()
    {
        var cases = new TheoryData<PageSize, int>();
        foreach (var size in Enum.GetValues<PageSize>())
        {
            for (var count = 2; count <= 6; count++)
            {
                cases.Add(size, count);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(GoalCases))]
    public async Task Goal_prompt_rules_do_not_enter_the_next_prompt_label(PageSize size, int count)
    {
        var prompts = SyntheticPrompts(count);
        var definition = PressRoomCatalog.ById("goal-post");
        var values = new Dictionary<string, string>(PressRoomCatalog.Defaults(definition), StringComparer.Ordinal)
        {
            ["prompts"] = string.Join('\n', prompts),
            ["pledge"] = Pledge,
            [PressRoomCatalog.PageKey] = size switch
            {
                PageSize.Letter => "Letter",
                PageSize.A4 => "A4",
                PageSize.LetterLandscape => "Letter landscape",
                PageSize.A4Landscape => "A4 landscape",
                _ => throw new ArgumentOutOfRangeException(nameof(size)),
            },
        };
        var document = definition.Build(new PressInputs(values));
        await VerifyRenderedLayout(document, prompts, $"goal-{size}-{count}");
        var expectedPages = count == 6 && size is PageSize.LetterLandscape or PageSize.A4Landscape ? 2 : 1;
        Assert.Equal(expectedPages, document.Nodes.Count);
        if (expectedPages == 2)
        {
            Assert.Equal([5, 1], document.Nodes.OfType<VectorGraphic>().Select(page =>
                page.Primitives.OfType<TextLabel>().Count(label => label.Text != Pledge)));
        }
    }

    [Theory]
    [InlineData(32.24, 1)]
    [InlineData(32.247, 1)]
    [InlineData(32.249, 2)]
    [InlineData(32.2499, 2)]
    [InlineData(32.25, 2)]
    [InlineData(32.26, 2)]
    public async Task Goal_pagination_handles_just_fitting_touching_and_overflowing_ink(double marginMm, int expectedPages)
    {
        var prompts = SyntheticPrompts(6);
        var document = LearnerHeldKit.GoalPost(prompts, Pledge, PageSize.Letter, marginMm);
        await VerifyRenderedLayout(document, prompts,
            "goal-margin-" + marginMm.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(expectedPages, document.Nodes.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.MaxValue)]
    [InlineData(88)]
    public void Impossible_goal_margins_refuse_without_zero_capacity_pagination(double marginMm)
    {
        var refusal = Assert.Throws<ArgumentException>(() =>
            LearnerHeldKit.GoalPost(SyntheticPrompts(6), Pledge, PageSize.A4Landscape, marginMm));
        Assert.Equal("marginMm", refusal.ParamName);
    }

    [Theory]
    [InlineData(PageSize.A4Landscape)]
    [InlineData(PageSize.LetterLandscape)]
    [InlineData(PageSize.A4)]
    [InlineData(PageSize.Letter)]
    public async Task Portfolio_reflection_keeps_four_pages_and_noncolliding_prompt_rules(PageSize size)
    {
        var prompts = SyntheticPrompts(4);
        var document = LearnerHeldKit.PortfolioPassport(
            ["Synthetic choice", "Synthetic reason"], prompts, 8, Pledge, size);
        Assert.Equal(4, document.Nodes.Count);
        await VerifyRenderedLayout(document, prompts, $"portfolio-{size}", promptedPageIndex: 3);
    }

    private async Task VerifyRenderedLayout(
        ArtifactDocument document,
        string[] prompts,
        string caseName,
        int? promptedPageIndex = null)
    {
        var issues = DocumentValidator.Validate(document);
        Assert.DoesNotContain(issues, issue => issue.Severity == ValidationSeverity.Blocking);
        var approved = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green), "Synthetic layout reviewer", issues,
            new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero));
        var renderer = new AccessibleHtmlRenderer();
        var pdfRequest = new RenderRequest(RenderTarget.PrintPdf);
        var htmlRequest = new RenderRequest(RenderTarget.PrintHtml);
        var pdf = await renderer.RenderAsync(approved, pdfRequest, CancellationToken.None);
        var html = await renderer.RenderAsync(approved, htmlRequest, CancellationToken.None);
        Assert.Equal(pdf.Content.ToArray(), (await renderer.RenderAsync(approved, pdfRequest, CancellationToken.None)).Content.ToArray());
        Assert.Equal(html.Content.ToArray(), (await renderer.RenderAsync(approved, htmlRequest, CancellationToken.None)).Content.ToArray());

        var allPages = document.Nodes.OfType<VectorGraphic>().ToArray();
        var promptedPages = promptedPageIndex is int index ? [allPages[index]] : allPages;
        var labels = promptedPages.SelectMany(page => page.Primitives.OfType<TextLabel>())
            .Where(label => label.Text != Pledge).ToArray();
        Assert.Equal(prompts, labels.Select(label => label.Text));
        Assert.All(allPages, page => Assert.Single(page.Primitives.OfType<TextLabel>(), label => label.Text == Pledge));
        Assert.Equal(prompts.Length * 3, promptedPages.Sum(page => page.Primitives.OfType<LineSeg>().Count()));

        var htmlText = Encoding.UTF8.GetString(html.Content.Span);
        var svgPages = new List<XElement>();
        for (var cursor = 0; ;)
        {
            var start = htmlText.IndexOf("<svg ", cursor, StringComparison.Ordinal);
            if (start < 0)
            {
                break;
            }

            var end = htmlText.IndexOf("</svg>", start, StringComparison.Ordinal) + "</svg>".Length;
            svgPages.Add(XElement.Parse(htmlText[start..end]));
            cursor = end;
        }

        Assert.Equal(allPages.Length, svgPages.Count);
        Assert.Contains($"/Count {allPages.Length}", Encoding.Latin1.GetString(pdf.Content.Span), StringComparison.Ordinal);
        var measurements = new List<object>();
        var violations = new List<string>();
        foreach (var page in promptedPages)
        {
            var pageIndex = Array.IndexOf(allPages, page);
            var pageLabels = page.Primitives.OfType<TextLabel>().Where(label => label.Text != Pledge).ToArray();
            var rules = page.Primitives.OfType<LineSeg>().ToArray();
            Assert.Equal(pageLabels.Length * 3, rules.Length);
            var renderedLabels = svgPages[pageIndex].Descendants()
                .Where(element => element.Name.LocalName == "text" && element.Value != Pledge).ToArray();
            var renderedRules = svgPages[pageIndex].Descendants()
                .Where(element => element.Name.LocalName == "line").ToArray();
            Assert.Equal(pageLabels.Select(label => label.Text), renderedLabels.Select(label => label.Value));
            Assert.Equal(rules.Length, renderedRules.Length);
            for (var promptIndex = 0; promptIndex < pageLabels.Length; promptIndex++)
            {
                var label = pageLabels[promptIndex];
                Assert.Equal(5, label.FontSizeMm);
                Assert.Equal(label.Y + 11, rules[promptIndex * 3].Y1, 9);
                Assert.Equal(9, rules[promptIndex * 3 + 1].Y1 - rules[promptIndex * 3].Y1, 9);
                Assert.Equal(9, rules[promptIndex * 3 + 2].Y1 - rules[promptIndex * 3 + 1].Y1, 9);
            }

            Assert.All(rules, rule =>
            {
                Assert.InRange(rule.Y1, 0, page.HeightMm);
                Assert.Equal(rule.Y1, rule.Y2);
                Assert.Equal(0.3, rule.StrokeWidthMm);
            });
            for (var promptIndex = 0; promptIndex + 1 < pageLabels.Length; promptIndex++)
            {
                var previousRule = rules[promptIndex * 3 + 2];
                var nextLabel = pageLabels[promptIndex + 1];
                var gap = nextLabel.Y - nextLabel.FontSizeMm - previousRule.Y1 - previousRule.StrokeWidthMm / 2;
                var renderedGap = Number(renderedLabels[promptIndex + 1], "y")
                    - Number(renderedLabels[promptIndex + 1], "font-size")
                    - Number(renderedRules[promptIndex * 3 + 2], "y1")
                    - Number(renderedRules[promptIndex * 3 + 2], "stroke-width") / 2;
                measurements.Add(new { pageIndex, promptIndex, gapMm = gap, renderedGapMm = renderedGap });
                if (gap <= 0 || renderedGap <= 0)
                {
                    violations.Add(FormattableString.Invariant(
                        $"{caseName} page {pageIndex + 1}, prompts {promptIndex + 1}->{promptIndex + 2}: rule-to-next-label gap {gap:0.######} mm; rendered SVG gap {renderedGap:0.######} mm."));
                }
            }
        }

        var evidence = JsonSerializer.Serialize(new
        {
            caseName,
            pages = allPages.Length,
            pdfSha256 = Convert.ToHexString(SHA256.HashData(pdf.Content.Span)),
            htmlSha256 = Convert.ToHexString(SHA256.HashData(html.Content.Span)),
            measurements,
            violations,
        }, EvidenceJsonOptions);
        output.WriteLine(evidence);
        var evidenceDirectory = Environment.GetEnvironmentVariable("OCF_I25_EVIDENCE_DIRECTORY");
        if (!string.IsNullOrWhiteSpace(evidenceDirectory))
        {
            Directory.CreateDirectory(evidenceDirectory);
            await File.WriteAllBytesAsync(Path.Combine(evidenceDirectory, caseName + ".pdf"), pdf.Content.ToArray());
            await File.WriteAllBytesAsync(Path.Combine(evidenceDirectory, caseName + ".html"), html.Content.ToArray());
            await File.WriteAllTextAsync(Path.Combine(evidenceDirectory, caseName + ".json"), evidence);
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static decimal Number(XElement element, string attribute)
        => decimal.Parse(element.Attribute(attribute)!.Value, CultureInfo.InvariantCulture);

    private static string[] SyntheticPrompts(int count)
        => [.. Enumerable.Range(1, count).Select(index => $"Synthetic prompt {index}: my chosen next step")];
}
