// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Domain;

public readonly record struct ArtifactId(Guid Value)
{
    public static ArtifactId NewId() => new(Guid.NewGuid());
}

/// <summary>
/// A narrow in-process use classification. The enum is not provenance: trusted
/// built-in workflows control where a non-Unknown value is accepted, and data
/// loaded from mutable packages is downgraded. Unknown fails closed at consumers
/// such as Access Remix. Formal/high-stakes assessment remains a distinct
/// refused class even when its visible title says only "worksheet."
/// </summary>
public enum ArtifactPurpose
{
    Unknown = 0,
    ClassroomSupport = 1,
    FormalOrHighStakesAssessment = 2,
}

/// <summary>
/// The closed inventory of engine-owned workflows allowed to attest purpose.
/// It is diagnostic provenance, not a serialized selector or a caller-provided
/// string. Package metadata never rehydrates one of these authorities.
/// </summary>
internal enum ArtifactPurposeAuthority
{
    BuiltInAllAboard,
    DesktopExplicitIntendedUse,
    AccessRemixDerivative,
    TrustedLayoutDerivative,
    TestFixture,
}

/// <summary>
/// Opaque in-memory evidence binding one purpose to one exact immutable document
/// object and lane. Only trusted assemblies can receive or derive this type; a
/// public caller can observe the classification but cannot manufacture it.
/// </summary>
internal sealed class ArtifactPurposeEvidence
{
    private readonly ArtifactDocument _document;

    private ArtifactPurposeEvidence(
        ArtifactDocument document,
        DataLane lane,
        ArtifactPurpose purpose,
        ArtifactPurposeAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!Enum.IsDefined(lane))
        {
            throw new ArgumentOutOfRangeException(nameof(lane), lane, "The artifact data lane is undefined.");
        }

        if (!Enum.IsDefined(purpose) || purpose == ArtifactPurpose.Unknown)
        {
            throw new ArgumentOutOfRangeException(
                nameof(purpose),
                purpose,
                "Purpose evidence must attest one defined, non-Unknown purpose.");
        }

        if (!Enum.IsDefined(authority))
        {
            throw new ArgumentOutOfRangeException(nameof(authority), authority, "The purpose authority is undefined.");
        }

        _document = document;
        Lane = lane;
        Purpose = purpose;
        Authority = authority;
    }

    internal ArtifactPurpose Purpose { get; }

    internal DataLane Lane { get; }

    internal ArtifactPurposeAuthority Authority { get; }

    internal static ArtifactPurposeEvidence ClassroomSupport(
        ArtifactDocument document,
        DataLane lane,
        ArtifactPurposeAuthority authority)
        => new(document, lane, ArtifactPurpose.ClassroomSupport, authority);

    internal static ArtifactPurposeEvidence ForTest(
        ArtifactDocument document,
        DataLane lane,
        ArtifactPurpose purpose)
        => new(document, lane, purpose, ArtifactPurposeAuthority.TestFixture);

    internal ArtifactPurposeEvidence Derive(
        ArtifactDocument document,
        DataLane lane,
        ArtifactPurposeAuthority authority)
        => new(document, lane, Purpose, authority);

    internal bool AppliesTo(ArtifactDocument document, DataLane lane)
        => ReferenceEquals(_document, document) && Lane == lane;
}

/// <summary>
/// An exact, immutable revision of an artifact. Approval binds to this, not to
/// the artifact in general. Construction is engine-owned so invalid revision,
/// lane, or raw purpose metadata cannot be supplied through the public API.
/// </summary>
public sealed class ArtifactRevision
{
    internal ArtifactRevision(
        ArtifactId id,
        int number,
        ArtifactDocument document,
        DataLane lane,
        ArtifactPurposeEvidence? purposeEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("An artifact revision requires a non-empty identity.", nameof(id));
        }

        if (number < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "Revision numbers begin at one.");
        }

        if (!Enum.IsDefined(lane))
        {
            throw new ArgumentOutOfRangeException(nameof(lane), lane, "The artifact data lane is undefined.");
        }

        if (purposeEvidence is not null && !purposeEvidence.AppliesTo(document, lane))
        {
            throw new InvalidOperationException(
                "Purpose evidence does not bind this exact immutable document and lane.");
        }

        Id = id;
        Number = number;
        Document = document;
        Lane = lane;
        PurposeEvidence = purposeEvidence;
    }

    public ArtifactId Id { get; }

    public int Number { get; }

    public ArtifactDocument Document { get; }

    public DataLane Lane { get; }

    public ArtifactPurpose Purpose => PurposeEvidence?.Purpose ?? ArtifactPurpose.Unknown;

    internal ArtifactPurposeEvidence? PurposeEvidence { get; }

    internal bool HasAuthenticatedPurpose(ArtifactPurpose purpose)
        => PurposeEvidence is { } evidence
            && evidence.Purpose == purpose
            && evidence.AppliesTo(Document, Lane);

    internal ArtifactRevision Edited(ArtifactDocument editedDocument)
    {
        ArgumentNullException.ThrowIfNull(editedDocument);
        if (Number == int.MaxValue)
        {
            throw new InvalidOperationException("The artifact revision number cannot be incremented safely.");
        }

        // Intended-use evidence binds the exact document that was classified.
        // A generic edit therefore returns to Unknown until a trusted workflow
        // explicitly classifies the new immutable document.
        return new ArtifactRevision(Id, Number + 1, editedDocument, Lane);
    }
}

