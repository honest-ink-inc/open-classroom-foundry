using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Application;

/// <summary>
/// Gate B as a UI-independent presenter (ADR-002): the review surface binds to
/// this; the logic never lives in a Form. Every edit is a new draft revision and
/// a TeacherEdited → AwaitingTeacherReview pass through the state machine;
/// approval is possible only while the machine awaits review and no blocking
/// issue stands.
/// </summary>
public sealed class ReviewSession
{
    private readonly IArtifactValidator _validator;
    private readonly IApprovalGate _gate;

    public ReviewSession(DraftArtifact draft, JobStateMachine machine, IArtifactValidator validator, IApprovalGate gate)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(gate);

        if (machine.State != JobState.AwaitingTeacherReview)
        {
            throw new ArgumentException($"A review session begins at {JobState.AwaitingTeacherReview}; the machine is at {machine.State}.", nameof(machine));
        }

        Draft = draft;
        Machine = machine;
        _validator = validator;
        _gate = gate;
        Issues = _validator.Validate(draft.Revision.Document);
    }

    public DraftArtifact Draft { get; private set; }

    public JobStateMachine Machine { get; }

    public IReadOnlyList<ValidationIssue> Issues { get; private set; }

    public bool CanApprove
        => Machine.State == JobState.AwaitingTeacherReview && !DocumentValidator.HasBlockingIssues(Issues);

    public void ReplaceNode(int index, DocumentNode replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var nodes = CopyNodes();
        nodes[index] = replacement;
        ApplyEdit(nodes);
    }

    public void RemoveNode(int index)
    {
        var nodes = CopyNodes();
        nodes.RemoveAt(index);
        ApplyEdit(nodes);
    }

    public void MoveNode(int fromIndex, int toIndex)
    {
        var nodes = CopyNodes();
        var node = nodes[fromIndex];
        nodes.RemoveAt(fromIndex);
        nodes.Insert(toIndex, node);
        ApplyEdit(nodes);
    }

    public ApprovedArtifact Approve(string approvedBy, DateTimeOffset approvedAtUtc)
    {
        if (!CanApprove)
        {
            throw new InvalidOperationException("Approval is not available: the review is not awaiting the teacher, or blocking issues remain.");
        }

        var approved = _gate.Approve(Draft, approvedBy, Issues, approvedAtUtc);
        Machine.Transition(JobState.Approved);
        return approved;
    }

    public void Reject()
    {
        Machine.Transition(JobState.Declined);
    }

    private List<DocumentNode> CopyNodes() => [.. Draft.Revision.Document.Nodes];

    private void ApplyEdit(List<DocumentNode> nodes)
    {
        Machine.Transition(JobState.TeacherEdited);
        Draft = Draft.WithEditedDocument(new ArtifactDocument(nodes, Draft.Revision.Document.Language));
        Issues = _validator.Validate(Draft.Revision.Document);
        Machine.Transition(JobState.AwaitingTeacherReview);
    }
}
