using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;
using Foundry.Rendering;

namespace Foundry.Tests.Rendering;

// The Calibration Press's definition of done: the ruler spans measure true at
// 100 percent scale in the rendered SVG, millimeter for millimeter.

public class CalibrationRenderingTests
{
    [Fact]
    public async Task The_proof_page_renders_millimeter_true_with_the_hundred_millimeter_ruler_intact()
    {
        var approved = ApprovalGate.Approve(
            DraftArtifact.New(CalibrationPress.ProofPage(), DataLane.Green),
            "teacher@example.org",
            [],
            new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));

        var output = await new AccessibleHtmlRenderer().RenderAsync(
            approved, new RenderRequest(RenderTarget.Svg), CancellationToken.None);

        Assert.Equal("image/svg+xml", output.MimeType);
        var svg = Encoding.UTF8.GetString(output.Content.Span);

        // The page is Letter, declared in physical millimeters.
        Assert.Contains("width=\"215.9mm\" height=\"279.4mm\"", svg, StringComparison.Ordinal);
        Assert.Contains("viewBox=\"0 0 215.9 279.4\"", svg, StringComparison.Ordinal);

        // The horizontal ruler baseline runs from 22 to 122: exactly 100 mm.
        Assert.Contains("<line x1=\"22\" y1=\"70\" x2=\"122\" y2=\"70\"", svg, StringComparison.Ordinal);

        // The vertical ruler baseline runs from 90 to 190: exactly 100 mm.
        Assert.Contains("<line x1=\"22\" y1=\"90\" x2=\"22\" y2=\"190\"", svg, StringComparison.Ordinal);
    }
}