/// <summary>
/// Unapproved content. No render, export, save-as-final, or print API accepts this type (ADR-004).
/// </summary>
public sealed class DraftArtifact
{
    internal DraftArtifact(ArtifactRevision revision)
    {
        Revision = revision ?? throw new ArgumentNullException(nameof(revision));
    }

    public ArtifactRevision Revision { get; }

    public static DraftArtifact New(
        ArtifactDocument document,
        DataLane lane)
        => new(new ArtifactRevision(ArtifactId.NewId(), 1, document, lane));

    /// <summary>
    /// Compatibility seam for trusted test assemblies exercising persisted
    /// legacy purpose claims. It is intentionally internal: production callers
    /// must present opaque workflow evidence instead of a raw enum.
    /// </summary>
    internal static DraftArtifact New(
        ArtifactDocument document,
        DataLane lane,
        ArtifactPurpose purpose)
        => purpose == ArtifactPurpose.Unknown
            ? New(document, lane)
            : NewClassified(document, lane, ArtifactPurposeEvidence.ForTest(document, lane, purpose));

    internal static DraftArtifact NewClassified(
        ArtifactDocument document,
        DataLane lane,
        ArtifactPurposeEvidence evidence)
        => new(new ArtifactRevision(ArtifactId.NewId(), 1, document, lane, evidence));

    /// <summary>Every edit is a new revision; revision numbers never repeat within an artifact.</summary>
    public DraftArtifact WithEditedDocument(ArtifactDocument editedDocument)
        => new(Revision.Edited(editedDocument));

    internal static DraftArtifact TrustedLayoutDerivative(
        ApprovedArtifact source,
        ArtifactDocument document,
        DataLane lane)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        var evidence = source.Revision.PurposeEvidence?.Derive(
            document,
            lane,
            ArtifactPurposeAuthority.TrustedLayoutDerivative);
        return evidence is null ? New(document, lane) : NewClassified(document, lane, evidence);
    }
}

/// <summary>Proof that a named teacher approved one exact revision at one moment.</summary>
public sealed record ApprovalReceipt(ArtifactId ArtifactId, int RevisionNumber, string ApprovedBy, DateTimeOffset ApprovedAtUtc);

/// <summary>
/// The only type the render, export, save-as-final, and print sinks accept (ADR-004).
/// Constructable solely through <see cref="ApprovalGate"/> — there is no other path.
/// </summary>
public sealed class ApprovedArtifact
{
    internal ApprovedArtifact(
        ArtifactRevision revision,
        ApprovalReceipt receipt,
        IReadOnlyList<ValidationIssue> validationIssues)
    {
        Revision = revision;
        Receipt = receipt;
        ValidationIssues = validationIssues;
    }

    public ArtifactRevision Revision { get; }

    public ApprovalReceipt Receipt { get; }

    /// <summary>
    /// The frozen findings accepted at the approval boundary. This includes a
    /// fresh run of the domain's structural validator over <see cref="Revision"/>
    /// as well as the caller-supplied recipe findings.
    /// </summary>
    public IReadOnlyList<ValidationIssue> ValidationIssues { get; }

    /// <summary>
    /// Any later edit invalidates approval: the result is a draft at a new revision,
    /// and the old receipt no longer matches anything renderable.
    /// </summary>
    public DraftArtifact Edit(ArtifactDocument editedDocument)
        => new(Revision.Edited(editedDocument));
}

/// <summary>
/// The single constructor of approved content. Approval is architectural, not ceremonial:
/// it requires the outstanding validation issues and fails closed on any blocking one.
/// </summary>
internal static class ApprovalGate
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

        var reportedIssues = FreezeAndValidateIssues(outstandingIssues, "reported");
        var structuralIssues = FreezeAndValidateIssues(
            DocumentValidator.Validate(draft.Revision.Document),
            "structural");

        if (DocumentValidator.HasBlockingIssues(reportedIssues)
            || DocumentValidator.HasBlockingIssues(structuralIssues))
        {
            throw new InvalidOperationException(
                "Approval is blocked: the draft has unresolved blocking validation issues. Fail closed; fix or reject.");
        }

        var acceptedIssues = FreezeAndValidateIssues(
            [.. reportedIssues.Concat(structuralIssues).Distinct()],
            "accepted");
        var receipt = new ApprovalReceipt(draft.Revision.Id, draft.Revision.Number, approvedBy, approvedAtUtc);
        return new ApprovedArtifact(draft.Revision, receipt, acceptedIssues);
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<ValidationIssue> FreezeAndValidateIssues(
        IReadOnlyList<ValidationIssue> issues,
        string source)
    {
        ArgumentNullException.ThrowIfNull(issues);

        var snapshot = issues.ToArray();
        for (var index = 0; index < snapshot.Length; index++)
        {
            var issue = snapshot[index] ?? throw new InvalidOperationException(
                    $"Approval is blocked: the {source} validation result contains a null issue at index {index}.");
            if (!Enum.IsDefined(issue.Severity))
            {
                throw new InvalidOperationException(
                    $"Approval is blocked: the {source} validation result contains an undefined severity at index {index}.");
            }

            if (string.IsNullOrWhiteSpace(issue.Code) || string.IsNullOrWhiteSpace(issue.Message))
            {
                throw new InvalidOperationException(
                    $"Approval is blocked: the {source} validation result contains an incomplete issue at index {index}.");
            }
        }

        return Array.AsReadOnly(snapshot);
    }
}
