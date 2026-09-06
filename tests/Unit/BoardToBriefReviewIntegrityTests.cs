// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Xunit.Abstractions;

namespace Foundry.Tests.Unit;

/// <summary>
/// Wholly synthetic catalog-to-review measurements for constitution #10 and
/// plan section 10.2. These controls do not authenticate source material,
/// confer human review or rights authority, or admit a recipe change.
/// No fixture is passed to a render, save, print, or export sink.
/// </summary>
public sealed class BoardToBriefReviewIntegrityTests(ITestOutputHelper output)
{
    public enum SyntheticEdit
    {
        Unchanged,
        OrdinaryTeacherNoteEdited,
        LockedDateHiddenInTeacherOnlyNotice,
        LockedDateRemoved,
        TeacherOnlyLockUnchanged,
        TeacherOnlyLockRemoved,
        DuplicateLockVisibleCopyRemoved,
        VisibleLockExtendedWithTeacherCopy,
        DistinctVisibleTokenAndTeacherOnlyLock,
        VisibleLockMovedWithUnlockedProseEdit,
    }

    private const string SyntheticDate = "DATE-10";
    private const string DistinctSyntheticDate = "DATE-100";
    private const string SyntheticNote = "Wholly synthetic preparation note.";
    private static readonly LockedField SyntheticLock = new(LockedFieldKind.Date, SyntheticDate);
    private static readonly DateTimeOffset SyntheticApprovalInstant = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions EvidenceJson = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Theory]
    [InlineData(SyntheticEdit.Unchanged, false)]
    [InlineData(SyntheticEdit.OrdinaryTeacherNoteEdited, false)]
    [InlineData(SyntheticEdit.LockedDateHiddenInTeacherOnlyNotice, true)]
    [InlineData(SyntheticEdit.LockedDateRemoved, true)]
    [InlineData(SyntheticEdit.TeacherOnlyLockUnchanged, false)]
    [InlineData(SyntheticEdit.TeacherOnlyLockRemoved, true)]
    [InlineData(SyntheticEdit.DuplicateLockVisibleCopyRemoved, true)]
    [InlineData(SyntheticEdit.VisibleLockExtendedWithTeacherCopy, true)]
    [InlineData(SyntheticEdit.DistinctVisibleTokenAndTeacherOnlyLock, false)]
    [InlineData(SyntheticEdit.VisibleLockMovedWithUnlockedProseEdit, false)]
    public void Board_review_preserves_original_lock_visibility_and_allows_permitted_edits(
        SyntheticEdit edit,
        bool expectedBlocker)
    {
        var mode = ModuleStudioCatalog.ByModeKey("board-to-brief", "0.2.0");
        var values = ModuleStudioCatalog.Defaults(mode);
        var originallyTeacherOnly = edit is SyntheticEdit.TeacherOnlyLockUnchanged
            or SyntheticEdit.TeacherOnlyLockRemoved;
        var distinctVisibleToken = edit == SyntheticEdit.DistinctVisibleTokenAndTeacherOnlyLock;
        var originalTeacherCopy = originallyTeacherOnly || distinctVisibleToken
            || edit is SyntheticEdit.DuplicateLockVisibleCopyRemoved or SyntheticEdit.VisibleLockExtendedWithTeacherCopy;
        values["lines"] = "Synthetic board-role fixture|title\n" +
            (originallyTeacherOnly ? string.Empty : (distinctVisibleToken ? DistinctSyntheticDate : SyntheticDate) + "|date\n") +
            "Read the synthetic task card.|step\n" + SyntheticNote + "|note" +
            (originalTeacherCopy ? "\n" + SyntheticDate + "|note" : string.Empty);
        values["locked-fields"] = "date|" + SyntheticDate;
        values["language"] = "en";
        var outcome = Assert.IsType<Func<ModuleInputValues, ModuleBuildOutcome>>(mode.Build)(new ModuleInputValues(values));
        var session = new ReviewSession(outcome.CreateDraft(), MachineAtReview(), outcome.Validator);

        try
        {
            var baselineRevision = session.Draft.Revision;
            var baselineDocument = baselineRevision.Document;
            var baselineHash = ArtifactDocumentFingerprint.Compute(baselineDocument);
            session.SetRequiredIssuesAcknowledged(true);
            output.WriteLine("BOARD_ROLE_BASELINE " + JsonSerializer.Serialize(new
            {
                Scope = "Wholly synthetic supplied-role control; no source, rights or human authentication; no sink",
                Edit = edit,
                Inputs = values,
                Recipe = new { outcome.Recipe.Id, outcome.Recipe.Version, outcome.Recipe.OutputSchemaId, outcome.Recipe.EvaluationSuiteVersion },
                Document = baselineDocument,
                Roles = baselineDocument.Nodes.Select((node, index) => new { Index = index, Role = node.GetType().Name }),
                ExactLockOccurrencesByNode = LockOccurrences(baselineDocument),
                ExpectedOriginallyNonTeacherOccurrence = !originallyTeacherOnly && !distinctVisibleToken,
                DocumentHash = baselineHash,
                baselineRevision.Id,
                baselineRevision.Number,
                baselineRevision.Lane,
                session.Issues,
                session.RequiredAcknowledgements,
                session.CanApprove,
                session.Machine.State,
                SinkInvoked = false,
            }, EvidenceJson));

            Assert.False(DocumentValidator.HasBlockingIssues(session.Issues));
            Assert.NotEmpty(session.RequiredAcknowledgements);
            Assert.True(session.CanApprove);
            Assert.Equal(!originallyTeacherOnly && !distinctVisibleToken,
                LockOccurrences(baselineDocument).Any(item => !item.TeacherOnly && item.ExactOccurrence));
            Assert.Equal(originalTeacherCopy,
                LockOccurrences(baselineDocument).Any(item => item.TeacherOnly && item.ExactOccurrence));
            Assert.Contains(baselineDocument.Nodes, node => node is TeacherOnlyNotice { Text: SyntheticNote });
            if (originallyTeacherOnly)
            {
                Assert.Empty(baselineDocument.Nodes.OfType<Paragraph>());
            }
            else
            {
                Assert.Equal(distinctVisibleToken ? DistinctSyntheticDate : SyntheticDate,
                    Assert.Single(baselineDocument.Nodes.OfType<Paragraph>()).Text);
            }

            Assert.False(LockedFieldValidator.ContainsExactOccurrence(DistinctSyntheticDate, SyntheticLock));
            var expectedEditCount = 1;

            switch (edit)
            {
                case SyntheticEdit.Unchanged:
                case SyntheticEdit.TeacherOnlyLockUnchanged:
                case SyntheticEdit.DistinctVisibleTokenAndTeacherOnlyLock:
                    session.SetRequiredIssuesAcknowledged(false);
                    expectedEditCount = 0;
                    break;
                case SyntheticEdit.OrdinaryTeacherNoteEdited:
                    session.ReplaceNode(NodeIndex(baselineDocument, node => node is TeacherOnlyNotice { Text: SyntheticNote }),
                        new TeacherOnlyNotice("Wholly synthetic revised preparation note."));
                    break;
                case SyntheticEdit.LockedDateHiddenInTeacherOnlyNotice:
                    session.ReplaceNode(NodeIndex(baselineDocument, node => node is Paragraph { Text: SyntheticDate }),
                        new TeacherOnlyNotice(SyntheticDate));
                    break;
                case SyntheticEdit.LockedDateRemoved:
                case SyntheticEdit.DuplicateLockVisibleCopyRemoved:
                    session.RemoveNode(NodeIndex(baselineDocument, node => node is Paragraph { Text: SyntheticDate }));
                    break;
                case SyntheticEdit.TeacherOnlyLockRemoved:
                    session.RemoveNode(NodeIndex(baselineDocument, node => node is TeacherOnlyNotice { Text: SyntheticDate }));
                    break;
                case SyntheticEdit.VisibleLockExtendedWithTeacherCopy:
                    session.ReplaceNode(NodeIndex(baselineDocument, node => node is Paragraph { Text: SyntheticDate }),
                        new Paragraph(DistinctSyntheticDate));
                    break;
                case SyntheticEdit.VisibleLockMovedWithUnlockedProseEdit:
                    var originalDateIndex = NodeIndex(baselineDocument, node => node is Paragraph { Text: SyntheticDate });
                    var originalStepsIndex = NodeIndex(baselineDocument, node => node is OrderedSteps);
                    session.MoveNode(originalDateIndex, originalStepsIndex);
                    session.ReplaceNode(NodeIndex(session.Draft.Revision.Document, node => node is OrderedSteps),
                        new OrderedSteps(["Read the synthetic revised task card."]));
                    expectedEditCount = 2;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(edit), edit, "Unknown synthetic edit.");
            }

            var beforeAcknowledgement = session.CanApprove;
            var requiredAcknowledgements = session.RequiredAcknowledgements;
            session.SetRequiredIssuesAcknowledged(true);
            var canApproveAtAttempt = session.CanApprove;
            var attemptedRevision = session.Draft.Revision;
            var directIssues = outcome.Validator.Validate(attemptedRevision.Document);
            ApprovedArtifact? approved = null;
            InvalidOperationException? refusal = null;
            try
            {
                approved = session.Approve("synthetic-board-review@example.invalid", SyntheticApprovalInstant);
            }
            catch (InvalidOperationException failure)
            {
                refusal = failure;
            }

            output.WriteLine("BOARD_ROLE_OBSERVATION " + JsonSerializer.Serialize(new
            {
                Scope = "Wholly synthetic supplied-role control; no source, rights or human authentication; no sink",
                Edit = edit,
                ExpectedBlocker = expectedBlocker,
                EditRoute = "Existing ReviewSession replacement/removal/movement or unchanged revision",
                BaselineDocumentHash = baselineHash,
                attemptedRevision.Document,
                Roles = attemptedRevision.Document.Nodes.Select((node, index) => new { Index = index, Role = node.GetType().Name }),
                ExactLockOccurrencesByNode = LockOccurrences(attemptedRevision.Document),
                DocumentHash = ArtifactDocumentFingerprint.Compute(attemptedRevision.Document),
                attemptedRevision.Id,
                attemptedRevision.Number,
                attemptedRevision.Lane,
                session.Issues,
                DirectValidatorIssues = directIssues,
                RequiredAcknowledgements = requiredAcknowledgements,
                CanApproveBeforeFreshAcknowledgement = beforeAcknowledgement,
                CanApproveAfterFreshAcknowledgement = canApproveAtAttempt,
                TypedApprovalReturned = approved is not null,
                ExactAttemptedRevisionApproved = approved is null ? (bool?)null : ReferenceEquals(attemptedRevision, approved.Revision),
                ApprovalReceipt = approved?.Receipt,
                ApprovalIssues = approved?.ValidationIssues,
                RefusalType = refusal?.GetType().FullName,
                RefusalMessage = refusal?.Message,
                FinalState = session.Machine.State,
                SinkInvoked = false,
            }, EvidenceJson));

            Assert.False(beforeAcknowledgement);
            Assert.NotEmpty(requiredAcknowledgements);
            Assert.Equal(directIssues, session.Issues);
            Assert.Equal(baselineRevision.Id, attemptedRevision.Id);
            Assert.Equal(baselineRevision.Number + expectedEditCount, attemptedRevision.Number);
            var actualBlockingCodes = session.Issues.Where(issue => issue.Severity == ValidationSeverity.Blocking)
                .Select(issue => issue.Code).ToArray();
            var description = $"Synthetic edit {edit}: blocking codes=[{string.Join(", ", actualBlockingCodes)}]; " +
                $"CanApprove after fresh acknowledgement={canApproveAtAttempt}; typed approval returned={approved is not null}; " +
                $"refusal={refusal?.Message ?? "None"}.";
            if (expectedBlocker)
            {
                Assert.True(DocumentValidator.HasBlockingIssues(session.Issues),
                    "Expected a blocking issue when the exact lock is absent altogether or its original non-teacher occurrence is lost. " + description);
                Assert.False(canApproveAtAttempt, description);
                Assert.Null(approved);
                Assert.NotNull(refusal);
                Assert.Equal(JobState.AwaitingTeacherReview, session.Machine.State);
                var blockingIssue = Assert.Single(session.Issues,
                    issue => issue.Severity == ValidationSeverity.Blocking);
                Assert.Equal("locked.missing", blockingIssue.Code);
                if (edit is SyntheticEdit.LockedDateRemoved or SyntheticEdit.TeacherOnlyLockRemoved)
                {
                    Assert.DoesNotContain("Outside teacher-only notes:", blockingIssue.Message, StringComparison.Ordinal);
                }
                else
                {
                    Assert.StartsWith("Outside teacher-only notes: ", blockingIssue.Message, StringComparison.Ordinal);
                }
            }
            else
            {
                Assert.Empty(actualBlockingCodes);
                Assert.True(canApproveAtAttempt, description);
                Assert.Null(refusal);
                Assert.NotNull(approved);
                Assert.Same(attemptedRevision, approved.Revision);
                Assert.Equal(JobState.Approved, session.Machine.State);
                Assert.Equal(!originallyTeacherOnly && !distinctVisibleToken,
                    LockOccurrences(attemptedRevision.Document).Any(item => !item.TeacherOnly && item.ExactOccurrence));
            }

            if (expectedEditCount == 0)
            {
                Assert.Equal(baselineHash, ArtifactDocumentFingerprint.Compute(attemptedRevision.Document));
            }

            if (edit == SyntheticEdit.VisibleLockMovedWithUnlockedProseEdit)
            {
                Assert.Equal(SyntheticDate, Assert.Single(attemptedRevision.Document.Nodes.OfType<Paragraph>()).Text);
                Assert.True(NodeIndex(attemptedRevision.Document, node => node is Paragraph { Text: SyntheticDate })
                    > NodeIndex(attemptedRevision.Document, node => node is OrderedSteps));
                Assert.Equal("Read the synthetic revised task card.",
                    Assert.Single(Assert.Single(attemptedRevision.Document.Nodes.OfType<OrderedSteps>()).Steps));
            }
        }
        finally
        {
            // No asynchronous operation, native child or sink belongs to these
            // controls. Close a refused in-memory review without approving it.
            if (session.Machine.State == JobState.AwaitingTeacherReview)
            {
                session.Cancel();
            }
        }
    }

    private sealed record LockOccurrenceObservation(
        int Index,
        string Role,
        bool TeacherOnly,
        bool ExactOccurrence,
        IReadOnlyList<string> Texts);

    private static LockOccurrenceObservation[] LockOccurrences(ArtifactDocument document)
        => [.. document.Nodes.Select((node, index) =>
        {
            var texts = DocumentText.CollectStrings(new ArtifactDocument([node], document.Language));
            return new LockOccurrenceObservation(index, node.GetType().Name, node is TeacherOnlyNotice,
                texts.Any(text => LockedFieldValidator.ContainsExactOccurrence(text, SyntheticLock)), texts);
        })];

    private static int NodeIndex(ArtifactDocument document, Func<DocumentNode, bool> predicate)
        => Assert.Single(document.Nodes.Select((node, index) => (node, index)),
            item => predicate(item.node)).index;

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
}
