// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

public enum PageSize
{
    Letter,
    A4,
    LetterLandscape,
    A4Landscape,
}

public enum GridQuadrants
{
    Four,
    First,
}

/// <summary>
/// The Blankforms Press (spec §5.1): parameterized print-perfect classics. Inputs
/// are parameters, never prose; geometry is exact in millimeters; identical
/// parameters produce identical documents.
/// </summary>
public static partial class BlankformsPress
{
    public const double DefaultMarginMm = 12;

    public static (double WidthMm, double HeightMm) Dimensions(PageSize size) => size switch
    {
        PageSize.A4 => (210, 297),
        PageSize.LetterLandscape => (279.4, 215.9),
        PageSize.A4Landscape => (297, 210),
        _ => (215.9, 279.4),
    };

    public static ArtifactDocument GraphPaper(PageSize size = PageSize.Letter, double pitchMm = 5, double marginMm = DefaultMarginMm, int majorEvery = 5)
    {
        if (pitchMm <= 0)
        {
            throw new ArgumentException("Grid pitch must be positive.", nameof(pitchMm));
        }

        var (width, height) = Dimensions(size);
        var primitives = new List<VectorPrimitive>();

        var right = width - marginMm;
        var bottom = height - marginMm;

        var index = 0;
        for (var x = marginMm; x <= right + 0.0001; x += pitchMm, index++)
        {
            primitives.Add(new LineSeg(x, marginMm, x, bottom, MajorOrMinor(index, majorEvery)));
        }

        index = 0;
        for (var y = marginMm; y <= bottom + 0.0001; y += pitchMm, index++)
        {
            primitives.Add(new LineSeg(marginMm, y, right, y, MajorOrMinor(index, majorEvery)));
        }

        return Wrap(new VectorGraphic(width, height, primitives,
            $"Graph paper, {Fmt(pitchMm)} millimeter squares"));
    }

    /// <summary>Spec §5.1 "one or four quadrants": the first-quadrant variant puts the origin at the lower left, axes on the grid's own edges.</summary>
    public static ArtifactDocument CoordinateGrid(PageSize size = PageSize.Letter, double pitchMm = 10, double marginMm = DefaultMarginMm, GridQuadrants quadrants = GridQuadrants.Four)
    {
        var document = GraphPaper(size, pitchMm, marginMm, majorEvery: 0);
        var graphic = (VectorGraphic)document.Nodes[0];

        var right = graphic.WidthMm - marginMm;
        var bottom = graphic.HeightMm - marginMm;
        var primitives = new List<VectorPrimitive>(graphic.Primitives);

        if (quadrants == GridQuadrants.First)
        {
            primitives.Add(new LineSeg(marginMm, bottom, right, bottom, 0.7));
            primitives.Add(new LineSeg(marginMm, marginMm, marginMm, bottom, 0.7));
        }
        else
        {
            var centerX = graphic.WidthMm / 2;
            var centerY = graphic.HeightMm / 2;
            primitives.Add(new LineSeg(marginMm, centerY, right, centerY, 0.7));
            primitives.Add(new LineSeg(centerX, marginMm, centerX, bottom, 0.7));
        }

        return Wrap(graphic with
        {
            Primitives = primitives,
            Description = quadrants == GridQuadrants.First
                ? $"First-quadrant coordinate grid, {Fmt(pitchMm)} millimeter squares, origin at the lower left"
                : $"Four-quadrant coordinate grid, {Fmt(pitchMm)} millimeter squares",
        });
    }

    /// <summary>RC-15: subdivisions place shorter minor ticks between integers — halves, quarters, tenths — with only the integers labeled.</summary>
    public static ArtifactDocument NumberLine(int from = 0, int to = 20, PageSize size = PageSize.Letter, double marginMm = DefaultMarginMm, int subdivisions = 1)
    {
        if (to <= from)
        {
            throw new ArgumentException("A number line runs left to right; 'to' must exceed 'from'.", nameof(to));
        }

        if (subdivisions is < 1 or > 10)
        {
            throw new ArgumentException("Between one and ten subdivisions per unit.", nameof(subdivisions));
        }

        var (width, height) = Dimensions(size);
        var y = height / 2;
        var right = width - marginMm;
        var span = right - marginMm;
        var count = to - from;

        var primitives = new List<VectorPrimitive> { new LineSeg(marginMm, y, right, y, 0.7) };

        for (var i = 0; i <= count; i++)
        {
            var x = marginMm + span * i / count;
            primitives.Add(new LineSeg(x, y - 4, x, y + 4, 0.5));
            primitives.Add(new TextLabel(x, y + 12, (from + i).ToString(System.Globalization.CultureInfo.InvariantCulture), 5));

            if (i < count)
            {
                for (var s = 1; s < subdivisions; s++)
                {
                    var minorX = x + span / count * s / subdivisions;
                    primitives.Add(new LineSeg(minorX, y - 2.5, minorX, y + 2.5, 0.35));
                }
            }
        }

        return Wrap(new VectorGraphic(width, height, primitives,
            subdivisions == 1
                ? $"Number line from {from} to {to}"
                : $"Number line from {from} to {to} with {subdivisions} subdivisions per unit"));
    }

