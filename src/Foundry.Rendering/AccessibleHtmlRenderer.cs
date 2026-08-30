// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
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
    internal const double MinimumTextScalePercent = 100;
    internal const double MaximumTextScalePercent = 200;

    private const int MaxEmbeddedAssetBytes = 16 * 1024 * 1024;
    private const int MaxDocumentNodes = 4096;
    private const int MaxDocumentRenderUnits = 16384;
    private const int MaxImageReferences = 512;
    private const int MaxCachedEmbeddedAssets = 256;
    private const long MaxSemanticTextCharacters = 2L * 1024 * 1024;
    private const long MaxEmbeddedDerivativeCharacters = 32L * 1024 * 1024;
    private const long MaxRenderedOutputCharacters = 32L * 1024 * 1024;
    private const int MaxRasterDimension = 16384;
    private const long MaxRasterPixels = 25_000_000;
    private const int MaxRasterStructureItems = 4096;
    private const int MaxSvgXmlNodes = 8192;
    private const int MaxSvgAttributes = 16384;
    private const int MaxSvgNestingDepth = 64;

    private static readonly uint[] PngCrcTable = BuildPngCrcTable();

    private static readonly HashSet<string> SafeSvgElements = new(StringComparer.Ordinal)
    {
        "svg", "g", "path", "circle", "ellipse", "rect", "line", "polyline", "polygon", "title", "desc",
    };

    private static readonly HashSet<string> SafeSvgAttributes = new(StringComparer.Ordinal)
    {
        "xmlns", "viewBox", "role", "aria-label", "x", "y", "x1", "y1", "x2", "y2", "cx", "cy", "r", "rx", "ry",
        "width", "height", "d", "points", "fill", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "stroke-dasharray",
        "transform", "opacity", "fill-opacity", "stroke-opacity",
    };

    private readonly IAssetCatalog? _assetCatalog;

    public AccessibleHtmlRenderer(IAssetCatalog? assetCatalog = null)
    {
        _assetCatalog = assetCatalog;
    }

    public Task<RenderedOutput> RenderAsync(ApprovedArtifact artifact, RenderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateTextScaleContract(request);

        if (request.Target == RenderTarget.Svg)
        {
            // Bound every SVG route before even the standalone-vector branch
            // begins materializing output. Asset sheets run a second, catalog-
            // aware pass below so their embedded bytes join the output budget.
            _ = EmbeddedAssetContext.Preflight(
                artifact.Revision.Document,
                assetCatalog: null,
                cancellationToken: cancellationToken);
            var graphics = artifact.Revision.Document.Nodes.OfType<VectorGraphic>().ToList();
            if (graphics.Count == 1 && artifact.Revision.Document.Nodes.Count == 1)
            {
                var svg = RenderSvg(graphics[0], standalone: true, cancellationToken);
                return Task.FromResult(new RenderedOutput(RenderTarget.Svg, Encoding.UTF8.GetBytes(svg), "image/svg+xml"));
            }

            if (graphics.Count > 0)
            {
                throw new NotSupportedException(
                    "Standalone SVG output requires a document containing exactly one vector sheet and no other nodes; mixed or multi-sheet documents export as print HTML.");
            }

            var assetSheet = RenderAssetSheetSvg(
                artifact.Revision.Document,
                request,
                _assetCatalog,
                cancellationToken);
            return Task.FromResult(new RenderedOutput(RenderTarget.Svg, Encoding.UTF8.GetBytes(assetSheet), "image/svg+xml"));
        }

        if (request.Target == RenderTarget.PrintPdf)
        {
            // The vector-first press: mm-exact geometry as true PDF operators,
            // deterministic bytes, no browser. Non-vector documents keep the
            // HTML print path (headless PDF conversion) until parity.
            var pdf = VectorPdfWriter.Write(artifact, request.Audience, cancellationToken);
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
            isUnapprovedPreview: false,
            _assetCatalog,
            cancellationToken: cancellationToken);
        return Task.FromResult(new RenderedOutput(request.Target, Encoding.UTF8.GetBytes(html), "text/html"));
    }

    /// <summary>
    /// Writes the exact learner snapshot admitted by the current portable
    /// package renderer. The approved-artifact parameter retains Gate B's type
    /// boundary, while the snapshot deliberately omits approval identity,
    /// asset bytes, and host-specific browser hints.
    /// </summary>
    public static Task<RenderedOutput> RenderPortableSnapshotAsync(
        ApprovedArtifact artifact,
        RenderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateTextScaleContract(request);
        var content = RenderPortableSnapshot(artifact.Revision.Document, request, cancellationToken);
        return Task.FromResult(new RenderedOutput(RenderTarget.AccessibleHtml, content, "text/html"));
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
        IAssetCatalog? assetCatalog = null,
        bool isPortableSnapshot = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateTextScaleContract(request);
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

        var embeddedAssets = EmbeddedAssetContext.Preflight(
            document,
            assetCatalog,
            supplementalText: request.Audience == RenderAudience.Teacher ? approval?.ApprovedBy : null,
            cancellationToken: cancellationToken);
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
        if (!isPortableSnapshot)
        {
            // WinForms WebBrowser otherwise falls back to its IE7 document
            // mode on an unconfigured host, where data-URI SVG symbols do not
            // load. Portable 0.7 snapshots are an exact-byte compatibility
            // contract and deliberately do not inherit this browser hint.
            builder.Append("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">\n");
        }

        builder.Append("<title>").Append(Text(title)).Append("</title>\n");
        builder.Append("<style>\n").Append(BaseStyle);
        if (assetCatalog is not null)
        {
            builder.Append(AssetStyle);
        }

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

        var derivative = RenderSemanticDerivative(document, request, embeddedAssets, cancellationToken);
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
        RenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateTextScaleContract(request);
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
            assetCatalog: null,
            isPortableSnapshot: true,
            cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Exact body fragment shared by approved output and draft preview. It has
    /// no frame, approval receipt, preview marker, or output capability. Keeping
    /// it separate lets tests prove equality without adding comments or other
    /// tokens to persisted approved snapshots.
    /// </summary>
    internal static string RenderSemanticDerivative(
        ArtifactDocument document,
        RenderRequest request,
        IAssetCatalog? assetCatalog = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateTextScaleContract(request);
        return RenderSemanticDerivative(
            document,
            request,
            EmbeddedAssetContext.Preflight(
                document,
                assetCatalog,
                cancellationToken: cancellationToken),
            cancellationToken);
    }

    private static string RenderSemanticDerivative(
        ArtifactDocument document,
        RenderRequest request,
        EmbeddedAssetContext embeddedAssets,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        // Step rows group into ordered lists whose numbering derives from document
        // order and continues across page breaks (chunking preserves numbering).
        var stepNumber = 1;
        var index = 0;
        while (index < document.Nodes.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (document.Nodes[index])
            {
                case StepRow:
                    var run = new List<StepRow>();
                    while (index < document.Nodes.Count && document.Nodes[index] is StepRow row)
                    {
                        run.Add(row);
                        index++;
                    }

                    AppendStepRun(builder, run, request, embeddedAssets, ref stepNumber, cancellationToken);
                    continue;

                case PageBreak:
                    builder.Append("<div class=\"page-break\" aria-hidden=\"true\"></div>\n");
                    index++;
                    continue;

                default:
                    AppendNode(builder, document.Nodes[index], request, embeddedAssets, cancellationToken);
                    index++;
                    continue;
            }
        }

        return builder.ToString();
    }

    private static void AppendStepRun(
        StringBuilder builder,
        List<StepRow> run,
        RenderRequest request,
        EmbeddedAssetContext embeddedAssets,
        ref int stepNumber,
        CancellationToken cancellationToken)
    {
        builder.Append("<ol class=\"steps\"");
        if (stepNumber > 1)
        {
            builder.Append(" start=\"").Append(stepNumber).Append('"');
        }

        builder.Append(">\n");

        foreach (var row in run)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.Append("<li>");
            if (row.Symbol is { } symbol)
            {
                AppendImage(builder, symbol, embeddedAssets, "asset step-symbol");
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

    private static void AppendNode(
        StringBuilder builder,
        DocumentNode node,
        RenderRequest request,
        EmbeddedAssetContext embeddedAssets,
        CancellationToken cancellationToken)
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
                    cancellationToken.ThrowIfCancellationRequested();
                    builder.Append("<li>").Append(Text(step)).Append("</li>\n");
                }

                builder.Append("</ol>\n");
                break;

            case UnorderedList list:
                builder.Append("<ul>\n");
                foreach (var item in list.Items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
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
                        cancellationToken.ThrowIfCancellationRequested();
                        builder.Append("<th scope=\"col\">").Append(Text(cell)).Append("</th>");
                    }

                    builder.Append("</tr></thead>\n");
                }

                builder.Append("<tbody>\n");
                foreach (var row in table.Rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    builder.Append("<tr>");
                    foreach (var cell in row)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
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
                AppendImage(builder, image, embeddedAssets, "asset");
                builder.Append('\n');
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
                    cancellationToken.ThrowIfCancellationRequested();
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
                    .Append(RenderSvg(graphic, standalone: false, cancellationToken))
                    .Append("\n</figure>\n");
                break;

            default:
                throw new NotSupportedException($"Unknown node type {node.GetType().Name}.");
        }
    }

    private static string Text(string value) => WebUtility.HtmlEncode(value);

    private static string Attribute(string value) => WebUtility.HtmlEncode(value);

    private static void AppendImage(
        StringBuilder builder,
        ImageReference image,
        EmbeddedAssetContext embeddedAssets,
        string cssClass)
    {
        if (!embeddedAssets.HasCatalog)
        {
            // Frozen portable snapshots written before the asset-aware renderer
            // carry this explicit semantic fallback. Production output and Gate
            // B always supply a catalog and therefore never take this path.
            var placeholderClass = cssClass.Contains("step-symbol", StringComparison.Ordinal)
                ? "asset-placeholder step-symbol"
                : "asset-placeholder";
            builder.Append("<figure class=\"").Append(placeholderClass).Append("\" data-asset-id=\"")
                .Append(Attribute(image.Asset.Value))
                .Append("\"><figcaption>").Append(Text(image.AltText)).Append("</figcaption></figure>");
            return;
        }

        var embedded = embeddedAssets.Get(image.Asset);
        builder.Append("<figure class=\"").Append(cssClass).Append("\" data-asset-id=\"")
            .Append(Attribute(image.Asset.Value))
            .Append("\"><img src=\"data:").Append(Attribute(embedded.MimeType)).Append(";base64,")
            .Append(embedded.Base64)
            .Append("\" alt=\"").Append(Attribute(image.AltText)).Append("\"></figure>");
    }

    /// <summary>
    /// Enforces the same finite 100-through-200 text-scale contract used by
    /// persisted render profiles. Export coordinators call this before choosing
    /// a native or browser-backed output path so routing cannot bypass it.
    /// </summary>
    public static void ValidateTextScaleContract(RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!double.IsFinite(request.TextScalePercent)
            || request.TextScalePercent < MinimumTextScalePercent
            || request.TextScalePercent > MaximumTextScalePercent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"Text scale must be a finite percentage from {MinimumTextScalePercent:0} through {MaximumTextScalePercent:0}.");
        }
    }

    private static EmbeddedImageSource ResolveEmbeddedImageSource(AssetId id, IAssetCatalog assetCatalog)
    {
        var provenance = assetCatalog.Find(id)
            ?? throw new InvalidOperationException(
                $"Asset '{id.Value}' has no provenance; rendering refused.");
        if (!assetCatalog.TryGetContent(id, out var content, out var mimeType))
        {
            throw new InvalidOperationException(
                $"Asset '{id.Value}' has no retrievable content; rendering refused.");
        }

        if (!string.Equals(mimeType, provenance.MimeType, StringComparison.Ordinal)
            || content.IsEmpty
            || content.Length > MaxEmbeddedAssetBytes)
        {
            throw new InvalidOperationException(
                $"Asset '{id.Value}' does not satisfy its bounded MIME contract; rendering refused.");
        }

        // IAssetCatalog exposes ReadOnlyMemory, not an immutable ownership
        // guarantee. Own the admitted bytes before hashing so a mutable custom
        // catalog cannot change them between validation and Base64 encoding.
        var ownedContent = content.ToArray();
        var actualHash = Convert.ToHexString(SHA256.HashData(ownedContent));
        if (!actualHash.Equals(provenance.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Asset '{id.Value}' does not match its recorded SHA-256; rendering refused.");
        }

        ValidateImageContent(ownedContent, mimeType, id);
        return new EmbeddedImageSource(mimeType, ownedContent);
    }

    private static string RenderAssetSheetSvg(
        ArtifactDocument document,
        RenderRequest request,
        IAssetCatalog? assetCatalog,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var embeddedAssets = EmbeddedAssetContext.Preflight(
            document,
            assetCatalog,
            requireReferencedAssets: true,
            cancellationToken: cancellationToken);
        var title = document.Nodes.OfType<Heading>().FirstOrDefault()?.Text ?? "Visual support";
        var documentLocale = document.Language ?? "en";
        var items = new List<AssetSheetItem>();
        ImageReference? pendingImage = null;
        foreach (var node in document.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (node)
            {
                case Heading:
                    break;
                case ImageReference image:
                    if (pendingImage is not null)
                    {
                        items.Add(new AssetSheetItem(
                            [new AssetSheetText(pendingImage.AltText, documentLocale)],
                            pendingImage));
                    }

                    pendingImage = image;
                    break;
                case Card card:
                    items.Add(new AssetSheetItem(
                        string.Equals(card.Title, card.Body, StringComparison.Ordinal)
                            ? [new AssetSheetText(card.Title, documentLocale)]
                            :
                            [
                                new AssetSheetText(card.Title, documentLocale),
                                new AssetSheetText(card.Body, documentLocale),
                            ],
                        pendingImage));
                    pendingImage = null;
                    break;
                case StepRow row:
                    if (pendingImage is not null)
                    {
                        items.Add(new AssetSheetItem(
                            [new AssetSheetText(pendingImage.AltText, documentLocale)],
                            pendingImage));
                        pendingImage = null;
                    }

                    var source = new AssetSheetText(row.Text, row.SourceLocale ?? documentLocale);
                    var texts = new List<AssetSheetText>();
                    if (request.TargetLanguageFirst && row.TargetText is not null)
                    {
                        texts.Add(new AssetSheetText(row.TargetText, row.TargetLocale ?? documentLocale));
                    }

                    texts.Add(source);
                    if (!request.TargetLanguageFirst && row.TargetText is not null)
                    {
                        texts.Add(new AssetSheetText(row.TargetText, row.TargetLocale ?? documentLocale));
                    }

                    items.Add(new AssetSheetItem(texts, row.Symbol));
                    break;
                case TeacherOnlyNotice notice when request.Audience == RenderAudience.Teacher:
                    items.Add(new AssetSheetItem(
                        [new AssetSheetText(notice.Text, documentLocale)],
                        null));
                    break;
                case TeacherOnlyNotice:
                    break;
                default:
                    throw new NotSupportedException(
                        $"Standalone symbol SVG does not support {node.GetType().Name}; use print or accessible HTML.");
            }
        }

        if (pendingImage is not null)
        {
            items.Add(new AssetSheetItem(
                [new AssetSheetText(pendingImage.AltText, documentLocale)],
                pendingImage));
        }

        if (items.Count == 0)
        {
            throw new NotSupportedException(
                "Standalone SVG output requires one vector sheet or a visual-support document with learner-visible content.");
        }

        var scale = request.TextScalePercent / 100d;
        var titleFontSize = 26d * scale;
        var bodyFontSize = 20d * scale;
        var lineAdvance = 25d * scale;
        var maximumRunes = Math.Max(12, (int)Math.Floor(38d / scale));
        var layouts = items.Select(item =>
        {
            var lines = new List<AssetSheetLine>();
            foreach (var text in item.Texts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lines.AddRange(WrapSvgText(text.Text, maximumRunes, cancellationToken)
                    .Select(line => new AssetSheetLine(line, text.Locale)));
            }

            return new AssetSheetLayout(
                item,
                lines,
                Math.Max(item.Image is null ? 68 : 112, 32 + (lines.Count * lineAdvance)));
        }).ToList();

        const int width = 816;
        const int margin = 24;
        var titleHeight = Math.Max(58, titleFontSize + 32);
        var height = margin + titleHeight + layouts.Sum(layout => layout.Height + 12) + margin;
        var documentIsRtl = IsRightToLeft(documentLocale);
        var titleX = documentIsRtl ? width - margin : margin;
        var titleAnchor = documentIsRtl ? "end" : "start";
        var description = BuildAssetSheetDescription(items);
        var builder = new StringBuilder();
        builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ")
            .Append(width).Append(' ').Append(Mm(height))
            .Append("\" width=\"").Append(width).Append("\" height=\"").Append(Mm(height))
            .Append("\" role=\"img\" aria-labelledby=\"asset-sheet-title\" aria-describedby=\"asset-sheet-description\"");
        AppendSvgLanguageAttributes(builder, documentLocale);
        builder.Append(">\n")
            .Append("<title id=\"asset-sheet-title\">").Append(Text(title)).Append("</title>\n")
            .Append("<desc id=\"asset-sheet-description\">").Append(Text(description)).Append("</desc>\n")
            .Append("<rect width=\"100%\" height=\"100%\" fill=\"#fff\"/>\n")
            .Append("<text x=\"").Append(titleX).Append("\" y=\"").Append(Mm(margin + titleFontSize))
            .Append("\" font-family=\"Segoe UI, sans-serif\" font-size=\"").Append(Mm(titleFontSize))
            .Append("\" font-weight=\"700\" text-anchor=\"").Append(titleAnchor).Append('"');
        AppendSvgLanguageAttributes(builder, documentLocale);
        builder.Append('>').Append(Text(title)).Append("</text>\n");

        var y = margin + titleHeight;
        for (var itemIndex = 0; itemIndex < layouts.Count; itemIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var layout = layouts[itemIndex];
            builder.Append("<g role=\"group\" aria-label=\"")
                .Append(Attribute(BuildAssetSheetItemDescription(layout.Item, itemIndex + 1))).Append("\">\n")
                .Append("<rect x=\"").Append(margin).Append("\" y=\"").Append(Mm(y))
                .Append("\" width=\"").Append(width - (2 * margin)).Append("\" height=\"").Append(Mm(layout.Height))
                .Append("\" rx=\"10\" fill=\"none\" stroke=\"#333\" stroke-width=\"2\"/>\n");
            var textLeft = margin + 18;
            var textRight = width - margin - 18;
            if (layout.Item.Image is { } image)
            {
                var embedded = embeddedAssets.Get(image.Asset);
                var imageX = documentIsRtl ? width - margin - 100 : margin + 12;
                builder.Append("<g role=\"img\" aria-label=\"").Append(Attribute(image.AltText)).Append("\">")
                    .Append("<image x=\"").Append(imageX).Append("\" y=\"").Append(Mm(y + 12))
                    .Append("\" width=\"88\" height=\"88\" preserveAspectRatio=\"xMidYMid meet\" href=\"data:")
                    .Append(Attribute(embedded.MimeType)).Append(";base64,").Append(embedded.Base64).Append("\"/>")
                    .Append("</g>\n");
                if (documentIsRtl)
                {
                    textRight -= 100;
                }
                else
                {
                    textLeft += 100;
                }
            }

            var textY = y + 20 + bodyFontSize;
            foreach (var line in layout.Lines)
            {
                var lineIsRtl = IsRightToLeft(line.Locale);
                builder.Append("<text x=\"").Append(lineIsRtl ? textRight : textLeft)
                    .Append("\" y=\"").Append(Mm(textY))
                    .Append("\" font-family=\"Segoe UI, sans-serif\" font-size=\"").Append(Mm(bodyFontSize))
                    .Append("\" text-anchor=\"").Append(lineIsRtl ? "end" : "start").Append('"');
                AppendSvgLanguageAttributes(builder, line.Locale ?? documentLocale);
                builder.Append('>').Append(Text(line.Text)).Append("</text>\n");
                textY += lineAdvance;
            }

            builder.Append("</g>\n");
            y += layout.Height + 12;
        }

        builder.Append("</svg>");
        return builder.ToString();
    }

    private static void AppendSvgLanguageAttributes(StringBuilder builder, string locale)
    {
        builder.Append(" lang=\"").Append(Attribute(locale))
            .Append("\" xml:lang=\"").Append(Attribute(locale))
            .Append("\" direction=\"").Append(IsRightToLeft(locale) ? "rtl" : "ltr")
            .Append("\" unicode-bidi=\"plaintext\"");
    }

    private static string BuildAssetSheetDescription(IReadOnlyList<AssetSheetItem> items)
        => string.Join(" ", items.Select((item, index) => BuildAssetSheetItemDescription(item, index + 1)));

    private static string BuildAssetSheetItemDescription(AssetSheetItem item, int oneBasedIndex)
    {
        var parts = new List<string>();
        if (item.Image is { } image)
        {
            parts.Add($"symbol: {image.AltText}");
        }

        parts.AddRange(item.Texts.Select(text =>
            text.Locale is null ? text.Text : $"{text.Text} [{text.Locale}]"));
        return $"Item {oneBasedIndex}: {string.Join("; ", parts)}.";
    }

    internal static IEnumerable<string> WrapSvgText(
        string value,
        int maximumRunes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRunes);

        foreach (var paragraph in value.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                yield return string.Empty;
                continue;
            }

            var line = new StringBuilder();
            var lineRunes = 0;
            foreach (var word in words)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remaining = new StringBuilder();
                var remainingRunes = 0;
                foreach (var rune in word.EnumerateRunes())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (remainingRunes == maximumRunes)
                    {
                        if (line.Length > 0)
                        {
                            yield return line.ToString();
                            line.Clear();
                            lineRunes = 0;
                        }

                        yield return remaining.ToString();
                        remaining.Clear();
                        remainingRunes = 0;
                    }

                    remaining.Append(rune);
                    remainingRunes++;
                }

                if (lineRunes > 0 && lineRunes + 1 + remainingRunes > maximumRunes)
                {
                    yield return line.ToString();
                    line.Clear();
                    lineRunes = 0;
                }

                if (lineRunes > 0)
                {
                    line.Append(' ');
                    lineRunes++;
                }

                line.Append(remaining);
                lineRunes += remainingRunes;
            }

            if (line.Length > 0)
            {
                yield return line.ToString();
            }
        }
    }

    private static SemanticTextBudget MeasureSemanticText(
        ArtifactDocument document,
        string? supplementalText,
        CancellationToken cancellationToken)
    {
        long characters = 0;
        long encodedUpperBound = 0;

        AddSemanticText(document.Language);
        AddSemanticText(supplementalText);
        foreach (var node in document.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (node)
            {
                case Heading heading:
                    AddSemanticText(heading.Text);
                    break;
                case Paragraph paragraph:
                    AddSemanticText(paragraph.Text);
                    break;
                case OrderedSteps steps:
                    foreach (var step in steps.Steps)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        AddSemanticText(step);
                    }

                    break;
                case UnorderedList list:
                    foreach (var item in list.Items)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        AddSemanticText(item);
                    }

                    break;
                case TableNode table:
                    if (table.HeaderRow is not null)
                    {
                        foreach (var cell in table.HeaderRow)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            AddSemanticText(cell);
                        }
                    }

                    foreach (var row in table.Rows)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        foreach (var cell in row)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            AddSemanticText(cell);
                        }
                    }

                    break;
                case Card card:
                    AddSemanticText(card.Title);
                    AddSemanticText(card.Body);
                    break;
                case ImageReference image:
                    AddImageText(image);
                    break;
                case BilingualPair pair:
                    AddSemanticText(pair.SourceText);
                    AddSemanticText(pair.TargetText);
                    AddSemanticText(pair.SourceLocale);
                    AddSemanticText(pair.TargetLocale);
                    break;
                case ChoiceSet choices:
                    foreach (var option in choices.Options)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        AddSemanticText(option);
                    }

                    break;
                case EvidenceLink evidence:
                    AddSemanticText(evidence.Claim);
                    AddSemanticText(evidence.SourcePointer);
                    break;
                case Citation citation:
                    AddSemanticText(citation.Text);
                    break;
                case TeacherOnlyNotice notice:
                    AddSemanticText(notice.Text);
                    break;
                case StepRow row:
                    AddSemanticText(row.Text);
                    AddSemanticText(row.TargetText);
                    AddSemanticText(row.SourceLocale);
                    AddSemanticText(row.TargetLocale);
                    if (row.Symbol is not null)
                    {
                        AddImageText(row.Symbol);
                    }

                    break;
                case VectorGraphic graphic:
                    AddSemanticText(graphic.Description);
                    foreach (var label in graphic.Primitives.OfType<TextLabel>())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        AddSemanticText(label.Text);
                    }

                    break;
                case PageBreak:
                    break;
            }
        }

        return new SemanticTextBudget(encodedUpperBound);

        void AddImageText(ImageReference image)
        {
            AddSemanticText(image.Asset.Value);
            AddSemanticText(image.AltText);
        }

        void AddSemanticText(string? value)
        {
            if (value is null)
            {
                return;
            }

            characters = checked(characters + value.Length);
            if (characters > MaxSemanticTextCharacters)
            {
                throw new InvalidOperationException(
                    $"Rendering refused because semantic text exceeds the bounded {MaxSemanticTextCharacters}-character limit.");
            }

            foreach (var character in value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                encodedUpperBound = checked(encodedUpperBound + HtmlEncodedCharacterUpperBound(character));
            }
        }
    }

    private static int HtmlEncodedCharacterUpperBound(char character)
        => character switch
        {
            '&' => 5,
            '<' or '>' => 4,
            '"' => 6,
            '\'' => 5,
            <= '\x7F' when !char.IsControl(character) => 1,
            _ => 10,
        };

    private static void ValidateProjectedOutputBudget(
        long renderUnits,
        SemanticTextBudget semanticText,
        long embeddedDerivativeCharacters)
    {
        const int MaximumSemanticEmissionMultiplicity = 4;
        const int MaximumStructuralCharactersPerRenderUnit = 512;
        const int FixedDocumentCharacters = 64 * 1024;

        var projectedCharacters = checked(
            FixedDocumentCharacters
            + (renderUnits * MaximumStructuralCharactersPerRenderUnit)
            + (semanticText.EncodedUpperBound * MaximumSemanticEmissionMultiplicity)
            + embeddedDerivativeCharacters);
        if (projectedCharacters > MaxRenderedOutputCharacters)
        {
            throw new InvalidOperationException(
                $"Rendering refused because HTML/SVG output exceeds the bounded {MaxRenderedOutputCharacters}-character budget.");
        }
    }

    private sealed class EmbeddedAssetContext
    {
        private readonly IReadOnlyDictionary<AssetId, EmbeddedImage> _cache;

        private EmbeddedAssetContext(
            bool hasCatalog,
            IReadOnlyDictionary<AssetId, EmbeddedImage> cache)
        {
            HasCatalog = hasCatalog;
            _cache = cache;
        }

        internal bool HasCatalog { get; }

        internal static EmbeddedAssetContext Preflight(
            ArtifactDocument document,
            IAssetCatalog? assetCatalog,
            bool requireReferencedAssets = false,
            string? supplementalText = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (document.Nodes.Count > MaxDocumentNodes)
            {
                throw new InvalidOperationException(
                    $"Rendering refused because the document exceeds the bounded {MaxDocumentNodes}-node limit.");
            }

            long renderUnits = document.Nodes.Count;
            var referenceCount = 0;
            var occurrences = new Dictionary<AssetId, int>();
            foreach (var node in document.Nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                renderUnits += node switch
                {
                    OrderedSteps steps => steps.Steps.Count,
                    UnorderedList list => list.Items.Count,
                    TableNode table => (table.HeaderRow?.Count ?? 0)
                        + table.Rows.Count
                        + table.Rows.Sum(row => (long)row.Count),
                    ChoiceSet choices => choices.Options.Count,
                    VectorGraphic graphic => graphic.Primitives.Count,
                    _ => 0,
                };
                if (renderUnits > MaxDocumentRenderUnits)
                {
                    throw new InvalidOperationException(
                        $"Rendering refused because the document exceeds the bounded {MaxDocumentRenderUnits}-unit limit.");
                }

                var image = node switch
                {
                    ImageReference direct => direct,
                    StepRow { Symbol: { } symbol } => symbol,
                    _ => null,
                };
                if (image is null)
                {
                    continue;
                }

                referenceCount++;
                if (referenceCount > MaxImageReferences)
                {
                    throw new InvalidOperationException(
                        $"Rendering refused because the document exceeds the bounded {MaxImageReferences}-image-reference limit.");
                }

                occurrences.TryGetValue(image.Asset, out var count);
                occurrences[image.Asset] = count + 1;
                if (occurrences.Count > MaxCachedEmbeddedAssets)
                {
                    throw new InvalidOperationException(
                        $"Rendering refused because the document exceeds the bounded {MaxCachedEmbeddedAssets}-asset cache limit.");
                }
            }

            var semanticText = MeasureSemanticText(document, supplementalText, cancellationToken);

            if (assetCatalog is null)
            {
                if (requireReferencedAssets && referenceCount > 0)
                {
                    throw new NotSupportedException(
                        "Symbol SVG output requires the exact local asset catalog used to build the artifact.");
                }

                ValidateProjectedOutputBudget(renderUnits, semanticText, embeddedDerivativeCharacters: 0);

                return new EmbeddedAssetContext(
                    hasCatalog: false,
                    new Dictionary<AssetId, EmbeddedImage>());
            }

            var sources = new Dictionary<AssetId, EmbeddedImageSource>(occurrences.Count);
            long derivativeCharacters = 0;
            foreach (var (id, count) in occurrences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var source = ResolveEmbeddedImageSource(id, assetCatalog);
                var base64Characters = ((long)source.Content.Length + 2) / 3 * 4;
                var perReferenceCharacters = checked(base64Characters + source.MimeType.Length + 13);
                derivativeCharacters = checked(derivativeCharacters + (perReferenceCharacters * count));
                if (derivativeCharacters > MaxEmbeddedDerivativeCharacters)
                {
                    throw new InvalidOperationException(
                        "Rendering refused because repeated image references exceed the cumulative embedded-derivative budget.");
                }

                sources.Add(id, source);
            }

            ValidateProjectedOutputBudget(renderUnits, semanticText, derivativeCharacters);

            var cache = new Dictionary<AssetId, EmbeddedImage>(sources.Count);
            foreach (var (id, source) in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cache.Add(id, new EmbeddedImage(
                    source.MimeType,
                    Convert.ToBase64String(source.Content.Span)));
            }

            return new EmbeddedAssetContext(hasCatalog: true, cache);
        }

        internal EmbeddedImage Get(AssetId id)
            => _cache.TryGetValue(id, out var embedded)
                ? embedded
                : throw new InvalidOperationException(
                    $"Asset '{id.Value}' was not admitted by the bounded rendering preflight.");
    }

    private static void ValidateImageContent(ReadOnlySpan<byte> content, string mimeType, AssetId id)
    {
        if (!IsSupportedSelfContainedImage(content, mimeType))
        {
            throw new InvalidOperationException(
                $"Asset '{id.Value}' is not a supported, self-contained image; rendering refused.");
        }
    }

    /// <summary>
    /// The shared admission boundary for image bytes that may be embedded in a
    /// project, open symbol pack, HTML document, or SVG sheet. SVG is a document
    /// format, so it receives a strict passive-element/attribute allowlist.
    /// Raster formats are structurally walked within fixed byte, structure,
    /// dimension, and pixel bounds before a browser or decoder sees them.
    /// </summary>
    public static bool IsSupportedSelfContainedImage(ReadOnlySpan<byte> content, string mimeType)
    {
        if (content.IsEmpty || content.Length > MaxEmbeddedAssetBytes)
        {
            return false;
        }

        return mimeType switch
        {
            "image/png" => IsStructurallyValidPng(content),
            "image/jpeg" => IsStructurallyValidJpeg(content),
            "image/svg+xml" => IsSafeSvg(content),
            _ => false,
        };
    }

    private static bool IsStructurallyValidPng(ReadOnlySpan<byte> content)
    {
        ReadOnlySpan<byte> signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (content.Length < 8 || !content[..8].SequenceEqual(signature))
        {
            return false;
        }

        var position = 8;
        var structureItems = 0;
        var sawHeader = false;
        var sawPalette = false;
        var sawImageData = false;
        var endedImageData = false;
        long imageDataBytes = 0;
        byte colorType = 0;
        while (position < content.Length)
        {
            if (++structureItems > MaxRasterStructureItems || content.Length - position < 12)
            {
                return false;
            }

            var dataLengthValue = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(position, 4));
            if (dataLengthValue > int.MaxValue)
            {
                return false;
            }

            var dataLength = (int)dataLengthValue;
            if (dataLength > content.Length - position - 12)
            {
                return false;
            }

            var chunkType = content.Slice(position + 4, 4);
            var chunkData = content.Slice(position + 8, dataLength);
            var recordedCrc = BinaryPrimitives.ReadUInt32BigEndian(
                content.Slice(position + 8 + dataLength, 4));
            if (!IsPngChunkType(chunkType)
                || ComputePngCrc(chunkType, chunkData) != recordedCrc)
            {
                return false;
            }

            var isHeader = chunkType.SequenceEqual("IHDR"u8);
            var isPalette = chunkType.SequenceEqual("PLTE"u8);
            var isImageData = chunkType.SequenceEqual("IDAT"u8);
            var isEnd = chunkType.SequenceEqual("IEND"u8);
            if (!sawHeader && !isHeader)
            {
                return false;
            }

            if (isHeader)
            {
                if (sawHeader || dataLength != 13)
                {
                    return false;
                }

                var width = BinaryPrimitives.ReadUInt32BigEndian(chunkData[..4]);
                var height = BinaryPrimitives.ReadUInt32BigEndian(chunkData.Slice(4, 4));
                var bitDepth = chunkData[8];
                colorType = chunkData[9];
                if (!IsBoundedRasterDimensions(width, height)
                    || !IsSupportedPngBitDepth(bitDepth, colorType)
                    || chunkData[10] != 0
                    || chunkData[11] != 0
                    || chunkData[12] > 1)
                {
                    return false;
                }

                sawHeader = true;
            }
            else if (isPalette)
            {
                if (sawPalette
                    || sawImageData
                    || dataLength is < 3 or > 768
                    || dataLength % 3 != 0)
                {
                    return false;
                }

                sawPalette = true;
            }
            else if (isImageData)
            {
                if (endedImageData || (colorType == 3 && !sawPalette))
                {
                    return false;
                }

                sawImageData = true;
                imageDataBytes += dataLength;
                if (imageDataBytes > MaxEmbeddedAssetBytes)
                {
                    return false;
                }
            }
            else
            {
                endedImageData |= sawImageData;
                if (isEnd)
                {
                    return dataLength == 0
                        && sawHeader
                        && sawImageData
                        && imageDataBytes > 0
                        && position + 12 == content.Length;
                }

                // Unknown critical chunks change the decoding contract. APNG's
                // animation chunks are ancillary but can multiply decoded
                // frames, so this single-frame admission boundary refuses them.
                if (char.IsAsciiLetterUpper((char)chunkType[0])
                    && !chunkType.SequenceEqual("PLTE"u8)
                    || chunkType.SequenceEqual("acTL"u8)
                    || chunkType.SequenceEqual("fcTL"u8)
                    || chunkType.SequenceEqual("fdAT"u8))
                {
                    return false;
                }
            }

            position += 12 + dataLength;
        }

        return false;
    }

    private static bool IsStructurallyValidJpeg(ReadOnlySpan<byte> content)
    {
        if (content.Length < 4 || content[0] != 0xFF || content[1] != 0xD8)
        {
            return false;
        }

        var position = 2;
        var structureItems = 0;
        var sawFrame = false;
        var sawScan = false;
        var sawEntropy = false;
        var sawQuantizationTable = false;
        var sawHuffmanTable = false;
        var frameComponents = new HashSet<byte>();
        while (position < content.Length)
        {
            if (++structureItems > MaxRasterStructureItems || content[position] != 0xFF)
            {
                return false;
            }

            while (position < content.Length && content[position] == 0xFF)
            {
                position++;
            }

            if (position >= content.Length)
            {
                return false;
            }

            var marker = content[position++];
            if (marker is 0x00 or 0x01 or 0xD8 or (>= 0xD0 and <= 0xD7))
            {
                return false;
            }

            if (marker == 0xD9)
            {
                return position == content.Length
                    && sawFrame
                    && sawScan
                    && sawEntropy
                    && sawQuantizationTable
                    && sawHuffmanTable;
            }

            if (position + 2 > content.Length)
            {
                return false;
            }

            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(position, 2));
            if (segmentLength < 2 || segmentLength > content.Length - position)
            {
                return false;
            }

            var payload = content.Slice(position + 2, segmentLength - 2);
            position += segmentLength;
            if (IsJpegStartOfFrame(marker))
            {
                if (marker is not (0xC0 or 0xC1 or 0xC2)
                    || sawFrame
                    || payload.Length < 9
                    || payload[0] != 8)
                {
                    return false;
                }

                var height = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(1, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(payload.Slice(3, 2));
                var componentCount = payload[5];
                if (!IsBoundedRasterDimensions(width, height)
                    || componentCount is < 1 or > 4
                    || payload.Length != 6 + (3 * componentCount))
                {
                    return false;
                }

                for (var index = 0; index < componentCount; index++)
                {
                    var componentOffset = 6 + (index * 3);
                    var componentId = payload[componentOffset];
                    var sampling = payload[componentOffset + 1];
                    if (!frameComponents.Add(componentId)
                        || (sampling >> 4) is < 1 or > 4
                        || (sampling & 0x0F) is < 1 or > 4
                        || payload[componentOffset + 2] > 3)
                    {
                        return false;
                    }
                }

                sawFrame = true;
            }
            else if (marker == 0xDB)
            {
                sawQuantizationTable = payload.Length >= 65;
            }
            else if (marker == 0xC4)
            {
                sawHuffmanTable = payload.Length >= 17;
            }
            else if (marker == 0xDA)
            {
                if (!sawFrame || !sawQuantizationTable || !sawHuffmanTable || payload.Length < 6)
                {
                    return false;
                }

                var scanComponentCount = payload[0];
                if (scanComponentCount is < 1 or > 4
                    || payload.Length != 4 + (2 * scanComponentCount))
                {
                    return false;
                }

                var scanComponents = new HashSet<byte>();
                for (var index = 0; index < scanComponentCount; index++)
                {
                    var componentOffset = 1 + (index * 2);
                    if (!frameComponents.Contains(payload[componentOffset])
                        || !scanComponents.Add(payload[componentOffset])
                        || (payload[componentOffset + 1] >> 4) > 3
                        || (payload[componentOffset + 1] & 0x0F) > 3)
                    {
                        return false;
                    }
                }

                sawScan = true;
                var scanHasEntropy = false;
                var markerStart = -1;
                while (position < content.Length)
                {
                    if (content[position] != 0xFF)
                    {
                        scanHasEntropy = true;
                        position++;
                        continue;
                    }

                    markerStart = position++;
                    while (position < content.Length && content[position] == 0xFF)
                    {
                        position++;
                    }

                    if (position >= content.Length)
                    {
                        return false;
                    }

                    var scanMarker = content[position];
                    if (scanMarker == 0x00)
                    {
                        scanHasEntropy = true;
                        position++;
                        continue;
                    }

                    if (scanMarker is >= 0xD0 and <= 0xD7)
                    {
                        position++;
                        continue;
                    }

                    break;
                }

                if (!scanHasEntropy || markerStart < 0)
                {
                    return false;
                }

                sawEntropy = true;
                position = markerStart;
            }
        }

        return false;
    }

    private static bool IsPngChunkType(ReadOnlySpan<byte> chunkType)
        => chunkType.Length == 4
            && chunkType.ToArray().All(value => char.IsAsciiLetter((char)value));

    private static bool IsSupportedPngBitDepth(byte bitDepth, byte colorType)
        => colorType switch
        {
            0 => bitDepth is 1 or 2 or 4 or 8 or 16,
            2 => bitDepth is 8 or 16,
            3 => bitDepth is 1 or 2 or 4 or 8,
            4 or 6 => bitDepth is 8 or 16,
            _ => false,
        };

    private static bool IsJpegStartOfFrame(byte marker)
        => marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC);

    private static bool IsBoundedRasterDimensions(long width, long height)
        => width is > 0 and <= MaxRasterDimension
            && height is > 0 and <= MaxRasterDimension
            && width <= MaxRasterPixels / height;

    private static uint ComputePngCrc(ReadOnlySpan<byte> chunkType, ReadOnlySpan<byte> chunkData)
    {
        var crc = uint.MaxValue;
        foreach (var value in chunkType)
        {
            crc = PngCrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        foreach (var value in chunkData)
        {
            crc = PngCrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static uint[] BuildPngCrcTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0
                    ? 0xEDB88320u ^ (value >> 1)
                    : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }

    private static bool IsSafeSvg(ReadOnlySpan<byte> content)
    {
        try
        {
            var ownedContent = content.ToArray();
            if (!HasBoundedSvgStructure(ownedContent))
            {
                return false;
            }

            using var stream = new MemoryStream(ownedContent, writable: false);
            using var reader = XmlReader.Create(stream, SvgReaderSettings());
            var document = XDocument.Load(reader, LoadOptions.None);
            if (document.Root is null
                || document.Root.Name.NamespaceName != "http://www.w3.org/2000/svg"
                || document.Root.Name.LocalName != "svg"
                || document.Nodes().Any(node => node is XProcessingInstruction or XDocumentType)
                || document.Root.DescendantNodes().Any(node => node is XProcessingInstruction or XDocumentType))
            {
                return false;
            }

            foreach (var element in document.Root.DescendantsAndSelf())
            {
                if (element.Name.NamespaceName != "http://www.w3.org/2000/svg"
                    || !SafeSvgElements.Contains(element.Name.LocalName))
                {
                    return false;
                }

                foreach (var attribute in element.Attributes())
                {
                    if (attribute.IsNamespaceDeclaration)
                    {
                        if (attribute.Name.LocalName != "xmlns"
                            || attribute.Value != "http://www.w3.org/2000/svg")
                        {
                            return false;
                        }

                        continue;
                    }

                    if (attribute.Name.NamespaceName.Length != 0
                        || !SafeSvgAttributes.Contains(attribute.Name.LocalName)
                        || attribute.Value.Contains("url(", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException or IOException)
        {
            return false;
        }
    }

    private static bool HasBoundedSvgStructure(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var reader = XmlReader.Create(stream, SvgReaderSettings());
        var nodeCount = 0;
        var attributeCount = 0;
        while (reader.Read())
        {
            if (reader.Depth + 1 > MaxSvgNestingDepth)
            {
                return false;
            }

            if (reader.NodeType is XmlNodeType.Element
                or XmlNodeType.Text
                or XmlNodeType.CDATA
                or XmlNodeType.Comment
                or XmlNodeType.ProcessingInstruction
                or XmlNodeType.DocumentType
                or XmlNodeType.XmlDeclaration
                or XmlNodeType.Whitespace
                or XmlNodeType.SignificantWhitespace)
            {
                if (++nodeCount > MaxSvgXmlNodes)
                {
                    return false;
                }
            }

            if (reader.NodeType == XmlNodeType.Element)
            {
                attributeCount = checked(attributeCount + reader.AttributeCount);
                if (attributeCount > MaxSvgAttributes)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static XmlReaderSettings SvgReaderSettings()
        => new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxEmbeddedAssetBytes,
        };

    /// <summary>Millimeter-exact SVG: physical units carry the press's dimensional accuracy onto paper.</summary>
    private static string RenderSvg(
        VectorGraphic graphic,
        bool standalone,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
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

    // This style is part of the admitted 0.7 portable snapshot renderer. Spell
    // its newlines explicitly so checkout policy cannot change package bytes.
    private const string BaseStyle =
        "body { font-family: \"Segoe UI\", system-ui, sans-serif; line-height: 1.5; margin: 2rem; }\r\n"
        + ".card { border: 1px solid #888; padding: 0.75rem 1rem; margin: 0.75rem 0; }\r\n"
        + ".card-title { font-weight: 700; margin: 0 0 0.3rem; }\r\n"
        + ".bilingual-pair { margin: 0.5rem 0; }\r\n"
        + ".bilingual-pair p { margin: 0.15rem 0; }\r\n"
        + ".teacher-only { border-left: 4px solid #8a6d24; padding-left: 0.75rem; }\r\n"
        + ".asset-placeholder { border: 1px dashed #888; padding: 0.5rem; }\r\n"
        + ".step-symbol { display: inline-block; margin-right: 0.5rem; vertical-align: middle; }\r\n"
        + ".steps li p { display: inline-block; margin: 0 0.4rem 0 0; }\r\n"
        + "table { border-collapse: collapse; }\r\n"
        + "th, td { border: 1px solid #666; padding: 0.3rem 0.6rem; text-align: left; }";

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

    private const string AssetStyle =
        """

        .asset { display: inline-block; margin: 0.35rem; }
        .asset img { display: block; width: 28mm; height: 28mm; object-fit: contain; }
        """;

    private sealed record EmbeddedImage(string MimeType, string Base64);

    private sealed record EmbeddedImageSource(string MimeType, ReadOnlyMemory<byte> Content);

    private sealed record AssetSheetText(string Text, string? Locale);

    private sealed record AssetSheetLine(string Text, string? Locale);

    private sealed record AssetSheetItem(IReadOnlyList<AssetSheetText> Texts, ImageReference? Image);

    private sealed record AssetSheetLayout(AssetSheetItem Item, IReadOnlyList<AssetSheetLine> Lines, double Height);

    private readonly record struct SemanticTextBudget(long EncodedUpperBound);
}
