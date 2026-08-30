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
            UiStrings.FormatWithoutMnemonic(
                UiStrings.LoadedProjectDocumentDigest,
                Contracts.ArtifactDocumentFingerprint.Compute(document)),
            UiStrings.FormatWithoutMnemonic(
                UiStrings.LoadedProjectDocumentLanguage,
                Value(document.Language)),
        };

        for (var index = 0; index < document.Nodes.Count; index++)
        {
            lines.Add(string.Empty);
            lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.LoadedProjectDocumentElement, index + 1));
            AddNode(lines, document.Nodes[index]);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AddNode(List<string> lines, DocumentNode node)
    {
        switch (node)
        {
            case Heading heading:
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeHeading, heading.Level, Value(heading.Text)));
                break;

            case Paragraph paragraph:
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeParagraph, Value(paragraph.Text)));
                break;

            case OrderedSteps steps:
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeSteps, steps.Steps.Count));
                for (var index = 0; index < steps.Steps.Count; index++)
                {
                    lines.Add(UiStrings.FormatWithoutMnemonic(
                        UiStrings.NodeOrderedStepItem,
                        index + 1,
                        Value(steps.Steps[index])));
                }

                break;

            case StepRow step:
                lines.Add(UiStrings.WithoutMnemonic(UiStrings.NodeStepRow));
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeTextContent, Value(step.Text)));
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeTranslationContent, Value(step.TargetText)));
                lines.Add(UiStrings.FormatWithoutMnemonic(
                    UiStrings.NodeLocalesContent,
                    Value(step.SourceLocale),
                    Value(step.TargetLocale)));
                lines.Add(UiStrings.FormatWithoutMnemonic(
                    UiStrings.NodeImageAssetIdentity,
                    Value(step.Symbol?.Asset.Value)));
                lines.Add(UiStrings.FormatWithoutMnemonic(
                    UiStrings.NodeSymbolAltContent,
                    Value(step.Symbol?.AltText)));
                break;

            case PageBreak:
                lines.Add(UiStrings.WithoutMnemonic(UiStrings.NodePageBreak));
                break;

            case UnorderedList list:
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeList, list.Items.Count));
                for (var index = 0; index < list.Items.Count; index++)
                {
                    lines.Add(UiStrings.FormatWithoutMnemonic(
                        UiStrings.NodeListItem,
                        index + 1,
                        Value(list.Items[index])));
                }

                break;

            case TableNode table:
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeTable, table.Rows.Count));
                if (table.HeaderRow is null)
                {
                    lines.Add(UiStrings.WithoutMnemonic(UiStrings.NodeTableNoHeaders));
                }
                else
                {
                    for (var column = 0; column < table.HeaderRow.Count; column++)
                    {
                        lines.Add(UiStrings.FormatWithoutMnemonic(
                            UiStrings.NodeTableHeaderCell,
                            column + 1,
                            Value(table.HeaderRow[column])));
                    }
                }

                for (var row = 0; row < table.Rows.Count; row++)
                {
                    for (var column = 0; column < table.Rows[row].Count; column++)
                    {
                        lines.Add(UiStrings.FormatWithoutMnemonic(
                            UiStrings.NodeTableCell,
                            row + 1,
                            column + 1,
                            Value(table.Rows[row][column])));
                    }
                }

                break;

            case Card card:
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeCard, Value(card.Title)));
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeBodyContent, Value(card.Body)));
                break;

            case ImageReference image:
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeImage, Value(image.AltText)));
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeImageAssetIdentity, Value(image.Asset.Value)));
                break;

            case BilingualPair pair:
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeTextContent, Value(pair.SourceText)));
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeTranslationContent, Value(pair.TargetText)));
                lines.Add(UiStrings.FormatWithoutMnemonic(
                    UiStrings.NodeLocalesContent,
                    Value(pair.SourceLocale),
                    Value(pair.TargetLocale)));
                break;

            case ChoiceSet choices:
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeChoices, choices.Options.Count));
                for (var index = 0; index < choices.Options.Count; index++)
                {
                    lines.Add(UiStrings.FormatWithoutMnemonic(
                        UiStrings.NodeChoiceItem,
                        index + 1,
                        Value(choices.Options[index])));
                }

                break;

            case EvidenceLink evidence:
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeEvidence, Value(evidence.Claim)));
                lines.Add(UiStrings.FormatWithoutMnemonic(
                    UiStrings.NodeSourcePointerContent,
                    Value(evidence.SourcePointer)));
                break;

            case Citation citation:
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeCitation, Value(citation.Text)));
                break;

            case TeacherOnlyNotice notice:
                lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeTeacherOnly, Value(notice.Text)));
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
        lines.Add(UiStrings.FormatWithoutMnemonic(UiStrings.NodeVectorGraphic, Value(graphic.Description)));
        lines.Add(UiStrings.FormatWithoutMnemonic(
            UiStrings.NodeDimensionsContent,
            Number(graphic.WidthMm),
            Number(graphic.HeightMm)));
        lines.Add(UiStrings.FormatWithoutMnemonic(
            UiStrings.NodeVectorPrimitiveCounts,
            graphic.Primitives.OfType<LineSeg>().Count(),
            graphic.Primitives.OfType<CircleShape>().Count(),
            graphic.Primitives.OfType<RectShape>().Count(),
            graphic.Primitives.OfType<TextLabel>().Count()));

        foreach (var primitive in graphic.Primitives)
        {
            lines.Add(primitive switch
            {
                LineSeg line => UiStrings.FormatWithoutMnemonic(
                    UiStrings.NodeVectorLineDetail,
                    Number(line.X1),
                    Number(line.Y1),
                    Number(line.X2),
                    Number(line.Y2),
                    Number(line.StrokeWidthMm),
                    Boolean(line.Dashed)),
                CircleShape circle => UiStrings.FormatWithoutMnemonic(
                    UiStrings.NodeVectorCircleDetail,
                    Number(circle.CenterX),
                    Number(circle.CenterY),
                    Number(circle.RadiusMm),
                    Number(circle.StrokeWidthMm),
                    Boolean(circle.Filled)),
                RectShape rectangle => UiStrings.FormatWithoutMnemonic(
                    UiStrings.NodeVectorRectangleDetail,
                    Number(rectangle.X),
                    Number(rectangle.Y),
                    Number(rectangle.WidthMm),
                    Number(rectangle.HeightMm),
                    Number(rectangle.StrokeWidthMm),
                    Boolean(rectangle.Filled)),
                TextLabel label => UiStrings.FormatWithoutMnemonic(
                    UiStrings.NodeVectorTextLabelDetail,
                    Number(label.X),
                    Number(label.Y),
                    Value(label.Text),
                    Number(label.FontSizeMm),
                    Anchor(label.Anchor)),
                null => UiStrings.WithoutMnemonic(UiStrings.ExactValueNotSet),
                _ => primitive.GetType().Name,
            });
        }
    }

    private static string Value(string? value)
        => value is null
            ? UiStrings.WithoutMnemonic(UiStrings.ExactValueNotSet)
            : UiStrings.FormatWithoutMnemonic(
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

    private static string Boolean(bool value)
        => UiStrings.WithoutMnemonic(value ? UiStrings.BooleanYes : UiStrings.BooleanNo);

    private static string Anchor(TextAnchor anchor) => anchor switch
    {
        TextAnchor.Start => UiStrings.WithoutMnemonic(UiStrings.TextAnchorStart),
        TextAnchor.Middle => UiStrings.WithoutMnemonic(UiStrings.TextAnchorMiddle),
        TextAnchor.End => UiStrings.WithoutMnemonic(UiStrings.TextAnchorEnd),
        _ => anchor.ToString(),
    };

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
