// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Modules.DeterministicPress;

/// <summary>
/// The low-ink variant spec §5.1 promises every form (third forge menu,
/// item 8): a pure weight transformation applied BEFORE Gate B, so the
/// teacher reviews exactly what will print. Geometry never moves — stroke
/// weights scale down, filled shapes become outlines, text is untouched.
/// </summary>
public static class LowInkPress
{
    public const double StrokeFactor = 0.6;
    public const double MinimumStrokeMm = 0.2;

    public static ArtifactDocument Apply(ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var nodes = document.Nodes.Select(DocumentNode (node) => node switch
        {
            VectorGraphic graphic => graphic with
            {
                Primitives = [.. graphic.Primitives.Select(Lighten)],
                Description = graphic.Description + " (low ink)",
            },
            _ => node,
        }).ToList();

        return new ArtifactDocument(nodes, document.Language);
    }

    private static VectorPrimitive Lighten(VectorPrimitive primitive) => primitive switch
    {
        LineSeg line => line with { StrokeWidthMm = Thin(line.StrokeWidthMm) },
        RectShape rect => rect with { StrokeWidthMm = Thin(rect.StrokeWidthMm), Filled = false },
        CircleShape circle => circle with { StrokeWidthMm = Thin(circle.StrokeWidthMm), Filled = false },
        _ => primitive,
    };

    private static double Thin(double strokeMm) => Math.Max(MinimumStrokeMm, strokeMm * StrokeFactor);
}
