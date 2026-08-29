using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Modules.BuiltIn.RubricRelay;

public enum EvidenceStatus
{
    EvidenceFound,
    NoEvidence,
    Insufficient,
    Unreadable,
}

public sealed record CriterionEvidence(string Criterion, EvidenceStatus Status, string? ExactQuote = null, string? TeacherNote = null);

public sealed record RubricRelayResult(ArtifactDocument Document, IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Rubric Relay (plan §10.8), Amber by design, synthetic inputs only until
/// written district authorization. Framed as conference preparation everywhere —
/// the two questions are the product; the matrix is their evidence. Quotation
/// fidelity is deterministic law: a quote that is not verbatim in the artifact
/// blocks; a quote attached to no-evidence blocks; and no numeric field exists
/// anywhere, so a score cannot be represented, let alone leaked.
/// </summary>
public static class RubricRelayBuilder
{
    public static RubricRelayResult Build(
        string assignment,
        string deidentifiedArtifactText,
        IReadOnlyList<CriterionEvidence> matrix,
        string oneStrength,
        string oneRevisionMove,
        string conferenceQuestion1,
        string conferenceQuestion2)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignment);
        ArgumentNullException.ThrowIfNull(matrix);

        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(deidentifiedArtifactText))
        {
            issues.Add(ValidationIssue.Blocking("relay.artifact", "There is no work to prepare a conference about."));
        }

        if (matrix.Count == 0)
        {
            issues.Add(ValidationIssue.Blocking("relay.matrix", "The evidence-to-criterion matrix is empty."));
        }

        foreach (var entry in matrix)
        {
            if (entry.Status == EvidenceStatus.EvidenceFound)
            {
                if (string.IsNullOrWhiteSpace(entry.ExactQuote))
                {
                    issues.Add(ValidationIssue.Blocking("relay.evidence",
                        $"'{entry.Criterion}': evidence claimed with nothing to point to."));
                }
                else if (!deidentifiedArtifactText.Contains(entry.ExactQuote, StringComparison.Ordinal))
                {
                    issues.Add(ValidationIssue.Blocking("relay.quotation",
                        $"'{entry.Criterion}': the quote is not verbatim in the work. Quotation fidelity is one hundred percent or nothing."));
                }
            }
            else if (!string.IsNullOrWhiteSpace(entry.ExactQuote))
            {
                issues.Add(ValidationIssue.Blocking("relay.claim",
                    $"'{entry.Criterion}': a quote attached to {entry.Status} is a claim without evidence; absence, unreadability, and insufficiency stand alone."));
            }
        }

        foreach (var (value, code, message) in new[]
        {
            (oneStrength, "relay.strength", "Name exactly one verified strength."),
            (oneRevisionMove, "relay.revision", "Name exactly one prioritized revision move."),
            (conferenceQuestion1, "relay.questions", "Two conference questions are the product; the first is missing."),
            (conferenceQuestion2, "relay.questions", "Two conference questions are the product; the second is missing."),
        })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                issues.Add(ValidationIssue.Blocking(code, message));
            }
        }

        if (DocumentValidator.HasBlockingIssues(issues))
        {
            return new RubricRelayResult(ArtifactDocument.Empty, issues);
        }

        var nodes = new List<DocumentNode>
        {
            new Heading(1, $"Conference preparation: {assignment}"),
            new TableNode(
                ["Criterion", "Evidence status", "Exact quote", "Teacher note"],
                [.. matrix.Select(IReadOnlyList<string> (e) =>
                [
                    e.Criterion,
                    e.Status switch
                    {
                        EvidenceStatus.EvidenceFound => "Evidence found",
                        EvidenceStatus.NoEvidence => "No evidence",
                        EvidenceStatus.Insufficient => "Insufficient",
                        _ => "Unreadable",
                    },
                    e.ExactQuote ?? "—",
                    e.TeacherNote ?? "—",
                ])]),
            new Card("One strength to name", oneStrength),
            new Card("One revision move", oneRevisionMove),
            new Heading(2, "Two conference questions"),
            new OrderedSteps([conferenceQuestion1, conferenceQuestion2]),
            new TeacherOnlyNotice(
                "Conference preparation, Amber-governed: no score exists here and none can be derived. The artifact "
                + "text purges with the session; only this teacher-approved preparation remains."),
        };

        return new RubricRelayResult(new ArtifactDocument(nodes), issues);
    }

    public static RecipeManifest Recipe { get; } = new(
        Id: "rubric-relay",
        Version: "0.1.0",
        License: "GPL-3.0-or-later",
        MinimumEngineVersion: EngineIdentity.EngineVersion,
        InstructionalPurpose: "Prepare one focused conference: an evidence-to-criterion matrix, one strength, one revision move, and the two questions that are the product.",
        ProhibitedPurposes:
        [
            "scores, grades, rankings, or batch comparison of any kind",
            "effort, personality, disability, language-status, plagiarism, or AI-authorship inference",
            "persistent portfolios or histories",
            "real student artifacts before written district authorization - synthetic fixtures only",
        ],
        AllowedInputKinds: ["deidentified-artifact-synthetic", "teacher-approved-rubric"],
        MaximumLane: DataLane.Amber,
        RequiredProviderCapabilities: [],
        OutputSchemaId: "schema.rubric-relay.v1",
        ValidatorIds: ["document.structural", "relay.quotation"],
        EditorId: "editor.review-session",
        RendererId: "renderer.accessible-html",
        SupportedExports: [RenderTarget.AccessibleHtml, RenderTarget.PrintHtml],
        Warnings: ["No evidence means no evaluative claim; feedback preserves authorial choice."],
        EvaluationSuiteVersion: "0.1");
}
