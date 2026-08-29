// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Domain;

public readonly record struct ArtifactId(Guid Value)
{
    public static ArtifactId NewId() => new(Guid.NewGuid());
}

/// <summary>An exact, immutable revision of an artifact. Approval binds to this, not to the artifact in general.</summary>
public sealed record ArtifactRevision(ArtifactId Id, int Number, ArtifactDocument Document, DataLane Lane);

/// <summary>
/// Unapproved content. No render, export, save-as-final, or print API accepts this type (ADR-004).
/// </summary>
public sealed record DraftArtifact(ArtifactRevision Revision)
{
    public static DraftArtifact New(ArtifactDocument document, DataLane lane)
        => new(new ArtifactRevision(ArtifactId.NewId(), 1, document, lane));

    /// <summary>Every edit is a new revision; revision numbers never repeat within an artifact.</summary>
    public DraftArtifact WithEditedDocument(ArtifactDocument editedDocument)
        => new(Revision with { Number = Revision.Number + 1, Document = editedDocument });
}

/// <summary>Proof that a named teacher approved one exact revision at one moment.</summary>
public sealed record ApprovalReceipt(ArtifactId ArtifactId, int RevisionNumber, string ApprovedBy, DateTimeOffset ApprovedAtUtc);

/// <summary>
/// The only type the render, export, save-as-final, and print sinks accept (ADR-004).
/// Constructable solely through <see cref="ApprovalGate"/> — there is no other path.
/// </summary>
public sealed class ApprovedArtifact
{
    internal ApprovedArtifact(ArtifactRevision revision, ApprovalReceipt receipt)
    {
        Revision = revision;
        Receipt = receipt;
    }

    public ArtifactRevision Revision { get; }

    public ApprovalReceipt Receipt { get; }

    /// <summary>
    /// Any later edit invalidates approval: the result is a draft at a new revision,
    /// and the old receipt no longer matches anything renderable.
    /// </summary>
    public DraftArtifact Edit(ArtifactDocument editedDocument)
        => new(Revision with { Number = Revision.Number + 1, Document = editedDocument });
}

/// <summary>
/// The single constructor of approved content. Approval is architectural, not ceremonial:
/// it requires the outstanding validation issues and fails closed on any blocking one.
/// </summary>
public static class ApprovalGate
{
    public static ApprovedArtifact Approve(
        DraftArtifact draft,
        string approvedBy,
        IReadOnlyList<ValidationIssue> outstandingIssues,
        DateTimeOffset approvedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(outstandingIssues);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedBy);

        if (DocumentValidator.HasBlockingIssues(outstandingIssues))
        {
            throw new InvalidOperationException(
                "Approval is blocked: the draft has unresolved blocking validation issues. Fail closed; fix or reject.");
        }

        var receipt = new ApprovalReceipt(draft.Revision.Id, draft.Revision.Number, approvedBy, approvedAtUtc);
        return new ApprovedArtifact(draft.Revision, receipt);
    }
}
