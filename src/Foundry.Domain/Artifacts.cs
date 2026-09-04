// SPDX-License-Identifier: GPL-3.0-or-later
namespace Foundry.Domain;

public readonly record struct ArtifactId(Guid Value)
{
    public static ArtifactId NewId() => new(Guid.NewGuid());
}

/// <summary>
/// A narrow in-process use classification. The enum is not provenance: raw
/// values loaded from mutable packages do not rehydrate engine authority. The
/// current production build issues no purpose authority and exposes no Access
/// consumer; these non-Unknown values remain reserved for authenticated
/// workflow evidence and compatibility tests until protected review establishes
/// a consumer that can enforce them.
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
        ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(document);
        var lane = source.Revision.Lane;
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
/// Exact content identity for one image asset shown at Gate B. This record is
/// data, not authority: only the internal approval gate can attach bindings to
/// an <see cref="ApprovedArtifact"/>, and output adapters must re-establish the
/// same id, MIME type, and SHA-256 over owned bytes before rendering or saving.
/// </summary>
public sealed record ApprovedAssetBinding
{
    public ApprovedAssetBinding(
        AssetId assetId,
        string sha256,
        string mimeType,
        string provenanceSha256)
    {
        if (string.IsNullOrWhiteSpace(assetId.Value))
        {
            throw new ArgumentException("An approved asset binding requires an asset identity.", nameof(assetId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ValidateSha256(sha256, nameof(sha256));
        ValidateSha256(provenanceSha256, nameof(provenanceSha256));
        AssetId = assetId;
        Sha256 = sha256.ToUpperInvariant();
        MimeType = mimeType;
        ProvenanceSha256 = provenanceSha256.ToUpperInvariant();
    }

    public AssetId AssetId { get; }

    public string Sha256 { get; }

    public string MimeType { get; }

    /// <summary>
    /// Canonical digest of every field in the provenance record shown and
    /// accepted with this asset, including rights and attribution fields.
    /// </summary>
    public string ProvenanceSha256 { get; }

    private static void ValidateSha256(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length != 64 || !value.All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "An approved asset binding requires a 64-character SHA-256 value.",
                parameterName);
        }
    }
}

/// <summary>
/// The only type the render, export, save-as-final, and print sinks accept (ADR-004).
/// Constructable solely through <see cref="ApprovalGate"/> — there is no other path.
/// </summary>
public sealed class ApprovedArtifact
{
    private ApprovedArtifact(
        ArtifactRevision revision,
        ApprovalReceipt receipt,
        IReadOnlyList<ValidationIssue> validationIssues,
        IReadOnlyList<ApprovedAssetBinding> assetBindings)
    {
        Revision = revision;
        Receipt = receipt;
        ValidationIssues = validationIssues;
        AssetBindings = assetBindings;
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
    /// Canonically ordered identities of the exact image bytes shown for this
    /// revision at Gate B. An image reference without one of these bindings is
    /// never approvable; an output catalog must match every binding exactly.
    /// </summary>
    public IReadOnlyList<ApprovedAssetBinding> AssetBindings { get; }

    /// <summary>
    /// Any later edit invalidates approval: the result is a draft at a new revision,
    /// and the old receipt no longer matches anything renderable.
    /// </summary>
    public DraftArtifact Edit(ArtifactDocument editedDocument)
        => new(Revision.Edited(editedDocument));

    /// <summary>
    /// The assembly-owned half of <see cref="ApprovalGate"/>. Keeping the only
    /// constructor call inside this type prevents friend assemblies from
    /// manufacturing approval while preserving one validated public gate.
    /// </summary>
    internal static ApprovedArtifact ApproveThroughGate(
        DraftArtifact draft,
        string approvedBy,
        IReadOnlyList<ValidationIssue> outstandingIssues,
        DateTimeOffset approvedAtUtc,
        IReadOnlyList<ApprovedAssetBinding> reviewedAssetBindings)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(outstandingIssues);
        ArgumentNullException.ThrowIfNull(reviewedAssetBindings);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvedBy);

        if (draft.Revision.Lane == DataLane.Restricted)
        {
            throw new InvalidOperationException(
                "Approval is blocked: Restricted-lane artifacts cannot be approved in an early release.");
        }

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
        var acceptedAssetBindings = FreezeAndValidateAssetBindings(
            draft.Revision.Document,
            reviewedAssetBindings);
        var receipt = new ApprovalReceipt(draft.Revision.Id, draft.Revision.Number, approvedBy, approvedAtUtc);
        return new ApprovedArtifact(draft.Revision, receipt, acceptedIssues, acceptedAssetBindings);
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

    private static System.Collections.ObjectModel.ReadOnlyCollection<ApprovedAssetBinding> FreezeAndValidateAssetBindings(
        ArtifactDocument document,
        IReadOnlyList<ApprovedAssetBinding> reviewedAssetBindings)
    {
        var expectedIds = document.Nodes
            .SelectMany(node => node switch
            {
                ImageReference image => new[] { image.Asset },
                StepRow { Symbol: { } symbol } => [symbol.Asset],
                _ => [],
            })
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
        var snapshot = reviewedAssetBindings.ToArray();
        if (snapshot.Any(binding => binding is null))
        {
            throw new InvalidOperationException(
                "Approval is blocked: reviewed asset evidence contains a null binding.");
        }

        var ordered = snapshot
            .OrderBy(binding => binding.AssetId.Value, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Select(binding => binding.AssetId).Distinct().Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Approval is blocked: reviewed asset evidence contains a duplicate asset identity.");
        }

        if (!expectedIds.SequenceEqual(ordered.Select(binding => binding.AssetId)))
        {
            throw new InvalidOperationException(
                "Approval is blocked: every referenced image requires exact Gate B asset evidence, with no missing or unrelated binding.");
        }

        return Array.AsReadOnly(ordered);
    }
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
        => Approve(draft, approvedBy, outstandingIssues, approvedAtUtc, []);

    public static ApprovedArtifact Approve(
        DraftArtifact draft,
        string approvedBy,
        IReadOnlyList<ValidationIssue> outstandingIssues,
        DateTimeOffset approvedAtUtc,
        IReadOnlyList<ApprovedAssetBinding> reviewedAssetBindings)
        => ApprovedArtifact.ApproveThroughGate(
            draft,
            approvedBy,
            outstandingIssues,
            approvedAtUtc,
            reviewedAssetBindings);
}
