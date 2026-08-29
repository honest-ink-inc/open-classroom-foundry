// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

// The Blankforms second wave (handover 2026-08-29, forge item 2): hundred charts,
// dot paper, isometric dot paper. Parameters, never prose.

public static partial class BlankformsPress
{
    /// <summary>A 10-by-10 chart counting from <paramref name="start"/>; unlabeled cells are the blank variant where the counting work lives.</summary>
    public static ArtifactDocument HundredChart(int start = 1, double cellMm = 17, PageSize size = PageSize.Letter, double marginMm = DefaultMarginMm, bool labeled = true)
    {
        if (start is < 0 or > 900)
        {
            throw new ArgumentException("The chart starts between 0 and 900 so every cell stays three digits or fewer.", nameof(start));
        }

        var (width, height) = Dimensions(size);
        if (cellMm <= 0 || 10 * cellMm > width - 2 * marginMm || 10 * cellMm > height - 2 * marginMm)
        {
            throw new ArgumentException("Ten cells must fit inside the margins in both directions.", nameof(cellMm));
        }

        var left = (width - 10 * cellMm) / 2;
        var top = (height - 10 * cellMm) / 2;

        var primitives = new List<VectorPrimitive>();
        for (var row = 0; row < 10; row++)
        {
            for (var col = 0; col < 10; col++)
            {
                primitives.Add(new RectShape(left + col * cellMm, top + row * cellMm, cellMm, cellMm, 0.4));
                if (labeled)
                {
                    primitives.Add(new TextLabel(
                        left + col * cellMm + cellMm / 2,
                        top + row * cellMm + cellMm / 2 + 1.8,
                        (start + row * 10 + col).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        5));
                }
            }
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            labeled
                ? $"Hundred chart from {start} to {start + 99}, ten by ten"
                : "Blank hundred chart, ten by ten")]);
    }

    public static ArtifactDocument DotPaper(double pitchMm = 10, double dotRadiusMm = 0.5, PageSize size = PageSize.Letter, double marginMm = DefaultMarginMm)
    {
        ValidateDots(pitchMm, dotRadiusMm);

        var (width, height) = Dimensions(size);
        var right = width - marginMm;
        var bottom = height - marginMm;

        var primitives = new List<VectorPrimitive>();
        for (var y = marginMm; y <= bottom + 0.0001; y += pitchMm)
        {
            for (var x = marginMm; x <= right + 0.0001; x += pitchMm)
            {
                primitives.Add(new CircleShape(x, y, dotRadiusMm, 0.2, Filled: true));
            }
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"Square dot paper, {Fmt(pitchMm)} millimeter pitch")]);
    }

    /// <summary>A triangular lattice: rows pitch·√3/2 apart, odd rows offset half a pitch, so neighboring dots are equidistant.</summary>
    public static ArtifactDocument IsometricDotPaper(double pitchMm = 10, double dotRadiusMm = 0.5, PageSize size = PageSize.Letter, double marginMm = DefaultMarginMm)
    {
        ValidateDots(pitchMm, dotRadiusMm);

        var (width, height) = Dimensions(size);
        var right = width - marginMm;
        var bottom = height - marginMm;
        var rowHeight = pitchMm * Math.Sqrt(3) / 2;

        var primitives = new List<VectorPrimitive>();
        var row = 0;
        for (var y = marginMm; y <= bottom + 0.0001; y += rowHeight, row++)
        {
            var offset = row % 2 == 1 ? pitchMm / 2 : 0;
            for (var x = marginMm + offset; x <= right + 0.0001; x += pitchMm)
            {
                primitives.Add(new CircleShape(x, y, dotRadiusMm, 0.2, Filled: true));
            }
        }

        return new ArtifactDocument([new VectorGraphic(width, height, primitives,
            $"Isometric dot paper, {Fmt(pitchMm)} millimeter pitch")]);
    }

    private static void ValidateDots(double pitchMm, double dotRadiusMm)
    {
        if (pitchMm <= 0)
        {
            throw new ArgumentException("Dot pitch must be positive.", nameof(pitchMm));
        }

        if (dotRadiusMm <= 0 || dotRadiusMm >= pitchMm / 2)
        {
            throw new ArgumentException("Dots must be smaller than half the pitch.", nameof(dotRadiusMm));
        }
    }
}
