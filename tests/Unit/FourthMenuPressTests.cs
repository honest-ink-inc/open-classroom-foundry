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

// Bell-to-Bell's load-bearing invariant is cumulative clock arithmetic with
// a loud overrun refusal, asserted cell by cell against the catalog entry's
// own defaults — which meet the bell exactly.

public class BellToBellTests
{
    private static TableNode DefaultPlanTable(out ArtifactDocument document)
    {
        var definition = PressRoomCatalog.ById("bell-to-bell");
        document = definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition)));
        return document.Nodes.OfType<TableNode>().Single();
    }

    [Fact]
    public void Clock_times_accumulate_activity_by_activity_with_transitions_counted()
    {
        var table = DefaultPlanTable(out var document);

        // 8:30 + 5 + 1 = 8:36; + 15 + 1 = 8:52; + 20 + 1 = 9:13; + 8 + 1 = 9:22.
        Assert.Equal(["8:30", "5", "Warm-up", "1"], table.Rows[0]);
        Assert.Equal(["8:36", "15", "Mini-lesson", "1"], table.Rows[1]);
        Assert.Equal(["8:52", "20", "Guided practice", "1"], table.Rows[2]);
        Assert.Equal(["9:13", "8", "Share out", "1"], table.Rows[3]);

        // The closure holds the last three minutes before the 9:25 bell.
        Assert.Equal(["9:22", "3", "Pack up, reflect, and reset", ""], table.Rows[4]);

        Assert.Contains(document.Nodes.OfType<Paragraph>(),
            p => p.Text == "55 of 55 minutes planned; the bell at 9:25 is met exactly.");
    }

    [Fact]
    public void Open_minutes_are_named_and_the_closure_stays_protected_at_the_bell()
    {
        var definition = PressRoomCatalog.ById("bell-to-bell");
        var values = PressRoomCatalog.Defaults(definition);
        values["period"] = "60";
        var document = definition.Build(new PressInputs(values));

        // Five open minutes appear between the last transition and a closure
        // still anchored to the end of the period: 8:30 + 60 - 3 = 9:27.
        var table = document.Nodes.OfType<TableNode>().Single();
        Assert.Equal("9:27", table.Rows[4][0]);
        Assert.Contains(document.Nodes.OfType<Paragraph>(),
            p => p.Text == "55 of 60 minutes planned; 5 minute(s) open before the closure at 9:27.");
    }

    [Fact]
    public void An_overrunning_plan_is_refused_with_the_arithmetic_in_the_message()
    {
        var definition = PressRoomCatalog.ById("bell-to-bell");
        var values = PressRoomCatalog.Defaults(definition);
        values["period"] = "50";

        var refusal = Assert.Throws<ArgumentException>(() => definition.Build(new PressInputs(values)));
        Assert.Contains("needs 55 minutes but the period holds 50; trim 5 minute(s)", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_times_and_rows_are_refused_loudly_when_wrong()
    {
        Assert.Throws<ArgumentException>(() => BellToBell.ParseStartMinutes("8h30"));
        Assert.Throws<ArgumentException>(() => BellToBell.ParseStartMinutes("25:00"));
        Assert.Throws<ArgumentException>(() => BellToBell.ParseStartMinutes("8:75"));
        Assert.Equal(8 * 60 + 30, BellToBell.ParseStartMinutes("8:30"));
        Assert.Equal(13 * 60 + 5, BellToBell.ParseStartMinutes("13:05"));

        Assert.Throws<ArgumentException>(() => BellToBell.Parse([("5", null)]));
        Assert.Throws<ArgumentException>(() => BellToBell.Parse([("five", "Warm-up")]));
        Assert.Throws<ArgumentException>(() => BellToBell.Parse([("0", "Warm-up")]));

        var one = BellToBell.Parse([("5", "Warm-up")]);
        Assert.Throws<ArgumentException>(() => BellToBell.Plan("", "8:30", one, 55, 1, "Closure", 3));
        Assert.Throws<ArgumentException>(() => BellToBell.Plan("T", "8:30", one, 55, 1, "", 3));
        Assert.Throws<ArgumentException>(() => BellToBell.Plan("T", "8:30", [], 55, 1, "Closure", 3));
    }
}
