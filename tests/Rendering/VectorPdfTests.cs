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
    public void The_studio_sampler_composes_into_one_imposed_byte_identical_booklet()
    {
        // Menu 4, item 9: catalog + imposer + PDF press, composed here at the
        // consumer per the layering wall.
        var artifact = Approve(StudioSampler.Catalog());
        var sides = BookletImposition.PdfSides(BookletImposition.Compute(
            artifact.Revision.Document.Nodes.OfType<VectorGraphic>().Count()));

        var first = VectorPdfWriter.WriteImposed(artifact, sides, RenderAudience.Teacher);
        Assert.Equal(first, VectorPdfWriter.WriteImposed(artifact, sides, RenderAudience.Teacher));
        Assert.StartsWith("%PDF-1.4", Encoding.Latin1.GetString(first[..8]), StringComparison.Ordinal);
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
    public void Native_pdf_character_repertoire_is_the_documented_218_code_points()
    {
        var repertoire = Enumerable.Range(char.MinValue, char.MaxValue + 1)
            .Select(value => (char)value)
            .Where(VectorPdfWriter.CanEncodeWinAnsi)
            .ToArray();

        Assert.Equal(218, repertoire.Length);
        Assert.Contains(' ', repertoire);
        Assert.Contains('ÿ', repertoire);
        Assert.Contains('€', repertoire);
        Assert.DoesNotContain('\n', repertoire);
        Assert.DoesNotContain('★', repertoire);
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
    public void Native_pdf_refuses_documents_over_the_structural_work_budget()
    {
        var primitives = Enumerable.Range(0, VectorPdfWriter.MaxPdfRenderUnits)
            .Select(_ => (VectorPrimitive)new LineSeg(0, 0, 1, 1))
            .ToList();
        var document = new ArtifactDocument(
            [new VectorGraphic(100, 100, primitives, "Bounded sheet")]);

        var refusal = Assert.Throws<InvalidOperationException>(() =>
            VectorPdfWriter.Write(Approve(document), RenderAudience.Learner));

        Assert.Contains("unit limit", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_pdf_refuses_semantic_text_over_its_bound()
    {
        var oversized = new string('A', checked((int)VectorPdfWriter.MaxPdfTextCharacters + 1));
        var document = new ArtifactDocument(
            [new VectorGraphic(100, 100, [new TextLabel(50, 50, oversized)], "Bounded sheet")]);

        var refusal = Assert.Throws<InvalidOperationException>(() =>
            VectorPdfWriter.Write(Approve(document), RenderAudience.Learner));

        Assert.Contains("semantic text", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Imposition_refuses_repetition_that_would_exceed_the_pdf_output_bound()
    {
        var label = new string('A', 1_000_000);
        var document = new ArtifactDocument(
            [new VectorGraphic(100, 100, [new TextLabel(50, 50, label)], "Repeated page")]);
        var repeatedSides = Enumerable.Repeat((Left: 1, Right: 1), 20).ToList();

        var refusal = Assert.Throws<InvalidOperationException>(() =>
            VectorPdfWriter.WriteImposed(
                Approve(document),
                repeatedSides,
                RenderAudience.Learner));

        Assert.Contains("byte limit", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_pdf_honors_cancellation_before_materializing_output()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            VectorPdfWriter.Write(
                Approve(LabeledPages(2)),
                RenderAudience.Learner,
                cancellation.Token));
    }

    private static ArtifactDocument LabeledPages(int count, double widthMm = 100, double heightMm = 200)
        => new([.. Enumerable.Range(1, count).Select(i => new VectorGraphic(
            widthMm, heightMm, [new TextLabel(widthMm / 2, heightMm / 2, $"P{i}")], $"Page {i}"))]);

    [Fact]
    public void The_imposed_booklet_places_every_page_in_saddle_stitch_order_at_the_pdf_layer()
    {
        // Six content pages pad to eight: sides (8,1) (2,7) (6,3) (4,5).
        var sides = BookletImposition.PdfSides(BookletImposition.Compute(6));
        var pdf = Encoding.Latin1.GetString(VectorPdfWriter.WriteImposed(
            Approve(LabeledPages(6)), sides, RenderAudience.Learner));

        Assert.Contains("/Count 4", pdf, StringComparison.Ordinal);

        var streams = pdf.Split(">>\nstream\n", StringSplitOptions.None).Skip(1)
            .Select(s => s.Split("endstream")[0]).ToList();
        Assert.Equal(4, streams.Count);

        // Pages 100x200 on a 200x100 sheet: scale 0.5, slots at 25 and 125 mm.
        var k = VectorPdfWriter.PointsPerMm;
        var left = $"q 0.5 0 0 0.5 {Fmt(25 * k)} 0 cm";
        var right = $"q 0.5 0 0 0.5 {Fmt(125 * k)} 0 cm";

        // Side 1: page 8 is a padding blank, so only P1 prints — in the RIGHT slot.
        Assert.Contains("(P1) Tj", streams[0], StringComparison.Ordinal);
        Assert.DoesNotContain("(P8", streams[0], StringComparison.Ordinal);
        Assert.Contains(right, streams[0], StringComparison.Ordinal);
        Assert.DoesNotContain(left, streams[0], StringComparison.Ordinal);

        // Side 2: P2 left, blank right. Sides 3 and 4: (6,3) and (4,5).
        Assert.Contains("(P2) Tj", streams[1], StringComparison.Ordinal);
        Assert.Contains(left, streams[1], StringComparison.Ordinal);
        Assert.Contains("(P6) Tj", streams[2], StringComparison.Ordinal);
        Assert.Contains("(P3) Tj", streams[2], StringComparison.Ordinal);
        Assert.Contains("(P4) Tj", streams[3], StringComparison.Ordinal);
        Assert.Contains("(P5) Tj", streams[3], StringComparison.Ordinal);
    }

    [Fact]
    public void Imposition_is_byte_deterministic_and_demands_one_uniform_page_size()
    {
        var artifact = Approve(LabeledPages(4));
        var sides = BookletImposition.PdfSides(BookletImposition.Compute(4));

        Assert.Equal(
            VectorPdfWriter.WriteImposed(artifact, sides, RenderAudience.Learner),
            VectorPdfWriter.WriteImposed(artifact, sides, RenderAudience.Learner));

        var mixed = new ArtifactDocument(
        [
            new VectorGraphic(100, 200, [new TextLabel(50, 100, "A")], "One"),
            new VectorGraphic(210, 297, [new TextLabel(50, 100, "B")], "Two"),
        ]);
        Assert.Throws<NotSupportedException>(
            () => VectorPdfWriter.WriteImposed(Approve(mixed), sides, RenderAudience.Learner));
    }

    [Fact]
    public void Imposition_refuses_mixed_semantic_nodes_instead_of_dropping_them()
    {
        var mixed = new ArtifactDocument(
        [
            new VectorGraphic(100, 200, [new TextLabel(50, 100, "A")], "One"),
            new Paragraph("Do not drop this paragraph"),
        ]);

        Assert.Throws<NotSupportedException>(() =>
            VectorPdfWriter.WriteImposed(
                Approve(mixed),
                [(1, 1)],
                RenderAudience.Learner));
    }

    [Fact]
    public void The_teacher_booklet_carries_the_short_edge_instruction_page_first()
    {
        var sides = BookletImposition.PdfSides(BookletImposition.Compute(4));
        var pdf = Encoding.Latin1.GetString(VectorPdfWriter.WriteImposed(
            Approve(LabeledPages(4)), sides, RenderAudience.Teacher));

        Assert.Contains("/Count 3", pdf, StringComparison.Ordinal); // instructions + two sides
        var first = pdf.Split(">>\nstream\n", StringSplitOptions.None)[1].Split("endstream")[0];
        Assert.Contains("SHORT edge", first, StringComparison.Ordinal);
        Assert.Contains("Fold the whole stack", first, StringComparison.Ordinal);
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
