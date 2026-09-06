// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;
using Foundry.Rendering;

namespace Foundry.Tests.Rendering;

// Nominal model geometry, serialized SVG and the native Courier PDF contract.
// These checks do not measure browser/system-font ink or a physical printer.
public sealed class VersionedCalibrationLayoutTests
{
    public static TheoryData<PageSize, double, bool> GeometryCases()
    {
        var data = new TheoryData<PageSize, double, bool>();
        foreach (var size in new[] { PageSize.Letter, PageSize.A4 })
        {
            foreach (var margin in Enumerable.Range(5, 21).Select(value => (double)value).Concat([5.001, 12.345, 24.999]))
            {
                data.Add(size, margin, false);
                data.Add(size, margin, true);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GeometryCases))]
    public async Task Candidate_wraps_original_words_and_translates_every_instrument_without_scaling(PageSize size, double margin, bool lowInk)
    {
        var historical = Graphic(CalibrationPress.ProofPage(size, margin));
        var candidate = Graphic(CalibrationPressReplacement.ProofPage(size, margin));
        var originalInstructions = historical.Primitives.Skip(3).Take(5).Cast<TextLabel>().ToArray();
        var instructionCount = candidate.Primitives.Count - (historical.Primitives.Count - 5);
        var instructions = candidate.Primitives.Skip(3).Take(instructionCount).Cast<TextLabel>().ToArray();

        Assert.Equal(historical.WidthMm, candidate.WidthMm);
        Assert.Equal(historical.HeightMm, candidate.HeightMm);
        Assert.Equal(historical.Description, candidate.Description);
        Assert.Equal(historical.Primitives.Take(3), candidate.Primitives.Take(3));
        Assert.Equal(string.Join(' ', originalInstructions.Select(label => label.Text)),
            string.Join(' ', instructions.Select(label => label.Text)));
        Assert.InRange(instructions.Length, 5, 8);
        Assert.All(instructions, label =>
        {
            Assert.Equal(margin + 10, label.X);
            Assert.Equal(3.5, label.FontSizeMm);
            Assert.Equal(TextAnchor.Start, label.Anchor);
            Assert.InRange(label.Text.Length, 1, 67);
        });
        for (var index = 0; index < instructions.Length; index++)
        {
            Assert.Equal(margin + 19 + index * 5.5, instructions[index].Y, 9);
        }

        var baseline = candidate.Primitives.OfType<LineSeg>()
            .Single(line => line.StrokeWidthMm == 0.6 && line.Y1 == line.Y2);
        var translation = baseline.Y1 - 70;
        Assert.InRange(translation, 0, 24.5);
        Assert.InRange(baseline.Y1 - 6 - instructions[^1].Y, 6 - 1e-9, double.MaxValue);
        Assert.Equal(historical.Primitives.Skip(8).Select(primitive => Translate(primitive, translation)),
            candidate.Primitives.Skip(3 + instructionCount));

        var selected = PressRoomCatalog.ById("calibration-proof", "0.2.0");
        var values = PressRoomCatalog.Defaults(selected);
        values[PressRoomCatalog.PageKey] = size == PageSize.A4 ? "A4" : "Letter";
        values["margin"] = margin.ToString(CultureInfo.InvariantCulture);
        var document = selected.BuildForReview(new PressInputs(values)).Document;
        Assert.Equal(candidate.Primitives, Graphic(document).Primitives);
        if (lowInk)
        {
            document = selected.ApplyLowInk(document);
        }

        var actual = Graphic(document);
        Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(document)));
        AssertFrameAndInstrumentGeometry(actual, margin, lowInk);
        AssertNativeCourierNominalTextBounds(actual, margin);
        var approved = Approve(document);
        var rendered = await new AccessibleHtmlRenderer().RenderAsync(
            approved, new RenderRequest(RenderTarget.Svg), CancellationToken.None);
        var svg = XElement.Parse(Encoding.UTF8.GetString(rendered.Content.Span));
        Assert.Equal(Number(actual.WidthMm), decimal.Parse(svg.Attribute("width")!.Value[..^2], CultureInfo.InvariantCulture));
        Assert.Equal(Number(actual.HeightMm), decimal.Parse(svg.Attribute("height")!.Value[..^2], CultureInfo.InvariantCulture));
        var svgLines = svg.Descendants().Where(element => element.Name.LocalName == "line").ToArray();
        var modelLines = actual.Primitives.OfType<LineSeg>().ToArray();
        Assert.Equal(modelLines.Length, svgLines.Length);
        for (var index = 0; index < modelLines.Length; index++)
        {
            Assert.Equal(Number(modelLines[index].X1), Attribute(svgLines[index], "x1"));
            Assert.Equal(Number(modelLines[index].X2), Attribute(svgLines[index], "x2"));
            Assert.Equal(Number(modelLines[index].Y1), Attribute(svgLines[index], "y1"));
            Assert.Equal(Number(modelLines[index].Y2), Attribute(svgLines[index], "y2"));
        }

        var renderedRules = svgLines.Where(element => Attribute(element, "stroke-width") == (lowInk ? 0.36m : 0.6m)).ToArray();
        Assert.Equal(2, renderedRules.Length);
        Assert.Contains(renderedRules, line => Attribute(line, "x2") - Attribute(line, "x1") == 100m);
        Assert.Contains(renderedRules, line => Attribute(line, "y2") - Attribute(line, "y1") == 100m);
    }

    [Theory]
    [InlineData(PageSize.Letter, 5)]
    [InlineData(PageSize.Letter, 12)]
    [InlineData(PageSize.Letter, 25)]
    [InlineData(PageSize.A4, 5)]
    [InlineData(PageSize.A4, 12.345)]
    [InlineData(PageSize.A4, 25)]
    public async Task Candidate_keeps_supported_both_audience_outputs_deterministic(PageSize size, double margin)
    {
        var definition = PressRoomCatalog.ById("calibration-proof", "0.2.0");
        foreach (var lowInk in new[] { false, true })
        {
            var document = CalibrationPressReplacement.ProofPage(size, margin);
            var approved = Approve(lowInk ? definition.ApplyLowInk(document) : document);
            var renderer = new AccessibleHtmlRenderer();
            foreach (var audience in Enum.GetValues<RenderAudience>())
            {
                foreach (var target in new[] { RenderTarget.AccessibleHtml, RenderTarget.PrintHtml, RenderTarget.PrintPdf })
                {
                    var request = new RenderRequest(target, audience);
                    var first = await renderer.RenderAsync(approved, request, CancellationToken.None);
                    var second = await renderer.RenderAsync(approved, request, CancellationToken.None);
                    Assert.Equal(first.Content.ToArray(), second.Content.ToArray());
                    Assert.NotEmpty(first.Content.ToArray());
                }
            }
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.MaxValue)]
    [InlineData(4.999)]
    [InlineData(25.001)]
    public void Candidate_refuses_nonfinite_or_out_of_range_margins(double margin)
    {
        var refusal = Assert.Throws<ArgumentException>(() => CalibrationPressReplacement.ProofPage(marginMm: margin));
        Assert.Equal("marginMm", refusal.ParamName);
    }

    [Theory]
    [InlineData(PageSize.LetterLandscape)]
    [InlineData(PageSize.A4Landscape)]
    public void Candidate_remains_a_portrait_instrument(PageSize size)
        => Assert.Throws<ArgumentException>(() => CalibrationPressReplacement.ProofPage(size));

    private static void AssertFrameAndInstrumentGeometry(VectorGraphic graphic, double margin, bool lowInk)
    {
        var frame = Assert.IsType<RectShape>(graphic.Primitives[0]);
        Assert.Equal(margin, frame.X);
        Assert.Equal(margin, frame.Y);
        Assert.Equal(graphic.WidthMm - 2 * margin, frame.WidthMm);
        Assert.Equal(graphic.HeightMm - 2 * margin, frame.HeightMm);
        foreach (var primitive in graphic.Primitives)
        {
            var coordinates = primitive switch
            {
                LineSeg line => new[] { line.X1, line.Y1, line.X2, line.Y2, line.StrokeWidthMm },
                RectShape rectangle => [rectangle.X, rectangle.Y, rectangle.WidthMm, rectangle.HeightMm, rectangle.StrokeWidthMm],
                CircleShape circle => [circle.CenterX, circle.CenterY, circle.RadiusMm, circle.StrokeWidthMm],
                TextLabel label => [label.X, label.Y, label.FontSizeMm],
                _ => throw new NotSupportedException(primitive.GetType().Name),
            };
            Assert.All(coordinates, coordinate => Assert.True(double.IsFinite(coordinate)));
        }

        var baselines = graphic.Primitives.OfType<LineSeg>()
            .Where(line => line.StrokeWidthMm == (lowInk ? 0.6 * 0.6 : 0.6)).ToArray();
        Assert.Equal(2, baselines.Length);
        var horizontal = Assert.Single(baselines, line => line.Y1 == line.Y2);
        var vertical = Assert.Single(baselines, line => line.X1 == line.X2);
        Assert.Equal(100, horizontal.X2 - horizontal.X1, 9);
        Assert.Equal(100, vertical.Y2 - vertical.Y1, 9);
        var hTicks = graphic.Primitives.OfType<LineSeg>()
            .Where(line => line.X1 == line.X2 && line.Y1 == horizontal.Y1 && line.Y2 < line.Y1).ToArray();
        var vTicks = graphic.Primitives.OfType<LineSeg>()
            .Where(line => line.Y1 == line.Y2 && line.X1 == vertical.X1 && line.X2 > line.X1
                && line.Y1 >= vertical.Y1 && line.Y1 <= vertical.Y2).ToArray();
        Assert.Equal(101, hTicks.Length);
        Assert.Equal(101, vTicks.Length);
        for (var tick = 0; tick <= 100; tick++)
        {
            var length = tick % 10 == 0 ? 6 : tick % 5 == 0 ? 4.5 : 3;
            Assert.Equal(tick, hTicks[tick].X1 - horizontal.X1, 9);
            Assert.Equal(tick, vTicks[tick].Y1 - vertical.Y1, 9);
            Assert.Equal(length, hTicks[tick].Y1 - hTicks[tick].Y2, 9);
            Assert.Equal(length, vTicks[tick].X2 - vTicks[tick].X1, 9);
            var stroke = tick % 10 == 0 ? 0.5 : 0.3;
            Assert.Equal(lowInk ? Math.Max(0.2, stroke * 0.6) : stroke, hTicks[tick].StrokeWidthMm);
            Assert.Equal(lowInk ? Math.Max(0.2, stroke * 0.6) : stroke, vTicks[tick].StrokeWidthMm);
        }

        var rings = graphic.Primitives.OfType<CircleShape>().ToArray();
        Assert.Equal(3, rings.Length);
        Assert.Equal(graphic.WidthMm, rings[0].CenterX + rings[2].CenterX, 9);
        Assert.Equal(graphic.WidthMm / 2, rings[1].CenterX);
        Assert.All(rings, ring =>
        {
            Assert.Equal(6, ring.RadiusMm);
            Assert.Equal(rings[0].CenterY, ring.CenterY);
        });
        var patches = graphic.Primitives.OfType<RectShape>().Skip(1).ToArray();
        Assert.Equal(6, patches.Length);
        Assert.All(patches, patch =>
        {
            Assert.Equal(22, patch.WidthMm);
            Assert.Equal(14, patch.HeightMm);
        });
        Assert.False(patches[0].Filled);
        Assert.True(patches[^1].Filled);
        Assert.Single(patches, patch => patch.Filled);
    }

    private static void AssertNativeCourierNominalTextBounds(VectorGraphic graphic, double margin)
    {
        foreach (var label in graphic.Primitives.OfType<TextLabel>())
        {
            // VectorPdfWriter's current WinAnsi Courier advance is 0.6 em.
            // This is not a browser font measurement or a glyph-ink claim.
            var width = label.Text.Length * label.FontSizeMm * 0.6;
            var left = label.X - (label.Anchor == TextAnchor.Middle ? width / 2 : label.Anchor == TextAnchor.End ? width : 0);
            Assert.True(double.IsFinite(left));
            Assert.InRange(left, margin, graphic.WidthMm - margin);
            Assert.InRange(left + width, margin, graphic.WidthMm - margin);
            Assert.InRange(label.Y - label.FontSizeMm, margin, graphic.HeightMm - margin);
            Assert.InRange(label.Y + label.FontSizeMm * 0.25, margin, graphic.HeightMm - margin);
        }
    }

    private static ApprovedArtifact Approve(ArtifactDocument document)
        => ApprovalGate.Approve(DraftArtifact.New(document, DataLane.Green),
            "synthetic-compatibility@example.invalid", DocumentValidator.Validate(document),
            new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero));

    private static VectorGraphic Graphic(ArtifactDocument document)
        => Assert.IsType<VectorGraphic>(Assert.Single(document.Nodes));

    private static VectorPrimitive Translate(VectorPrimitive primitive, double dy) => primitive switch
    {
        LineSeg line => line with { Y1 = line.Y1 + dy, Y2 = line.Y2 + dy },
        RectShape rectangle => rectangle with { Y = rectangle.Y + dy },
        CircleShape circle => circle with { CenterY = circle.CenterY + dy },
        TextLabel label => label with { Y = label.Y + dy },
        _ => throw new NotSupportedException(primitive.GetType().Name),
    };

    private static decimal Number(double value)
        => decimal.Parse(value.ToString("0.###", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private static decimal Attribute(XElement element, string name)
        => decimal.Parse(element.Attribute(name)!.Value, CultureInfo.InvariantCulture);
}
