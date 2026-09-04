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

    private static ApprovedArtifact Approve(ArtifactDocument document, IAssetCatalog? catalog = null)
    {
        var reviewedAssets = ExactAssetCatalogSnapshot.CaptureForReview(document, catalog);
        return ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green),
            "teacher@example.org",
            [],
            SomeInstant,
            reviewedAssets.Bindings);
    }

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
    public void Image_references_without_exact_gate_b_assets_are_not_approvable()
    {
        var document = new ArtifactDocument(
            [new ImageReference(new AssetId("symbols.stop.v1"), "A red stop sign")]);

        var error = Assert.Throws<InvalidOperationException>(() => Approve(document));

        Assert.Contains("placeholder is not review evidence", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_images_are_verified_and_embedded_in_the_self_contained_output()
    {
        var svg = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><circle cx=\"5\" cy=\"5\" r=\"4\"/></svg>");
        var id = new AssetId("symbols.safe.v1");
        var catalog = new OneAssetCatalog(id, svg, "image/svg+xml");
        var artifact = Approve(
            new ArtifactDocument([new ImageReference(id, "A plain circle")]),
            catalog);
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
        var reviewedCatalog = new OneAssetCatalog(id, safe, "image/svg+xml");
        var artifact = Approve(
            new ArtifactDocument([new ImageReference(id, "A symbol")]),
            reviewedCatalog);

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
    public async Task Same_asset_bytes_with_changed_rights_provenance_are_refused_after_approval()
    {
        var id = new AssetId("symbols.rights-bound.v1");
        var content = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"5\" cy=\"5\" r=\"4\"/></svg>");
        var document = new ArtifactDocument([new ImageReference(id, "A rights-bound symbol")]);
        var reviewedCatalog = new OneAssetCatalog(
            id,
            content,
            "image/svg+xml",
            license: "CC0-1.0");
        var substitutedCatalog = new OneAssetCatalog(
            id,
            content,
            "image/svg+xml",
            license: "LicenseRef-unreviewed-replacement");
        var artifact = Approve(document, reviewedCatalog);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer(substitutedCatalog).RenderAsync(
                artifact,
                new RenderRequest(RenderTarget.AccessibleHtml),
                CancellationToken.None));

        Assert.Contains("exact asset bytes, MIME types, and provenance reviewed", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xD801)]
    public void Invalid_utf16_provenance_is_refused_instead_of_lossily_fingerprinted(
        int invalidCodeUnit)
    {
        var id = new AssetId("symbols.invalid-provenance-text.v1");
        var content = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"5\" cy=\"5\" r=\"4\"/></svg>");
        var document = new ArtifactDocument([new ImageReference(id, "A symbol")]);
        var invalidLicense = new string((char)invalidCodeUnit, 1);
        var catalog = new OneAssetCatalog(
            id,
            content,
            "image/svg+xml",
            license: invalidLicense);

        Assert.Throws<EncoderFallbackException>(() => Approve(document, catalog));
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
        var document = new ArtifactDocument(
        [
            new ImageReference(firstId, "First symbol"),
            new ImageReference(secondId, "Second symbol"),
        ]);
        var reviewedCatalog = new StaticAssetCatalog(
            (firstId, "first.svg", original),
            (secondId, "second.svg", second));
        var artifact = Approve(document, reviewedCatalog);

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
        ]), catalog);
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
        var reviewedCatalog = new OneAssetCatalog(id, svg, "image/svg+xml");
        var document = new ArtifactDocument(
            [.. Enumerable.Range(1, 4)
                .Select(index => (DocumentNode)new ImageReference(id, $"Repeated symbol {index}"))]);

        var output = await new AccessibleHtmlRenderer(catalog).RenderAsync(
            Approve(document, reviewedCatalog),
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

        var countCatalog = new OneAssetCatalog(id, smallSvg, "image/svg+xml");
        var countException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer(countCatalog)
                .RenderAsync(
                    Approve(tooManyReferences, countCatalog),
                    new RenderRequest(RenderTarget.AccessibleHtml),
                    CancellationToken.None));
        Assert.Contains("image-reference limit", countException.Message, StringComparison.Ordinal);

        var largeSvg = Encoding.UTF8.GetBytes(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\"><desc>{new string(' ', 900_000)}</desc></svg>");
        var repeatedLargeAsset = new ArtifactDocument(
            [.. Enumerable.Range(1, 28)
                .Select(index => (DocumentNode)new ImageReference(id, $"Large reference {index}"))]);

        var budgetCatalog = new OneAssetCatalog(id, largeSvg, "image/svg+xml");
        var budgetException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AccessibleHtmlRenderer(budgetCatalog)
                .RenderAsync(
                    Approve(repeatedLargeAsset, budgetCatalog),
                    new RenderRequest(RenderTarget.AccessibleHtml),
                    CancellationToken.None));
        Assert.Contains("cumulative embedded-derivative budget", budgetException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Raster_decode_budget_is_cumulative_and_reference_weighted()
    {
        var id = new AssetId("symbols.large-raster.v1");
        var compressedLargeRaster = BuildPng(
            5_000,
            5_000,
            includeEnd: true,
            bitDepth: 1,
            colorType: 0);
        var withinBudget = new ArtifactDocument(
            [.. Enumerable.Range(1, 4)
                .Select(index => (DocumentNode)new ImageReference(id, $"Large raster {index}"))]);
        var beyondBudget = new ArtifactDocument(
            [.. Enumerable.Range(1, 5)
                .Select(index => (DocumentNode)new ImageReference(id, $"Large raster {index}"))]);
        var rasterCatalog = new OneAssetCatalog(id, compressedLargeRaster, "image/png");
        var renderer = new AccessibleHtmlRenderer(rasterCatalog);

        var admitted = await renderer.RenderAsync(
            Approve(withinBudget, rasterCatalog),
            new RenderRequest(RenderTarget.AccessibleHtml),
            CancellationToken.None);
        Assert.False(admitted.Content.IsEmpty);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            renderer.RenderAsync(
                Approve(beyondBudget, rasterCatalog),
                new RenderRequest(RenderTarget.AccessibleHtml),
                CancellationToken.None));
        Assert.Contains("cumulative raster-decode budget", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Raster_admission_rejects_signature_only_truncation_and_oversized_dimensions()
    {
        byte[] pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        byte[] jpegMarkers = [0xFF, 0xD8, 0xFF, 0xD9];
        var completePng = BuildPng(1, 1, includeEnd: true);
        var missingEnd = BuildPng(1, 1, includeEnd: false);
        var oversized = BuildPng(
            16_384,
            16_384,
            includeEnd: true,
            completeImageData: false);
        var corruptCrc = completePng.ToArray();
        corruptCrc[^1] ^= 0x01;
        var completeJpeg = BuildJpeg();
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
    public void Png_admission_rejects_a_declared_extent_without_complete_decoded_scanlines()
    {
        var malformed = BuildPng(
            5_000,
            5_000,
            includeEnd: true,
            bitDepth: 1,
            colorType: 0,
            completeImageData: false);

        Assert.False(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(malformed, "image/png"));
    }

    [Fact]
    public void GdiPlus_png_with_allowlisted_ancillary_chunks_is_admitted()
    {
        // Synthetic 2-by-2 bitmap encoded by the Windows GDI+ PNG codec. Its
        // chunk order is IHDR, sRGB, gAMA, pHYs, IDAT, IEND.
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAALSURBVBhXY2BABwAAEgABp3qZbgAAAABJRU5ErkJggg==");

        Assert.True(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(png, "image/png"));
    }

    [Theory]
    [InlineData("iCCP")]
    [InlineData("zTXt")]
    [InlineData("iTXt")]
    [InlineData("acTL")]
    [InlineData("fcTL")]
    [InlineData("fdAT")]
    [InlineData("vpAg")]
    public void Png_admission_rejects_compressed_active_and_unknown_ancillary_chunks(string chunkType)
    {
        var payload = chunkType switch
        {
            "acTL" => new byte[8],
            "fcTL" => new byte[26],
            "fdAT" => new byte[5],
            _ => Encoding.ASCII.GetBytes("synthetic\0compressed"),
        };
        var hostile = InsertPngChunk(
            BuildPng(1, 1, includeEnd: true),
            chunkType,
            payload,
            beforeChunkType: "IDAT");

        Assert.False(
            AccessibleHtmlRenderer.IsSupportedSelfContainedImage(hostile, "image/png"),
            $"PNG chunk {chunkType} must not cross the fail-closed ancillary allowlist.");
    }

    [Fact]
    public void Png_admission_validates_profile_chunk_length_order_value_and_duplicates()
    {
        var plain = BuildPng(1, 1, includeEnd: true);
        var truecolor = BuildPng(1, 1, includeEnd: true, colorType: 2);
        byte[] gamma = [0x00, 0x00, 0xB1, 0x8F];
        byte[] palette = [0x00, 0x00, 0x00];
        byte[] physicalDimensions = [0x00, 0x00, 0x0E, 0xC3, 0x00, 0x00, 0x0E, 0xC3, 0x01];

        var duplicateSrgb = InsertPngChunk(plain, "sRGB", [0], "IDAT");
        duplicateSrgb = InsertPngChunk(duplicateSrgb, "sRGB", [0], "IDAT");
        var duplicateGamma = InsertPngChunk(plain, "gAMA", gamma, "IDAT");
        duplicateGamma = InsertPngChunk(duplicateGamma, "gAMA", gamma, "IDAT");
        var duplicatePhysicalDimensions = InsertPngChunk(plain, "pHYs", physicalDimensions, "IDAT");
        duplicatePhysicalDimensions = InsertPngChunk(
            duplicatePhysicalDimensions,
            "pHYs",
            physicalDimensions,
            "IDAT");
        var srgbAfterPalette = InsertPngChunk(truecolor, "PLTE", palette, "IDAT");
        srgbAfterPalette = InsertPngChunk(srgbAfterPalette, "sRGB", [0], "IDAT");
        var gammaAfterPalette = InsertPngChunk(truecolor, "PLTE", palette, "IDAT");
        gammaAfterPalette = InsertPngChunk(gammaAfterPalette, "gAMA", gamma, "IDAT");

        (string Name, byte[] Content)[] rejected =
        [
            ("sRGB length", InsertPngChunk(plain, "sRGB", [0, 0], "IDAT")),
            ("sRGB intent", InsertPngChunk(plain, "sRGB", [4], "IDAT")),
            ("sRGB duplicate", duplicateSrgb),
            ("sRGB order", srgbAfterPalette),
            ("gAMA length", InsertPngChunk(plain, "gAMA", [0, 0, 1], "IDAT")),
            ("gAMA zero", InsertPngChunk(plain, "gAMA", [0, 0, 0, 0], "IDAT")),
            ("gAMA duplicate", duplicateGamma),
            ("gAMA order", gammaAfterPalette),
            ("pHYs length", InsertPngChunk(plain, "pHYs", new byte[8], "IDAT")),
            ("pHYs unit", InsertPngChunk(plain, "pHYs", [0, 0, 0, 1, 0, 0, 0, 1, 2], "IDAT")),
            ("pHYs duplicate", duplicatePhysicalDimensions),
            ("pHYs order", InsertPngChunk(plain, "pHYs", physicalDimensions, "IEND")),
        ];

        foreach (var (name, content) in rejected)
        {
            Assert.False(
                AccessibleHtmlRenderer.IsSupportedSelfContainedImage(content, "image/png"),
                $"Malformed {name} chunk was admitted.");
        }
    }

    [Fact]
    public void Png_admission_validates_palette_and_transparency_order_length_and_duplicates()
    {
        byte[] palette = [0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF];
        var indexed = BuildPng(1, 1, includeEnd: true, bitDepth: 1, colorType: 3);
        var validIndexed = InsertPngChunk(indexed, "PLTE", palette, "IDAT");
        validIndexed = InsertPngChunk(validIndexed, "tRNS", [0x00, 0xFF], "IDAT");
        Assert.True(AccessibleHtmlRenderer.IsSupportedSelfContainedImage(validIndexed, "image/png"));

        var duplicatePalette = InsertPngChunk(
            BuildPng(1, 1, includeEnd: true, colorType: 2),
            "PLTE",
            palette,
            "IDAT");
        duplicatePalette = InsertPngChunk(duplicatePalette, "PLTE", palette, "IDAT");
        var duplicateTransparency = InsertPngChunk(
            BuildPng(1, 1, includeEnd: true, colorType: 0),
            "tRNS",
            [0, 0],
            "IDAT");
        duplicateTransparency = InsertPngChunk(
            duplicateTransparency,
            "tRNS",
            [0, 0],
            "IDAT");
        var paletteAfterTransparency = InsertPngChunk(
            BuildPng(1, 1, includeEnd: true, colorType: 2),
            "tRNS",
            new byte[6],
            "IDAT");
        paletteAfterTransparency = InsertPngChunk(
            paletteAfterTransparency,
            "PLTE",
            palette,
            "IDAT");

        (string Name, byte[] Content)[] rejected =
        [
            ("missing indexed palette", indexed),
            ("palette on grayscale", InsertPngChunk(BuildPng(1, 1, true, colorType: 0), "PLTE", palette, "IDAT")),
            ("duplicate palette", duplicatePalette),
            ("late palette", InsertPngChunk(BuildPng(1, 1, true, colorType: 2), "PLTE", palette, "IEND")),
            ("too many indexed entries", InsertPngChunk(indexed, "PLTE", new byte[9], "IDAT")),
            ("indexed transparency before palette", InsertPngChunk(indexed, "tRNS", [0], "IDAT")),
            ("indexed transparency too long", InsertPngChunk(InsertPngChunk(indexed, "PLTE", palette, "IDAT"), "tRNS", [0, 1, 2], "IDAT")),
            ("alpha transparency", InsertPngChunk(BuildPng(1, 1, true), "tRNS", [0, 0], "IDAT")),
            ("duplicate transparency", duplicateTransparency),
            ("palette after transparency", paletteAfterTransparency),
            ("late transparency", InsertPngChunk(BuildPng(1, 1, true, colorType: 0), "tRNS", [0, 0], "IEND")),
        ];

        foreach (var (name, content) in rejected)
        {
            Assert.False(
                AccessibleHtmlRenderer.IsSupportedSelfContainedImage(content, "image/png"),
                $"Malformed {name} structure was admitted.");
        }
    }

    [Fact]
    public void Jpeg_admission_rejects_illegal_ff_selectors()
    {
        (string Name, byte Marker, int PayloadOffset)[] selectors =
        [
            ("DQT", 0xDB, 0),
            ("DHT", 0xC4, 0),
            ("SOF component quantization", 0xC0, 8),
            ("SOS component Huffman", 0xDA, 2),
        ];

        foreach (var (name, marker, payloadOffset) in selectors)
        {
            var hostile = BuildJpeg();
            var markerOffset = FindJpegMarker(hostile, marker);
            hostile[markerOffset + 4 + payloadOffset] = 0xFF;

            Assert.False(
                AccessibleHtmlRenderer.IsSupportedSelfContainedImage(hostile, "image/jpeg"),
                $"Illegal 0xFF {name} selector was admitted.");
        }
    }

    [Fact]
    public void Jpeg_admission_requires_well_formed_and_referenced_quantization_and_huffman_tables()
    {
        var malformedQuantizationPrecision = BuildJpeg();
        var dqtOffset = FindJpegMarker(malformedQuantizationPrecision, 0xDB);
        malformedQuantizationPrecision[dqtOffset + 4] = 0x10;

        var zeroQuantizationValue = BuildJpeg();
        dqtOffset = FindJpegMarker(zeroQuantizationValue, 0xDB);
        zeroQuantizationValue[dqtOffset + 5] = 0;

        var oversubscribedHuffmanTree = BuildJpeg();
        var dhtOffset = FindJpegMarker(oversubscribedHuffmanTree, 0xC4);
        oversubscribedHuffmanTree[dhtOffset + 5] = 3;

        var missingQuantizationReference = BuildJpeg();
        var frameOffset = FindJpegMarker(missingQuantizationReference, 0xC0);
        missingQuantizationReference[frameOffset + 12] = 3;

        var missingHuffmanReference = BuildJpeg();
        var scanOffset = FindJpegMarker(missingHuffmanReference, 0xDA);
        missingHuffmanReference[scanOffset + 6] = 0x22;

        (string Name, byte[] Content)[] rejected =
        [
            ("truncated 16-bit DQT", malformedQuantizationPrecision),
            ("zero DQT value", zeroQuantizationValue),
            ("oversubscribed DHT", oversubscribedHuffmanTree),
            ("missing DQT reference", missingQuantizationReference),
            ("missing DHT reference", missingHuffmanReference),
        ];

        foreach (var (name, content) in rejected)
        {
            Assert.False(
                AccessibleHtmlRenderer.IsSupportedSelfContainedImage(content, "image/jpeg"),
                $"JPEG with {name} was admitted.");
        }
    }

    [Fact]
    public async Task Approved_symbol_output_without_the_exact_reviewed_catalog_is_refused()
    {
        var id = new AssetId("symbols.missing.v1");
        var content = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        var reviewedCatalog = new OneAssetCatalog(id, content, "image/svg+xml");
        var artifact = Approve(
            new ArtifactDocument([new ImageReference(id, "Missing symbol"), new Card("Card", "Card")]),
            reviewedCatalog);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
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

    [Fact]
    public async Task Png_admission_observes_cancellation_raised_during_asset_resolution()
    {
        var id = new AssetId("symbols.cancel-png.v1");
        using var cancelled = new CancellationTokenSource();
        var catalog = new OneAssetCatalog(
            id,
            BuildPng(1, 1, includeEnd: true),
            "image/png",
            onContentRead: cancelled.Cancel);
        var reviewedCatalog = new OneAssetCatalog(
            id,
            BuildPng(1, 1, includeEnd: true),
            "image/png");
        var artifact = Approve(
            new ArtifactDocument([new ImageReference(id, "Synthetic cancellation proof")]),
            reviewedCatalog);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new AccessibleHtmlRenderer(catalog).RenderAsync(
                artifact,
                new RenderRequest(RenderTarget.AccessibleHtml),
                cancelled.Token));
    }

    private static byte[] BuildPng(
        int width,
        int height,
        bool includeEnd,
        byte bitDepth = 8,
        byte colorType = 6,
        bool completeImageData = true)
    {
        using var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), (uint)height);
        header[8] = bitDepth;
        header[9] = colorType;
        WritePngChunk(output, "IHDR"u8, header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            if (completeImageData)
            {
                var channels = colorType switch
                {
                    0 or 3 => 1,
                    2 => 3,
                    4 => 2,
                    6 => 4,
                    _ => throw new ArgumentOutOfRangeException(nameof(colorType)),
                };
                var rowBytes = checked((int)(((long)width * channels * bitDepth + 7) / 8));
                var scanline = new byte[rowBytes + 1];
                for (var row = 0; row < height; row++)
                {
                    zlib.Write(scanline);
                }
            }
            else
            {
                zlib.Write([0, 0, 0, 0, 0]);
            }
        }

        WritePngChunk(output, "IDAT"u8, compressed.ToArray());
        if (includeEnd)
        {
            WritePngChunk(output, "IEND"u8, []);
        }

        return output.ToArray();
    }

    private static byte[] InsertPngChunk(
        byte[] png,
        string chunkType,
        ReadOnlySpan<byte> chunkData,
        string beforeChunkType)
    {
        if (chunkType.Length != 4 || beforeChunkType.Length != 4)
        {
            throw new ArgumentException("PNG test chunk names must contain exactly four characters.");
        }

        using var output = new MemoryStream();
        output.Write(png.AsSpan(0, 8));
        var position = 8;
        var inserted = false;
        while (position < png.Length)
        {
            var dataLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(position, 4)));
            var existingType = Encoding.ASCII.GetString(png, position + 4, 4);
            if (!inserted && string.Equals(existingType, beforeChunkType, StringComparison.Ordinal))
            {
                WritePngChunk(output, Encoding.ASCII.GetBytes(chunkType), chunkData);
                inserted = true;
            }

            output.Write(png.AsSpan(position, 12 + dataLength));
            position += 12 + dataLength;
        }

        if (!inserted)
        {
            throw new InvalidOperationException($"PNG test insertion point {beforeChunkType} was not found.");
        }

        return output.ToArray();
    }

    private static byte[] BuildJpeg()
        => Convert.FromBase64String(
            "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAACAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD8qqKKKAP/2Q==");

    private static int FindJpegMarker(byte[] jpeg, byte marker)
    {
        ReadOnlySpan<byte> markerBytes = [0xFF, marker];
        var offset = jpeg.AsSpan().IndexOf(markerBytes);
        if (offset <= 0)
        {
            throw new InvalidOperationException($"Synthetic JPEG marker 0xFF{marker:X2} was not found.");
        }

        return offset;
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

    private sealed class StaticAssetCatalog : IAssetCatalog
    {
        private readonly IReadOnlyDictionary<AssetId, (AssetProvenance Provenance, byte[] Content)> _assets;

        internal StaticAssetCatalog(params (AssetId Id, string FileName, byte[] Content)[] assets)
        {
            _assets = assets.ToDictionary(
                asset => asset.Id,
                asset => (
                    new AssetProvenance(
                        asset.Id,
                        $"concept.{asset.Id.Value}",
                        "1.0.0",
                        asset.FileName,
                        "image/svg+xml",
                        "synthetic test",
                        "synthetic test",
                        "CC0-1.0",
                        Convert.ToHexString(SHA256.HashData(asset.Content)),
                        "Synthetic fixture",
                        "Synthetic fixture",
                        Redistributable: true),
                    asset.Content.ToArray()));
        }

        public IReadOnlyList<AssetProvenance> All
            => [.. _assets.Values.Select(asset => asset.Provenance)];

        public AssetProvenance? Find(AssetId id)
            => _assets.TryGetValue(id, out var asset) ? asset.Provenance : null;

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            if (_assets.TryGetValue(id, out var asset))
            {
                content = asset.Content;
                mimeType = asset.Provenance.MimeType;
                return true;
            }

            content = default;
            mimeType = string.Empty;
            return false;
        }
    }

    private sealed class OneAssetCatalog : IAssetCatalog
    {
        private readonly ReadOnlyMemory<byte> _content;
        private readonly Action? _onContentRead;
        private readonly AssetProvenance _provenance;

        internal int FindCalls { get; private set; }

        internal int ContentCalls { get; private set; }

        internal OneAssetCatalog(
            AssetId id,
            ReadOnlyMemory<byte> content,
            string mimeType,
            ReadOnlyMemory<byte>? recordedContent = null,
            Action? onContentRead = null,
            string license = "CC0-1.0")
        {
            _content = content;
            _onContentRead = onContentRead;
            _provenance = new AssetProvenance(
                id,
                "concept.test",
                "1.0.0",
                "test.svg",
                mimeType,
                "test",
                "test",
                license,
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
                _onContentRead?.Invoke();
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
                "synthetic test",
                "CC0-1.0",
                Convert.ToHexString(SHA256.HashData(content)),
                "Synthetic fixture",
                "Synthetic fixture",
                Redistributable: true);
    }
}
