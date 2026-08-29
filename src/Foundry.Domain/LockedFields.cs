namespace Foundry.Domain;

/// <summary>
/// The truth-bearing kinds of constitution requirement 10: values the model may
/// never alter, verified by deterministic comparison — not by judgment.
/// </summary>
public enum LockedFieldKind
{
    Date,
    Number,
    ProperName,
    Negation,
    Quotation,
    Citation,
    Unit,
    Url,
    Condition,
    RightsMetadata,
}

/// <summary>A value that must survive generation verbatim.</summary>
public sealed record LockedField(LockedFieldKind Kind, string ExactValue);

public static class LockedFieldValidator
{
    /// <summary>
    /// Every locked value must appear verbatim (ordinal comparison) somewhere in the
    /// document's text. Absence is a blocking issue: a dropped date or a softened
    /// negation is a factual failure, never a stylistic one.
    /// </summary>
    public static IReadOnlyList<ValidationIssue> Validate(ArtifactDocument document, IReadOnlyList<LockedField> lockedFields)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(lockedFields);

        var issues = new List<ValidationIssue>();
        var text = string.Join('\n', DocumentText.CollectStrings(document));

        foreach (var field in lockedFields)
        {
            if (string.IsNullOrWhiteSpace(field.ExactValue))
            {
                issues.Add(ValidationIssue.Blocking("locked.empty", $"A locked {field.Kind} has no value to protect."));
                continue;
            }

            if (!text.Contains(field.ExactValue, StringComparison.Ordinal))
            {
                issues.Add(ValidationIssue.Blocking(
                    "locked.missing",
                    $"Locked {field.Kind} '{field.ExactValue}' does not appear verbatim in the document."));
            }
        }

        return issues;
    }
}

/// <summary>Enumerates every human-readable string in a document, in document order.</summary>
public static class DocumentText
{
    public static IReadOnlyList<string> CollectStrings(ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var strings = new List<string>();

        foreach (var node in document.Nodes)
        {
            switch (node)
            {
                case Heading heading:
                    strings.Add(heading.Text);
                    break;
                case Paragraph paragraph:
                    strings.Add(paragraph.Text);
                    break;
                case OrderedSteps steps:
                    strings.AddRange(steps.Steps);
                    break;
                case UnorderedList list:
                    strings.AddRange(list.Items);
                    break;
                case TableNode table:
                    if (table.HeaderRow is not null)
                    {
                        strings.AddRange(table.HeaderRow);
                    }

                    foreach (var row in table.Rows)
                    {
                        strings.AddRange(row);
                    }

                    break;
                case Card card:
                    strings.Add(card.Title);
                    strings.Add(card.Body);
                    break;
                case ImageReference image:
                    strings.Add(image.AltText);
                    break;
                case BilingualPair pair:
                    strings.Add(pair.SourceText);
                    strings.Add(pair.TargetText);
                    break;
                case ChoiceSet choices:
                    strings.AddRange(choices.Options);
                    break;
                case EvidenceLink evidence:
                    strings.Add(evidence.Claim);
                    strings.Add(evidence.SourcePointer);
                    break;
                case Citation citation:
                    strings.Add(citation.Text);
                    break;
                case TeacherOnlyNotice notice:
                    strings.Add(notice.Text);
                    break;
                case VectorGraphic graphic:
                    strings.Add(graphic.Description);
                    strings.AddRange(graphic.Primitives.OfType<TextLabel>().Select(l => l.Text));
                    break;
                case StepRow step:
                    strings.Add(step.Text);
                    if (step.TargetText is not null)
                    {
                        strings.Add(step.TargetText);
                    }

                    if (step.Symbol is { } stepSymbol)
                    {
                        strings.Add(stepSymbol.AltText);
                    }

                    break;
                default:
                    break;
            }
        }

        return strings;
    }
}
