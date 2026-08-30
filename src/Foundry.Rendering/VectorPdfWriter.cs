// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Rendering;

/// <summary>
/// The vector-first PDF press (second forge menu, item 3): millimeter-exact
/// geometry becomes true PDF vector operators — no browser, no rasterization,
/// no third-party library, and no timestamp anywhere, so identical input gives
/// byte-identical output. Text is set in Courier, a standard-14 face whose
/// every glyph is exactly 600/1000 em wide: anchor arithmetic is exact instead
/// of trusting a metrics table typed from memory. (A Helvetica upgrade waits
/// for an AFM width table imported with provenance.) Characters outside
/// WinAnsi refuse loudly — the HTML print path handles full Unicode; a silent
/// substitute glyph would be a lie in ink. Like every renderer, it accepts
/// only an ApprovedArtifact (ADR-004).
/// </summary>
public static class VectorPdfWriter
{
    public const double PointsPerMm = 72.0 / 25.4;

    internal const int MaxPdfPages = 4096;
    internal const int MaxPdfRenderUnits = 16384;
    internal const long MaxPdfTextCharacters = 2L * 1024 * 1024;
    internal const int MaxPdfOutputBytes = 32 * 1024 * 1024;

    private const double CourierAdvance = 0.6; // 600/1000 em, every glyph
    private const double BezierArc = 0.5522847498; // circle-from-Béziers constant
    private const int FixedPdfOverheadBytes = 64 * 1024;
    private const int MaximumPdfOverheadBytesPerPage = 1024;

    /// <summary>True when the document is vector sheets (plus teacher-only notices) end to end.</summary>
    public static bool CanWrite(ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Nodes.Count is 0 or > MaxPdfPages)
        {
            return false;
        }

        long renderUnits = document.Nodes.Count;
        var hasVector = false;
        foreach (var node in document.Nodes)
        {
            if (node is VectorGraphic graphic)
            {
                hasVector = true;
                renderUnits += graphic.Primitives.Count;
                if (renderUnits > MaxPdfRenderUnits)
                {
                    return false;
                }
            }
            else if (node is not TeacherOnlyNotice)
            {
                return false;
            }
        }

