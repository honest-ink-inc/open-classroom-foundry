using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

// Two machine rows of the Deterministic Press spec §9 (second forge menu,
// item 8), made structural: the measured-geometry sweep and the egress-freedom
// claim. The third row — rights-metadata hard-fail — lives in the Integration
// suite beside the asset store it audits.

public class MeasuredGeometrySweepTests
{
    private const double ToleranceMm = 0.2;

    public static TheoryData<string> EveryPressId()
    {
        var data = new TheoryData<string>();
        foreach (var definition in PressRoomCatalog.All)
        {
            data.Add(definition.Id);
        }

        return data;
    }

    /// <summary>Spec §9: rendered vectors within ±0.2 mm at 100 percent scale, for every form — here, every catalog entry at its defaults.</summary>
    [Theory]
    [MemberData(nameof(EveryPressId))]
    public void Every_primitive_of_every_press_lies_on_the_declared_page(string id)
    {
        var definition = PressRoomCatalog.ById(id);
        var document = definition.Build(new PressInputs(PressRoomCatalog.Defaults(definition)));

        foreach (var graphic in document.Nodes.OfType<VectorGraphic>())
        {
            // The page itself is exactly a declared physical size.
            Assert.Contains((graphic.WidthMm, graphic.HeightMm), new[] { (215.9, 279.4), (210.0, 297.0) });

            foreach (var primitive in graphic.Primitives)
            {
                var (minX, minY, maxX, maxY) = Extent(primitive);
                Assert.True(
                    minX >= -ToleranceMm && minY >= -ToleranceMm
                        && maxX <= graphic.WidthMm + ToleranceMm && maxY <= graphic.HeightMm + ToleranceMm,
                    $"{definition.Id}: {primitive.GetType().Name} spans ({minX:0.##},{minY:0.##})-({maxX:0.##},{maxY:0.##}) outside the {graphic.WidthMm}x{graphic.HeightMm} page");
            }
        }
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) Extent(VectorPrimitive primitive) => primitive switch
    {
        LineSeg line => (Math.Min(line.X1, line.X2), Math.Min(line.Y1, line.Y2), Math.Max(line.X1, line.X2), Math.Max(line.Y1, line.Y2)),
        RectShape rect => (rect.X, rect.Y, rect.X + rect.WidthMm, rect.Y + rect.HeightMm),
        CircleShape circle => (circle.CenterX - circle.RadiusMm, circle.CenterY - circle.RadiusMm, circle.CenterX + circle.RadiusMm, circle.CenterY + circle.RadiusMm),
        TextLabel label => (label.X, label.Y, label.X, label.Y), // anchor point; glyph extents belong to the renderer
        _ => throw new NotSupportedException($"Unknown vector primitive {primitive.GetType().Name}."),
    };
}

public class EgressFreedomTests
{
    /// <summary>
    /// Spec §9: "network egress trace during press operation shows zero
    /// connections" — made structural: the press module and everything beneath
    /// it compile against no networking assembly at all. An assembly that
    /// cannot name the network cannot call it.
    /// </summary>
    [Theory]
    [InlineData(typeof(PressRoomCatalog))]        // Foundry.Modules.DeterministicPress
    [InlineData(typeof(ArtifactDocument))]        // Foundry.Domain
    [InlineData(typeof(Contracts.RecipeManifest))] // Foundry.Contracts
    [InlineData(typeof(Application.ReviewSession))] // Foundry.Application
    public void The_assembly_references_no_networking_surface(Type marker)
    {
        var referenced = marker.Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();

        Assert.All(referenced, name =>
        {
            Assert.False(
                name.StartsWith("System.Net", StringComparison.Ordinal)
                    || name.Contains("Http", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Sockets", StringComparison.OrdinalIgnoreCase),
                $"{marker.Assembly.GetName().Name} references networking assembly '{name}'");
        });
    }
}
