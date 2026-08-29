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

    private const double CourierAdvance = 0.6; // 600/1000 em, every glyph
    private const double BezierArc = 0.5522847498; // circle-from-Béziers constant

    /// <summary>True when the document is vector sheets (plus teacher-only notices) end to end.</summary>
    public static bool CanWrite(ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Nodes.Any(n => n is VectorGraphic)
            && document.Nodes.All(n => n is VectorGraphic or TeacherOnlyNotice);
    }

    public static byte[] Write(ApprovedArtifact artifact, RenderAudience audience)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var document = artifact.Revision.Document;
        if (!CanWrite(document))
        {
            throw new NotSupportedException(
                "The vector PDF press takes documents made of vector sheets; other documents take the HTML print path.");
        }

        var pages = new List<(double WidthMm, double HeightMm, string Content)>();
        foreach (var node in document.Nodes)
        {
            switch (node)
            {
                case VectorGraphic graphic:
                    pages.Add((graphic.WidthMm, graphic.HeightMm, SheetContent(graphic)));
                    break;

                case TeacherOnlyNotice notice when audience == RenderAudience.Teacher:
                    pages.Add((215.9, 279.4, NoticeContent(notice.Text)));
                    break;
            }
        }

        return Assemble(pages);
    }

    private static string SheetContent(VectorGraphic graphic)
    {
        var content = new StringBuilder();
        var height = graphic.HeightMm;

        foreach (var primitive in graphic.Primitives)
        {
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
                    AppendLabel(content, label, height);
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

    private static void AppendLabel(StringBuilder content, TextLabel label, double heightMm)
    {
        var size = label.FontSizeMm * PointsPerMm;
        var encoded = EncodeWinAnsi(label.Text);
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

    private static string NoticeContent(string text)
    {
        var content = new StringBuilder();
        var y = 259.4; // start 20 mm below the top of a Letter page
        foreach (var line in Wrap(text, 78))
        {
            content.Append("BT /F1 ").Append(Fmt(11.0)).Append(" Tf ")
                .Append(Fmt(20 * PointsPerMm)).Append(' ').Append(Fmt(y * PointsPerMm)).Append(" Td (")
                .Append(EscapeString(EncodeWinAnsi(line))).Append(") Tj ET\n");
            y -= 6;
        }

        return content.ToString();
    }

    private static IEnumerable<string> Wrap(string text, int columns)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var word in words)
        {
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

    private static byte[] Assemble(List<(double WidthMm, double HeightMm, string Content)> pages)
    {
        // Objects: 1 catalog, 2 page tree, 3 font, then per page (page, stream).
        var objects = new List<string>();
        var kids = string.Join(' ', Enumerable.Range(0, pages.Count).Select(i => $"{4 + 2 * i} 0 R"));

        objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
        objects.Add($"<< /Type /Pages /Kids [{kids}] /Count {pages.Count} >>");
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>");

        for (var i = 0; i < pages.Count; i++)
        {
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
            offsets.Add(pdf.Length);
            pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
        }

        var xrefOffset = pdf.Length;
        pdf.Append("xref\n0 ").Append(objects.Count + 1).Append('\n')
            .Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        pdf.Append("trailer\n<< /Size ").Append(objects.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n")
            .Append(xrefOffset).Append("\n%%EOF\n");

        return Latin1.GetBytes(pdf.ToString());
    }

    private static string Curve(double x1, double y1, double x2, double y2, double x3, double y3)
        => $"{Fmt(x1)} {Fmt(y1)} {Fmt(x2)} {Fmt(y2)} {Fmt(x3)} {Fmt(y3)} c\n";

    private static string Point(double xMm, double yMm, double heightMm)
        => $"{Fmt(xMm * PointsPerMm)} {Fmt((heightMm - yMm) * PointsPerMm)}";

    private static string Fmt(double value)
        => value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static Encoding Latin1 => Encoding.Latin1;

    private static string EncodeWinAnsi(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
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
