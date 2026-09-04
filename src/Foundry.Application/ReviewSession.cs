// SPDX-License-Identifier: GPL-3.0-or-later
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
    private readonly IAssetCatalog? _sourceAssetCatalog;
    private readonly HashSet<ValidationIssue> _acknowledgedIssues = [];
    private IReadOnlyList<ApprovedAssetBinding> _reviewedAssetBindings;

    public ReviewSession(
        DraftArtifact draft,
        JobStateMachine machine,
        IArtifactValidator validator)
        : this(
            draft,
            machine,
            validator,
            new DomainApprovalGate(),
            ReviewViewContext.ManualDefault)
    {
    }

    public ReviewSession(
        DraftArtifact draft,
        JobStateMachine machine,
        IArtifactValidator validator,
        ReviewViewContext viewContext)
        : this(draft, machine, validator, new DomainApprovalGate(), viewContext)
    {
    }

    internal ReviewSession(
        DraftArtifact draft,
        JobStateMachine machine,
        IArtifactValidator validator,
        IApprovalGate gate)
        : this(draft, machine, validator, gate, ReviewViewContext.ManualDefault)
    {
    }

    internal ReviewSession(
        DraftArtifact draft,
        JobStateMachine machine,
        IArtifactValidator validator,
        IApprovalGate gate,
        ReviewViewContext viewContext)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(viewContext);

        if (machine.State != JobState.AwaitingTeacherReview)
        {
            throw new ArgumentException($"A review session begins at {JobState.AwaitingTeacherReview}; the machine is at {machine.State}.", nameof(machine));
        }

        Draft = draft;
        Machine = machine;
        _validator = validator;
        _gate = gate;
        _sourceAssetCatalog = viewContext.AssetCatalog;
        (ViewContext, _reviewedAssetBindings) = BindExactReviewAssets(
            viewContext,
            draft.Revision.Document,
            _sourceAssetCatalog);
        Issues = ValidateAndFreeze(_validator, draft.Revision.Document);
    }

    public DraftArtifact Draft { get; private set; }

    public JobStateMachine Machine { get; }

    /// <summary>
    /// Exact, non-authorizing source and visual-profile context displayed for
    /// every revision in this review. Edits change only the revision; they do
    /// not silently change what audience, scale, ordering, or source was shown.
    /// </summary>
    public ReviewViewContext ViewContext { get; private set; }

    public IReadOnlyList<ValidationIssue> Issues { get; private set; }

    /// <summary>The one approval result emitted by this exact review session.</summary>
    public ApprovedArtifact? ApprovedResult { get; private set; }

    public bool CanApprove
        => Machine.State == JobState.AwaitingTeacherReview
            && Draft.Revision.Lane != DataLane.Restricted
            && !DocumentValidator.HasBlockingIssues(Issues)
            && Issues.Where(issue => issue.RequiresAcknowledgement)
                .All(_acknowledgedIssues.Contains);

    public IReadOnlyList<ValidationIssue> RequiredAcknowledgements
        => Array.AsReadOnly(Issues.Where(issue => issue.RequiresAcknowledgement).ToArray());

    public void SetRequiredIssuesAcknowledged(bool acknowledged)
    {
        if (Machine.State != JobState.AwaitingTeacherReview)
        {
            throw new InvalidOperationException(
                "Warnings can be acknowledged only while the draft awaits teacher review.");
        }

        foreach (var issue in RequiredAcknowledgements)
        {
            if (acknowledged)
            {
                _acknowledgedIssues.Add(issue);
            }
            else
            {
                _acknowledgedIssues.Remove(issue);
            }
        }
    }

    public void ReplaceNode(int index, DocumentNode replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var nodes = CopyNodes();
        nodes[index] = replacement;
        ApplyEdit(nodes);
    }

    /// <summary>
    /// Replaces the selected node only when the review still holds the exact
    /// revision that an editor displayed. Modal editors use this overload so a
    /// stale result can never land on a later draft or a different selection.
    /// </summary>
    public void ReplaceNode(
        int index,
        ArtifactRevision expectedRevision,
        DocumentNode replacement)
    {
        ArgumentNullException.ThrowIfNull(expectedRevision);
        ArgumentNullException.ThrowIfNull(replacement);

        if (!ReferenceEquals(Draft.Revision, expectedRevision))
        {
            throw new InvalidOperationException(
                "The selected-element edit is stale because the exact draft revision changed. Reopen the editor and review the current element.");
        }

        ReplaceNode(index, replacement);
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
        var currentIssues = ValidateAndFreeze(_validator, Draft.Revision.Document);
        if (!Issues.SequenceEqual(currentIssues))
        {
            throw new InvalidOperationException(
                "Approval is not available: validation changed for the unchanged draft. Restart review and fail closed.");
        }

        if (!CanApprove)
        {
            throw new InvalidOperationException("Approval is not available: the review is not awaiting the teacher, or blocking issues remain.");
        }

        var approved = _gate.Approve(
            Draft,
            approvedBy,
            Issues,
            approvedAtUtc,
            _reviewedAssetBindings);
        var revision = Draft.Revision;
        var expectedEvidence = Issues
            .Concat(DocumentValidator.Validate(revision.Document))
            .Distinct()
            .ToArray();
        if (!ReferenceEquals(approved.Revision, revision)
            || approved.Receipt.ArtifactId != revision.Id
            || approved.Receipt.RevisionNumber != revision.Number
            || !string.Equals(approved.Receipt.ApprovedBy, approvedBy, StringComparison.Ordinal)
            || approved.Receipt.ApprovedAtUtc != approvedAtUtc
            || !approved.ValidationIssues.SequenceEqual(expectedEvidence)
            || !approved.AssetBindings.SequenceEqual(_reviewedAssetBindings))
        {
            throw new InvalidOperationException(
                "Approval is not available: the gate returned a different revision, approval identity/time, or validation result.");
        }

        ApprovedResult = approved;
        Machine.Transition(JobState.Approved);
        return approved;
    }

    public void Reject()
    {
        Machine.Transition(JobState.Declined);
    }

    public void Cancel()
    {
        if (Machine.State != JobState.AwaitingTeacherReview)
        {
            throw new InvalidOperationException(
                "Cancellation is available only while the draft awaits teacher review.");
        }

        _acknowledgedIssues.Clear();
        Machine.Transition(JobState.Cancelled);
    }

    private List<DocumentNode> CopyNodes() => [.. Draft.Revision.Document.Nodes];

    private void ApplyEdit(List<DocumentNode> nodes)
    {
        var editedDraft = Draft.WithEditedDocument(new ArtifactDocument(nodes, Draft.Revision.Document.Language));
        var reboundAssets = BindExactReviewAssets(
            ViewContext,
            editedDraft.Revision.Document,
            _sourceAssetCatalog);
        var editedIssues = ValidateAndFreeze(_validator, editedDraft.Revision.Document);

        Machine.Transition(JobState.TeacherEdited);
        Draft = editedDraft;
        (ViewContext, _reviewedAssetBindings) = reboundAssets;
        Issues = editedIssues;
        _acknowledgedIssues.Clear();
        Machine.Transition(JobState.AwaitingTeacherReview);
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<ValidationIssue> ValidateAndFreeze(
        IArtifactValidator validator,
        ArtifactDocument document)
    {
        var returned = validator.Validate(document)
            ?? throw new InvalidOperationException(
                "Review cannot begin: the validator returned no issue collection. Fail closed.");
        var snapshot = returned.ToArray();

        for (var index = 0; index < snapshot.Length; index++)
        {
            var issue = snapshot[index] ?? throw new InvalidOperationException(
                    $"Review cannot continue: the validator returned a null issue at index {index}. Fail closed.");
            if (!Enum.IsDefined(issue.Severity))
            {
                throw new InvalidOperationException(
                    $"Review cannot continue: the validator returned an undefined severity at index {index}. Fail closed.");
            }

            if (string.IsNullOrWhiteSpace(issue.Code) || string.IsNullOrWhiteSpace(issue.Message))
            {
                throw new InvalidOperationException(
                    $"Review cannot continue: the validator returned an incomplete issue at index {index}. Fail closed.");
            }
        }

        return Array.AsReadOnly(snapshot);
    }

    private static (ReviewViewContext Context, IReadOnlyList<ApprovedAssetBinding> Bindings) BindExactReviewAssets(
        ReviewViewContext viewContext,
        ArtifactDocument document,
        IAssetCatalog? sourceCatalog)
    {
        var snapshot = ExactAssetCatalogSnapshot.CaptureForReview(document, sourceCatalog);
        if (sourceCatalog is null && snapshot.Bindings.Count == 0)
        {
            return (viewContext, snapshot.Bindings);
        }

        IAssetCatalog? exactCatalog = sourceCatalog is null && snapshot.Bindings.Count == 0
            ? null
            : snapshot;
        return (
            new ReviewViewContext(viewContext.PreviewRequest, viewContext.Source, exactCatalog),
            snapshot.Bindings);
    }

    /// <summary>
    /// The sole production adapter allowed to invoke the Domain mint. Keeping
    /// it private prevents friend assemblies from bypassing this session's
    /// state, acknowledgement, revision, validator, and exact-asset checks.
    /// </summary>
    private sealed class DomainApprovalGate : IApprovalGate
    {
        public ApprovedArtifact Approve(
            DraftArtifact draft,
            string approvedBy,
            IReadOnlyList<ValidationIssue> outstandingIssues,
            DateTimeOffset approvedAtUtc,
            IReadOnlyList<ApprovedAssetBinding> reviewedAssetBindings)
            => ApprovalGate.Approve(
                draft,
                approvedBy,
                outstandingIssues,
                approvedAtUtc,
                reviewedAssetBindings);
    }
}
