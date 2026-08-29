using Foundry.Domain;

namespace Foundry.Tests.Unit;

public class ApprovalGateTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static DraftArtifact SomeDraft()
        => DraftArtifact.New(new ArtifactDocument([new Paragraph("First, gather the materials.")]), DataLane.Green);

    [Fact]
    public void Approval_binds_to_the_exact_revision()
    {
        var draft = SomeDraft();

        var approved = ApprovalGate.Approve(draft, "teacher@example.org", [], SomeInstant);

        Assert.Equal(draft.Revision.Id, approved.Receipt.ArtifactId);
        Assert.Equal(draft.Revision.Number, approved.Receipt.RevisionNumber);
        Assert.Equal("teacher@example.org", approved.Receipt.ApprovedBy);
        Assert.Equal(SomeInstant, approved.Receipt.ApprovedAtUtc);
    }

    [Fact]
    public void Blocking_issues_fail_closed()
    {
        var issues = new[] { ValidationIssue.Blocking("doc.image.alt-text", "An image has no alternative text.") };

        Assert.Throws<InvalidOperationException>(
            () => ApprovalGate.Approve(SomeDraft(), "teacher@example.org", issues, SomeInstant));
    }

    [Fact]
    public void Warnings_do_not_block_approval()
    {
        var issues = new[] { ValidationIssue.Warning("doc.long", "The strip is long.") };

        var approved = ApprovalGate.Approve(SomeDraft(), "teacher@example.org", issues, SomeInstant);

        Assert.NotNull(approved);
    }

    [Fact]
    public void An_anonymous_approver_is_refused()
    {
        Assert.Throws<ArgumentException>(
            () => ApprovalGate.Approve(SomeDraft(), "   ", [], SomeInstant));
    }

    [Fact]
    public void Editing_an_approved_artifact_yields_a_new_draft_revision_and_a_stale_receipt()
    {
        var approved = ApprovalGate.Approve(SomeDraft(), "teacher@example.org", [], SomeInstant);

        var edited = approved.Edit(new ArtifactDocument([new Paragraph("Then, check your work.")]));

        Assert.Equal(approved.Revision.Number + 1, edited.Revision.Number);
        Assert.Equal(approved.Revision.Id, edited.Revision.Id);
        Assert.NotEqual(approved.Receipt.RevisionNumber, edited.Revision.Number);
    }

    [Fact]
    public void Every_draft_edit_increments_the_revision()
    {
        var draft = SomeDraft();

        var edited = draft.WithEditedDocument(ArtifactDocument.Empty);

        Assert.Equal(draft.Revision.Number + 1, edited.Revision.Number);
        Assert.Equal(draft.Revision.Id, edited.Revision.Id);
    }
}
