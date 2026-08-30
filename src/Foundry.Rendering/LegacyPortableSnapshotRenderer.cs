// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net;
using System.Text;
using Foundry.Domain;

namespace Foundry.Rendering;

/// <summary>
/// Frozen learner-HTML writer for the only admitted prior engine identity.
/// This is compatibility code, not a second current renderer: it is copied
/// from commit 1c41984 so the managed-upgrade reader can prove that a legacy
/// snapshot corresponds byte-for-byte before rewriting it through the current
/// semantic core. Do not alter it when current HTML changes.
/// </summary>
internal static class LegacyPortableSnapshotRenderer
{
    internal const string EngineVersion = "0.1.0-dev";

    internal static byte[] RenderV010Dev(ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var builder = new StringBuilder();
        var language = Attribute(document.Language ?? "en");
        var title = document.Nodes.OfType<Heading>().FirstOrDefault()?.Text ?? "Approved artifact";

        builder.Append("<!DOCTYPE html>\n");
        builder.Append("<html lang=\"").Append(language).Append("\">\n<head>\n<meta charset=\"utf-8\">\n");
        builder.Append("<title>").Append(Text(title)).Append("</title>\n");
        builder.Append("<style>\n").Append(BaseStyle);
        builder.Append("</style>\n</head>\n<body>\n");

        foreach (var node in document.Nodes)
        {
            AppendNode(builder, node);
        }

        builder.Append("</body>\n</html>\n");
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void AppendNode(StringBuilder builder, DocumentNode node)
    {
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
                builder.Append("<section class=\"card\">\n<h3>").Append(Text(card.Title)).Append("</h3>\n<p>")
                    .Append(Text(card.Body)).Append("</p>\n</section>\n");
                break;

            case ImageReference image:
                builder.Append("<figure class=\"asset-placeholder\" data-asset-id=\"")
                    .Append(Attribute(image.Asset.Value))
                    .Append("\"><figcaption>").Append(Text(image.AltText)).Append("</figcaption></figure>\n");
                break;

            case BilingualPair pair:
                builder.Append("<div class=\"bilingual-pair\">\n<p lang=\"")
                    .Append(Attribute(pair.SourceLocale)).Append("\" dir=\"auto\">").Append(Text(pair.SourceText))
                    .Append("</p>\n<p lang=\"")
                    .Append(Attribute(pair.TargetLocale)).Append("\" dir=\"auto\">").Append(Text(pair.TargetText))
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
                builder.Append("<p>").Append(Text(evidence.Claim)).Append("</p>\n");
                break;

            case Citation citation:
                builder.Append("<p class=\"citation\"><cite>").Append(Text(citation.Text)).Append("</cite></p>\n");
                break;

            case TeacherOnlyNotice:
                break;

            default:
                throw new NotSupportedException(
                    $"The 0.1.0-dev renderer did not admit node type {node.GetType().Name}.");
        }
    }

    private static string Text(string value) => WebUtility.HtmlEncode(value);

    private static string Attribute(string value) => WebUtility.HtmlEncode(value);

    // This is frozen compatibility output, so line endings are bytes in the
    // contract. Never use a raw multiline literal here: the repository's
    // checkout EOL policy would otherwise turn the old LF snapshot into CRLF.
    private const string BaseStyle =
        "body { font-family: \"Segoe UI\", system-ui, sans-serif; line-height: 1.5; margin: 2rem; }\n"
        + ".card { border: 1px solid #888; padding: 0.75rem 1rem; margin: 0.75rem 0; }\n"
        + ".bilingual-pair { margin: 0.5rem 0; }\n"
        + ".bilingual-pair p { margin: 0.15rem 0; }\n"
        + ".teacher-only { border-left: 4px solid #8a6d24; padding-left: 0.75rem; }\n"
        + ".asset-placeholder { border: 1px dashed #888; padding: 0.5rem; }\n"
        + "table { border-collapse: collapse; }\n"
        + "th, td { border: 1px solid #666; padding: 0.3rem 0.6rem; text-align: left; }";
}
