using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;
using Foundry.Rendering;

namespace Foundry.Tests.Rendering;

// The vector-first PDF press (second forge menu, item 3): deterministic bytes,
// millimeter-true geometry as PDF operators, loud refusal outside its lane.

public class VectorPdfTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static ApprovedArtifact Approve(ArtifactDocument document)
        => ApprovalGate.Approve(DraftArtifact.New(document, DataLane.Green), "teacher@example.org", [], SomeInstant);

    private static string Fmt(double value)
        => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void Identical_input_gives_byte_identical_pdf()
    {
        var artifact = Approve(CalibrationPress.ProofPage());

        Assert.Equal(
            VectorPdfWriter.Write(artifact, RenderAudience.Learner),
            VectorPdfWriter.Write(artifact, RenderAudience.Learner));
    }

    [Fact]
    public void The_calibration_rulers_measure_exactly_one_hundred_millimeters_in_points()
    {
        var pdf = Encoding.Latin1.GetString(
            VectorPdfWriter.Write(Approve(CalibrationPress.ProofPage()), RenderAudience.Learner));

        Assert.StartsWith("%PDF-1.4", pdf, StringComparison.Ordinal);

        // Letter is exactly 612 x 792 points; the mm-to-point arithmetic is exact.
        Assert.Contains("/MediaBox [0 0 612 792]", pdf, StringComparison.Ordinal);

        var k = VectorPdfWriter.PointsPerMm;
        var y = Fmt((279.4 - 70) * k);
        Assert.Contains($"{Fmt(22 * k)} {y} m {Fmt(122 * k)} {y} l S", pdf, StringComparison.Ordinal);

        // 100 mm is 283.465 points: the span the printed ruler must measure.
        Assert.Equal("283.465", Fmt(122 * k - 22 * k));
    }

    [Fact]
    public void Every_page_of_a_multi_sheet_document_lands_in_the_page_tree()
    {
        var roster = Enumerable.Range(1, 30).Select(i => $"Seat {i}").ToList();
        var pdf = Encoding.Latin1.GetString(VectorPdfWriter.Write(
            Approve(GroupingDeck.Cards(roster, groupSize: 4, seed: 1)), RenderAudience.Learner));

        Assert.Contains("/Count 2", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Teacher_only_notices_print_for_the_teacher_and_never_for_the_learner()
    {
        var artifact = Approve(PuzzlePress.WordSearch(["water", "cycle", "rain"], seed: 5));

        var learner = Encoding.Latin1.GetString(VectorPdfWriter.Write(artifact, RenderAudience.Learner));
        var teacher = Encoding.Latin1.GetString(VectorPdfWriter.Write(artifact, RenderAudience.Teacher));

        Assert.Contains("/Count 2", learner, StringComparison.Ordinal); // puzzle + key sheets
        Assert.Contains("/Count 3", teacher, StringComparison.Ordinal); // plus the notice page
        Assert.Contains("Answer key", teacher, StringComparison.Ordinal);
        Assert.DoesNotContain("Answer key: the same grid", learner, StringComparison.Ordinal);
    }

    [Fact]
    public void Characters_outside_winansi_refuse_loudly_never_substitute()
    {
        var document = new ArtifactDocument([new VectorGraphic(100, 100,
            [new TextLabel(50, 50, "★")], "A sheet with a star")]);

        var exception = Assert.Throws<NotSupportedException>(
            () => VectorPdfWriter.Write(Approve(document), RenderAudience.Learner));
        Assert.Contains("U+2605", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Winansi_text_including_accents_and_dashes_encodes_and_escapes()
    {
        var document = new ArtifactDocument([new VectorGraphic(100, 100,
            [new TextLabel(50, 50, "Tijeras — (á) 100%")], "A label sheet")]);

        var pdf = Encoding.Latin1.GetString(VectorPdfWriter.Write(Approve(document), RenderAudience.Learner));

        Assert.Contains("(Tijeras \u0097 \\(\u00e1\\) 100%) Tj", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_renderer_serves_print_pdf_natively_for_vector_documents()
    {
        var output = await new AccessibleHtmlRenderer().RenderAsync(
            Approve(BlankformsPress.HundredChart()),
            new RenderRequest(RenderTarget.PrintPdf, RenderAudience.Learner),
            CancellationToken.None);

        Assert.Equal("application/pdf", output.MimeType);
        Assert.StartsWith("%PDF-1.4", Encoding.Latin1.GetString(output.Content.Span), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_vector_documents_still_take_the_html_print_path()
    {
        var guide = BookletImposition.Guide(BookletImposition.Compute(8));

        await Assert.ThrowsAsync<NotSupportedException>(() => new AccessibleHtmlRenderer().RenderAsync(
            Approve(guide), new RenderRequest(RenderTarget.PrintPdf), CancellationToken.None));
    }

    [Fact]
    public void Anchored_text_is_positioned_by_exact_courier_arithmetic()
    {
        // A 10 mm middle-anchored "AB": each Courier glyph is 0.6 em, so the
        // pen starts exactly one glyph-width left of center.
        var size = 10 * VectorPdfWriter.PointsPerMm;
        var document = new ArtifactDocument([new VectorGraphic(200, 100,
            [new TextLabel(100, 50, "AB", 10)], "Anchor test sheet")]);

        var pdf = Encoding.Latin1.GetString(VectorPdfWriter.Write(Approve(document), RenderAudience.Learner));

        var expectedX = 100 * VectorPdfWriter.PointsPerMm - 0.6 * size;
        Assert.Contains($"{Fmt(expectedX)} {Fmt(50 * VectorPdfWriter.PointsPerMm)} Td (AB) Tj", pdf, StringComparison.Ordinal);
    }
}
