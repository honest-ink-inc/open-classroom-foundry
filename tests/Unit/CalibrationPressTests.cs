using Foundry.Domain;
using Foundry.Modules.DeterministicPress;
using Xunit;

namespace Foundry.Tests.Unit;

public class CalibrationPressTests
{
    private static VectorGraphic ProofSheet(PageSize size = PageSize.Letter)
        => Assert.IsType<VectorGraphic>(Assert.Single(CalibrationPress.ProofPage(size).Nodes));

    [Fact]
    public void Both_ruler_baselines_span_exactly_one_hundred_millimeters()
    {
        var graphic = ProofSheet();
        var baselines = graphic.Primitives.OfType<LineSeg>().Where(l => l.StrokeWidthMm == 0.6).ToList();

        Assert.Equal(2, baselines.Count);
        var horizontal = Assert.Single(baselines, l => l.Y1 == l.Y2);
        var vertical = Assert.Single(baselines, l => l.X1 == l.X2);
        Assert.Equal(100, horizontal.X2 - horizontal.X1, 9);
        Assert.Equal(100, vertical.Y2 - vertical.Y1, 9);
    }

    [Fact]
    public void Ruler_ticks_sit_at_exact_millimeter_multiples_with_centimeter_majors()
    {
        var graphic = ProofSheet();
        var horizontal = graphic.Primitives.OfType<LineSeg>().Single(l => l.StrokeWidthMm == 0.6 && l.Y1 == l.Y2);

        var ticks = graphic.Primitives.OfType<LineSeg>()
            .Where(l => l.X1 == l.X2 && l.Y1 == horizontal.Y1 && l.Y2 < l.Y1)
            .ToList();

        Assert.Equal(101, ticks.Count);
        Assert.All(ticks, t => Assert.Equal(0, (t.X1 - horizontal.X1) % 1, 9));
        Assert.Equal(11, ticks.Count(t => Math.Abs(t.Y1 - t.Y2 - 6) < 1e-9));   // centimeter majors
        Assert.Equal(10, ticks.Count(t => Math.Abs(t.Y1 - t.Y2 - 4.5) < 1e-9)); // half-centimeter

        // Each ruler numbers its centimeters 0 through 10.
        Assert.Equal(2, graphic.Primitives.OfType<TextLabel>().Count(l => l.Text == "10"));
    }

    [Fact]
    public void The_margin_frame_sits_exactly_at_the_declared_margin()
    {
        var graphic = ProofSheet(PageSize.A4);
        var frame = graphic.Primitives.OfType<RectShape>().Single(r => r.WidthMm > 100);

        Assert.Equal(12, frame.X);
        Assert.Equal(12, frame.Y);
        Assert.Equal(210 - 24, frame.WidthMm, 9);
        Assert.Equal(297 - 24, frame.HeightMm, 9);
    }

    [Fact]
    public void Duplex_ring_targets_are_mirror_symmetric_about_the_page_center()
    {
        var graphic = ProofSheet();
        var rings = graphic.Primitives.OfType<CircleShape>().Where(c => c.RadiusMm == 6).ToList();

        Assert.Equal(3, rings.Count);
        var centers = rings.Select(r => r.CenterX).ToList();
        Assert.All(centers, x => Assert.Contains(centers, other => Math.Abs(other - (graphic.WidthMm - x)) < 1e-9));
        Assert.Contains(centers, x => Math.Abs(x - graphic.WidthMm / 2) < 1e-9);
    }

    [Fact]
    public void The_density_ramp_steps_from_white_through_four_hatch_weights_to_solid()
    {
        var graphic = ProofSheet();
        var steps = graphic.Primitives.OfType<RectShape>().Where(r => r.WidthMm == 22).ToList();

        Assert.Equal(6, steps.Count);
        Assert.Single(steps, s => s.Filled);

        // Hatch counts follow the spacings 3.5, 2, 1, 0.5 over a 14 mm step.
        var hatch = graphic.Primitives.OfType<LineSeg>().Where(l => l.StrokeWidthMm == 0.25).ToList();
        Assert.Equal(3 + 6 + 13 + 27, hatch.Count);
        Assert.All(hatch, l => Assert.Equal(22, l.X2 - l.X1, 9));
    }

    [Fact]
    public void The_proof_page_is_deterministic_validates_and_refuses_silly_margins()
    {
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(CalibrationPress.ProofPage(PageSize.A4)),
            System.Text.Json.JsonSerializer.Serialize(CalibrationPress.ProofPage(PageSize.A4)));

        Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(CalibrationPress.ProofPage())));
        Assert.Throws<ArgumentException>(() => CalibrationPress.ProofPage(marginMm: 3));
        Assert.Throws<ArgumentException>(() => CalibrationPress.ProofPage(marginMm: 30));
    }

    [Fact]
    public void The_calibration_recipe_stands_green_and_parameter_only()
    {
        var recipe = DeterministicPressRecipes.Calibration;

        Assert.Equal("press.calibration", recipe.Id);
        Assert.Equal(DataLane.Green, recipe.MaximumLane);
        Assert.Empty(recipe.RequiredProviderCapabilities);
        Assert.Contains(recipe, DeterministicPressRecipes.All);
    }
}
