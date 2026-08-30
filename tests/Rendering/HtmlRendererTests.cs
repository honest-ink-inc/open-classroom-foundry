using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Security.Cryptography;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;

namespace Foundry.Tests.Rendering;

public class HtmlRendererTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static ApprovedArtifact Approve(ArtifactDocument document)
        => ApprovalGate.Approve(DraftArtifact.New(document, DataLane.Green), "teacher@example.org", [], SomeInstant);

    private static async Task<string> RenderAsync(ArtifactDocument document, RenderRequest request)
    {
        var output = await new AccessibleHtmlRenderer().RenderAsync(Approve(document), request, CancellationToken.None);
        return Encoding.UTF8.GetString(output.Content.Span);
    }

    [Fact]
    public async Task Hostile_content_is_escaped_never_executed()
    {
        var html = await RenderAsync(
            new ArtifactDocument([new Paragraph("<script>alert('x')</script> & <img src=x onerror=y>")]),
            new RenderRequest(RenderTarget.AccessibleHtml));

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("onerror=y>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Teacher_only_content_never_reaches_a_learner_rendering()
    {
        var document = new ArtifactDocument(
        [
            new Paragraph("Water each plant once."),
            new TeacherOnlyNotice("Fade the visual prompt after two independent successes."),
            new EvidenceLink("Steps match the staged photo.", "capture-1, region 2"),
        ]);

        var learner = await RenderAsync(document, new RenderRequest(RenderTarget.AccessibleHtml, RenderAudience.Learner));
        var teacher = await RenderAsync(document, new RenderRequest(RenderTarget.AccessibleHtml, RenderAudience.Teacher));

        Assert.DoesNotContain("Fade the visual prompt", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("capture-1", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("Approved by", learner, StringComparison.Ordinal);

        Assert.Contains("Fade the visual prompt", teacher, StringComparison.Ordinal);
        Assert.Contains("capture-1", teacher, StringComparison.Ordinal);
        Assert.Contains("Approved by teacher@example.org", teacher, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bilingual_pairs_carry_language_and_direction_semantics()
    {
        var html = await RenderAsync(
            new ArtifactDocument(
                [new BilingualPair("Raise your hand.", "ارفع يدك.", "en", "ar")],
                "en"),
            new RenderRequest(RenderTarget.AccessibleHtml));

        Assert.Contains("<p lang=\"en\" dir=\"auto\">Raise your hand.</p>", html, StringComparison.Ordinal);
        Assert.Contains("lang=\"ar\" dir=\"auto\"", html, StringComparison.Ordinal);
        Assert.Contains("ارفع يدك.", html, StringComparison.Ordinal);
        Assert.True(
            html.IndexOf("lang=\"en\"", StringComparison.Ordinal) < html.IndexOf("lang=\"ar\"", StringComparison.Ordinal),
            "The source language precedes the target in reading order.");
    }

    [Fact]
    public async Task Structure_is_semantic_headings_lists_and_scoped_table_headers()
    {
        var html = await RenderAsync(
            new ArtifactDocument(
            [
                new Heading(1, "Watering the class plants"),
                new OrderedSteps(["Pick up the can.", "Fill to the line."]),
                new TableNode(["Plant", "Days"], [["Fern", "2"], ["Cactus", "14"]]),
            ], "en"),
            new RenderRequest(RenderTarget.AccessibleHtml));

        Assert.Contains("<html lang=\"en\">", html, StringComparison.Ordinal);
        Assert.Contains("<title>Watering the class plants</title>", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Watering the class plants</h1>", html, StringComparison.Ordinal);
        Assert.Contains("<ol class=\"steps\">", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Plant</th>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Browser_compatibility_mode_is_current_without_changing_portable_snapshot_bytes()
    {
        var document = new ArtifactDocument([new Heading(1, "Synthetic compatibility proof")], "en");
        var request = new RenderRequest(RenderTarget.AccessibleHtml, RenderAudience.Learner);

        var ordinary = await RenderAsync(document, request);
        var portable = Encoding.UTF8.GetString(
            AccessibleHtmlRenderer.RenderPortableSnapshot(document, request));

        Assert.Contains(
            "<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">",
            ordinary,
            StringComparison.Ordinal);
        Assert.DoesNotContain("X-UA-Compatible", portable, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Image_placeholders_are_explicit_never_silent_gaps()
    {
        var html = await RenderAsync(
            new ArtifactDocument([new ImageReference(new AssetId("symbols.stop.v1"), "A red stop sign")]),
            new RenderRequest(RenderTarget.AccessibleHtml));

        Assert.Contains("data-asset-id=\"symbols.stop.v1\"", html, StringComparison.Ordinal);
        Assert.Contains("A red stop sign", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_images_are_verified_and_embedded_in_the_self_contained_output()
    {
        var svg = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><circle cx=\"5\" cy=\"5\" r=\"4\"/></svg>");
        var id = new AssetId("symbols.safe.v1");
        var catalog = new OneAssetCatalog(id, svg, "image/svg+xml");
        var artifact = Approve(new ArtifactDocument([new ImageReference(id, "A plain circle")]));
        var renderer = new AccessibleHtmlRenderer(catalog);

        var first = await renderer.RenderAsync(
            artifact, new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None);
        var second = await renderer.RenderAsync(
            artifact, new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None);
        var html = Encoding.UTF8.GetString(first.Content.Span);

        Assert.Contains("<img src=\"data:image/svg+xml;base64,", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"A plain circle\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<figure class=\"asset-placeholder\"", html, StringComparison.Ordinal);
        Assert.Equal(first.Content.ToArray(), second.Content.ToArray());
    }

    [Fact]
    public async Task Asset_hash_drift_and_active_svg_are_refused_not_rendered()
    {
        var id = new AssetId("symbols.hostile.v1");
        var safe = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"5\" cy=\"5\" r=\"4\"/></svg>");
        var active = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");
        var processingInstruction = Encoding.UTF8.GetBytes(
            "<?xml-stylesheet href=\"https://example.invalid/hostile.css\"?><svg xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"5\" cy=\"5\" r=\"4\"/></svg>");
        var artifact = Approve(new ArtifactDocument([new ImageReference(id, "A symbol")]));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer(new OneAssetCatalog(id, safe, "image/svg+xml", recordedContent: active))
                .RenderAsync(artifact, new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer(new OneAssetCatalog(id, active, "image/svg+xml"))
                .RenderAsync(artifact, new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer(new OneAssetCatalog(id, processingInstruction, "image/svg+xml"))
                .RenderAsync(artifact, new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None));
    }

    [Fact]
    public void Svg_admission_rejects_node_attribute_and_nesting_amplification()
    {
        var ordinary = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><g><circle cx=\"5\" cy=\"5\" r=\"4\"/></g></svg>");
        var tooManyNodes = Encoding.UTF8.GetBytes(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\">{string.Concat(Enumerable.Repeat("<g/>", 9_000))}</svg>");
        string[] admittedAttributes =
        [
            "x", "y", "x1", "y1", "x2", "y2", "cx", "cy", "r", "rx", "ry",
            "width", "height", "d", "points", "fill", "stroke", "stroke-width",
            "stroke-linecap", "stroke-linejoin",
        ];
        var attributedElement = $"<g {string.Join(' ', admittedAttributes.Select((name, index) => $"{name}=\"{index}\""))}/>";
        var tooManyAttributes = Encoding.UTF8.GetBytes(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\">{string.Concat(Enumerable.Repeat(attributedElement, 900))}</svg>");
        var tooDeep = Encoding.UTF8.GetBytes(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\">{string.Concat(Enumerable.Repeat("<g>", 70))}"
            + $"{string.Concat(Enumerable.Repeat("</g>", 70))}</svg>");

        Assert.True(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(ordinary, "image/svg+xml"));
        Assert.False(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(tooManyNodes, "image/svg+xml"));
        Assert.False(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(tooManyAttributes, "image/svg+xml"));
        Assert.False(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(tooDeep, "image/svg+xml"));
    }

    [Fact]
    public async Task Semantic_text_and_projected_output_are_bounded_before_materialization()
    {
        var semanticOverflow = new ArtifactDocument(
            [new Paragraph(new string('x', (2 * 1024 * 1024) + 1))]);
        var semanticException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer().RenderAsync(
                Approve(semanticOverflow),
                new RenderRequest(RenderTarget.AccessibleHtml),
                CancellationToken.None));
        Assert.Contains("semantic text", semanticException.Message, StringComparison.Ordinal);

        var vectorOverflow = new ArtifactDocument(
        [
            new VectorGraphic(
                100,
                50,
                [new TextLabel(10, 10, semanticOverflow.Nodes.OfType<Paragraph>().Single().Text)],
                "Synthetic bounded vector"),
        ]);
        var svgException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer().RenderAsync(
                Approve(vectorOverflow),
                new RenderRequest(RenderTarget.Svg),
                CancellationToken.None));
        Assert.Contains("semantic text", svgException.Message, StringComparison.Ordinal);

        // The raw text remains below the semantic ceiling, but escaping and the
        // bounded cross-target emission multiplicity would exceed the complete
        // HTML/SVG character budget.
        var escapedOutputOverflow = new ArtifactDocument(
            [new Paragraph(new string('&', 1_700_000))]);
        var outputException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer().RenderAsync(
                Approve(escapedOutputOverflow),
                new RenderRequest(RenderTarget.AccessibleHtml),
                CancellationToken.None));
        Assert.Contains("HTML/SVG output", outputException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_bytes_are_owned_before_later_catalog_calls_can_mutate_them()
    {
        var firstId = new AssetId("symbols.first.v1");
        var secondId = new AssetId("symbols.second.v1");
        var original = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><title>safe</title></svg>");
        var mutated = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><title>evil</title></svg>");
        var second = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"5\" cy=\"5\" r=\"4\"/></svg>");
        var catalog = new MutatingCatalog(firstId, original, mutated, secondId, second);
        var artifact = Approve(new ArtifactDocument(
        [
            new ImageReference(firstId, "First symbol"),
            new ImageReference(secondId, "Second symbol"),
        ]));

        var output = await new AccessibleHtmlRenderer(catalog).RenderAsync(
            artifact,
            new RenderRequest(RenderTarget.AccessibleHtml),
            CancellationToken.None);
        var html = Encoding.UTF8.GetString(output.Content.Span);

        Assert.Contains(Convert.ToBase64String(original), html, StringComparison.Ordinal);
        Assert.DoesNotContain(Convert.ToBase64String(mutated), html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Visual_support_documents_export_as_deterministic_self_contained_svg()
    {
        var svg = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><circle cx=\"5\" cy=\"5\" r=\"4\"/></svg>");
        var id = new AssetId("symbols.safe.v1");
        var catalog = new OneAssetCatalog(id, svg, "image/svg+xml");
        var artifact = Approve(new ArtifactDocument(
        [
            new Heading(1, "First and then"),
            new ImageReference(id, "A plain circle"),
            new Card("First: Begin", "Begin the task"),
            new TeacherOnlyNotice("Teacher-only prompt"),
        ]));
        var renderer = new AccessibleHtmlRenderer(catalog);

        var first = await renderer.RenderAsync(
            artifact,
            new RenderRequest(RenderTarget.Svg, RenderAudience.Learner),
            CancellationToken.None);
        var repeated = await renderer.RenderAsync(
            artifact,
            new RenderRequest(RenderTarget.Svg, RenderAudience.Learner),
            CancellationToken.None);
        var output = Encoding.UTF8.GetString(first.Content.Span);

        Assert.Equal("image/svg+xml", first.MimeType);
        Assert.Contains(
            "role=\"img\" aria-labelledby=\"asset-sheet-title\" aria-describedby=\"asset-sheet-description\"",
            output,
            StringComparison.Ordinal);
        Assert.Contains("<title id=\"asset-sheet-title\">First and then</title>", output, StringComparison.Ordinal);
        Assert.Contains("<desc id=\"asset-sheet-description\">", output, StringComparison.Ordinal);
        Assert.Contains("symbol: A plain circle", output, StringComparison.Ordinal);
        Assert.Contains("href=\"data:image/svg+xml;base64,", output, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"A plain circle\"", output, StringComparison.Ordinal);
        Assert.Contains("First: Begin", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Teacher-only prompt", output, StringComparison.Ordinal);
        Assert.Equal(first.Content.ToArray(), repeated.Content.ToArray());
    }

    [Fact]
    public async Task Text_only_all_aboard_content_exports_as_svg_without_an_asset_catalog()
    {
        var artifact = Approve(new ArtifactDocument(
        [
            new Heading(1, "Today"),
            new StepRow("Open your book."),
            new StepRow("Read one page."),
            new StepRow("Close your book."),
        ], "en"));

        var output = await new AccessibleHtmlRenderer().RenderAsync(
            artifact,
            new RenderRequest(RenderTarget.Svg),
            CancellationToken.None);
        var svg = Encoding.UTF8.GetString(output.Content.Span);

        Assert.Equal("image/svg+xml", output.MimeType);
        Assert.Contains("Open your book.", svg, StringComparison.Ordinal);
        Assert.Contains("Close your book.", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("data:image", svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Asset_sheet_svg_preserves_target_first_locales_rtl_scale_and_complete_root_description()
    {
        var artifact = Approve(new ArtifactDocument(
        [
            new Heading(1, "جدول اليوم"),
            new StepRow("Open the book.", null, "افتح الكتاب.", "en", "ar"),
            new StepRow("Read.", null, "اقرأ.", "en", "ar"),
        ], "ar"));

        var output = await new AccessibleHtmlRenderer().RenderAsync(
            artifact,
            new RenderRequest(
                RenderTarget.Svg,
                RenderAudience.Learner,
                TextScalePercent: 150,
                TargetLanguageFirst: true),
            CancellationToken.None);
        var svg = Encoding.UTF8.GetString(output.Content.Span);

        Assert.Contains("lang=\"ar\" xml:lang=\"ar\" direction=\"rtl\"", svg, StringComparison.Ordinal);
        Assert.Contains("lang=\"en\" xml:lang=\"en\" direction=\"ltr\"", svg, StringComparison.Ordinal);
        Assert.Contains("font-size=\"39\"", svg, StringComparison.Ordinal);
        Assert.Contains("font-size=\"30\"", svg, StringComparison.Ordinal);
        Assert.Contains("<desc id=\"asset-sheet-description\">", svg, StringComparison.Ordinal);
        Assert.Contains("افتح الكتاب. [ar]", svg, StringComparison.Ordinal);
        Assert.Contains("Open the book. [en]", svg, StringComparison.Ordinal);
        Assert.True(
            svg.IndexOf("افتح الكتاب.", StringComparison.Ordinal)
                < svg.IndexOf("Open the book.", StringComparison.Ordinal),
            "The requested target language must precede the source in the SVG reading order.");
    }

    [Fact]
    public void A_long_unbroken_svg_word_is_chunked_in_one_forward_pass()
    {
        var word = new string('x', 200_000);

        var chunks = AccessibleHtmlRenderer.WrapSvgText(
            word,
            maximumRunes: 12,
            CancellationToken.None).ToList();

        Assert.Equal(16_667, chunks.Count);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, 12));
        Assert.Equal(word, string.Concat(chunks));
    }

    [Fact]
    public async Task Every_renderer_route_enforces_the_persisted_text_scale_contract()
    {
        var artifact = Approve(new ArtifactDocument(
        [
            new VectorGraphic(100, 50, [new TextLabel(50, 25, "Scale")], "Scale sheet"),
        ]));
        RenderTarget[] targets =
        [
            RenderTarget.AccessibleHtml,
            RenderTarget.PrintHtml,
            RenderTarget.PrintPdf,
            RenderTarget.Svg,
        ];
        double[] invalidScales = [double.NaN, double.PositiveInfinity, 99, 201];

        foreach (var target in targets)
        {
            foreach (var scale in invalidScales)
            {
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                    new AccessibleHtmlRenderer().RenderAsync(
                        artifact,
                        new RenderRequest(target, TextScalePercent: scale),
                        CancellationToken.None));
            }
        }
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    public async Task Text_scale_contract_includes_both_endpoints(double scale)
    {
        var artifact = Approve(new ArtifactDocument([new Paragraph("Endpoint scale")]));

        var output = await new AccessibleHtmlRenderer().RenderAsync(
            artifact,
            new RenderRequest(RenderTarget.AccessibleHtml, TextScalePercent: scale),
            CancellationToken.None);

        Assert.False(output.Content.IsEmpty);
    }

    [Fact]
    public async Task Repeated_asset_references_are_resolved_once_through_the_bounded_cache()
    {
        var svg = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><circle cx=\"5\" cy=\"5\" r=\"4\"/></svg>");
        var id = new AssetId("symbols.repeated.v1");
        var catalog = new OneAssetCatalog(id, svg, "image/svg+xml");
        var document = new ArtifactDocument(
            [.. Enumerable.Range(1, 4)
                .Select(index => (DocumentNode)new ImageReference(id, $"Repeated symbol {index}"))]);

        var output = await new AccessibleHtmlRenderer(catalog).RenderAsync(
            Approve(document),
            new RenderRequest(RenderTarget.AccessibleHtml),
            CancellationToken.None);
        var html = Encoding.UTF8.GetString(output.Content.Span);

        Assert.Equal(1, catalog.FindCalls);
        Assert.Equal(1, catalog.ContentCalls);
        Assert.Equal(4, html.Split("data:image/svg+xml;base64,", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task Repeated_references_cannot_exceed_count_or_cumulative_derivative_budgets()
    {
        var id = new AssetId("symbols.amplification.v1");
        var smallSvg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var tooManyReferences = new ArtifactDocument(
            [.. Enumerable.Range(1, 513)
                .Select(index => (DocumentNode)new ImageReference(id, $"Reference {index}"))]);

        var countException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer(new OneAssetCatalog(id, smallSvg, "image/svg+xml"))
                .RenderAsync(
                    Approve(tooManyReferences),
                    new RenderRequest(RenderTarget.AccessibleHtml),
                    CancellationToken.None));
        Assert.Contains("image-reference limit", countException.Message, StringComparison.Ordinal);

        var largeSvg = Encoding.UTF8.GetBytes(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"><desc>{new string(' ', 900_000)}</desc></svg>");
        var repeatedLargeAsset = new ArtifactDocument(
            [.. Enumerable.Range(1, 28)
                .Select(index => (DocumentNode)new ImageReference(id, $"Large reference {index}"))]);

        var budgetException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer(new OneAssetCatalog(id, largeSvg, "image/svg+xml"))
                .RenderAsync(
                    Approve(repeatedLargeAsset),
                    new RenderRequest(RenderTarget.AccessibleHtml),
                    CancellationToken.None));
        Assert.Contains("cumulative embedded-derivative budget", budgetException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Raster_admission_rejects_signature_only_truncation_and_oversized_dimensions()
    {
        byte[] pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        byte[] jpegMarkers = [0xFF, 0xD8, 0xFF, 0xD9];
        var completePng = BuildPng(1, 1, includeEnd: true);
        var missingEnd = BuildPng(1, 1, includeEnd: false);
        var oversized = BuildPng(16_384, 16_384, includeEnd: true);
        var corruptCrc = completePng.ToArray();
        corruptCrc[^1] ^= 0x01;
        var completeJpeg = Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAACAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD8qqKKKAP/2Q==");
        var oversizedJpeg = completeJpeg.ToArray();
        ReadOnlySpan<byte> baselineFrameMarker = [0xFF, 0xC0];
        var frameMarker = oversizedJpeg.AsSpan().IndexOf(baselineFrameMarker);
        Assert.True(frameMarker > 0, "The synthetic JPEG must expose its baseline frame marker.");
        BinaryPrimitives.WriteUInt16BigEndian(oversizedJpeg.AsSpan(frameMarker + 5, 2), ushort.MaxValue);
        BinaryPrimitives.WriteUInt16BigEndian(oversizedJpeg.AsSpan(frameMarker + 7, 2), ushort.MaxValue);
        var truncatedJpeg = completeJpeg[..^2];

        Assert.False(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(pngSignature, "image/png"));
        Assert.False(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(jpegMarkers, "image/jpeg"));
        Assert.True(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(completePng, "image/png"));
        Assert.True(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(completeJpeg, "image/jpeg"));
        Assert.False(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(missingEnd, "image/png"));
        Assert.False(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(corruptCrc, "image/png"));
        Assert.False(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(oversized, "image/png"));
        Assert.False(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(truncatedJpeg, "image/jpeg"));
        Assert.False(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(oversizedJpeg, "image/jpeg"));
    }

    [Fact]
    public async Task Symbol_svg_without_the_exact_catalog_is_refused()
    {
        var artifact = Approve(new ArtifactDocument(
            [new ImageReference(new AssetId("symbols.missing.v1"), "Missing symbol"), new Card("Card", "Card")]));

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            new AccessibleHtmlRenderer().RenderAsync(
                artifact,
                new RenderRequest(RenderTarget.Svg),
                CancellationToken.None));
    }

    [Fact]
    public async Task Rendering_is_deterministic_byte_for_byte()
    {
        var document = new ArtifactDocument([new Heading(1, "Ten-frames"), new Paragraph("Cut along the lines.")]);
        var artifact = Approve(document);
        var renderer = new AccessibleHtmlRenderer();

        var first = await renderer.RenderAsync(artifact, new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None);
        var second = await renderer.RenderAsync(artifact, new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None);

        Assert.Equal(first.Content.ToArray(), second.Content.ToArray());
    }

    [Fact]
    public async Task Print_html_adds_the_paper_stylesheet()
    {
        var html = await RenderAsync(
            new ArtifactDocument([new Paragraph("Cut along the lines.")]),
            new RenderRequest(RenderTarget.PrintHtml));

        Assert.Contains("@page", html, StringComparison.Ordinal);
        Assert.Contains("break-inside: avoid", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unsupported_targets_refuse_rather_than_pretend()
    {
        var renderer = new AccessibleHtmlRenderer();
        var artifact = Approve(new ArtifactDocument([new Paragraph("x")]));

        await Assert.ThrowsAsync<NotSupportedException>(
            () => renderer.RenderAsync(artifact, new RenderRequest(RenderTarget.PrintPdf), CancellationToken.None));
    }

    [Fact]
    public async Task Cancellation_is_respected()
    {
        var renderer = new AccessibleHtmlRenderer();
        var artifact = Approve(new ArtifactDocument([new Paragraph("x")]));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => renderer.RenderAsync(artifact, new RenderRequest(RenderTarget.AccessibleHtml), cancelled.Token));
    }

    private static byte[] BuildPng(int width, int height, bool includeEnd)
    {
        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), (uint)height);
        header[8] = 8;
        header[9] = 6;
        WritePngChunk(output, "IHDR"u8, header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write([0, 0, 0, 0, 0]);
        }

        WritePngChunk(output, "IDAT"u8, compressed.ToArray());
        if (includeEnd)
        {
            WritePngChunk(output, "IEND"u8, []);
        }

        return output.ToArray();
    }

    private static void WritePngChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        output.Write(length);
        output.Write(type);
        output.Write(data);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, PngCrc(type, data));
        output.Write(crc);
    }

    private static uint PngCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in type)
        {
            crc = UpdatePngCrc(crc, value);
        }

        foreach (var value in data)
        {
            crc = UpdatePngCrc(crc, value);
        }

        return ~crc;
    }

    private static uint UpdatePngCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0
                ? 0xEDB88320u ^ (crc >> 1)
                : crc >> 1;
        }

        return crc;
    }

    private sealed class OneAssetCatalog : IAssetCatalog
    {
        private readonly ReadOnlyMemory<byte> _content;
        private readonly AssetProvenance _provenance;

        internal int FindCalls { get; private set; }

        internal int ContentCalls { get; private set; }

        internal OneAssetCatalog(
            AssetId id,
            ReadOnlyMemory<byte> content,
            string mimeType,
            ReadOnlyMemory<byte>? recordedContent = null)
        {
            _content = content;
            _provenance = new AssetProvenance(
                id,
                "concept.test",
                "1.0.0",
                "test.svg",
                mimeType,
                "test",
                "test",
                "CC0-1.0",
                Convert.ToHexString(SHA256.HashData((recordedContent ?? content).Span)),
                "Test symbol",
                "A symbol",
                Redistributable: true);
        }

        public IReadOnlyList<AssetProvenance> All => [_provenance];

        public AssetProvenance? Find(AssetId id)
        {
            FindCalls++;
            return id == _provenance.Id ? _provenance : null;
        }

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            ContentCalls++;
            if (id == _provenance.Id)
            {
                content = _content;
                mimeType = _provenance.MimeType;
                return true;
            }

            content = default;
            mimeType = string.Empty;
            return false;
        }
    }

    private sealed class MutatingCatalog : IAssetCatalog
    {
        private readonly AssetId _firstId;
        private readonly byte[] _firstContent;
        private readonly byte[] _firstMutation;
        private readonly AssetId _secondId;
        private readonly byte[] _secondContent;
        private readonly IReadOnlyDictionary<AssetId, AssetProvenance> _provenance;

        internal MutatingCatalog(
            AssetId firstId,
            byte[] firstContent,
            byte[] firstMutation,
            AssetId secondId,
            byte[] secondContent)
        {
            Assert.Equal(firstContent.Length, firstMutation.Length);
            _firstId = firstId;
            _firstContent = [.. firstContent];
            _firstMutation = [.. firstMutation];
            _secondId = secondId;
            _secondContent = [.. secondContent];
            _provenance = new Dictionary<AssetId, AssetProvenance>
            {
                [firstId] = Provenance(firstId, "first.svg", firstContent),
                [secondId] = Provenance(secondId, "second.svg", secondContent),
            };
        }

        public IReadOnlyList<AssetProvenance> All => [.. _provenance.Values];

        public AssetProvenance? Find(AssetId id) => _provenance.GetValueOrDefault(id);

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            if (id == _firstId)
            {
                content = _firstContent;
                mimeType = "image/svg+xml";
                return true;
            }

            if (id == _secondId)
            {
                _firstMutation.CopyTo(_firstContent, 0);
                content = _secondContent;
                mimeType = "image/svg+xml";
                return true;
            }

            content = default;
            mimeType = string.Empty;
            return false;
        }

        private static AssetProvenance Provenance(AssetId id, string fileName, byte[] content)
            => new(
                id,
                $"concept.{id.Value}",
                "1.0.0",
                fileName,
                "image/svg+xml",
                "synthetic test",
                "test",
                "CC0-1.0",
                Convert.ToHexString(SHA256.HashData(content)),
                "Synthetic mutation proof",
                "A synthetic symbol",
                Redistributable: true);
    }
}
