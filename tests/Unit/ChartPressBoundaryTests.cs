// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

public sealed class ChartPressBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_zero_category_remains_visible_without_an_unapprovable_zero_area_bar(bool horizontal)
    {
        var document = BuildFromCatalog("Zero | 0\nNonzero | 1", horizontal);
        var issues = DocumentValidator.Validate(document);

        Assert.DoesNotContain(issues, issue => issue.Severity == ValidationSeverity.Blocking);
        var graphic = Assert.IsType<VectorGraphic>(Assert.Single(document.Nodes));
        var bar = Assert.Single(graphic.Primitives.OfType<RectShape>());
        Assert.True(bar.WidthMm > 0 && bar.HeightMm > 0);
        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), label => label.Text == "Zero");
        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), label => label.Text == "Nonzero");
        Assert.Equal(2, graphic.Primitives.OfType<TextLabel>().Count(label => label.Text == "0"));

        var approved = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green),
            "Synthetic chart reviewer",
            issues,
            new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero));
        Assert.Equal(DataLane.Green, approved.Revision.Lane);
    }

    [Theory]
    [InlineData(1_600_000_000, 200_000_000)]
    [InlineData(1_600_000_001, 500_000_000)]
    [InlineData(2_000_000_000, 500_000_000)]
    [InlineData(int.MaxValue - 1, 500_000_000)]
    [InlineData(int.MaxValue, 500_000_000)]
    public void Grid_step_covers_the_entire_admitted_nonnegative_integer_range(int maximum, int expectedStep)
        => Assert.Equal(expectedStep, ChartPress.GridStep(maximum));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_largest_admitted_value_has_a_positive_bounded_axis_and_proportional_bars(bool horizontal)
    {
        var document = BuildFromCatalog("Largest | 2147483647\nHalf range | 1073741823", horizontal);
        Assert.DoesNotContain(DocumentValidator.Validate(document),
            issue => issue.Severity == ValidationSeverity.Blocking);

        var graphic = Assert.IsType<VectorGraphic>(Assert.Single(document.Nodes));
        var bars = graphic.Primitives.OfType<RectShape>().ToArray();
        Assert.Equal(2, bars.Length);
        var grid = graphic.Primitives.OfType<LineSeg>()
            .Where(line => line.StrokeWidthMm == 0.25).ToArray();
        Assert.Equal(5, grid.Length);

        var largestLength = horizontal ? bars[0].WidthMm : bars[0].HeightMm;
        var smallerLength = horizontal ? bars[1].WidthMm : bars[1].HeightMm;
        Assert.Equal((double)int.MaxValue / 1_073_741_823, largestLength / smallerLength, 9);
        foreach (var numeral in new[] { "500000000", "1000000000", "1500000000", "2000000000", "2500000000" })
        {
            Assert.Contains(graphic.Primitives.OfType<TextLabel>(), label => label.Text == numeral);
        }

        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), label => label.Text == "2147483647");
        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), label => label.Text == "1073741823");
        Assert.All(bars, bar =>
        {
            Assert.InRange(bar.X, 0, graphic.WidthMm);
            Assert.InRange(bar.Y, 0, graphic.HeightMm);
            Assert.InRange(bar.X + bar.WidthMm, 0, graphic.WidthMm);
            Assert.InRange(bar.Y + bar.HeightMm, 0, graphic.HeightMm);
        });
    }

    private static ArtifactDocument BuildFromCatalog(string data, bool horizontal)
    {
        var definition = PressRoomCatalog.ById("bar-chart");
        var values = new Dictionary<string, string>(PressRoomCatalog.Defaults(definition), StringComparer.Ordinal)
        {
            ["data"] = data,
            ["orientation"] = horizontal ? "Across" : "Up",
        };
        return definition.Build(new PressInputs(values));
    }
}
