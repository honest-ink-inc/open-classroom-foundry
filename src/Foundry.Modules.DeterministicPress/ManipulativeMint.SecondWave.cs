// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// The Manipulative Mint second wave (handover 2026-08-29, forge item 2): fraction
// circles, spinner faces, box nets. Proportions are exact arithmetic (spec §5.4).

public static partial class ManipulativeMint
{
    /// <summary>Like the strips, a whole circle leads; unlabeled circles keep the naming work for the learner (RC-14).</summary>
    public static ArtifactDocument FractionCircles(IReadOnlyList<int> denominators, double radiusMm = 30, PageSize size = PageSize.Letter, double marginMm = BlankformsPress.DefaultMarginMm, bool labeled = true)
    {
        ArgumentNullException.ThrowIfNull(denominators);
        if (denominators.Count == 0 || denominators.Any(d => d is < 2 or > 16))
        {
            throw new ArgumentException("Denominators between 2 and 16.", nameof(denominators));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var cellWidth = (width - 2 * marginMm) / 2;
        var cellHeight = 2 * radiusMm + 12;
        if (2 * radiusMm > cellWidth)
        {
            throw new ArgumentException("Two circles per row must fit inside the margins.", nameof(radiusMm));
        }

        var circles = new List<int> { 1 };
        circles.AddRange(denominators);
        var rows = (circles.Count + 1) / 2;
        if (marginMm + rows * cellHeight > height - marginMm)
        {
            throw new ArgumentException("Too many circles for one page at this radius.", nameof(denominators));
        }

        var primitives = new List<VectorPrimitive>();
        for (var i = 0; i < circles.Count; i++)
        {
            var cx = marginMm + i % 2 * cellWidth + cellWidth / 2;
            var cy = marginMm + i / 2 * cellHeight + cellHeight / 2;
            var denominator = circles[i];

            primitives.Add(new CircleShape(cx, cy, radiusMm, 0.5));

            if (denominator == 1)
            {
                if (labeled)
                {
                    primitives.Add(new TextLabel(cx, cy + 2, "1", 6));
                }

                continue;
            }

            for (var k = 0; k < denominator; k++)
            {
                var angle = 2 * Math.PI * k / denominator - Math.PI / 2;
                primitives.Add(new LineSeg(cx, cy, cx + radiusMm * Math.Cos(angle), cy + radiusMm * Math.Sin(angle), 0.4));

                if (labeled)
                {
                    var mid = 2 * Math.PI * (k + 0.5) / denominator - Math.PI / 2;
                    primitives.Add(new TextLabel(
                        cx + 0.6 * radiusMm * Math.Cos(mid),
                        cy + 0.6 * radiusMm * Math.Sin(mid) + 1.5,
                        $"1/{denominator}",
                        4));
                }
            }
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"Fraction circles{(labeled ? "" : " (unlabeled)")}: one whole and {string.Join(", ", denominators.Select(d => $"1/{d}"))}")]);
    }

    /// <summary>Sectors are exact arcs of the circle; the spinner is a paperclip held at the center by a pencil point — no moving parts to print.</summary>
    public static ArtifactDocument SpinnerFace(int sectors, IReadOnlyList<string>? sectorLabels = null, double radiusMm = 45, PageSize size = PageSize.Letter)
    {
        if (sectors is < 2 or > 12)
        {
            throw new ArgumentException("Between two and twelve sectors.", nameof(sectors));
        }

        if (sectorLabels is not null && (sectorLabels.Count != sectors || sectorLabels.Any(string.IsNullOrWhiteSpace)))
        {
            throw new ArgumentException("One non-blank label per sector, or none.", nameof(sectorLabels));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var cx = width / 2;
        var cy = height / 2;

        var primitives = new List<VectorPrimitive>
        {
            new CircleShape(cx, cy, radiusMm, 0.7),
            new CircleShape(cx, cy, 2, 0.5, Filled: true),
        };

        for (var k = 0; k < sectors; k++)
        {
            var angle = 2 * Math.PI * k / sectors - Math.PI / 2;
            primitives.Add(new LineSeg(cx, cy, cx + radiusMm * Math.Cos(angle), cy + radiusMm * Math.Sin(angle), 0.5));

            if (sectorLabels is not null)
            {
                var mid = 2 * Math.PI * (k + 0.5) / sectors - Math.PI / 2;
                primitives.Add(new TextLabel(
                    cx + 0.65 * radiusMm * Math.Cos(mid),
                    cy + 0.65 * radiusMm * Math.Sin(mid) + 2,
                    sectorLabels[k],
                    6));
            }
        }

        primitives.Add(new TextLabel(cx, cy + radiusMm + 10, "Spin a paperclip held at the center dot by a pencil point.", 4));

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A {sectors}-sector spinner face with equal sectors{(sectorLabels is null ? "" : " and teacher labels")}")]);
    }

    /// <summary>A cuboid cross net in the foldables' line language: solid cuts, dashed folds, legend at the foot.</summary>
    public static ArtifactDocument BoxNet(double lengthMm = 55, double depthMm = 35, double heightMm = 30, PageSize size = PageSize.Letter, double marginMm = BlankformsPress.DefaultMarginMm)
    {
        if (lengthMm <= 0 || depthMm <= 0 || heightMm <= 0)
        {
            throw new ArgumentException("Box edges must be positive.", nameof(lengthMm));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        var netWidth = 2 * (lengthMm + depthMm);
        var netHeight = 2 * depthMm + heightMm;
        if (netWidth > width - 2 * marginMm || netHeight > height - 2 * marginMm)
        {
            throw new ArgumentException("The unfolded net must fit inside the margins.", nameof(lengthMm));
        }

        var x0 = (width - netWidth) / 2;
        var y0 = (height - netHeight) / 2;
        var midY = y0 + depthMm;

        var primitives = new List<VectorPrimitive>
        {
            // Middle row: side, front, side, back. Top and bottom flaps join the front.
            new RectShape(x0, midY, depthMm, heightMm, 0.5),
            new RectShape(x0 + depthMm, midY, lengthMm, heightMm, 0.5),
            new RectShape(x0 + depthMm + lengthMm, midY, depthMm, heightMm, 0.5),
            new RectShape(x0 + 2 * depthMm + lengthMm, midY, lengthMm, heightMm, 0.5),
            new RectShape(x0 + depthMm, y0, lengthMm, depthMm, 0.5),
            new RectShape(x0 + depthMm, midY + heightMm, lengthMm, depthMm, 0.5),

            // Shared edges are folds, not cuts.
            new LineSeg(x0 + depthMm, midY, x0 + depthMm, midY + heightMm, 0.35, Dashed: true),
            new LineSeg(x0 + depthMm + lengthMm, midY, x0 + depthMm + lengthMm, midY + heightMm, 0.35, Dashed: true),
            new LineSeg(x0 + 2 * depthMm + lengthMm, midY, x0 + 2 * depthMm + lengthMm, midY + heightMm, 0.35, Dashed: true),
            new LineSeg(x0 + depthMm, midY, x0 + depthMm + lengthMm, midY, 0.35, Dashed: true),
            new LineSeg(x0 + depthMm, midY + heightMm, x0 + depthMm + lengthMm, midY + heightMm, 0.35, Dashed: true),
        };

        // Legend: the line language, printed on every sheet (spec §5.5).
        var legendY = height - marginMm / 2;
        primitives.Add(new LineSeg(marginMm, legendY, marginMm + 14, legendY, 0.5));
        primitives.Add(new TextLabel(marginMm + 17, legendY + 1.5, "cut", 4, TextAnchor.Start));
        primitives.Add(new LineSeg(marginMm + 34, legendY, marginMm + 48, legendY, 0.5, Dashed: true));
        primitives.Add(new TextLabel(marginMm + 51, legendY + 1.5, "fold", 4, TextAnchor.Start));

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A cut-and-fold box net for a {Fmt2(lengthMm)} by {Fmt2(depthMm)} by {Fmt2(heightMm)} millimeter cuboid; solid lines cut, dashed lines fold")]);
    }

    private static string Fmt2(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
