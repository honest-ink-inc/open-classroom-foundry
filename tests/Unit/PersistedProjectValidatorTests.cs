// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Tests.Unit;

public sealed class PersistedProjectValidatorTests
{
    private static ApprovedArtifact Approved(IReadOnlyList<ValidationIssue>? issues = null)
    {
        var document = new ArtifactDocument(
            [new Heading(1, "Synthetic routine"), new StepRow("Place the sample card in the tray.")],
            "en");
        return ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green, ArtifactPurpose.ClassroomSupport),
            "Synthetic teacher",
            issues ?? [],
            DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void The_exact_envelope_rehydrates_without_weakening_and_preserves_findings()
    {
        var warning = ValidationIssue.Warning("synthetic.warning", "A synthetic warning remains visible.");
        var approved = Approved([warning]);
        var envelope = ProjectValidationEnvelope.Exact(
            approved,
            "synthetic.recipe",
            "1.0.0");
        var validator = PersistedProjectValidator.Create(envelope, [warning]);

        Assert.Equal(DataLane.Green, envelope.Lane);
        Assert.Equal(ArtifactPurpose.ClassroomSupport, envelope.Purpose);
        Assert.Contains(warning, validator.Validate(approved.Revision.Document));

        var changed = new ArtifactDocument(
            [new Heading(1, "Synthetic routine")],
            approved.Revision.Document.Language);
        Assert.Contains(
            validator.Validate(changed),
            issue => issue.Code == "project.saved-revision-changed"
                && issue.Severity == ValidationSeverity.Warning
                && issue.RequiresAcknowledgement);
    }

    [Fact]
    public void Unknown_contexts_fail_closed_and_package_notice_codes_never_become_engine_findings()
    {
        var approved = Approved();
        var envelope = ProjectValidationEnvelope.Exact(approved, "synthetic.recipe", "1.0.0");

        Assert.Throws<InvalidOperationException>(() => PersistedProjectValidator.Create(
            envelope with { Kind = "future-validator" },
            []));
        Assert.Throws<ArgumentNullException>(() => PersistedProjectValidator.Create(
            envelope,
            null!));
        var packageWithForgedClaim = envelope with
        {
            UntrustedNoticeCodes = ["forged.seat-approved"],
        };
        var trustedWarning = ValidationIssue.Warning(
            "project.origin-unverified",
            "Synthetic engine-owned warning.",
            requiresAcknowledgement: true);
        var validator = PersistedProjectValidator.Create(packageWithForgedClaim, [trustedWarning]);
        var findings = validator.Validate(approved.Revision.Document);

        Assert.Contains(trustedWarning, findings);
        Assert.DoesNotContain(findings, issue => issue.Code == "forged.seat-approved");
    }

    [Fact]
    public void The_saved_render_profile_is_document_bound_and_range_checked()
    {
        var approved = Approved();
        var profile = ProjectRenderProfile.For(
            approved,
            RenderAudience.Teacher,
            175,
            targetLanguageFirst: true);

        Assert.Equal(ArtifactDocumentFingerprint.Compute(approved.Revision.Document), profile.ArtifactSha256);
        Assert.Equal(
            new RenderRequest(RenderTarget.PrintHtml, RenderAudience.Teacher, 175, true),
            profile.Request(RenderTarget.PrintHtml));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectRenderProfile.For(approved, textScalePercent: 99));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectRenderProfile.For(approved, textScalePercent: double.NaN));
    }
}
