// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

// The fourth forge menu's presses. The Chart Press's load-bearing invariant is
// proportional truth: bar length is LINEAR in the teacher's value, asserted as
// arithmetic against the catalog entry's own defaults.

public class ChartPressTests
{
    [Fact]
    public void Bars_are_proportionally_true_to_the_value()
    {
        // The catalog defaults carry the invariant on their face: Sun 18 is
        // exactly twice Shade 9; Window 12 is exactly two-thirds of Sun.
        var definition = PressRoomCatalog.ById("bar-chart");
        var graphic = (VectorGraphic)definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition))).Nodes[0];

        var bars = graphic.Primitives.OfType<RectShape>().Where(r => r.StrokeWidthMm == 0.6).ToList();
        Assert.Equal(3, bars.Count);
        Assert.Equal(bars[1].HeightMm * 2, bars[0].HeightMm, 9);
        Assert.Equal(bars[0].HeightMm * 2 / 3, bars[2].HeightMm, 9);

        // Every bar stands on the same baseline; nothing floats.
        Assert.All(bars, b => Assert.Equal(bars[0].Y + bars[0].HeightMm, b.Y + b.HeightMm, 9));

        // Values and labels ride in ink, verbatim.
        var labels = graphic.Primitives.OfType<TextLabel>().ToList();
        foreach (var text in new[] { "Sun", "Shade", "Window", "18", "9", "12" })
        {
            Assert.Contains(labels, l => l.Text == text);
        }
    }

    [Fact]
    public void Gridlines_fall_at_clean_intervals_evenly_spaced()
    {
        // Max 18 → the smallest clean step needing at most eight lines is 5,
        // so the axis tops out at 20 with gridlines at 5, 10, 15, 20.
        var definition = PressRoomCatalog.ById("bar-chart");
        var graphic = (VectorGraphic)definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition))).Nodes[0];

        var grid = graphic.Primitives.OfType<LineSeg>().Where(l => l.StrokeWidthMm == 0.25).OrderByDescending(l => l.Y1).ToList();
        Assert.Equal(4, grid.Count);
        var spacing = grid[0].Y1 - grid[1].Y1;
        for (var i = 1; i < grid.Count - 1; i++)
        {
            Assert.Equal(spacing, grid[i].Y1 - grid[i + 1].Y1, 9);
        }

        var labels = graphic.Primitives.OfType<TextLabel>().ToList();
        foreach (var numeral in new[] { "0", "5", "10", "15", "20" })
        {
            Assert.Contains(labels, l => l.Text == numeral);
        }
    }

    [Fact]
    public void Horizontal_bars_mirror_the_same_arithmetic_and_meet_the_axis_exactly()
    {
        var graphic = (VectorGraphic)ChartPress.Sheet(
            "Test", ChartPress.Parse([("A", "4"), ("B", "8")]), horizontal: true).Nodes[0];

        var bars = graphic.Primitives.OfType<RectShape>().Where(r => r.StrokeWidthMm == 0.6).ToList();
        Assert.Equal(2, bars.Count);
        Assert.Equal(bars[0].WidthMm * 2, bars[1].WidthMm, 9);
        Assert.Equal(bars[0].X, bars[1].X, 9); // both grow from the zero line

        // Max 8 at step 1: the longest bar ends exactly on the last gridline —
        // the bars and the axis tell one arithmetic.
        var lastGrid = graphic.Primitives.OfType<LineSeg>().Where(l => l.StrokeWidthMm == 0.25).Max(l => l.X1);
        Assert.Equal(lastGrid, bars[1].X + bars[1].WidthMm, 9);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(8, 1)]
    [InlineData(9, 2)]
    [InlineData(16, 2)]
    [InlineData(17, 5)]
    [InlineData(40, 5)]
    [InlineData(41, 10)]
    [InlineData(1000000, 200000)]
    public void The_gridline_step_is_the_smallest_clean_interval(int max, int expected)
        => Assert.Equal(expected, ChartPress.GridStep(max));

    [Fact]
    public void Parsing_and_validation_refuse_loudly_when_wrong()
    {
        Assert.Throws<ArgumentException>(() => ChartPress.Parse([("A", null)]));
        Assert.Throws<ArgumentException>(() => ChartPress.Parse([("A", "four")]));
        Assert.Throws<ArgumentException>(() => ChartPress.Parse([("A", "-3")]));
        Assert.Throws<ArgumentException>(() => ChartPress.Parse([("", "5")]));

        var pair = ChartPress.Parse([("A", "1"), ("B", "2")]);
        Assert.Throws<ArgumentException>(() => ChartPress.Sheet("", pair));
        Assert.Throws<ArgumentException>(() => ChartPress.Sheet("T", [new ChartDatum("A", 1)]));
        Assert.Throws<ArgumentException>(() => ChartPress.Sheet("T",
            [.. Enumerable.Range(1, 13).Select(i => new ChartDatum($"B{i}", i))]));
        Assert.Throws<ArgumentException>(() => ChartPress.Sheet("T",
            [new ChartDatum("A", 0), new ChartDatum("B", 0)]));
    }
}
