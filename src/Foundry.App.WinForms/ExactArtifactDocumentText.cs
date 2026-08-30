// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using Foundry.Domain;

namespace Foundry.App.WinForms;

/// <summary>
/// An exact, read-only semantic account of an artifact document for the lane
/// preflight. It deliberately covers every field on every admitted node and
/// vector primitive; this is content inspection, independent of Gate B state.
/// </summary>
internal static class ExactArtifactDocumentText
{
    public static string Describe(ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var lines = new List<string>
        {
            UiStrings.Format(
                UiStrings.LoadedProjectDocumentDigest,
                Contracts.ArtifactDocumentFingerprint.Compute(document)),
            UiStrings.Format(
                UiStrings.LoadedProjectDocumentLanguage,
                Value(document.Language)),
        };

        for (var index = 0; index < document.Nodes.Count; index++)
        {
            lines.Add(string.Empty);
            lines.Add(UiStrings.Format(UiStrings.LoadedProjectDocumentElement, index + 1));
            AddNode(lines, document.Nodes[index]);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AddNode(List<string> lines, DocumentNode node)
    {
        switch (node)
        {
            case Heading heading:
                lines.Add(UiStrings.Format(UiStrings.NodeHeading, heading.Level, Value(heading.Text)));
                break;

            case Paragraph paragraph:
                lines.Add(UiStrings.Format(UiStrings.NodeParagraph, Value(paragraph.Text)));
                break;

            case OrderedSteps steps:
                lines.Add(UiStrings.Format(UiStrings.NodeSteps, steps.Steps.Count));
                for (var index = 0; index < steps.Steps.Count; index++)
                {
                    lines.Add(UiStrings.Format(UiStrings.NodeOrderedStepItem, index + 1, Value(steps.Steps[index])));
                }

                break;

            case StepRow step:
                lines.Add(UiStrings.NodeStepRow);
                lines.Add(UiStrings.Format(UiStrings.NodeTextContent, Value(step.Text)));
                lines.Add(UiStrings.Format(UiStrings.NodeTranslationContent, Value(step.TargetText)));
                lines.Add(UiStrings.Format(
                    UiStrings.NodeLocalesContent,
                    Value(step.SourceLocale),
                    Value(step.TargetLocale)));
                lines.Add(UiStrings.Format(
                    UiStrings.NodeImageAssetIdentity,
                    Value(step.Symbol?.Asset.Value)));
                lines.Add(UiStrings.Format(
                    UiStrings.NodeSymbolAltContent,
                    Value(step.Symbol?.AltText)));
                break;

            case PageBreak:
                lines.Add(UiStrings.NodePageBreak);
                break;

            case UnorderedList list:
                lines.Add(UiStrings.Format(UiStrings.NodeList, list.Items.Count));
                for (var index = 0; index < list.Items.Count; index++)
                {
                    lines.Add(UiStrings.Format(UiStrings.NodeListItem, index + 1, Value(list.Items[index])));
                }

                break;

            case TableNode table:
                lines.Add(UiStrings.Format(UiStrings.NodeTable, table.Rows.Count));
                if (table.HeaderRow is null)
                {
                    lines.Add(UiStrings.NodeTableNoHeaders);
                }
                else
                {
                    for (var column = 0; column < table.HeaderRow.Count; column++)
                    {
                        lines.Add(UiStrings.Format(
                            UiStrings.NodeTableHeaderCell,
                            column + 1,
                            Value(table.HeaderRow[column])));
                    }
                }

                for (var row = 0; row < table.Rows.Count; row++)
                {
                    for (var column = 0; column < table.Rows[row].Count; column++)
                    {
                        lines.Add(UiStrings.Format(
                            UiStrings.NodeTableCell,
                            row + 1,
                            column + 1,
                            Value(table.Rows[row][column])));
                    }
                }

                break;

            case Card card:
                lines.Add(UiStrings.Format(UiStrings.NodeCard, Value(card.Title)));
                lines.Add(UiStrings.Format(UiStrings.NodeBodyContent, Value(card.Body)));
                break;

            case ImageReference image:
                lines.Add(UiStrings.Format(UiStrings.NodeImage, Value(image.AltText)));
                lines.Add(UiStrings.Format(UiStrings.NodeImageAssetIdentity, Value(image.Asset.Value)));
                break;

            case BilingualPair pair:
                lines.Add(UiStrings.Format(UiStrings.NodeTextContent, Value(pair.SourceText)));
                lines.Add(UiStrings.Format(UiStrings.NodeTranslationContent, Value(pair.TargetText)));
                lines.Add(UiStrings.Format(
                    UiStrings.NodeLocalesContent,
                    Value(pair.SourceLocale),
                    Value(pair.TargetLocale)));
                break;

            case ChoiceSet choices:
                lines.Add(UiStrings.Format(UiStrings.NodeChoices, choices.Options.Count));
                for (var index = 0; index < choices.Options.Count; index++)
                {
                    lines.Add(UiStrings.Format(UiStrings.NodeChoiceItem, index + 1, Value(choices.Options[index])));
                }

                break;

            case EvidenceLink evidence:
                lines.Add(UiStrings.Format(UiStrings.NodeEvidence, Value(evidence.Claim)));
                lines.Add(UiStrings.Format(UiStrings.NodeSourcePointerContent, Value(evidence.SourcePointer)));
                break;

            case Citation citation:
                lines.Add(UiStrings.Format(UiStrings.NodeCitation, Value(citation.Text)));
                break;

            case TeacherOnlyNotice notice:
                lines.Add(UiStrings.Format(UiStrings.NodeTeacherOnly, Value(notice.Text)));
                break;

            case VectorGraphic graphic:
                AddVectorGraphic(lines, graphic);
                break;

            default:
                lines.Add(node.GetType().Name);
                break;
        }
    }

    private static void AddVectorGraphic(List<string> lines, VectorGraphic graphic)
    {
        lines.Add(UiStrings.Format(UiStrings.NodeVectorGraphic, Value(graphic.Description)));
        lines.Add(UiStrings.Format(
            UiStrings.NodeDimensionsContent,
            Number(graphic.WidthMm),
            Number(graphic.HeightMm)));
        lines.Add(UiStrings.Format(
            UiStrings.NodeVectorPrimitiveCounts,
            graphic.Primitives.OfType<LineSeg>().Count(),
            graphic.Primitives.OfType<CircleShape>().Count(),
            graphic.Primitives.OfType<RectShape>().Count(),
            graphic.Primitives.OfType<TextLabel>().Count()));

        foreach (var primitive in graphic.Primitives)
        {
            lines.Add(primitive switch
            {
                LineSeg line => UiStrings.Format(
                    UiStrings.NodeVectorLineDetail,
                    Number(line.X1),
                    Number(line.Y1),
                    Number(line.X2),
                    Number(line.Y2),
                    Number(line.StrokeWidthMm),
                    Boolean(line.Dashed)),
                CircleShape circle => UiStrings.Format(
                    UiStrings.NodeVectorCircleDetail,
                    Number(circle.CenterX),
                    Number(circle.CenterY),
                    Number(circle.RadiusMm),
                    Number(circle.StrokeWidthMm),
                    Boolean(circle.Filled)),
                RectShape rectangle => UiStrings.Format(
                    UiStrings.NodeVectorRectangleDetail,
                    Number(rectangle.X),
                    Number(rectangle.Y),
                    Number(rectangle.WidthMm),
                    Number(rectangle.HeightMm),
                    Number(rectangle.StrokeWidthMm),
                    Boolean(rectangle.Filled)),
                TextLabel label => UiStrings.Format(
                    UiStrings.NodeVectorTextLabelDetail,
                    Number(label.X),
                    Number(label.Y),
                    Value(label.Text),
                    Number(label.FontSizeMm),
                    Anchor(label.Anchor)),
                null => UiStrings.ExactValueNotSet,
                _ => primitive.GetType().Name,
            });
        }
    }

    private static string Value(string? value)
        => value is null
            ? UiStrings.ExactValueNotSet
            : UiStrings.Format(
                UiStrings.LoadedProjectExactStringFrame,
                value.Length,
                Escape(value));

    private static string Escape(string value)
    {
        var escaped = new System.Text.StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            switch (character)
            {
                case '\\':
                    escaped.Append("\\\\");
                    continue;
                case '"':
                    escaped.Append("\\\"");
                    continue;
                case '\r':
                    escaped.Append("\\r");
                    continue;
                case '\n':
                    escaped.Append("\\n");
                    continue;
                case '\t':
                    escaped.Append("\\t");
                    continue;
            }

            if (char.IsHighSurrogate(character)
                && index + 1 < value.Length
                && char.IsLowSurrogate(value[index + 1]))
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(value, index);
                if (RequiresEscape(category))
                {
                    AppendUnicodeEscape(escaped, character);
                    AppendUnicodeEscape(escaped, value[++index]);
                }
                else
                {
                    escaped.Append(character);
                    escaped.Append(value[++index]);
                }

                continue;
            }

            var characterCategory = char.GetUnicodeCategory(character);
            if (RequiresEscape(characterCategory)
                || (character != ' ' && char.IsWhiteSpace(character)))
            {
                AppendUnicodeEscape(escaped, character);
            }
            else
            {
                escaped.Append(character);
            }
        }

        return escaped.ToString();
    }

    private static bool RequiresEscape(UnicodeCategory category)
        => category is UnicodeCategory.Control
            or UnicodeCategory.Format
            or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator
            or UnicodeCategory.Surrogate;

    private static void AppendUnicodeEscape(System.Text.StringBuilder escaped, char character)
    {
        escaped.Append("\\u");
        escaped.Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
    }

    private static string Boolean(bool value) => value ? UiStrings.BooleanYes : UiStrings.BooleanNo;

    private static string Anchor(TextAnchor anchor) => anchor switch
    {
        TextAnchor.Start => UiStrings.TextAnchorStart,
        TextAnchor.Middle => UiStrings.TextAnchorMiddle,
        TextAnchor.End => UiStrings.TextAnchorEnd,
        _ => anchor.ToString(),
    };

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
