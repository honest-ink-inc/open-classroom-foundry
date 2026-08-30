// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Domain;

/// <summary>
/// Deterministic structural validation of the semantic document. Module-specific
/// invariants layer on top; these rules hold for every artifact.
/// </summary>
public static class DocumentValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var issues = new List<ValidationIssue>();

        if (document.Language is not null && !LanguageTag.IsStructurallyValid(document.Language))
        {
            issues.Add(ValidationIssue.Blocking(
                "doc.language.tag",
                "The document language is not a structurally valid language tag (for example en, es-MX, or zh-Hant)."));
        }

        foreach (var node in document.Nodes)
        {
            switch (node)
            {
                case Heading heading:
                    if (heading.Level is < 1 or > 6)
                    {
                        issues.Add(ValidationIssue.Blocking("doc.heading.level", $"Heading level {heading.Level} is outside 1–6."));
                    }

                    RequireText(issues, heading.Text, "doc.heading.empty", "A heading has no text.");
                    break;

                case Paragraph paragraph:
                    RequireText(issues, paragraph.Text, "doc.paragraph.empty", "A paragraph has no text.");
                    break;

                case OrderedSteps steps:
                    if (steps.Steps.Count == 0)
                    {
                        issues.Add(ValidationIssue.Blocking("doc.steps.empty", "An ordered step sequence has no steps."));
                    }

                    foreach (var step in steps.Steps)
                    {
                        RequireText(issues, step, "doc.steps.blank-step", "A step has no text.");
                    }

                    break;

                case UnorderedList list:
                    if (list.Items.Count == 0)
                    {
                        issues.Add(ValidationIssue.Blocking("doc.list.empty", "A list has no items."));
                    }

                    foreach (var item in list.Items)
                    {
                        RequireText(issues, item, "doc.list.blank-item", "A list item has no text.");
                    }

                    break;

                case TableNode table:
                    ValidateTable(issues, table);
                    break;

                case Card card:
                    RequireText(issues, card.Title, "doc.card.title", "A card has no title.");
                    RequireText(issues, card.Body, "doc.card.body", "A card has no body.");
                    break;

                case ImageReference image:
                    if (string.IsNullOrWhiteSpace(image.Asset.Value))
                    {
                        issues.Add(ValidationIssue.Blocking("doc.image.asset", "An image reference has no asset identity."));
                    }

                    RequireText(issues, image.AltText, "doc.image.alt-text", "An image has no alternative text.");
                    break;

                case BilingualPair pair:
                    RequireText(issues, pair.SourceText, "doc.bilingual.source", "A bilingual pair has no source text.");
                    RequireText(issues, pair.TargetText, "doc.bilingual.target", "A bilingual pair has no target text.");
                    RequireText(issues, pair.SourceLocale, "doc.bilingual.locale", "A bilingual pair has no source locale.");
                    RequireText(issues, pair.TargetLocale, "doc.bilingual.locale", "A bilingual pair has no target locale.");
                    RequireLanguageTag(issues, pair.SourceLocale, "doc.bilingual.locale-tag", "source");
                    RequireLanguageTag(issues, pair.TargetLocale, "doc.bilingual.locale-tag", "target");
                    break;

                case ChoiceSet choices:
                    if (choices.Options.Count < 2)
                    {
                        issues.Add(ValidationIssue.Blocking("doc.choice.options", "A choice set offers fewer than two options; a single option is not a choice."));
                    }

                    var uniqueChoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var option in choices.Options)
                    {
                        RequireText(issues, option, "doc.choice.blank-option", "A choice option has no text.");
                        if (!string.IsNullOrWhiteSpace(option)
                            && !uniqueChoices.Add(option.Trim()))
                        {
                            issues.Add(ValidationIssue.Blocking(
                                "doc.choice.duplicate-option",
                                "A choice set repeats the same option."));
                        }
                    }

                    break;

                case EvidenceLink evidence:
                    RequireText(issues, evidence.Claim, "doc.evidence.claim", "An evidence link has no claim.");
                    RequireText(issues, evidence.SourcePointer, "doc.evidence.source", "An evidence link points at nothing.");
                    break;

                case Citation citation:
                    RequireText(issues, citation.Text, "doc.citation.empty", "A citation has no text.");
                    break;

                case TeacherOnlyNotice notice:
                    RequireText(issues, notice.Text, "doc.teacher-notice.empty", "A teacher-only notice has no text.");
                    break;

                case StepRow step:
                    RequireText(issues, step.Text, "doc.step-row.text", "A step has no text.");
                    if (step.Symbol is { } stepSymbol)
                    {
                        if (string.IsNullOrWhiteSpace(stepSymbol.Asset.Value))
                        {
                            issues.Add(ValidationIssue.Blocking("doc.image.asset", "A step symbol has no asset identity."));
                        }

                        RequireText(issues, stepSymbol.AltText, "doc.image.alt-text", "A step symbol has no alternative text.");
                    }

                    if (step.TargetText is not null
                        || step.SourceLocale is not null
                        || step.TargetLocale is not null)
                    {
                        RequireText(issues, step.TargetText, "doc.step-row.target", "A bilingual step has a blank translation.");
                        RequireText(issues, step.SourceLocale, "doc.step-row.locale", "A bilingual step has no source locale.");
                        RequireText(issues, step.TargetLocale, "doc.step-row.locale", "A bilingual step has no target locale.");
                        RequireLanguageTag(issues, step.SourceLocale, "doc.step-row.locale-tag", "source");
                        RequireLanguageTag(issues, step.TargetLocale, "doc.step-row.locale-tag", "target");
                    }

                    break;

                case PageBreak:
                    break;

                case VectorGraphic graphic:
                    ValidateVectorGraphic(issues, graphic);
                    break;

                default:
                    issues.Add(ValidationIssue.Blocking("doc.node.unknown", $"Unknown node type {node.GetType().Name}."));
                    break;
            }
        }

        return issues;
    }

    public static bool HasBlockingIssues(IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return issues.Any(i => i.Severity == ValidationSeverity.Blocking);
    }

    private static void ValidateTable(List<ValidationIssue> issues, TableNode table)
    {
        if (table.Rows.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("doc.table.empty", "A table has no rows."));
        }

        var columnCount = table.HeaderRow?.Count
            ?? (table.Rows.Count == 0 ? 0 : table.Rows[0].Count);
        if (columnCount == 0)
        {
            issues.Add(ValidationIssue.Blocking("doc.table.columns", "A table has no columns."));
        }

        if (table.HeaderRow is not null)
        {
            foreach (var header in table.HeaderRow)
            {
                RequireText(issues, header, "doc.table.blank-cell", "A table header cell is blank.");
            }
        }

        foreach (var row in table.Rows)
        {
            if (row.Count != columnCount)
            {
                issues.Add(ValidationIssue.Blocking(
                    "doc.table.ragged",
                    "A table row has a different number of cells from the other rows."));
            }

            foreach (var cell in row)
            {
                RequireText(issues, cell, "doc.table.blank-cell", "A table body cell is blank.");
            }
        }
    }

    private static void ValidateVectorGraphic(List<ValidationIssue> issues, VectorGraphic graphic)
    {
        if (!PositiveFinite(graphic.WidthMm) || !PositiveFinite(graphic.HeightMm))
        {
            issues.Add(ValidationIssue.Blocking("doc.vector.size", "A vector sheet has no finite positive printable size."));
        }

        if (graphic.Primitives.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("doc.vector.empty", "A vector sheet has no geometry."));
        }

        RequireText(issues, graphic.Description, "doc.vector.description", "A vector sheet has no accessible description.");
        foreach (var primitive in graphic.Primitives)
        {
            switch (primitive)
            {
                case LineSeg line:
                    if (!Finite(line.X1)
                        || !Finite(line.Y1)
                        || !Finite(line.X2)
                        || !Finite(line.Y2)
                        || !PositiveFinite(line.StrokeWidthMm)
                        || (line.X1 == line.X2 && line.Y1 == line.Y2))
                    {
                        issues.Add(ValidationIssue.Blocking(
                            "doc.vector.line",
                            "A vector line has non-finite coordinates, a non-positive stroke, or no length."));
                    }

                    break;

                case CircleShape circle:
                    if (!Finite(circle.CenterX)
                        || !Finite(circle.CenterY)
                        || !PositiveFinite(circle.RadiusMm)
                        || !PositiveFinite(circle.StrokeWidthMm))
                    {
                        issues.Add(ValidationIssue.Blocking(
                            "doc.vector.circle",
                            "A vector circle has non-finite coordinates or non-positive geometry."));
                    }

                    break;

                case RectShape rectangle:
                    if (!Finite(rectangle.X)
                        || !Finite(rectangle.Y)
                        || !PositiveFinite(rectangle.WidthMm)
                        || !PositiveFinite(rectangle.HeightMm)
                        || !PositiveFinite(rectangle.StrokeWidthMm))
                    {
                        issues.Add(ValidationIssue.Blocking(
                            "doc.vector.rectangle",
                            "A vector rectangle has non-finite coordinates or non-positive geometry."));
                    }

                    break;

                case TextLabel label:
                    RequireText(issues, label.Text, "doc.vector.label", "A vector text label is blank.");
                    if (!Finite(label.X)
                        || !Finite(label.Y)
                        || !PositiveFinite(label.FontSizeMm))
                    {
                        issues.Add(ValidationIssue.Blocking(
                            "doc.vector.label-geometry",
                            "A vector text label has non-finite coordinates or a non-positive font size."));
                    }

                    if (!Enum.IsDefined(label.Anchor))
                    {
                        issues.Add(ValidationIssue.Blocking(
                            "doc.vector.anchor",
                            "A vector text label has an unknown anchor."));
                    }

                    break;

                case null:
                    issues.Add(ValidationIssue.Blocking(
                        "doc.vector.primitive",
                        "A vector sheet contains a missing primitive."));
                    break;

                default:
                    issues.Add(ValidationIssue.Blocking(
                        "doc.vector.primitive",
                        $"Unknown vector primitive type {primitive.GetType().Name}."));
                    break;
            }
        }
    }

    private static bool Finite(double value) => double.IsFinite(value);

    private static bool PositiveFinite(double value) => double.IsFinite(value) && value > 0;

    private static void RequireText(List<ValidationIssue> issues, string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(ValidationIssue.Blocking(code, message));
        }
    }

    private static void RequireLanguageTag(
        List<ValidationIssue> issues,
        string? value,
        string code,
        string role)
    {
        // Missing values already receive the established locale-required issue;
        // this finding distinguishes malformed keyboard input from absence.
        if (!string.IsNullOrWhiteSpace(value) && !LanguageTag.IsStructurallyValid(value))
        {
            issues.Add(ValidationIssue.Blocking(
                code,
                $"The {role} locale is not a structurally valid language tag (for example en, es-MX, or zh-Hant)."));
        }
    }
}
