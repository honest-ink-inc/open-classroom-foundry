// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

/// <summary>
/// Calibration &amp; Proof Press: one page that tests the printer before any
/// artifact takes the blame (spec §5.1 invariant: "a printed calibration rule on
/// request"). Two 100 mm rulers confess driver scaling; the outer frame probes the
/// declared margin; ring targets prove duplex registration by holding two flipped
/// prints to the light; a six-step density ramp exposes banding and toner trouble.
/// </summary>
public static class CalibrationPress
{
    public const double RulerLengthMm = 100;

    public static ArtifactDocument ProofPage(PageSize size = PageSize.Letter, double marginMm = BlankformsPress.DefaultMarginMm)
    {
        if (marginMm is < 5 or > 25)
        {
            throw new ArgumentException("A margin between 5 and 25 millimeters keeps every instrument on the page.", nameof(marginMm));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var primitives = new List<VectorPrimitive>
        {
            // The margin probe: if any edge of this frame is missing, the printer
            // cannot reach the declared margin.
            new RectShape(marginMm, marginMm, width - 2 * marginMm, height - 2 * marginMm, 0.4),
            new TextLabel(width / 2, marginMm + 4.5, $"frame edge = {Fmt(marginMm)} mm margin", 3),
            new TextLabel(width / 2, marginMm + 12, "Printer calibration and proof sheet", 7),
        };

        var left = marginMm + 10;
        string[] instructions =
        [
            "1. Print this page at 100 percent scale - never \"fit to page\".",
            "2. Both rulers must measure exactly 100 mm against a trusted ruler.",
            $"3. Every edge of the outer frame must be visible; it sits at the {Fmt(marginMm)} mm margin.",
            "4. Duplex: print two copies, flip on the long edge, hold to light - the ring targets must coincide.",
            "5. The density ramp must darken evenly left to right; jumps or banding are driver or toner trouble.",
        ];
        for (var i = 0; i < instructions.Length; i++)
        {
            primitives.Add(new TextLabel(left, marginMm + 19 + i * 5.5, instructions[i], 3.5, TextAnchor.Start));
        }

        AddHorizontalRuler(primitives, left, 70);
        AddVerticalRuler(primitives, left, 90);
        AddDuplexTargets(primitives, width, 118);
        AddDensityRamp(primitives, left, 205);

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"Printer calibration and proof sheet: two 100 mm rulers, a margin frame at {Fmt(marginMm)} mm, duplex ring targets, and a six-step ink-density ramp")]);
    }

    private static void AddHorizontalRuler(List<VectorPrimitive> primitives, double x0, double y)
    {
        primitives.Add(new LineSeg(x0, y, x0 + RulerLengthMm, y, 0.6));
        for (var mm = 0; mm <= RulerLengthMm; mm++)
        {
            var (length, weight) = TickShape(mm);
            primitives.Add(new LineSeg(x0 + mm, y, x0 + mm, y - length, weight));
            if (mm % 10 == 0)
            {
                primitives.Add(new TextLabel(x0 + mm, y + 6, (mm / 10).ToString(System.Globalization.CultureInfo.InvariantCulture), 3.5));
            }
        }
    }

    private static void AddVerticalRuler(List<VectorPrimitive> primitives, double x, double y0)
    {
        primitives.Add(new LineSeg(x, y0, x, y0 + RulerLengthMm, 0.6));
        for (var mm = 0; mm <= RulerLengthMm; mm++)
        {
            var (length, weight) = TickShape(mm);
            primitives.Add(new LineSeg(x, y0 + mm, x + length, y0 + mm, weight));
            if (mm % 10 == 0)
            {
                primitives.Add(new TextLabel(x + 9, y0 + mm + 1.5, (mm / 10).ToString(System.Globalization.CultureInfo.InvariantCulture), 3.5, TextAnchor.Start));
            }
        }
    }

    private static (double Length, double Weight) TickShape(int mm) => mm switch
    {
        _ when mm % 10 == 0 => (6, 0.5),
        _ when mm % 5 == 0 => (4.5, 0.3),
        _ => (3, 0.3),
    };

    private static void AddDuplexTargets(List<VectorPrimitive> primitives, double pageWidth, double y)
    {
        // A long-edge duplex flip mirrors x about the page center: the center ring
        // lands on itself and the outer pair land on each other.
        foreach (var x in new[] { pageWidth / 2 - 45, pageWidth / 2, pageWidth / 2 + 45 })
        {
            primitives.Add(new CircleShape(x, y, 6, 0.5));
            primitives.Add(new LineSeg(x - 9, y, x + 9, y, 0.35));
            primitives.Add(new LineSeg(x, y - 9, x, y + 9, 0.35));
        }
    }

    private static void AddDensityRamp(List<VectorPrimitive> primitives, double x0, double y)
    {
        const double stepWidth = 22;
        const double stepHeight = 14;
        double?[] hatchSpacings = [null, 3.5, 2, 1, 0.5, null];

        for (var step = 0; step < hatchSpacings.Length; step++)
        {
            var x = x0 + step * stepWidth;
            var solid = step == hatchSpacings.Length - 1;
            primitives.Add(new RectShape(x, y, stepWidth, stepHeight, 0.4, Filled: solid));

            if (hatchSpacings[step] is { } spacing)
            {
                for (var offset = spacing; offset < stepHeight; offset += spacing)
                {
                    primitives.Add(new LineSeg(x, y + offset, x + stepWidth, y + offset, 0.25));
                }
            }

            primitives.Add(new TextLabel(x + stepWidth / 2, y + stepHeight + 6, ((char)('A' + step)).ToString(), 3.5));
        }
    }

    private static string Fmt(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
