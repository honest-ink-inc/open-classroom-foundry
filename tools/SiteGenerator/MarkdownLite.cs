// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
using System.Text.RegularExpressions;

namespace Foundry.Tools.SiteGenerator;

/// <summary>
/// A small deterministic Markdown renderer for the repository's own documents
/// (handover 2026-08-29, forge item 7). It covers exactly what the governing
/// documents use — headings, paragraphs, blockquotes, lists, tables, rules,
/// fences, links, emphasis, strikethrough, inline code — and escapes everything
/// else as text. No third-party dependency: the site's correctness is this
/// repository's own testable code, like every other artifact.
/// </summary>
public static partial class MarkdownLite
{
    public static string ToHtml(string markdown, Func<string, string>? rewriteLink = null)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        rewriteLink ??= href => href;

        var html = new StringBuilder();
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index];

            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                index = AppendFence(html, lines, index);
            }
            else if (HeadingPattern().Match(line) is { Success: true } heading)
            {
                var level = heading.Groups[1].Length;
                html.Append("<h").Append(level).Append('>')
                    .Append(Inline(heading.Groups[2].Value, rewriteLink))
                    .Append("</h").Append(level).Append(">\n");
                index++;
            }
            else if (RulePattern().IsMatch(line))
            {
                html.Append("<hr>\n");
                index++;
            }
            else if (line.StartsWith('>'))
            {
                index = AppendBlockquote(html, lines, index, rewriteLink);
            }
            else if (index + 1 < lines.Length && line.StartsWith('|') && TableSeparatorPattern().IsMatch(lines[index + 1]))
            {
                index = AppendTable(html, lines, index, rewriteLink);
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                index = AppendList(html, lines, index, "ul", l => l.StartsWith("- ", StringComparison.Ordinal) ? l[2..] : null, rewriteLink);
            }
            else if (OrderedItemPattern().IsMatch(line))
            {
                index = AppendList(html, lines, index, "ol", l => OrderedItemPattern().Match(l) is { Success: true } m ? m.Groups[1].Value : null, rewriteLink);
            }
            else
            {
                index = AppendParagraph(html, lines, index, rewriteLink);
            }
        }

        return html.ToString();
    }

    private static int AppendFence(StringBuilder html, string[] lines, int index)
    {
        html.Append("<pre><code>");
        index++;
        while (index < lines.Length && !lines[index].StartsWith("```", StringComparison.Ordinal))
        {
            html.Append(Escape(lines[index])).Append('\n');
            index++;
        }

        html.Append("</code></pre>\n");
        return index + 1;
    }

    private static int AppendBlockquote(StringBuilder html, string[] lines, int index, Func<string, string> rewriteLink)
    {
        var quoted = new List<string>();
        while (index < lines.Length && lines[index].StartsWith('>'))
        {
            quoted.Add(lines[index].TrimStart('>').TrimStart());
            index++;
        }

        html.Append("<blockquote><p>")
            .Append(Inline(string.Join(' ', quoted), rewriteLink))
            .Append("</p></blockquote>\n");
        return index;
    }

    private static int AppendTable(StringBuilder html, string[] lines, int index, Func<string, string> rewriteLink)
    {
        html.Append("<table>\n<thead><tr>");
        foreach (var cell in Cells(lines[index]))
        {
            html.Append("<th>").Append(Inline(cell, rewriteLink)).Append("</th>");
        }

        html.Append("</tr></thead>\n<tbody>\n");
        index += 2;
        while (index < lines.Length && lines[index].StartsWith('|'))
        {
            html.Append("<tr>");
            foreach (var cell in Cells(lines[index]))
            {
                html.Append("<td>").Append(Inline(cell, rewriteLink)).Append("</td>");
            }

            html.Append("</tr>\n");
            index++;
        }

        html.Append("</tbody>\n</table>\n");
        return index;
    }

    private static int AppendList(StringBuilder html, string[] lines, int index, string tag, Func<string, string?> item, Func<string, string> rewriteLink)
    {
        html.Append('<').Append(tag).Append(">\n");
        while (index < lines.Length && item(lines[index]) is { } text)
        {
            html.Append("<li>").Append(Inline(text, rewriteLink)).Append("</li>\n");
            index++;
        }

        html.Append("</").Append(tag).Append(">\n");
        return index;
    }

    private static int AppendParagraph(StringBuilder html, string[] lines, int index, Func<string, string> rewriteLink)
    {
        var collected = new List<string>();
        while (index < lines.Length
            && !string.IsNullOrWhiteSpace(lines[index])
            && !lines[index].StartsWith('#')
            && !lines[index].StartsWith('>')
            && !lines[index].StartsWith("- ", StringComparison.Ordinal)
            && !lines[index].StartsWith("```", StringComparison.Ordinal)
            && !lines[index].StartsWith('|'))
        {
            collected.Add(lines[index].Trim());
            index++;
        }

        html.Append("<p>").Append(Inline(string.Join(' ', collected), rewriteLink)).Append("</p>\n");
        return index;
    }

    private static IEnumerable<string> Cells(string row)
        => row.Trim().Trim('|').Split('|').Select(c => c.Trim());

    /// <summary>Escape first, then build markup — document text can never smuggle tags.</summary>
    private static string Inline(string text, Func<string, string> rewriteLink)
    {
        var escaped = Escape(text);
        escaped = CodeSpanPattern().Replace(escaped, "<code>$1</code>");
        escaped = LinkPattern().Replace(escaped, match =>
            $"<a href=\"{Escape(rewriteLink(match.Groups[2].Value))}\">{match.Groups[1].Value}</a>");
        escaped = BoldPattern().Replace(escaped, "<strong>$1</strong>");
        escaped = StrikePattern().Replace(escaped, "<del>$1</del>");
        escaped = ItalicPattern().Replace(escaped, "<em>$1</em>");
        return escaped;
    }

    private static string Escape(string text)
        => text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^-{3,}\s*$")]
    private static partial Regex RulePattern();

    [GeneratedRegex(@"^\|[\s:\-|]+\|?\s*$")]
    private static partial Regex TableSeparatorPattern();

    [GeneratedRegex(@"^(?:\d{1,3})\.\s+(.*)$")]
    private static partial Regex OrderedItemPattern();

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex CodeSpanPattern();

    [GeneratedRegex(@"\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex BoldPattern();

    [GeneratedRegex(@"~~([^~]+)~~")]
    private static partial Regex StrikePattern();

    [GeneratedRegex(@"\*([^*]+)\*")]
    private static partial Regex ItalicPattern();
}
