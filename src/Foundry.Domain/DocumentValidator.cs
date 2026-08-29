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

                    break;

                case TableNode table:
                    if (table.Rows.Count == 0)
                    {
                        issues.Add(ValidationIssue.Blocking("doc.table.empty", "A table has no rows."));
                    }

                    break;

                case Card card:
                    RequireText(issues, card.Title, "doc.card.title", "A card has no title.");
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
                    RequireText(issues, pair.SourceLocale, "doc.bilingual.locale", "A bilingual pair has no source locale.");
                    RequireText(issues, pair.TargetLocale, "doc.bilingual.locale", "A bilingual pair has no target locale.");
                    break;

                case ChoiceSet choices:
                    if (choices.Options.Count < 2)
                    {
                        issues.Add(ValidationIssue.Blocking("doc.choice.options", "A choice set offers fewer than two options; a single option is not a choice."));
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

    private static void RequireText(List<ValidationIssue> issues, string value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(ValidationIssue.Blocking(code, message));
        }
    }
}
