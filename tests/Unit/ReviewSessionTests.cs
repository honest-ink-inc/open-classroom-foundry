using System.Security.Cryptography;
using System.Text;
using Foundry.Application;
using Foundry.Contracts;
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
            new ReviewViewContext(
                ReviewViewContext.ManualDefault.PreviewRequest,
                assetCatalog: new FixtureAssetCatalog()));

    [Fact]
    public void A_session_requires_a_machine_awaiting_review()
    {
        Assert.Throws<ArgumentException>(() => new ReviewSession(
            DraftArtifact.New(ArtifactDocument.Empty, DataLane.Green),
            new JobStateMachine(),
            new DefaultArtifactValidator()));
    }

    [Fact]
    public void Restricted_session_never_reports_approval_ready_and_still_refuses_approval()
    {
        var session = new ReviewSession(
            DraftArtifact.New(
                new ArtifactDocument([new Paragraph("Synthetic restricted fixture.")]),
                DataLane.Restricted),
            MachineAtReview(),
            new DefaultArtifactValidator());

        Assert.False(session.CanApprove);
        var exception = Assert.Throws<InvalidOperationException>(
            () => session.Approve("teacher@example.org", SomeInstant));
        Assert.Contains("Approval is not available", exception.Message, StringComparison.Ordinal);
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
    public void Cancellation_ends_an_in_flight_review_without_approval()
    {
        var session = SessionOver(new Paragraph("Water each plant once."));

        session.Cancel();

        Assert.Equal(JobState.Cancelled, session.Machine.State);
        Assert.Null(session.ApprovedResult);
        Assert.False(session.CanApprove);
        Assert.Throws<InvalidOperationException>(session.Cancel);
    }

    [Fact]
    public void The_document_language_survives_edits()
    {
        var draft = DraftArtifact.New(new ArtifactDocument([new Paragraph("Hola")], "es"), DataLane.Green);
        var session = new ReviewSession(draft, MachineAtReview(), new DefaultArtifactValidator());

        session.ReplaceNode(0, new Paragraph("Hola a todos"));

        Assert.Equal("es", session.Draft.Revision.Document.Language);
    }

    [Fact]
    public void Typed_node_replacements_bind_to_the_exact_displayed_revision_and_preserve_every_field()
    {
        var cases = new (DocumentNode Original, DocumentNode Replacement)[]
        {
            (
                new BilingualPair("Open.", "Abre.", "en-US", "es-US"),
                new BilingualPair("Close.", "Cierra.", "en-GB", "es-MX")),
            (
                new TableNode(["Item", "Count"], [["Cup", "1"]]),
                new TableNode(["Material", "Quantity"], [["Spoon", "2"], ["Tray", "3"]])),
            (
                new StepRow("Lift.", null),
                new StepRow(
                    "Lift the blue card.",
                    new ImageReference(new AssetId("symbol.blue-card.v2"), "Blue card symbol"),
                    "Levanta la tarjeta azul.",
                    "en-US",
                    "es-US")),
            (
                new VectorGraphic(100, 80, [new LineSeg(1, 2, 3, 4)], "Original geometry"),
                new VectorGraphic(
                    210.5,
                    297.25,
                    [
                        new LineSeg(1.25, 2.5, 3.75, 4.125, 0.4, true),
                        new CircleShape(10, 11, 12, 0.5, true),
                        new RectShape(13, 14, 15, 16, 0.6, false),
                        new TextLabel(17, 18, "x + y", 4.75, TextAnchor.End),
                    ],
                    "Exact edited geometry")),
            (
                new EvidenceLink("Original claim", "authorized:page-1#line-2"),
                new EvidenceLink("Reviewed claim", "authorized:page-7#line-9")),
        };

        foreach (var (original, replacement) in cases)
        {
            var session = SessionOver(original);
            var displayedRevision = session.Draft.Revision;

            session.ReplaceNode(0, displayedRevision, replacement);

            Assert.NotSame(displayedRevision, session.Draft.Revision);
            Assert.Equal(displayedRevision.Number + 1, session.Draft.Revision.Number);
            Assert.Equal(
                ArtifactDocumentFingerprint.Compute(new ArtifactDocument([replacement])),
                ArtifactDocumentFingerprint.Compute(session.Draft.Revision.Document));
            Assert.Equal(JobState.AwaitingTeacherReview, session.Machine.State);
        }
    }

    [Fact]
    public void A_modal_editor_result_for_a_stale_revision_is_refused_without_an_edit()
    {
        var session = SessionOver(new Paragraph("First exact revision."));
        var stale = session.Draft.Revision;
        session.ReplaceNode(0, new Paragraph("Second exact revision."));
        var current = session.Draft.Revision;

        var error = Assert.Throws<InvalidOperationException>(() => session.ReplaceNode(
            0,
            stale,
            new Paragraph("Stale modal result.")));

        Assert.Contains("stale", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(current, session.Draft.Revision);
        Assert.Equal("Second exact revision.", Assert.IsType<Paragraph>(current.Document.Nodes[0]).Text);
    }

    [Fact]
    public void A_typed_replacement_clears_required_warning_acknowledgement()
    {
        var warning = ValidationIssue.Warning(
            "synthetic.required",
            "Synthetic required warning.",
            requiresAcknowledgement: true);
        var session = new ReviewSession(
            DraftArtifact.New(new ArtifactDocument([new Paragraph("Original")]), DataLane.Green),
            MachineAtReview(),
            new DelegatingValidator(_ => [warning]));
        session.SetRequiredIssuesAcknowledged(acknowledged: true);
        Assert.True(session.CanApprove);
        var displayedRevision = session.Draft.Revision;

        session.ReplaceNode(0, displayedRevision, new Paragraph("Edited"));

        Assert.False(session.CanApprove);
        Assert.Single(session.RequiredAcknowledgements);
    }

    [Fact]
    public void Validator_results_are_defensively_frozen_before_review()
    {
        var returnedIssues = new List<ValidationIssue>
        {
            ValidationIssue.Blocking("synthetic.block", "Synthetic blocking issue."),
        };
        var session = new ReviewSession(
            DraftArtifact.New(new ArtifactDocument([new Paragraph("Synthetic content")]), DataLane.Green),
            MachineAtReview(),
            new DelegatingValidator(_ => returnedIssues));

        returnedIssues.Clear();

        Assert.False(session.CanApprove);
        Assert.Single(session.Issues);
        Assert.Throws<NotSupportedException>(() => ((IList<ValidationIssue>)session.Issues).Clear());
        Assert.Throws<InvalidOperationException>(
            () => session.Approve("teacher@example.org", SomeInstant));
    }

    [Fact]
    public void A_changed_validator_result_for_an_unchanged_revision_fails_closed()
    {
        var returnedIssues = new List<ValidationIssue>();
        var session = new ReviewSession(
            DraftArtifact.New(new ArtifactDocument([new Paragraph("Synthetic content")]), DataLane.Green),
            MachineAtReview(),
            new DelegatingValidator(_ => returnedIssues));
        returnedIssues.Add(ValidationIssue.Warning("synthetic.changed", "Synthetic changed result."));

        var error = Assert.Throws<InvalidOperationException>(
            () => session.Approve("teacher@example.org", SomeInstant));

        Assert.Contains("validation changed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(JobState.AwaitingTeacherReview, session.Machine.State);
    }

    [Fact]
    public void Null_and_undefined_validator_findings_fail_closed()
    {
        Assert.Throws<InvalidOperationException>(() => new ReviewSession(
            DraftArtifact.New(new ArtifactDocument([new Paragraph("Synthetic content")]), DataLane.Green),
            MachineAtReview(),
            new DelegatingValidator(_ => [null!])));

        Assert.Throws<InvalidOperationException>(() => new ReviewSession(
            DraftArtifact.New(new ArtifactDocument([new Paragraph("Synthetic content")]), DataLane.Green),
            MachineAtReview(),
            new DelegatingValidator(_ =>
            [
                new ValidationIssue(
                    (ValidationSeverity)int.MaxValue,
                    "synthetic.invalid-severity",
                    "Synthetic invalid severity."),
            ])));
    }

    [Fact]
    public void Caller_collection_mutation_after_validation_cannot_change_the_approved_revision()
    {
        var options = new List<string> { "Synthetic first", "Synthetic second" };
        var session = new ReviewSession(
            DraftArtifact.New(new ArtifactDocument([new ChoiceSet(options)]), DataLane.Green),
            MachineAtReview(),
            new DefaultArtifactValidator());

        options.Clear();
        var approved = session.Approve("teacher@example.org", SomeInstant);

        Assert.Equal(2, Assert.IsType<ChoiceSet>(approved.Revision.Document.Nodes[0]).Options.Count);
        Assert.Same(session.Draft.Revision, approved.Revision);
    }

    [Fact]
    public void A_gate_cannot_substitute_a_different_valid_revision()
    {
        var session = new ReviewSession(
            DraftArtifact.New(new ArtifactDocument([new Paragraph("Reviewed content")]), DataLane.Green),
            MachineAtReview(),
            new DefaultArtifactValidator(),
            new SubstitutingApprovalGate());

        var error = Assert.Throws<InvalidOperationException>(
            () => session.Approve("teacher@example.org", SomeInstant));

        Assert.Contains("different revision", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(JobState.AwaitingTeacherReview, session.Machine.State);
    }

    [Fact]
    public void A_gate_cannot_append_unreviewed_validation_evidence()
    {
        var session = new ReviewSession(
            DraftArtifact.New(new ArtifactDocument([new Paragraph("Reviewed content")]), DataLane.Green),
            MachineAtReview(),
            new DefaultArtifactValidator(),
            new AppendingApprovalGate());

        var error = Assert.Throws<InvalidOperationException>(
            () => session.Approve("teacher@example.org", SomeInstant));

        Assert.Contains("validation result", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(session.ApprovedResult);
        Assert.Equal(JobState.AwaitingTeacherReview, session.Machine.State);
    }

    [Fact]
    public void A_gate_cannot_substitute_the_reviewer_identity_or_approval_instant()
    {
        var session = new ReviewSession(
            DraftArtifact.New(new ArtifactDocument([new Paragraph("Reviewed content")]), DataLane.Green),
            MachineAtReview(),
            new DefaultArtifactValidator(),
            new SubstitutingReceiptApprovalGate());

        var error = Assert.Throws<InvalidOperationException>(
            () => session.Approve("teacher@example.org", SomeInstant));

        Assert.Contains("approval identity/time", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(session.ApprovedResult);
        Assert.Equal(JobState.AwaitingTeacherReview, session.Machine.State);
    }

    private sealed class DelegatingValidator(
        Func<ArtifactDocument, IReadOnlyList<ValidationIssue>> validate) : IArtifactValidator
    {
        public IReadOnlyList<ValidationIssue> Validate(ArtifactDocument document) => validate(document);
    }

    private sealed class SubstitutingApprovalGate : IApprovalGate
    {
        public ApprovedArtifact Approve(
            DraftArtifact draft,
            string approvedBy,
            IReadOnlyList<ValidationIssue> outstandingIssues,
            DateTimeOffset approvedAtUtc,
            IReadOnlyList<ApprovedAssetBinding> reviewedAssetBindings)
            => ApprovalGate.Approve(
                DraftArtifact.New(new ArtifactDocument([new Paragraph("Substituted content")]), DataLane.Green),
                approvedBy,
                outstandingIssues,
                approvedAtUtc,
                reviewedAssetBindings);
    }

    private sealed class AppendingApprovalGate : IApprovalGate
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
                [.. outstandingIssues, ValidationIssue.Warning(
                    "forged.seat-approved",
                    "Synthetic forged approval claim.")],
                approvedAtUtc,
                reviewedAssetBindings);
    }

    private sealed class SubstitutingReceiptApprovalGate : IApprovalGate
    {
        public ApprovedArtifact Approve(
            DraftArtifact draft,
            string approvedBy,
            IReadOnlyList<ValidationIssue> outstandingIssues,
            DateTimeOffset approvedAtUtc,
            IReadOnlyList<ApprovedAssetBinding> reviewedAssetBindings)
            => ApprovalGate.Approve(
                draft,
                "different-reviewer@example.invalid",
                outstandingIssues,
                approvedAtUtc.AddMinutes(-1),
                reviewedAssetBindings);
    }

    private sealed class FixtureAssetCatalog : IAssetCatalog
    {
        private static readonly byte[] Content = Encoding.UTF8.GetBytes(
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"10\"/></svg>");
        private static readonly string ContentHash = Convert.ToHexString(SHA256.HashData(Content));

        public IReadOnlyList<AssetProvenance> All => [];

        public AssetProvenance? Find(AssetId id)
            => new(
                id,
                $"concept.{id.Value}",
                "1.0.0",
                "fixture.svg",
                "image/svg+xml",
                "synthetic test",
                "synthetic test",
                "CC0-1.0",
                ContentHash,
                "Synthetic fixture",
                "Synthetic fixture",
                Redistributable: true);

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            content = Content;
            mimeType = "image/svg+xml";
            return true;
        }
    }
}
