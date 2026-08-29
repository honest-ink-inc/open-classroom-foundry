using Foundry.Application;
using Foundry.Domain;

namespace Foundry.Tests.Unit;

public class ReviewSessionTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static JobStateMachine MachineAtReview()
    {
        var machine = new JobStateMachine();
        foreach (var state in new[]
        {
            JobState.Imported, JobState.Normalized, JobState.DataLaneConfirmed,
            JobState.DraftGenerated, JobState.SchemaValidated, JobState.InvariantsValidated,
            JobState.AwaitingTeacherReview,
        })
        {
            machine.Transition(state);
        }

        return machine;
    }

    private static ReviewSession SessionOver(params DocumentNode[] nodes)
        => new(
            DraftArtifact.New(new ArtifactDocument(nodes), DataLane.Green),
            MachineAtReview(),
            new DefaultArtifactValidator(),
            new DomainApprovalGate());

    [Fact]
    public void A_session_requires_a_machine_awaiting_review()
    {
        Assert.Throws<ArgumentException>(() => new ReviewSession(
            DraftArtifact.New(ArtifactDocument.Empty, DataLane.Green),
            new JobStateMachine(),
            new DefaultArtifactValidator(),
            new DomainApprovalGate()));
    }

    [Fact]
    public void Every_edit_is_a_new_revision_through_the_edit_loop()
    {
        var session = SessionOver(new Paragraph("First, gather the materials."));
        var initialRevision = session.Draft.Revision.Number;

        session.ReplaceNode(0, new Paragraph("First, gather your materials."));

        Assert.Equal(initialRevision + 1, session.Draft.Revision.Number);
        Assert.Equal(JobState.AwaitingTeacherReview, session.Machine.State);
    }

    [Fact]
    public void Approval_is_blocked_until_the_teacher_fixes_or_removes_the_offending_element()
    {
        var session = SessionOver(
            new Paragraph("Water each plant once."),
            new ImageReference(new AssetId("symbols.watering-can.v1"), "  "));

        Assert.False(session.CanApprove);
        Assert.Throws<InvalidOperationException>(() => session.Approve("teacher@example.org", SomeInstant));

        session.ReplaceNode(1, new ImageReference(new AssetId("symbols.watering-can.v1"), "A green watering can"));

        Assert.True(session.CanApprove);
        var approved = session.Approve("teacher@example.org", SomeInstant);

        Assert.Equal(session.Draft.Revision.Number, approved.Receipt.RevisionNumber);
        Assert.Equal(JobState.Approved, session.Machine.State);
    }

    [Fact]
    public void Removing_a_blocking_element_also_clears_the_block()
    {
        var session = SessionOver(
            new Paragraph("Water each plant once."),
            new ChoiceSet(["Comply"]));

        Assert.False(session.CanApprove);

        session.RemoveNode(1);

        Assert.True(session.CanApprove);
        Assert.Single(session.Draft.Revision.Document.Nodes);
    }

    [Fact]
    public void Reordering_preserves_content_and_passes_through_the_edit_loop()
    {
        var session = SessionOver(new Paragraph("Second"), new Paragraph("First"));

        session.MoveNode(1, 0);

        Assert.Equal("First", Assert.IsType<Paragraph>(session.Draft.Revision.Document.Nodes[0]).Text);
        Assert.Equal("Second", Assert.IsType<Paragraph>(session.Draft.Revision.Document.Nodes[1]).Text);
    }

    [Fact]
    public void Rejection_declines_the_job()
    {
        var session = SessionOver(new Paragraph("Water each plant once."));

        session.Reject();

        Assert.Equal(JobState.Declined, session.Machine.State);
        Assert.False(session.CanApprove);
    }

    [Fact]
    public void The_document_language_survives_edits()
    {
        var draft = DraftArtifact.New(new ArtifactDocument([new Paragraph("Hola")], "es"), DataLane.Green);
        var session = new ReviewSession(draft, MachineAtReview(), new DefaultArtifactValidator(), new DomainApprovalGate());

        session.ReplaceNode(0, new Paragraph("Hola a todos"));

        Assert.Equal("es", session.Draft.Revision.Document.Language);
    }
}
