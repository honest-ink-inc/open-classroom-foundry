using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

public enum PageSize
{
    Letter,
    A4,
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

    public static ArtifactDocument CoordinateGrid(PageSize size = PageSize.Letter, double pitchMm = 10, double marginMm = DefaultMarginMm)
    {
        var document = GraphPaper(size, pitchMm, marginMm, majorEvery: 0);
        var graphic = (VectorGraphic)document.Nodes[0];

        var centerX = graphic.WidthMm / 2;
        var centerY = graphic.HeightMm / 2;
        var primitives = new List<VectorPrimitive>(graphic.Primitives)
        {
            new LineSeg(marginMm, centerY, graphic.WidthMm - marginMm, centerY, 0.7),
            new LineSeg(centerX, marginMm, centerX, graphic.HeightMm - marginMm, 0.7),
        };

        return Wrap(graphic with
        {
            Primitives = primitives,
            Description = $"Four-quadrant coordinate grid, {Fmt(pitchMm)} millimeter squares",
        });
    }

    public static ArtifactDocument NumberLine(int from = 0, int to = 20, PageSize size = PageSize.Letter, double marginMm = DefaultMarginMm)
    {
        if (to <= from)
        {
            throw new ArgumentException("A number line runs left to right; 'to' must exceed 'from'.", nameof(to));
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
        }

        return Wrap(new VectorGraphic(width, height, primitives, $"Number line from {from} to {to}"));
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

    public static ArtifactDocument ClockFace(double radiusMm = 70, bool numerals = true, PageSize size = PageSize.Letter)
    {
        var (width, height) = Dimensions(size);
        var cx = width / 2;
        var cy = height / 2;

        var primitives = new List<VectorPrimitive>
        {
            new CircleShape(cx, cy, radiusMm, 1.0),
            new CircleShape(cx, cy, 1.5, 1.0, Filled: true),
        };

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
