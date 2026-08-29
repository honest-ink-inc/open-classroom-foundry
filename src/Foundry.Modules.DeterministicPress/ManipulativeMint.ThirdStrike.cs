// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// The Manipulative Mint's third strike (spec §5.4's named remainder; second
// forge menu, item 7): algebra tiles, base-ten blocks, tangrams. Proportions
// are exact arithmetic — that is the press invariant above all.

public static partial class ManipulativeMint
{
    /// <summary>
    /// Algebra tiles: unit squares, x-rods, and x² squares. The x length is
    /// deliberately NOT a whole number of units — if it were, learners could
    /// count instead of reason, and the algebra would leak out of the tiles.
    /// </summary>
    public static ArtifactDocument AlgebraTiles(
        double unitMm = 12,
        double xMm = 45,
        int xSquaredCount = 2,
        int xCount = 6,
        int unitCount = 10,
        bool labeled = true,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        if (unitMm is < 8 or > 25 || xMm is < 20 or > 80)
        {
            throw new ArgumentException("Units between 8 and 25 mm; x between 20 and 80 mm.", nameof(unitMm));
        }

        if (Math.Abs(xMm / unitMm - Math.Round(xMm / unitMm)) < 1e-9)
        {
            throw new ArgumentException("x must not be a whole number of units — that is the algebra.", nameof(xMm));
        }

        if (xSquaredCount is < 0 or > 4 || xCount is < 0 or > 12 || unitCount is < 0 or > 30
            || xSquaredCount + xCount + unitCount == 0)
        {
            throw new ArgumentException("At most four x-squared, twelve x, and thirty unit tiles; at least one tile.", nameof(unitCount));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        const double gap = 4;
        var primitives = new List<VectorPrimitive>();
        var x = marginMm;
        var y = marginMm;
        var rowHeight = 0.0;

        void Place(double tileWidth, double tileHeight, string? label, double fontSize)
        {
            if (x + tileWidth > width - marginMm)
            {
                x = marginMm;
                y += rowHeight + gap;
                rowHeight = 0;
            }

            if (y + tileHeight > height - marginMm)
            {
                throw new ArgumentException("Too many tiles for one page at these dimensions.", nameof(unitCount));
            }

            primitives.Add(new RectShape(x, y, tileWidth, tileHeight, 0.5));
            if (label is not null)
            {
                primitives.Add(new TextLabel(x + tileWidth / 2, y + tileHeight / 2 + fontSize / 3, label, fontSize));
            }

            x += tileWidth + gap;
            rowHeight = Math.Max(rowHeight, tileHeight);
        }

        for (var i = 0; i < xSquaredCount; i++)
        {
            Place(xMm, xMm, labeled ? "x²" : null, 7);
        }

        for (var i = 0; i < xCount; i++)
        {
            Place(xMm, unitMm, labeled ? "x" : null, 5);
        }

        for (var i = 0; i < unitCount; i++)
        {
            Place(unitMm, unitMm, labeled ? "1" : null, 4);
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"Algebra tiles{(labeled ? "" : " (unlabeled)")}: {xSquaredCount} x-squared, {xCount} x, and {unitCount} unit tiles; x is deliberately not a whole number of units")]);
    }

    /// <summary>Base-ten blocks at true proportions: a flat is exactly ten rods, a rod exactly ten units — asserted as arithmetic.</summary>
    public static ArtifactDocument BaseTenBlocks(
        double unitMm = 10,
        int flatCount = 1,
        int rodCount = 4,
        int unitCount = 12,
        PageSize size = PageSize.Letter,
        double marginMm = BlankformsPress.DefaultMarginMm)
    {
        if (unitMm is < 6 or > 15)
        {
            throw new ArgumentException("Units between 6 and 15 millimeters.", nameof(unitMm));
        }

        if (flatCount is < 0 or > 2 || rodCount is < 0 or > 10 || unitCount is < 0 or > 30
            || flatCount + rodCount + unitCount == 0)
        {
            throw new ArgumentException("At most two flats, ten rods, and thirty units; at least one block.", nameof(unitCount));
        }

        var (width, height) = BlankformsPress.Dimensions(size);
        const double gap = 5;
        var ten = 10 * unitMm;
        var primitives = new List<VectorPrimitive>();
        var y = marginMm;

        for (var f = 0; f < flatCount; f++)
        {
            Require(y + ten);
            primitives.Add(new RectShape(marginMm, y, ten, ten, 0.6));
            for (var line = 1; line < 10; line++)
            {
                primitives.Add(new LineSeg(marginMm + line * unitMm, y, marginMm + line * unitMm, y + ten, 0.3));
                primitives.Add(new LineSeg(marginMm, y + line * unitMm, marginMm + ten, y + line * unitMm, 0.3));
            }

            y += ten + gap;
        }

        for (var r = 0; r < rodCount; r++)
        {
            Require(y + unitMm);
            primitives.Add(new RectShape(marginMm, y, ten, unitMm, 0.6));
            for (var line = 1; line < 10; line++)
            {
                primitives.Add(new LineSeg(marginMm + line * unitMm, y, marginMm + line * unitMm, y + unitMm, 0.3));
            }

            y += unitMm + gap;
        }

        var ux = marginMm;
        var unitsTop = y;
        for (var u = 0; u < unitCount; u++)
        {
            if (ux + unitMm > width - marginMm)
            {
                ux = marginMm;
                unitsTop += unitMm + gap;
            }

            Require(unitsTop + unitMm);
            primitives.Add(new RectShape(ux, unitsTop, unitMm, unitMm, 0.6));
            ux += unitMm + gap;
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"Base-ten blocks at {unitMm.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} millimeters per unit: {flatCount} flats, {rodCount} rods, {unitCount} units — a rod is exactly ten units, a flat exactly ten rods")]);

        void Require(double bottom)
        {
            if (bottom > height - marginMm)
            {
                throw new ArgumentException("Too many blocks for one page at this unit size.", nameof(unitCount));
            }
        }
    }

    /// <summary>
    /// The classic seven-piece tangram as an exact dissection: a square and six
    /// cuts. Piece areas fall out of the construction — two larges at s²/4, a
    /// medium, square, and parallelogram at s²/8, two smalls at s²/16.
    /// </summary>
    public static ArtifactDocument Tangram(double sideMm = 160, PageSize size = PageSize.Letter)
    {
        var (width, height) = BlankformsPress.Dimensions(size);
        if (sideMm < 80 || sideMm > Math.Min(width, height) - 2 * BlankformsPress.DefaultMarginMm)
        {
            throw new ArgumentException("The square must be at least 80 millimeters and fit inside the margins.", nameof(sideMm));
        }

        var t = sideMm / 4;
        var ox = (width - sideMm) / 2;
        var oy = (height - sideMm) / 2;

        (double X, double Y) P(double gx, double gy)
        {
            return (ox + gx * t, oy + gy * t);
        }

        LineSeg Cut((double X, double Y) a, (double X, double Y) b)
        {
            return new(a.X, a.Y, b.X, b.Y, 0.5);
        }

        var primitives = new List<VectorPrimitive>
        {
            new RectShape(ox, oy, sideMm, sideMm, 0.7),
            Cut(P(0, 4), P(4, 0)), // the long diagonal
            Cut(P(0, 0), P(2, 2)), // splits the upper half into the two large triangles
            Cut(P(2, 4), P(4, 2)), // the medium triangle's hypotenuse
            Cut(P(2, 2), P(3, 3)), // square and parallelogram share this edge
            Cut(P(3, 1), P(4, 2)), // the square's lower edge
            Cut(P(1, 3), P(2, 4)), // the parallelogram's outer edge
        };

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"A classic seven-piece tangram square, {sideMm.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)} millimeters to a side, drawn as its six exact dissection cuts")]);
    }
}
