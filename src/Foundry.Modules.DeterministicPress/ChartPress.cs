// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// The Chart Press (the deterministic heart of atlas #88; fourth forge menu,
// item 2). Bars are proportionally TRUE: a value twice as large is a bar
// exactly twice as long, asserted as arithmetic. The numbers are the
// teacher's claim, drawn exactly as typed — never computed, corrected,
// aggregated, or interpreted.

/// <summary>One bar: the teacher's label and value, verbatim.</summary>
public sealed record ChartDatum(string Label, int Value);

public static class ChartPress
{
    private const int MaxGridlines = 8;

    /// <summary>Parses teacher lines of "label | value" — the values are the teacher's claim, drawn, never checked against anything.</summary>
    public static IReadOnlyList<ChartDatum> Parse(IReadOnlyList<(string Left, string? Right)> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var data = new List<ChartDatum>();
        foreach (var (left, right) in lines)
        {
            if (string.IsNullOrWhiteSpace(left))
            {
                throw new ArgumentException("A bar has no label; write label | value.", nameof(lines));
            }

            if (string.IsNullOrWhiteSpace(right))
            {
                throw new ArgumentException($"'{left}' has no value; write label | value.", nameof(lines));
            }

            if (!int.TryParse(right, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                throw new ArgumentException($"'{right}' is not a whole number for '{left}'.", nameof(lines));
            }

            if (value < 0)
            {
                throw new ArgumentException($"'{left}' is negative; bars run from zero.", nameof(lines));
            }

            data.Add(new ChartDatum(left, value));
        }

        return data;
    }

    /// <summary>The smallest clean gridline step (1, 2, or 5 times a power of ten) that needs at most eight gridlines to cover the largest value.</summary>
    public static int GridStep(int maxValue)
    {
        if (maxValue <= 0)
        {
            throw new ArgumentException("The step is defined only above zero.", nameof(maxValue));
        }

        for (long magnitude = 1; ; magnitude *= 10)
        {
            foreach (var clean in new[] { 1, 2, 5 })
            {
                var step = clean * magnitude;
                if ((maxValue + step - 1) / step <= MaxGridlines)
                {
                    return checked((int)step);
                }
            }
        }
    }

    public static ArtifactDocument Sheet(
        string title,
        IReadOnlyList<ChartDatum> data,
        bool horizontal = false,
        PageSize size = PageSize.LetterLandscape,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("The chart needs a title.", nameof(title));
        }

        if (data.Count is < 2 or > 12)
        {
            throw new ArgumentException("Between two and twelve bars.", nameof(data));
        }

        if (data.Any(d => d.Value < 0))
        {
            throw new ArgumentException("Every value must be zero or more; bars run from zero.", nameof(data));
        }

        var max = data.Max(d => d.Value);
        if (max == 0)
        {
            throw new ArgumentException("At least one value must be above zero; an all-zero chart draws nothing.", nameof(data));
        }

        var step = GridStep(max);
        // The parser admits the complete nonnegative Int32 range. Ceiling
        // arithmetic and the next clean axis limit must therefore be wider:
        // Int32.MaxValue uses a 500,000,000 step and a 2,500,000,000 axis.
        var axisMax = (max + (long)step - 1) / step * step;

        var (width, height) = BlankformsPress.Dimensions(size);
        var primitives = new List<VectorPrimitive>
        {
            new TextLabel(width / 2, marginMm + 4, title, 5),
        };

        // The plot rectangle leaves room for numerals on the value axis and
        // the teacher's labels on the category axis.
        var top = marginMm + 12;
        var bottom = height - marginMm - 12;
        var left = marginMm + (horizontal ? 45 : 14);
        var right = width - marginMm - 4;
        var plotWidth = right - left;
        var plotHeight = bottom - top;

        primitives.Add(new LineSeg(left, bottom, right, bottom, 0.7)); // category or value baseline
        primitives.Add(new LineSeg(left, top, left, bottom, 0.7));

        var slotSpan = horizontal ? plotHeight : plotWidth;
        var barSpan = horizontal ? plotWidth : plotHeight;
        var slot = slotSpan / data.Count;
        var barThickness = slot * 0.6;

        for (var g = (long)step; g <= axisMax; g += step)
        {
            var numeral = g.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var along = barSpan * g / axisMax;
            if (horizontal)
            {
                primitives.Add(new LineSeg(left + along, top, left + along, bottom, 0.25));
                primitives.Add(new TextLabel(left + along, bottom + 6, numeral, 4));
            }
            else
            {
                primitives.Add(new LineSeg(left, bottom - along, right, bottom - along, 0.25));
                primitives.Add(new TextLabel(left - 2, bottom - along + 1.5, numeral, 4, TextAnchor.End));
            }
        }

        primitives.Add(horizontal
            ? new TextLabel(left, bottom + 6, "0", 4)
            : new TextLabel(left - 2, bottom + 1.5, "0", 4, TextAnchor.End));

        for (var i = 0; i < data.Count; i++)
        {
            var entry = data[i];
            var length = barSpan * entry.Value / axisMax; // THE invariant: length is linear in the value
            var acrossBar = (horizontal ? top : left) + slot * i + slot * 0.2;
            var value = entry.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (horizontal)
            {
                if (entry.Value > 0)
                {
                    primitives.Add(new RectShape(left, acrossBar, length, barThickness, 0.6));
                }
                primitives.Add(new TextLabel(left + length + 2, acrossBar + barThickness / 2 + 1.5, value, 4, TextAnchor.Start));
                primitives.Add(new TextLabel(left - 2, acrossBar + barThickness / 2 + 1.5, entry.Label, 4, TextAnchor.End));
            }
            else
            {
                if (entry.Value > 0)
                {
                    primitives.Add(new RectShape(acrossBar, bottom - length, barThickness, length, 0.6));
                }
                primitives.Add(new TextLabel(acrossBar + barThickness / 2, bottom - length - 2, value, 4));
                primitives.Add(new TextLabel(acrossBar + barThickness / 2, bottom + 6, entry.Label, 4));
            }
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A proportionally true bar chart titled {title}: {data.Count} teacher-valued bars running {(horizontal ? "across" : "up")}, gridlines every {step}")]);
    }
}