    public static ArtifactDocument TenFrames(int frames = 2, double cellMm = 22, PageSize size = PageSize.Letter, double marginMm = DefaultMarginMm)
    {
        if (frames is < 1 or > 6)
        {
            throw new ArgumentException("Between one and six frames fit a page.", nameof(frames));
        }

        var (width, height) = Dimensions(size);
        var frameWidth = cellMm * 5;
        var frameHeight = cellMm * 2;
        var left = (width - frameWidth) / 2;
        var gap = (height - 2 * marginMm - frames * frameHeight) / Math.Max(1, frames + 1);

        var primitives = new List<VectorPrimitive>();
        for (var f = 0; f < frames; f++)
        {
            var top = marginMm + gap + f * (frameHeight + gap);
            for (var row = 0; row < 2; row++)
            {
                for (var col = 0; col < 5; col++)
                {
                    primitives.Add(new RectShape(left + col * cellMm, top + row * cellMm, cellMm, cellMm, 0.6));
                }
            }
        }

        return Wrap(new VectorGraphic(width, height, primitives,
            $"{frames} empty ten-frames, {Fmt(cellMm)} millimeter cells"));
    }

    public static ArtifactDocument ClockFace(double radiusMm = 70, bool numerals = true, bool minuteTicks = true, PageSize size = PageSize.Letter)
    {
        var (width, height) = Dimensions(size);
        var cx = width / 2;
        var cy = height / 2;

        var primitives = new List<VectorPrimitive>
        {
            new CircleShape(cx, cy, radiusMm, 1.0),
            new CircleShape(cx, cy, 1.5, 1.0, Filled: true),
        };

        if (minuteTicks)
        {
            // RC-15: the 48 non-hour minute marks, shorter and lighter than the hour marks.
            for (var minute = 0; minute < 60; minute++)
            {
                if (minute % 5 == 0)
                {
                    continue;
                }

                var minuteAngle = (minute * 6 - 90) * Math.PI / 180;
                primitives.Add(new LineSeg(
                    cx + (radiusMm - 3) * Math.Cos(minuteAngle),
                    cy + (radiusMm - 3) * Math.Sin(minuteAngle),
                    cx + radiusMm * Math.Cos(minuteAngle),
                    cy + radiusMm * Math.Sin(minuteAngle),
                    0.4));
            }
        }

        for (var hour = 1; hour <= 12; hour++)
        {
            var angle = (hour * 30 - 90) * Math.PI / 180;
            var outerX = cx + radiusMm * Math.Cos(angle);
            var outerY = cy + radiusMm * Math.Sin(angle);
            var innerX = cx + (radiusMm - 6) * Math.Cos(angle);
            var innerY = cy + (radiusMm - 6) * Math.Sin(angle);
            primitives.Add(new LineSeg(innerX, innerY, outerX, outerY, 0.8));

            if (numerals)
            {
                var textX = cx + (radiusMm - 14) * Math.Cos(angle);
                var textY = cy + (radiusMm - 14) * Math.Sin(angle) + 2.5;
                primitives.Add(new TextLabel(textX, textY, hour.ToString(System.Globalization.CultureInfo.InvariantCulture), 7));
            }
        }

        return Wrap(new VectorGraphic(width, height, primitives,
            numerals ? "Blank clock face with numerals and no hands" : "Blank clock face with hour marks and no hands"));
    }

    public static ArtifactDocument MusicStaves(int staves = 8, PageSize size = PageSize.Letter, double marginMm = DefaultMarginMm)
    {
        if (staves is < 1 or > 12)
        {
            throw new ArgumentException("Between one and twelve staves fit a page.", nameof(staves));
        }

        var (width, height) = Dimensions(size);
        const double lineSpacing = 2.0;
        var staffHeight = lineSpacing * 4;
        var gap = (height - 2 * marginMm - staves * staffHeight) / Math.Max(1, staves + 1);
        var right = width - marginMm;

        var primitives = new List<VectorPrimitive>();
        for (var s = 0; s < staves; s++)
        {
            var top = marginMm + gap + s * (staffHeight + gap);
            for (var line = 0; line < 5; line++)
            {
                var y = top + line * lineSpacing;
                primitives.Add(new LineSeg(marginMm, y, right, y, 0.3));
            }

            primitives.Add(new LineSeg(marginMm, top, marginMm, top + staffHeight, 0.5));
            primitives.Add(new LineSeg(right, top, right, top + staffHeight, 0.5));
        }

        return Wrap(new VectorGraphic(width, height, primitives, $"{staves} blank five-line music staves"));
    }

    private static double MajorOrMinor(int index, int majorEvery)
        => majorEvery > 0 && index % majorEvery == 0 ? 0.5 : 0.25;

    private static ArtifactDocument Wrap(VectorGraphic graphic) => new([graphic]);

    private static string Fmt(double value) => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
