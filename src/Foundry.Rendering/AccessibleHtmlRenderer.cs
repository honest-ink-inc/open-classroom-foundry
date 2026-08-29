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

        if (request.Target is not (RenderTarget.AccessibleHtml or RenderTarget.PrintHtml))
        {
            throw new NotSupportedException(
                $"{request.Target} rendering arrives with the print pipeline; this renderer produces HTML and SVG.");
        }

        var html = Render(artifact, request);
        return Task.FromResult(new RenderedOutput(request.Target, Encoding.UTF8.GetBytes(html), "text/html"));
    }

    private static string Render(ApprovedArtifact artifact, RenderRequest request)
    {
        var document = artifact.Revision.Document;
        var builder = new StringBuilder();

        var language = Attribute(document.Language ?? "en");
        var title = document.Nodes.OfType<Heading>().FirstOrDefault()?.Text ?? "Approved artifact";

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

        builder.Append("</style>\n</head>\n<body>\n");

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

        if (request.Audience == RenderAudience.Teacher)
        {
            builder.Append("<footer class=\"approval\"><p>Approved by ")
                .Append(Text(artifact.Receipt.ApprovedBy))
                .Append(" · revision ")
                .Append(artifact.Receipt.RevisionNumber)
                .Append(" · ")
                .Append(Text(artifact.Receipt.ApprovedAtUtc.ToString("u", System.Globalization.CultureInfo.InvariantCulture)))
                .Append("</p></footer>\n");
        }

        builder.Append("</body>\n</html>\n");
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
}
