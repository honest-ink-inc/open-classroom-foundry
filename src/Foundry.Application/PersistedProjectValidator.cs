// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Application;

/// <summary>
/// Rehydrates the first admitted saved-validation envelope. Package-provided
/// messages and notice codes are compatibility metadata only and never become
/// engine findings. Callers must supply the complete engine-owned notice set
/// independently of every mutable package selector and inventory.
/// </summary>
public sealed class PersistedProjectValidator : IArtifactValidator
{
    private readonly ProjectValidationEnvelope _envelope;
    private readonly IReadOnlyList<ValidationIssue> _trustedNotices;

    private PersistedProjectValidator(
        ProjectValidationEnvelope envelope,
        IReadOnlyList<ValidationIssue> trustedNotices)
    {
        _envelope = envelope;
        _trustedNotices = trustedNotices;
    }

    public static PersistedProjectValidator Create(
        ProjectValidationEnvelope envelope,
        IReadOnlyList<ValidationIssue> trustedNotices)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.SchemaVersion != ProjectValidationEnvelope.CurrentSchemaVersion
            || !string.Equals(
                envelope.Kind,
                ProjectValidationEnvelope.ExactApprovedDocumentKind,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The saved project declares an unknown validation context and cannot be reviewed by this build.");
        }

        if (envelope.UntrustedNoticeCodes is null
            || envelope.UntrustedNoticeCodes.Any(code => !ProjectValidationEnvelope.IsStableNoticeCode(code))
            || envelope.UntrustedNoticeCodes.Distinct(StringComparer.Ordinal).Count()
                != envelope.UntrustedNoticeCodes.Count)
        {
            throw new InvalidOperationException(
                "The saved project has an invalid notice inventory and cannot be reviewed by this build.");
        }

        ArgumentNullException.ThrowIfNull(trustedNotices);
        var notices = trustedNotices.Distinct().ToArray();
        if (notices.Any(issue => issue.Severity == ValidationSeverity.Blocking
                || !Enum.IsDefined(issue.Severity)
                || !ProjectValidationEnvelope.IsStableNoticeCode(issue.Code))
            || notices.Any(issue => issue.RequiresAcknowledgement && issue.Severity != ValidationSeverity.Warning))
        {
            throw new InvalidOperationException(
                "The saved project notice inventory is unknown to this exact engine recipe and cannot be reviewed.");
        }

        return new PersistedProjectValidator(envelope, notices);
    }

    public IReadOnlyList<ValidationIssue> Validate(ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var issues = new List<ValidationIssue>();
        issues.AddRange(DocumentValidator.Validate(document));
        issues.AddRange(_trustedNotices);

        if (!string.Equals(
            ArtifactDocumentFingerprint.Compute(document),
            _envelope.ArtifactSha256,
            StringComparison.Ordinal))
        {
            // artifact.json is the portable semantic source of truth (ADR-003),
            // so an offline edit must remain possible even when typed source
            // parameters from the originating studio are unavailable. The edit
            // loses that unauthenticated recipe claim and must be acknowledged;
            // the host saves it under the engine-owned portable-editor identity.
            issues.Add(ValidationIssue.Warning(
                "project.saved-revision-changed",
                "This semantic document changed after reopen. Review the full portable edit; it no longer claims the package's original recipe, purpose, or protected-seat review.",
                requiresAcknowledgement: true));
        }

        return [.. issues.Distinct()];
    }
}
