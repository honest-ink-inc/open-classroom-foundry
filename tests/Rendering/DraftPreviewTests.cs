// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;

namespace Foundry.Tests.Rendering;

public sealed class DraftPreviewTests
{
    private static readonly DateTimeOffset SomeInstant = new(
        2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Preview_and_approved_output_share_the_exact_semantic_derivative()
    {
        var document = new ArtifactDocument(
        [
            new Heading(1, "Synthetic bilingual page"),
            new BilingualPair("Source first", "Target first", "en", "es"),
            new EvidenceLink("Synthetic claim", "source section 2"),
            new TeacherOnlyNotice("Synthetic teacher context"),
        ], "en");
        var draft = DraftArtifact.New(document, DataLane.Green);
        var request = new RenderRequest(
            RenderTarget.PrintHtml,
            RenderAudience.Teacher,
            TextScalePercent: 175,
            TargetLanguageFirst: true);

        var preview = UnapprovedDraftPreviewFactory.Create(draft, request);
        var approved = ApprovalGate.Approve(draft, "teacher@example.org", [], SomeInstant);
        var rendered = await new AccessibleHtmlRenderer().RenderAsync(
            approved,
            request,
            CancellationToken.None);
        var approvedHtml = Encoding.UTF8.GetString(rendered.Content.Span);
        var derivative = AccessibleHtmlRenderer.RenderSemanticDerivative(document, request);

        Assert.Same(draft.Revision, preview.Revision);
        Assert.Equal(request, preview.Request);
        Assert.Equal(derivative, Derivative(preview.DocumentText));
        Assert.Contains(derivative, approvedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("<!--", approvedHtml, StringComparison.Ordinal);
        Assert.Contains(AccessibleHtmlRenderer.UnapprovedMark, preview.DocumentText, StringComparison.Ordinal);
        Assert.Contains("data-review-state=\"unapproved\"", preview.DocumentText, StringComparison.Ordinal);
        Assert.Contains("@media print", preview.DocumentText, StringComparison.Ordinal);
        Assert.Contains("position: fixed", preview.DocumentText, StringComparison.Ordinal);
        Assert.DoesNotContain("Approved by", preview.DocumentText, StringComparison.Ordinal);
        Assert.Contains("Approved by teacher@example.org", approvedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Preview_honors_teacher_learner_scale_and_language_order_profiles()
    {
        var draft = DraftArtifact.New(new ArtifactDocument(
        [
            new BilingualPair("source-language", "target-language", "en", "es"),
            new EvidenceLink("shared-claim", "teacher-source-pointer"),
            new TeacherOnlyNotice("teacher-only-notice"),
        ]), DataLane.Green);

        var learner = UnapprovedDraftPreviewFactory.Create(
            draft,
            new RenderRequest(
                RenderTarget.AccessibleHtml,
                RenderAudience.Learner,
                TextScalePercent: 125,
                TargetLanguageFirst: false)).DocumentText;
        var teacher = UnapprovedDraftPreviewFactory.Create(
            draft,
            new RenderRequest(
                RenderTarget.AccessibleHtml,
                RenderAudience.Teacher,
                TextScalePercent: 200,
                TargetLanguageFirst: true)).DocumentText;

        Assert.Contains("body { font-size: 125%; }", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("teacher-only-notice", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("teacher-source-pointer", learner, StringComparison.Ordinal);
        Assert.True(
            learner.IndexOf("source-language", StringComparison.Ordinal)
                < learner.IndexOf("target-language", StringComparison.Ordinal));

        Assert.Contains("body { font-size: 200%; }", teacher, StringComparison.Ordinal);
        Assert.Contains("teacher-only-notice", teacher, StringComparison.Ordinal);
        Assert.Contains("teacher-source-pointer", teacher, StringComparison.Ordinal);
        Assert.True(
            teacher.IndexOf("target-language", StringComparison.Ordinal)
                < teacher.IndexOf("source-language", StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_has_no_approved_artifact_or_output_capability_shape()
    {
        var previewType = typeof(UnapprovedDraftPreview);
        var factoryType = typeof(UnapprovedDraftPreviewFactory);

        Assert.False(typeof(IRenderer).IsAssignableFrom(factoryType));
        Assert.False(typeof(IExporter).IsAssignableFrom(factoryType));
        Assert.False(typeof(IPrinter).IsAssignableFrom(factoryType));
        Assert.False(typeof(IProjectStore).IsAssignableFrom(factoryType));
        Assert.False(typeof(ApprovedArtifact).IsAssignableFrom(previewType));
        Assert.DoesNotContain(
            previewType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SelectMany(constructor => constructor.GetParameters()),
            parameter => parameter.ParameterType == typeof(ApprovedArtifact));
        Assert.DoesNotContain(
            factoryType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.ReturnType == typeof(ApprovedArtifact));

        var friends = typeof(AccessibleHtmlRenderer).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["Foundry.App.WinForms", "Foundry.Tests.Rendering"],
            friends);

        foreach (var sink in new[] { typeof(IRenderer), typeof(IExporter), typeof(IPrinter), typeof(IProjectStore) })
        {
            var payload = Assert.Single(
                Assert.Single(sink.GetMethods()).GetParameters(),
                parameter => parameter.ParameterType == typeof(ApprovedArtifact));
            Assert.Equal(typeof(ApprovedArtifact), payload.ParameterType);
        }
    }

    [Fact]
    public void Preview_refuses_non_html_output_targets()
    {
        var draft = DraftArtifact.New(
            new ArtifactDocument([new Paragraph("Synthetic")]),
            DataLane.Green);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UnapprovedDraftPreviewFactory.Create(
                draft,
                new RenderRequest(RenderTarget.PrintPdf)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UnapprovedDraftPreviewFactory.Create(
                draft,
                new RenderRequest(RenderTarget.Svg)));
    }

    [Fact]
    public async Task Browser_load_marker_binds_the_exact_revision_request_bytes_and_generation()
    {
        var document = new ArtifactDocument(
        [
            new Heading(1, "Synthetic marker proof"),
            new Paragraph("Exact preview content."),
        ]);
        var draft = DraftArtifact.New(document, DataLane.Green);
        var request = new RenderRequest(
            RenderTarget.AccessibleHtml,
            RenderAudience.Learner,
            TextScalePercent: 125,
            TargetLanguageFirst: false);

        var first = UnapprovedDraftPreviewFactory.CreateForBrowser(draft, request, 7);
        var repeated = UnapprovedDraftPreviewFactory.CreateForBrowser(draft, request, 7);
        var nextGeneration = UnapprovedDraftPreviewFactory.CreateForBrowser(draft, request, 8);
        var nextRevision = UnapprovedDraftPreviewFactory.CreateForBrowser(
            draft.WithEditedDocument(document),
            request,
            7);
        var nextRequest = UnapprovedDraftPreviewFactory.CreateForBrowser(
            draft,
            request with { TargetLanguageFirst = true },
            7);
        var unmarked = UnapprovedDraftPreviewFactory.Create(draft, request);
        var approved = ApprovalGate.Approve(draft, "teacher@example.org", [], SomeInstant);
        var approvedOutput = await new AccessibleHtmlRenderer().RenderAsync(
            approved,
            request,
            CancellationToken.None);
        var approvedHtml = Encoding.UTF8.GetString(approvedOutput.Content.Span);

        Assert.NotNull(first.LoadMarker);
        Assert.Matches("^v1-[0-9a-f]{16}-[0-9a-f]{64}$", first.LoadMarker);
        Assert.Equal(first.LoadMarker, repeated.LoadMarker);
        Assert.Equal(first.DocumentText, repeated.DocumentText);
        Assert.NotEqual(first.LoadMarker, nextGeneration.LoadMarker);
        Assert.NotEqual(first.LoadMarker, nextRevision.LoadMarker);
        Assert.NotEqual(first.LoadMarker, nextRequest.LoadMarker);
        Assert.Contains(
            $"<meta id=\"{UnapprovedDraftPreviewFactory.LoadMarkerElementId}\" name=\"{UnapprovedDraftPreviewFactory.LoadMarkerElementId}\" content=\"{first.LoadMarker}\">",
            first.DocumentText,
            StringComparison.Ordinal);
        Assert.Equal(Derivative(unmarked.DocumentText), Derivative(first.DocumentText));
        Assert.DoesNotContain(
            UnapprovedDraftPreviewFactory.LoadMarkerElementId,
            unmarked.DocumentText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            UnapprovedDraftPreviewFactory.LoadMarkerElementId,
            approvedHtml,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Vector_preview_preserves_mm_geometry_that_maps_to_final_pdf_points()
    {
        var graphic = new VectorGraphic(
            210.125,
            297.25,
            [
                new LineSeg(1.25, 2.5, 103.75, 204.125, 0.35, Dashed: true),
                new RectShape(5.5, 7.75, 20.25, 31.5, 0.4, Filled: false),
                new CircleShape(45.125, 60.75, 9.5, 0.2, Filled: true),
                new TextLabel(90.25, 110.5, "Exact label", 4.75, TextAnchor.End),
            ],
            "Synthetic exact vector");
        var document = new ArtifactDocument([graphic]);
        var draft = DraftArtifact.New(document, DataLane.Green);
        var preview = UnapprovedDraftPreviewFactory.Create(
            draft,
            new RenderRequest(RenderTarget.PrintHtml));
        var approved = ApprovalGate.Approve(draft, "teacher@example.org", [], SomeInstant);

        var svgOutput = await new AccessibleHtmlRenderer().RenderAsync(
            approved,
            new RenderRequest(RenderTarget.Svg),
            CancellationToken.None);
        var svg = Encoding.UTF8.GetString(svgOutput.Content.Span);
        var pdf = Encoding.Latin1.GetString(VectorPdfWriter.Write(
            approved,
            RenderAudience.Learner));

        var exactLine = "<line x1=\"1.25\" y1=\"2.5\" x2=\"103.75\" y2=\"204.125\" stroke=\"#000\" stroke-width=\"0.35\" stroke-linecap=\"round\" stroke-dasharray=\"3 2\"/>";
        Assert.Contains("viewBox=\"0 0 210.125 297.25\" width=\"210.125mm\" height=\"297.25mm\"", preview.DocumentText, StringComparison.Ordinal);
        Assert.Contains(exactLine, preview.DocumentText, StringComparison.Ordinal);
        Assert.Contains(exactLine, svg, StringComparison.Ordinal);

        Assert.Contains(
            $"/MediaBox [0 0 {Pdf(graphic.WidthMm)} {Pdf(graphic.HeightMm)}]",
            pdf,
            StringComparison.Ordinal);
        Assert.Contains(
            $"{Pdf(1.25)} {Pdf(graphic.HeightMm - 2.5)} m "
                + $"{Pdf(103.75)} {Pdf(graphic.HeightMm - 204.125)} l S",
            pdf,
            StringComparison.Ordinal);
        Assert.Contains($"{Pdf(0.35)} w", pdf, StringComparison.Ordinal);
    }

    private static string Derivative(string html)
    {
        var start = html.IndexOf(AccessibleHtmlRenderer.ExactDerivativeStart, StringComparison.Ordinal);
        var end = html.IndexOf(AccessibleHtmlRenderer.ExactDerivativeEnd, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        start += AccessibleHtmlRenderer.ExactDerivativeStart.Length;
        if (html[start] == '\r')
        {
            start++;
        }

        if (html[start] == '\n')
        {
            start++;
        }

        return html[start..end];
    }

    private static string Pdf(double millimeters)
        => (millimeters * VectorPdfWriter.PointsPerMm)
            .ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
