// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Rendering;

/// <summary>
/// Renders the semantic ArtifactDocument as self-contained accessible HTML —
/// the first digital accessibility target (plan §8) — and as print-ready HTML
/// for the paper pipeline. Every string is escaped; bilingual pairs carry lang
/// and dir attributes so reading order and bidirectional text survive; teacher
/// -only content never reaches a learner rendering. Output is deterministic:
/// identical artifact and request produce identical bytes.
/// </summary>
public sealed class AccessibleHtmlRenderer : IRenderer
{
    public Task<RenderedOutput> RenderAsync(ApprovedArtifact artifact, RenderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Target == RenderTarget.Svg)
        {
            var graphics = artifact.Revision.Document.Nodes.OfType<VectorGraphic>().ToList();
            if (graphics.Count != 1)
            {
                throw new NotSupportedException(
                    "Standalone SVG output requires a document with exactly one vector sheet; multi-sheet documents export as print HTML.");
            }

            var svg = RenderSvg(graphics[0], standalone: true);
            return Task.FromResult(new RenderedOutput(RenderTarget.Svg, Encoding.UTF8.GetBytes(svg), "image/svg+xml"));
        }

        if (request.Target == RenderTarget.PrintPdf)
        {
            // The vector-first press: mm-exact geometry as true PDF operators,
            // deterministic bytes, no browser. Non-vector documents keep the
            // HTML print path (headless PDF conversion) until parity.
            var pdf = VectorPdfWriter.Write(artifact, request.Audience);
            return Task.FromResult(new RenderedOutput(RenderTarget.PrintPdf, pdf, "application/pdf"));
        }

        if (request.Target is not (RenderTarget.AccessibleHtml or RenderTarget.PrintHtml))
        {
            throw new NotSupportedException(
                $"{request.Target} rendering is not part of this renderer; it produces HTML, SVG, and vector-first PDF.");
        }