        return hasVector;
    }

    public static byte[] Write(ApprovedArtifact artifact, RenderAudience audience)
        => Write(artifact, audience, CancellationToken.None);

    public static byte[] Write(
        ApprovedArtifact artifact,
        RenderAudience audience,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(audience))
        {
            throw new ArgumentOutOfRangeException(nameof(audience));
        }

        var document = artifact.Revision.Document;
        ValidateDocument(document, cancellationToken);

        var pages = new List<(double WidthMm, double HeightMm, string Content)>();
        long contentCharacters = 0;
        foreach (var node in document.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (node)
            {
                case VectorGraphic graphic:
                    AddPage(
                        pages,
                        graphic.WidthMm,
                        graphic.HeightMm,
                        SheetContent(graphic, cancellationToken),
                        ref contentCharacters);
                    break;

                case TeacherOnlyNotice notice when audience == RenderAudience.Teacher:
                    AddPage(
                        pages,
                        215.9,
                        279.4,
                        NoticeContent(notice.Text, cancellationToken),
                        ref contentCharacters);
                    break;
            }
        }

        return Assemble(pages, cancellationToken);
    }

    /// <summary>
    /// Two-up imposition: each entry in <paramref name="sides"/> becomes one
    /// landscape sheet side carrying two content pages (1-based; numbers past
    /// the content count are the padding blanks). The caller supplies the
    /// side order — the signature arithmetic lives with the Booklet Binder,
    /// the placement transform lives here, and neither duplicates the other.
    /// Sheets are the same paper turned sideways; pages scale uniformly and
    /// center in their half. Teacher audience gets an instruction page first,
    /// and it says SHORT-edge duplex, because that is what keeps these
    /// landscape sheets upright through the fold.
    /// </summary>
    public static byte[] WriteImposed(ApprovedArtifact artifact, IReadOnlyList<(int Left, int Right)> sides, RenderAudience audience)
        => WriteImposed(artifact, sides, audience, CancellationToken.None);

    public static byte[] WriteImposed(
        ApprovedArtifact artifact,
        IReadOnlyList<(int Left, int Right)> sides,
        RenderAudience audience,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(sides);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(audience))
        {
            throw new ArgumentOutOfRangeException(nameof(audience));
        }

        ValidateDocument(artifact.Revision.Document, cancellationToken);

        var content = artifact.Revision.Document.Nodes.OfType<VectorGraphic>().ToList();
        if (content.Count == 0 || sides.Count == 0)
        {
            throw new NotSupportedException("Imposition takes a document of vector sheets and at least one side.");
        }

        var instructionPageCount = audience == RenderAudience.Teacher ? 1 : 0;
        if (sides.Count > MaxPdfPages - instructionPageCount)
        {
            throw new InvalidOperationException(
                $"PDF rendering refused because output exceeds the bounded {MaxPdfPages}-page limit.");
        }

        if (content.Any(g =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return g.WidthMm != content[0].WidthMm || g.HeightMm != content[0].HeightMm;
        }))
        {
            throw new NotSupportedException("Imposition requires every content page to share one page size.");
        }

        var sourceWidth = content[0].WidthMm;
        var sourceHeight = content[0].HeightMm;
        var sheetWidth = sourceHeight;   // the same paper, turned sideways
        var sheetHeight = sourceWidth;
        var slotWidth = sheetWidth / 2;
        var scale = Math.Min(slotWidth / sourceWidth, sheetHeight / sourceHeight);
        var offsetY = (sheetHeight - scale * sourceHeight) / 2 * PointsPerMm;
        var slack = (slotWidth - scale * sourceWidth) / 2 * PointsPerMm;
        var renderedContent = content
            .Select(graphic => SheetContent(graphic, cancellationToken))
            .ToList();

        var pages = new List<(double WidthMm, double HeightMm, string Content)>();
        long contentCharacters = 0;
        if (audience == RenderAudience.Teacher)
        {
            var instructions = new StringBuilder();
            var y = sheetHeight - 20;
            foreach (var line in new[]
            {
                $"Saddle-stitch booklet: {content.Count} content pages on {sides.Count / 2} sheets.",
                "1. Print double-sided, flipping on the SHORT edge.",
                "2. Keep the sheets in printed order.",
                "3. Fold the whole stack in half.",
                "4. Staple twice on the fold.",
            })
            {
                cancellationToken.ThrowIfCancellationRequested();
                instructions.Append("BT /F1 ").Append(Fmt(11.0)).Append(" Tf ")
                    .Append(Fmt(20 * PointsPerMm)).Append(' ').Append(Fmt(y * PointsPerMm)).Append(" Td (")
                    .Append(EscapeString(EncodeWinAnsi(line, cancellationToken))).Append(") Tj ET\n");
                y -= 8;
            }

            AddPage(
                pages,
                sheetWidth,
                sheetHeight,
                instructions.ToString(),
                ref contentCharacters);
        }

        foreach (var (left, right) in sides)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var side = new StringBuilder();
            AppendSlot(side, left, slack);
            AppendSlot(side, right, slotWidth * PointsPerMm + slack);
            AddPage(
                pages,
                sheetWidth,
                sheetHeight,
                side.ToString(),
                ref contentCharacters);
        }

        return Assemble(pages, cancellationToken);

        void AppendSlot(StringBuilder side, int pageNumber, double offsetX)
        {
            if (pageNumber < 1 || pageNumber > content.Count)
            {
                return; // a padding blank, explicit in the plan, empty in ink
            }

            side.Append("q ").Append(Fmt(scale)).Append(" 0 0 ").Append(Fmt(scale)).Append(' ')
                .Append(Fmt(offsetX)).Append(' ').Append(Fmt(offsetY)).Append(" cm\n")
                .Append(renderedContent[pageNumber - 1])
                .Append("Q\n");
        }
    }

    private static void ValidateDocument(
        ArtifactDocument document,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (document.Nodes.Count > MaxPdfPages)
        {
            throw new InvalidOperationException(
                $"PDF rendering refused because the document exceeds the bounded {MaxPdfPages}-page/node limit.");
        }

        long renderUnits = document.Nodes.Count;
        long textCharacters = 0;
        var hasVector = false;
        AddText(document.Language);
        foreach (var node in document.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (node)
            {
                case VectorGraphic graphic:
                    hasVector = true;
                    renderUnits += graphic.Primitives.Count;
                    EnsureRenderUnitsBound(renderUnits);
                    AddText(graphic.Description);
                    foreach (var primitive in graphic.Primitives)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (primitive is TextLabel label)
                        {
                            AddText(label.Text);
                        }
                    }

                    break;

                case TeacherOnlyNotice notice:
                    AddText(notice.Text);
                    break;

                default:
                    throw new NotSupportedException(
                        "The vector PDF press takes documents made of vector sheets and teacher-only notices; other documents take the HTML print path.");
            }
        }

        if (!hasVector)
        {
            throw new NotSupportedException(
                "The vector PDF press takes documents made of vector sheets; other documents take the HTML print path.");
        }

        EnsureRenderUnitsBound(renderUnits);

        void AddText(string? value)
        {
            if (value is null)
            {
                return;
            }

            textCharacters += value.Length;
            if (textCharacters > MaxPdfTextCharacters)
            {
                throw new InvalidOperationException(
                    $"PDF rendering refused because semantic text exceeds the bounded {MaxPdfTextCharacters}-character limit.");
            }
        }
    }

    private static void EnsureRenderUnitsBound(long renderUnits)
    {
        if (renderUnits > MaxPdfRenderUnits)
        {
            throw new InvalidOperationException(
                $"PDF rendering refused because the document exceeds the bounded {MaxPdfRenderUnits}-unit limit.");
        }
    }

    private static void AddPage(
        List<(double WidthMm, double HeightMm, string Content)> pages,
        double widthMm,
        double heightMm,
        string content,
        ref long contentCharacters)
    {
        if (pages.Count >= MaxPdfPages)
        {
            throw new InvalidOperationException(
                $"PDF rendering refused because output exceeds the bounded {MaxPdfPages}-page limit.");
        }

        contentCharacters = checked(contentCharacters + content.Length);
        var projectedOutput = checked(
            FixedPdfOverheadBytes
            + contentCharacters
            + ((long)(pages.Count + 1) * MaximumPdfOverheadBytesPerPage));
        if (projectedOutput > MaxPdfOutputBytes)
        {
            throw new InvalidOperationException(
                $"PDF rendering refused because output exceeds the bounded {MaxPdfOutputBytes}-byte limit.");
        }

        pages.Add((widthMm, heightMm, content));
    }

    private static string SheetContent(VectorGraphic graphic, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = new StringBuilder();
        var height = graphic.HeightMm;

        foreach (var primitive in graphic.Primitives)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (primitive)
            {
                case LineSeg line:
                    content.Append(Fmt(line.StrokeWidthMm * PointsPerMm)).Append(" w\n");
                    if (line.Dashed)
                    {
                        content.Append('[').Append(Fmt(2 * PointsPerMm)).Append("] 0 d\n");
                    }

                    content.Append(Point(line.X1, line.Y1, height)).Append(" m ")
                        .Append(Point(line.X2, line.Y2, height)).Append(" l S\n");
                    if (line.Dashed)
                    {
                        content.Append("[] 0 d\n");
                    }

                    break;

                case RectShape rect:
                    content.Append(Fmt(rect.StrokeWidthMm * PointsPerMm)).Append(" w\n")
                        .Append(Point(rect.X, rect.Y + rect.HeightMm, height)).Append(' ')
                        .Append(Fmt(rect.WidthMm * PointsPerMm)).Append(' ')
                        .Append(Fmt(rect.HeightMm * PointsPerMm)).Append(" re ")
                        .Append(rect.Filled ? "f" : "S").Append('\n');
                    break;

                case CircleShape circle:
                    AppendCircle(content, circle, height);
                    break;

                case TextLabel label:
                    AppendLabel(content, label, height, cancellationToken);
                    break;

                default:
                    throw new NotSupportedException(primitive.GetType().Name);
            }
        }

        return content.ToString();
    }

    private static void AppendCircle(StringBuilder content, CircleShape circle, double heightMm)
    {
        var cx = circle.CenterX * PointsPerMm;
        var cy = (heightMm - circle.CenterY) * PointsPerMm;
        var r = circle.RadiusMm * PointsPerMm;
        var c = r * BezierArc;

        content.Append(Fmt(circle.StrokeWidthMm * PointsPerMm)).Append(" w\n")
            .Append(Fmt(cx + r)).Append(' ').Append(Fmt(cy)).Append(" m\n")
            .Append(Curve(cx + r, cy + c, cx + c, cy + r, cx, cy + r))
            .Append(Curve(cx - c, cy + r, cx - r, cy + c, cx - r, cy))
            .Append(Curve(cx - r, cy - c, cx - c, cy - r, cx, cy - r))
            .Append(Curve(cx + c, cy - r, cx + r, cy - c, cx + r, cy))
            .Append(circle.Filled ? "f" : "S").Append('\n');
    }

    private static void AppendLabel(
        StringBuilder content,
        TextLabel label,
        double heightMm,
        CancellationToken cancellationToken)
    {
        var size = label.FontSizeMm * PointsPerMm;
        var encoded = EncodeWinAnsi(label.Text, cancellationToken);
        var width = encoded.Length * CourierAdvance * size;

        var x = label.X * PointsPerMm - label.Anchor switch
        {
            TextAnchor.Middle => width / 2,
            TextAnchor.End => width,
            _ => 0,
        };

        content.Append("BT /F1 ").Append(Fmt(size)).Append(" Tf ")
            .Append(Fmt(x)).Append(' ').Append(Fmt((heightMm - label.Y) * PointsPerMm)).Append(" Td (")
            .Append(EscapeString(encoded)).Append(") Tj ET\n");
    }

    private static string NoticeContent(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = new StringBuilder();
        var y = 259.4; // start 20 mm below the top of a Letter page
        foreach (var line in Wrap(text, 78, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            content.Append("BT /F1 ").Append(Fmt(11.0)).Append(" Tf ")
                .Append(Fmt(20 * PointsPerMm)).Append(' ').Append(Fmt(y * PointsPerMm)).Append(" Td (")
                .Append(EscapeString(EncodeWinAnsi(line, cancellationToken))).Append(") Tj ET\n");
            y -= 6;
        }

        return content.ToString();
    }

    private static IEnumerable<string> Wrap(
        string text,
        int columns,
        CancellationToken cancellationToken)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var word in words)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (line.Length > 0 && line.Length + 1 + word.Length > columns)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            yield return line.ToString();
        }
    }

    private static byte[] Assemble(
        List<(double WidthMm, double HeightMm, string Content)> pages,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureProjectedOutputBound(pages);
        // Objects: 1 catalog, 2 page tree, 3 font, then per page (page, stream).
        var objects = new List<string>();
        var kids = string.Join(' ', Enumerable.Range(0, pages.Count).Select(i => $"{4 + 2 * i} 0 R"));

        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add($"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>");

        for (var i = 0; i < pages.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (widthMm, heightMm, content) = pages[i];
            objects.Add(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Fmt(widthMm * PointsPerMm)} {Fmt(heightMm * PointsPerMm)}] " +
                $"/Resources << /Font << /F1 3 0 R >> >> /Contents {5 + 2 * i} 0 R >>");
            // Every character in the assembled PDF is <= 0xFF by construction
            // (WinAnsi-encoded text, ASCII operators), so length IS byte count.
            objects.Add($"<< /Length {content.Length} >>\nstream\n{content}endstream");
        }

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            offsets.Add(pdf.Length);
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
            EnsureOutputBound(pdf.Length);
        }

        var xrefOffset = pdf.Length;
        pdf.Append("xref\n0 ").Append(objects.Count + 1).Append('\n')
            .Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            pdf.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        pdf.Append("trailer\n<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n")
            .Append(xrefOffset).Append("\n%%EOF\n");

        cancellationToken.ThrowIfCancellationRequested();
        EnsureOutputBound(pdf.Length);
        return Latin1.GetBytes(pdf.ToString());
    }

    private static void EnsureProjectedOutputBound(
        List<(double WidthMm, double HeightMm, string Content)> pages)
    {
        long contentCharacters = 0;
        foreach (var (WidthMm, HeightMm, Content) in pages)
        {
            contentCharacters = checked(contentCharacters + Content.Length);
        }

        var projectedOutput = checked(
            FixedPdfOverheadBytes
            + contentCharacters
            + ((long)pages.Count * MaximumPdfOverheadBytesPerPage));
        if (projectedOutput > MaxPdfOutputBytes)
        {
            throw new InvalidOperationException(
                $"PDF rendering refused because output exceeds the bounded {MaxPdfOutputBytes}-byte limit.");
        }
    }

    private static void EnsureOutputBound(int outputBytes)
    {
        if (outputBytes > MaxPdfOutputBytes)
        {
            throw new InvalidOperationException(
                $"PDF rendering refused because output exceeds the bounded {MaxPdfOutputBytes}-byte limit.");
        }
    }

    private static string Curve(double x1, double y1, double x2, double y2, double x3, double y3)
        => $"{Fmt(x1)} {Fmt(y1)} {Fmt(x2)} {Fmt(y2)} {Fmt(x3)} {Fmt(y3)} c\n";

    private static string Point(double xMm, double yMm, double heightMm)
        => $"{Fmt(xMm * PointsPerMm)} {Fmt((heightMm - yMm) * PointsPerMm)}";

    private static string Fmt(double value)
        => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static Encoding Latin1 => Encoding.Latin1;

    private static string EncodeWinAnsi(string text, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            cancellationToken.ThrowIfCancellationRequested();
            builder.Append((char)EncodeWinAnsi(ch));
        }

        return builder.ToString();
    }

    private static byte EncodeWinAnsi(char ch) => ch switch
    {
        >= ' ' and <= '~' => (byte)ch,
        >= ' ' and <= 'ÿ' => (byte)ch,
        '€' => 0x80, // €
        '‚' => 0x82,
        'ƒ' => 0x83,
        '„' => 0x84,
        '…' => 0x85, // …
        '†' => 0x86,
        '‡' => 0x87,
        'ˆ' => 0x88,
        '‰' => 0x89,
        'Š' => 0x8A,
        '‹' => 0x8B,
        'Œ' => 0x8C,
        'Ž' => 0x8E,
        '‘' => 0x91,
        '’' => 0x92,
        '“' => 0x93,
        '”' => 0x94,
        '•' => 0x95, // •
        '–' => 0x96, // –
        '—' => 0x97, // —
        '˜' => 0x98,
        '™' => 0x99,
        'š' => 0x9A,
        '›' => 0x9B,
        'œ' => 0x9C,
        'ž' => 0x9E,
        'Ÿ' => 0x9F,
        _ => throw new NotSupportedException(
            $"Character U+{(int)ch:X4} has no WinAnsi encoding; this document needs the HTML print path."),
    };

    private static string EscapeString(string encoded)
        => encoded
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
}