        var html = RenderHtmlDocument(
            artifact.Revision.Document,
            request,
            artifact.Receipt,
            isUnapprovedPreview: false);
        return Task.FromResult(new RenderedOutput(request.Target, Encoding.UTF8.GetBytes(html), "text/html"));
    }

    /// <summary>
    /// The one semantic HTML core used by approved output and the sealed
    /// in-process draft preview. Approval controls the frame and output
    /// capability, not a second rendering implementation: the derivative
    /// between the exact-derivative markers is byte-for-byte the same for the
    /// same document and request.
    /// </summary>
    internal static string RenderHtmlDocument(
        ArtifactDocument document,
        RenderRequest request,
        ApprovalReceipt? approval,
        bool isUnapprovedPreview,
        bool isPortableSnapshot = false)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Target is not (RenderTarget.AccessibleHtml or RenderTarget.PrintHtml))
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        if (isPortableSnapshot
            ? isUnapprovedPreview || approval is not null || request.Audience != RenderAudience.Learner
            : isUnapprovedPreview == (approval is not null))
        {
            throw new ArgumentException(
                "HTML must be framed as either an approved output with a receipt or an unapproved preview without one.");
        }

        var builder = new StringBuilder();

        var language = Attribute(document.Language ?? "en");
        var documentTitle = document.Nodes.OfType<Heading>().FirstOrDefault()?.Text
            ?? (isUnapprovedPreview ? UnapprovedMark : "Approved artifact");
        var title = isUnapprovedPreview
            ? $"{UnapprovedMark} — {documentTitle}"
            : documentTitle;

        builder.Append("<!DOCTYPE html>\n");
        builder.Append("<html lang=\"").Append(language).Append('"');
        if (IsRightToLeft(document.Language))
        {
            // RC-10: the page direction follows the document language; per-pair
            // dir="auto" isolates segments, but the document itself must flow rtl.
            builder.Append(" dir=\"rtl\"");
        }

        builder.Append(">\n<head>\n<meta charset=\"utf-8\">\n");
        builder.Append("<title>").Append(Text(title)).Append("</title>\n");
        builder.Append("<style>\n").Append(BaseStyle);
        if (request.TextScalePercent != 100)
        {
            builder.Append("\nbody { font-size: ").Append(Mm(request.TextScalePercent)).Append("%; }");
        }

        if (request.Target == RenderTarget.PrintHtml)
        {
            builder.Append(PrintStyle);
        }

        if (isUnapprovedPreview)
        {
            builder.Append(UnapprovedPreviewStyle);
        }

        builder.Append("</style>\n</head>\n<body");
        if (isUnapprovedPreview)
        {
            builder.Append(" class=\"unapproved-draft-preview\" data-review-state=\"unapproved\"");
        }

        builder.Append(">\n");
        if (isUnapprovedPreview)
        {
            // Both literal banners remain in the document if its complete
            // contents are copied. The fixed watermark is visible over every
            // printed page; the embedded browser disables its own copy/print/
            // save shortcuts as a second, UI-level containment measure.
            builder.Append("<aside class=\"unapproved-draft-banner\" role=\"status\">")
                .Append(UnapprovedMark)
                .Append(" — preview only; approve the exact revision before use.</aside>\n")
                .Append("<div class=\"unapproved-draft-watermark\" aria-hidden=\"true\">")
                .Append(UnapprovedMark)
                .Append("</div>\n");
        }

        var derivative = RenderSemanticDerivative(document, request);
        if (isUnapprovedPreview)
        {
            builder.Append(ExactDerivativeStart).Append('\n');
        }

        builder.Append(derivative);
        if (isUnapprovedPreview)
        {
            builder.Append(ExactDerivativeEnd).Append('\n');
        }

        if (request.Audience == RenderAudience.Teacher && approval is not null)
        {
            builder.Append("<footer class=\"approval\"><p>Approved by ")
                .Append(Text(approval.ApprovedBy))
                .Append(" · revision ")
                .Append(approval.RevisionNumber)
                .Append(" · ")
                .Append(Text(approval.ApprovedAtUtc.ToString("u", System.Globalization.CultureInfo.InvariantCulture)))
                .Append("</p></footer>\n");
        }

        if (isUnapprovedPreview)
        {
            builder.Append("<p class=\"unapproved-draft-banner unapproved-draft-banner-end\">")
                .Append(UnapprovedMark)
                .Append(" — end of preview.</p>\n");
        }

        builder.Append("</body>\n</html>\n");
        return builder.ToString();
    }

    /// <summary>
    /// Canonical portable-project snapshot. A package carries the exact learner
    /// derivative, never the approval receipt or teacher-only material. Keeping
    /// this on the renderer's semantic core lets the hostile-package reader
    /// reconstruct and compare every byte from artifact.json and the bounded
    /// render profile without minting a synthetic approval identity.
    /// </summary>
    internal static byte[] RenderPortableSnapshot(
        ArtifactDocument document,
        RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Target != RenderTarget.AccessibleHtml
            || request.Audience != RenderAudience.Learner)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "A portable project snapshot is always learner-audience accessible HTML.");
        }

        return Encoding.UTF8.GetBytes(RenderHtmlDocument(
            document,
            request,
            approval: null,
            isUnapprovedPreview: false,
            isPortableSnapshot: true));
    }

    /// <summary>
    /// Exact body fragment shared by approved output and draft preview. It has
    /// no frame, approval receipt, preview marker, or output capability. Keeping
    /// it separate lets tests prove equality without adding comments or other
    /// tokens to persisted approved snapshots.
    /// </summary>
    internal static string RenderSemanticDerivative(
        ArtifactDocument document,
        RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);
        var builder = new StringBuilder();
        // Step rows group into ordered lists whose numbering derives from document
        // order and continues across page breaks (chunking preserves numbering).
        var stepNumber = 1;
        var index = 0;
        while (index < document.Nodes.Count)
        {
            switch (document.Nodes[index])
            {
                case StepRow:
                    var run = new List<StepRow>();
                    while (index < document.Nodes.Count && document.Nodes[index] is StepRow row)
                    {
                        run.Add(row);
                        index++;
                    }

                    AppendStepRun(builder, run, request, ref stepNumber);
                    continue;

                case PageBreak:
                    builder.Append("<div class=\"page-break\" aria-hidden=\"true\"></div>\n");
                    index++;
                    continue;

                default:
                    AppendNode(builder, document.Nodes[index], request);
                    index++;
                    continue;
            }
        }

        return builder.ToString();
    }

    private static void AppendStepRun(StringBuilder builder, List<StepRow> run, RenderRequest request, ref int stepNumber)
    {
        builder.Append("<ol class=\"steps\"");
        if (stepNumber > 1)
        {
            builder.Append(" start=\"").Append(stepNumber).Append('"');
        }

        builder.Append(">\n");

        foreach (var row in run)
        {
            builder.Append("<li>");
            if (row.Symbol is { } symbol)
            {
                builder.Append("<figure class=\"asset-placeholder step-symbol\" data-asset-id=\"")
                    .Append(Attribute(symbol.Asset.Value))
                    .Append("\"><figcaption>").Append(Text(symbol.AltText)).Append("</figcaption></figure>");
            }

            AppendRowTexts(builder, row, request);
            builder.Append("</li>\n");
            stepNumber++;
        }

        builder.Append("</ol>\n");
    }

    private static void AppendRowTexts(StringBuilder builder, StepRow row, RenderRequest request)
    {
        void Source()
        {
            if (row.SourceLocale is { } locale)
            {
                builder.Append("<p lang=\"").Append(Attribute(locale)).Append("\" dir=\"auto\">").Append(Text(row.Text)).Append("</p>");
            }
            else
            {
                builder.Append("<p>").Append(Text(row.Text)).Append("</p>");
            }
        }

        void Target()
        {
            if (row.TargetText is { } target && row.TargetLocale is { } locale)
            {
                builder.Append("<p lang=\"").Append(Attribute(locale)).Append("\" dir=\"auto\">").Append(Text(target)).Append("</p>");
            }
        }

        if (request.TargetLanguageFirst)
        {
            Target();
            Source();
        }
        else
        {
            Source();
            Target();
        }
    }

    private static void AppendNode(StringBuilder builder, DocumentNode node, RenderRequest request)
    {
        var audience = request.Audience;
        switch (node)
        {
            case Heading heading:
                builder.Append("<h").Append(heading.Level).Append('>')
                    .Append(Text(heading.Text))
                    .Append("</h").Append(heading.Level).Append(">\n");
                break;

            case Paragraph paragraph:
                builder.Append("<p>").Append(Text(paragraph.Text)).Append("</p>\n");
                break;

            case OrderedSteps steps:
                builder.Append("<ol class=\"steps\">\n");
                foreach (var step in steps.Steps)
                {
                    builder.Append("<li>").Append(Text(step)).Append("</li>\n");
                }

                builder.Append("</ol>\n");
                break;

            case UnorderedList list:
                builder.Append("<ul>\n");
                foreach (var item in list.Items)
                {
                    builder.Append("<li>").Append(Text(item)).Append("</li>\n");
                }

                builder.Append("</ul>\n");
                break;

            case TableNode table:
                builder.Append("<table>\n");
                if (table.HeaderRow is not null)
                {
                    builder.Append("<thead><tr>");
                    foreach (var cell in table.HeaderRow)
                    {
                        builder.Append("<th scope=\"col\">").Append(Text(cell)).Append("</th>");
                    }

                    builder.Append("</tr></thead>\n");
                }

                builder.Append("<tbody>\n");
                foreach (var row in table.Rows)
                {
                    builder.Append("<tr>");
                    foreach (var cell in row)
                    {
                        builder.Append("<td>").Append(Text(cell)).Append("</td>");
                    }

                    builder.Append("</tr>\n");
                }

                builder.Append("</tbody>\n</table>\n");
                break;

            case Card card:
                // A card is a physical object, not a document section: a bold
                // paragraph keeps the screen-reader heading outline honest (RC-11).
                builder.Append("<section class=\"card\">\n<p class=\"card-title\">").Append(Text(card.Title)).Append("</p>\n");
                if (!string.IsNullOrWhiteSpace(card.Body))
                {
                    builder.Append("<p>").Append(Text(card.Body)).Append("</p>\n");
                }

                builder.Append("</section>\n");
                break;

            case ImageReference image:
                // Asset bytes resolve when the asset kernel lands; until then the
                // placeholder is explicit, never a silent gap.
                builder.Append("<figure class=\"asset-placeholder\" data-asset-id=\"")
                    .Append(Attribute(image.Asset.Value))
                    .Append("\"><figcaption>").Append(Text(image.AltText)).Append("</figcaption></figure>\n");
                break;

            case BilingualPair pair:
                var (firstText, firstLocale, secondText, secondLocale) = request.TargetLanguageFirst
                    ? (pair.TargetText, pair.TargetLocale, pair.SourceText, pair.SourceLocale)
                    : (pair.SourceText, pair.SourceLocale, pair.TargetText, pair.TargetLocale);

                builder.Append("<div class=\"bilingual-pair\">\n<p lang=\"")
                    .Append(Attribute(firstLocale)).Append("\" dir=\"auto\">").Append(Text(firstText))
                    .Append("</p>\n<p lang=\"")
                    .Append(Attribute(secondLocale)).Append("\" dir=\"auto\">").Append(Text(secondText))
                    .Append("</p>\n</div>\n");
                break;

            case ChoiceSet choices:
                builder.Append("<ul class=\"choices\">\n");
                foreach (var option in choices.Options)
                {
                    builder.Append("<li>").Append(Text(option)).Append("</li>\n");
                }

                builder.Append("</ul>\n");
                break;

            case EvidenceLink evidence:
                builder.Append("<p>").Append(Text(evidence.Claim));
                if (audience == RenderAudience.Teacher)
                {
                    builder.Append(" <span class=\"evidence-source\">[").Append(Text(evidence.SourcePointer)).Append("]</span>");
                }

                builder.Append("</p>\n");
                break;

            case Citation citation:
                builder.Append("<p class=\"citation\"><cite>").Append(Text(citation.Text)).Append("</cite></p>\n");
                break;

            case TeacherOnlyNotice notice:
                if (audience == RenderAudience.Teacher)
                {
                    builder.Append("<aside class=\"teacher-only\"><p>").Append(Text(notice.Text)).Append("</p></aside>\n");
                }

                break;

            case VectorGraphic graphic:
                builder.Append("<figure class=\"vector-sheet\" role=\"img\" aria-label=\"")
                    .Append(Attribute(graphic.Description))
                    .Append("\">\n")
                    .Append(RenderSvg(graphic, standalone: false))
                    .Append("\n</figure>\n");
                break;

            default:
                throw new NotSupportedException($"Unknown node type {node.GetType().Name}.");
        }
    }

    private static string Text(string value) => WebUtility.HtmlEncode(value);

    private static string Attribute(string value) => WebUtility.HtmlEncode(value);

    /// <summary>Millimeter-exact SVG: physical units carry the press's dimensional accuracy onto paper.</summary>
    private static string RenderSvg(VectorGraphic graphic, bool standalone)
    {
        var builder = new StringBuilder();
        builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ")
            .Append(Mm(graphic.WidthMm)).Append(' ').Append(Mm(graphic.HeightMm))
            .Append("\" width=\"").Append(Mm(graphic.WidthMm)).Append("mm\" height=\"")
            .Append(Mm(graphic.HeightMm)).Append("mm\"");

        if (standalone)
        {
            builder.Append(" role=\"img\" aria-label=\"").Append(Attribute(graphic.Description)).Append('"');
        }

        builder.Append(">\n");

        foreach (var primitive in graphic.Primitives)
        {
            switch (primitive)
            {
                case LineSeg line:
                    builder.Append("<line x1=\"").Append(Mm(line.X1)).Append("\" y1=\"").Append(Mm(line.Y1))
                        .Append("\" x2=\"").Append(Mm(line.X2)).Append("\" y2=\"").Append(Mm(line.Y2))
                        .Append("\" stroke=\"#000\" stroke-width=\"").Append(Mm(line.StrokeWidthMm))
                        .Append("\" stroke-linecap=\"round\"");
                    if (line.Dashed)
                    {
                        builder.Append(" stroke-dasharray=\"3 2\"");
                    }

                    builder.Append("/>\n");
                    break;
                case CircleShape circle:
                    builder.Append("<circle cx=\"").Append(Mm(circle.CenterX)).Append("\" cy=\"").Append(Mm(circle.CenterY))
                        .Append("\" r=\"").Append(Mm(circle.RadiusMm))
                        .Append("\" fill=\"").Append(circle.Filled ? "#000" : "none")
                        .Append("\" stroke=\"#000\" stroke-width=\"").Append(Mm(circle.StrokeWidthMm)).Append("\"/>\n");
                    break;
                case RectShape rect:
                    builder.Append("<rect x=\"").Append(Mm(rect.X)).Append("\" y=\"").Append(Mm(rect.Y))
                        .Append("\" width=\"").Append(Mm(rect.WidthMm)).Append("\" height=\"").Append(Mm(rect.HeightMm))
                        .Append("\" fill=\"").Append(rect.Filled ? "#000" : "none")
                        .Append("\" stroke=\"#000\" stroke-width=\"").Append(Mm(rect.StrokeWidthMm)).Append("\"/>\n");
                    break;
                case TextLabel label:
                    builder.Append("<text x=\"").Append(Mm(label.X)).Append("\" y=\"").Append(Mm(label.Y))
                        .Append("\" font-size=\"").Append(Mm(label.FontSizeMm))
                        .Append("\" font-family=\"'Segoe UI', system-ui, sans-serif\" text-anchor=\"")
                        .Append(label.Anchor switch { TextAnchor.Start => "start", TextAnchor.End => "end", _ => "middle" })
                        .Append("\">").Append(Text(label.Text)).Append("</text>\n");
                    break;
                default:
                    throw new NotSupportedException($"Unknown vector primitive {primitive.GetType().Name}.");
            }
        }

        builder.Append("</svg>");
        return builder.ToString();
    }

    private static string Mm(double value)
        => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static bool IsRightToLeft(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return false;
        }

        var primary = language.Split('-')[0].ToLowerInvariant();
        return primary is "ar" or "he" or "fa" or "ur";
    }

    internal const string UnapprovedMark = "UNAPPROVED DRAFT — NOT FOR USE";

    internal const string ExactDerivativeStart = "<!-- exact-artifact-derivative:start -->";

    internal const string ExactDerivativeEnd = "<!-- exact-artifact-derivative:end -->";

    private const string BaseStyle =
        """
        body { font-family: "Segoe UI", system-ui, sans-serif; line-height: 1.5; margin: 2rem; }
        .card { border: 1px solid #888; padding: 0.75rem 1rem; margin: 0.75rem 0; }
        .card-title { font-weight: 700; margin: 0 0 0.3rem; }
        .bilingual-pair { margin: 0.5rem 0; }
        .bilingual-pair p { margin: 0.15rem 0; }
        .teacher-only { border-left: 4px solid #8a6d24; padding-left: 0.75rem; }
        .asset-placeholder { border: 1px dashed #888; padding: 0.5rem; }
        .step-symbol { display: inline-block; margin-right: 0.5rem; vertical-align: middle; }
        .steps li p { display: inline-block; margin: 0 0.4rem 0 0; }
        table { border-collapse: collapse; }
        th, td { border: 1px solid #666; padding: 0.3rem 0.6rem; text-align: left; }
        """;

    private const string PrintStyle =
        """

        @page { margin: 12mm; }
        body { margin: 0; }
        h1, h2, h3 { break-after: avoid; }
        li, .card, .bilingual-pair, tr { break-inside: avoid; }
        figure.vector-sheet { break-after: page; break-inside: avoid; margin: 0; }
        .page-break { break-after: page; }
        """;

    private const string UnapprovedPreviewStyle =
        """

        .unapproved-draft-preview { position: relative; }
        .unapproved-draft-banner {
          position: relative;
          z-index: 2147483647;
          border: 4px solid #8b0000;
          background: #fff4f4;
          color: #650000;
          font-weight: 800;
          padding: 0.75rem;
          margin: 0 0 1rem 0;
          text-align: center;
        }
        .unapproved-draft-banner-end { margin: 1rem 0 0 0; }
        .unapproved-draft-watermark {
          position: fixed;
          z-index: 2147483646;
          left: 8%;
          right: 8%;
          top: 42%;
          transform: rotate(-22deg);
          border: 0.16em solid rgba(139, 0, 0, 0.34);
          color: rgba(139, 0, 0, 0.34);
          font-size: 3.2rem;
          font-weight: 900;
          line-height: 1.15;
          padding: 0.35em;
          text-align: center;
          pointer-events: none;
        }
        @media print {
          .unapproved-draft-banner,
          .unapproved-draft-watermark { display: block !important; }
          .unapproved-draft-watermark { position: fixed; }
        }
        """;
}
