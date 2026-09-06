// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;

namespace Foundry.Tests.InstructionalEvals;

/// <summary>
/// Wholly synthetic, version-0.2 review-route controls. They measure supplied
/// structure, arithmetic, audience and exact revision approval, never source
/// authenticity, curriculum quality, rights or actual teacher/seat acceptance.
/// No replacement case sends an artifact to a sink.
/// </summary>
internal static class ReplacementModuleReviewCorpus
{
    private const string SyntheticDate = "DATE-10";
    private const string SyntheticNote = "Independent synthetic preparation note.";
    private const string SyntheticContingency = "Synthetic contingency remains separate.";
    private static readonly DateTimeOffset Instant = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);

    internal static RecipeEvaluationCorpus LessonLoom() => Corpus("lesson-loom",
    [
        Case("review-unchanged", () => ReviewUnchanged("lesson-loom")),
        Case("review-edit-after-approval-needs-fresh-acknowledgement", () => ReviewPermittedNoteEdit("lesson-loom")),
        Case("review-independent-notes-outside-protected-structure", () => ReviewIndependentNotes("lesson-loom")),
        Case("refusal-blank-evidence", () => ReviewBuildRefusal("lesson-loom",
            values => values["evidence"] = " ", "loom.evidence")),
        Case("refusal-wide-builder-minute-total", () => ReviewBuildRefusal("lesson-loom",
            values => values["phases"] =
                "Launch|2147483647|Synthetic launch.|Synthetic check A|Synthetic response A\n" +
                "Work|2147483647|Synthetic work.||\n" +
                "Closure|47|Synthetic closure.|Synthetic check B|Synthetic response B",
            "loom.timing", "4294967341")),
        Case("refusal-wrapped-reviewed-minute-total", () => ReviewLessonMinutes(
            ["2147483647", "2147483647", "47"])),
        Case("refusal-ordinary-reviewed-minute-total", () => ReviewLessonMinutes(["10", "30", "6"])),
    ]);

    internal static RecipeEvaluationCorpus BoardToBrief() => Corpus("board-to-brief",
    [
        Case("review-unchanged", () => ReviewUnchanged("board-to-brief")),
        Case("review-edit-after-approval-needs-fresh-acknowledgement", () => ReviewPermittedNoteEdit("board-to-brief")),
        Case("review-independent-notes-outside-protected-structure", () => ReviewIndependentNotes("board-to-brief")),
        Case("refusal-original-visible-lock-hidden-in-teacher-note", () => ReviewBoardLock(hidden: true)),
        Case("refusal-original-lock-removed", () => ReviewBoardLock(hidden: false)),
        Case("review-original-teacher-only-lock-stays-permitted", ReviewOriginalTeacherOnlyLock),
        Case("review-unlocked-prose-and-visible-lock-reordering", ReviewBoardReordering),
    ]);

    internal static RecipeEvaluationCorpus SourceLens() => Corpus("source-lens",
    [
        Case("review-unchanged", () => ReviewUnchanged("source-lens")),
        Case("review-edit-after-approval-needs-fresh-acknowledgement", () => ReviewPermittedNoteEdit("source-lens")),
        Case("review-independent-notes-outside-protected-structure", () => ReviewIndependentNotes("source-lens")),
        Case("refusal-excerpt-25-replaced-with-125", () => ReviewSourceExcerpt(hidden: false)),
        Case("refusal-excerpt-hidden-in-teacher-note", () => ReviewSourceExcerpt(hidden: true)),
        Case("refusal-date-place-associations-swapped", () => ReviewSourceMetadata(reorderOnly: false)),
        Case("review-metadata-row-reordering", () => ReviewSourceMetadata(reorderOnly: true)),
        Case("review-optional-metadata-not-recorded", () => ReviewUnchanged("source-lens", values =>
        {
            values["place"] = "";
            values["audience"] = " ";
            values["provenance"] = "";
        })),
    ]);

    private static RecipeEvaluationCorpus Corpus(string recipeId, RecipeEvaluationCase[] cases)
        => new(new(recipeId, "0.2.0", "0.2"), $"{nameof(ReplacementModuleReviewCorpus)}.{recipeId}", cases);

    private static RecipeEvaluationCase Case(string id, Action evaluate)
        => new(id, () =>
        {
            evaluate();
            return Task.CompletedTask;
        });

    private static ModuleBuildOutcome Build(string recipeId, Action<Dictionary<string, object?>>? change = null)
    {
        var key = new RecipeEvaluationKey(recipeId, "0.2.0", "0.2");
        var mode = ModuleStudioCatalog.ByModeKey(key.RecipeId, key.RecipeVersion);
        RecipeEvaluationDispatcher.RequireManifest(key, mode.Recipe);
        var values = ModuleStudioCatalog.Defaults(mode);
        values["language"] = "en";
        switch (recipeId)
        {
            case "lesson-loom":
                values["title"] = "Synthetic replacement lesson";
                values["target"] = "Synthetic target.";
                values["evidence"] = "Synthetic evidence.";
                values["total-minutes"] = "45";
                values["phases"] =
                    "Launch|10|Synthetic launch.|Synthetic check A|Synthetic response A\n" +
                    "Work|30|Synthetic work.||\n" +
                    "Closure|5|Synthetic closure.|Synthetic check B|Synthetic response B";
                values["contingencies"] = SyntheticContingency;
                break;
            case "board-to-brief":
                values["lines"] = "Synthetic replacement brief|title\n" + SyntheticDate + "|date\n" +
                    "Read the synthetic card.|step\n" + SyntheticNote + "|note";
                values["locked-fields"] = "date|" + SyntheticDate;
                break;
            case "source-lens":
                values["creator"] = "Synthetic creator";
                values["title"] = "Synthetic replacement source";
                values["date"] = "Synthetic date";
                values["type"] = "Synthetic record";
                values["rights"] = "Wholly synthetic repository fixture; GPL-3.0-or-later";
                values["place"] = "Synthetic place";
                values["audience"] = "Synthetic audience";
                values["provenance"] = "Generated wholly for this synthetic repository control";
                values["excerpt"] = "25";
                values["transcript-verified"] = "true";
                break;
            default:
                throw new ArgumentException("No replacement review input is registered for this exact recipe.", nameof(recipeId));
        }

        change?.Invoke(values);
        var outcome = Assert.IsType<Func<ModuleInputValues, ModuleBuildOutcome>>(mode.Build)(new ModuleInputValues(values));
        Assert.Same(mode.Recipe, outcome.Recipe);
        RecipeEvaluationDispatcher.RequireManifest(key, outcome.Recipe);
        return outcome;
    }

    private static void ReviewUnchanged(string recipeId, Action<Dictionary<string, object?>>? change = null)
    {
        var outcome = Build(recipeId, change);
        InReview(outcome, session =>
        {
            Assert.Empty(Blockers(session));
            AssertFreshAcknowledgementRequired(session);
            AssertTypedApproval(session);
        });
        // A different review of the same immutable outcome cannot inherit the
        // first session's acknowledgement or approval.
        InReview(outcome, session =>
        {
            AssertFreshAcknowledgementRequired(session);
            AssertTypedApproval(session);
        });
    }

    private static void ReviewPermittedNoteEdit(string recipeId)
    {
        var outcome = Build(recipeId);
        InReview(outcome, session =>
        {
            AssertFreshAcknowledgementRequired(session);
            var first = AssertTypedApproval(session);
            var firstRevision = first.Revision;
            var originalHash = ArtifactDocumentFingerprint.Compute(firstRevision.Document);
            var noteIndex = NodeIndex(session, node => node is TeacherOnlyNotice notice
                && (recipeId != "lesson-loom" || notice.Text == "Contingency: " + SyntheticContingency));
            session.ReplaceNode(noteIndex, new TeacherOnlyNotice("Revised independent synthetic preparation note."));
            Assert.Equal(firstRevision.Number + 1, session.Draft.Revision.Number);
            Assert.Equal(firstRevision.Id, session.Draft.Revision.Id);
            Assert.NotSame(firstRevision, session.Draft.Revision);
            Assert.Empty(Blockers(session));
            AssertFreshAcknowledgementRequired(session);
            var second = AssertTypedApproval(session);
            Assert.NotSame(first, second);
            Assert.Same(firstRevision, first.Revision);
            Assert.Equal(originalHash, ArtifactDocumentFingerprint.Compute(first.Revision.Document));
        });
    }

    private static void ReviewIndependentNotes(string recipeId)
    {
        var outcome = Build(recipeId);
        var draft = outcome.CreateDraft().WithEditedDocument(new ArtifactDocument(
            [
                new Paragraph("Independent synthetic context before the protected section."),
                .. outcome.Document.Nodes,
                new TeacherOnlyNotice(SyntheticNote),
            ], outcome.Document.Language));
        InReview(outcome, session =>
        {
            Assert.Empty(Blockers(session));
            AssertFreshAcknowledgementRequired(session);
            var approved = AssertTypedApproval(session);
            Assert.Equal(draft.Revision.Number, approved.Revision.Number);
            Assert.Contains(approved.Revision.Document.Nodes, node => node is Paragraph
            {
                Text: "Independent synthetic context before the protected section.",
            });
        }, draft);
    }

    private static void ReviewBuildRefusal(
        string recipeId,
        Action<Dictionary<string, object?>> change,
        string expectedCode,
        string? expectedMessagePart = null)
    {
        var outcome = Build(recipeId, change);
        Assert.Contains(outcome.Issues, issue => issue.Code == expectedCode
            && issue.Severity == ValidationSeverity.Blocking
            && (expectedMessagePart is null || issue.Message.Contains(expectedMessagePart, StringComparison.Ordinal)));
        InReview(outcome, session =>
        {
            AssertFreshAcknowledgementRequired(session);
            AssertTypedRefusal(session, expectedCode, requireSingleIssue: false);
        });
    }

    private static void ReviewLessonMinutes(string[] minutes)
    {
        var outcome = Build("lesson-loom");
        InReview(outcome, session =>
        {
            session.SetRequiredIssuesAcknowledged(true);
            Assert.True(session.CanApprove);
            var index = NodeIndex(session, node => node is TableNode table
                && table.HeaderRow is not null
                && table.HeaderRow.SequenceEqual(["Phase", "Minutes", "Learners are doing"], StringComparer.Ordinal));
            var table = Assert.IsType<TableNode>(session.Draft.Revision.Document.Nodes[index]);
            Assert.Equal(minutes.Length, table.Rows.Count);
            session.ReplaceNode(index, table with
            {
                Rows = [.. table.Rows.Select((row, rowIndex) =>
                    (IReadOnlyList<string>)[row[0], minutes[rowIndex], row[2]])],
            });
            AssertFreshAcknowledgementRequired(session);
            AssertTypedRefusal(session, "loom.timing");
        });
    }

    private static void ReviewBoardLock(bool hidden)
    {
        var outcome = Build("board-to-brief");
        InReview(outcome, session =>
        {
            session.SetRequiredIssuesAcknowledged(true);
            Assert.True(session.CanApprove);
            var index = NodeIndex(session, node => node is Paragraph { Text: SyntheticDate });
            if (hidden)
            {
                session.ReplaceNode(index, new TeacherOnlyNotice(SyntheticDate));
            }
            else
            {
                session.RemoveNode(index);
            }

            AssertFreshAcknowledgementRequired(session);
            var issue = AssertTypedRefusal(session, "locked.missing");
            if (hidden)
            {
                Assert.StartsWith("Outside teacher-only notes: ", issue.Message, StringComparison.Ordinal);
                Assert.Contains(session.Draft.Revision.Document.Nodes,
                    node => node is TeacherOnlyNotice { Text: SyntheticDate });
            }
            else
            {
                Assert.DoesNotContain("Outside teacher-only notes: ", issue.Message, StringComparison.Ordinal);
            }
        });
    }

    private static void ReviewOriginalTeacherOnlyLock()
    {
        var outcome = Build("board-to-brief", values => values["lines"] =
            "Synthetic replacement brief|title\nRead the synthetic card.|step\n" + SyntheticDate + "|note");
        InReview(outcome, session =>
        {
            Assert.Empty(session.Draft.Revision.Document.Nodes.OfType<Paragraph>());
            Assert.Empty(Blockers(session));
            AssertFreshAcknowledgementRequired(session);
            AssertTypedApproval(session);
        });
    }

    private static void ReviewBoardReordering()
    {
        var outcome = Build("board-to-brief");
        InReview(outcome, session =>
        {
            session.SetRequiredIssuesAcknowledged(true);
            session.MoveNode(NodeIndex(session, node => node is Paragraph { Text: SyntheticDate }),
                session.Draft.Revision.Document.Nodes.Count - 1);
            session.ReplaceNode(NodeIndex(session, node => node is OrderedSteps),
                new OrderedSteps(["Read the revised synthetic card."]));
            Assert.Empty(Blockers(session));
            AssertFreshAcknowledgementRequired(session);
            var approved = AssertTypedApproval(session);
            Assert.IsType<Paragraph>(approved.Revision.Document.Nodes[^1]);
        });
    }

    private static void ReviewSourceExcerpt(bool hidden)
    {
        var outcome = Build("source-lens");
        InReview(outcome, session =>
        {
            session.SetRequiredIssuesAcknowledged(true);
            var index = NodeIndex(session, node => node is Paragraph { Text: "25" });
            session.ReplaceNode(index, hidden ? new TeacherOnlyNotice("25") : new Paragraph("125"));
            AssertFreshAcknowledgementRequired(session);
            AssertTypedRefusal(session, "lens.structure");
        });
    }

    private static void ReviewSourceMetadata(bool reorderOnly)
    {
        var outcome = Build("source-lens");
        InReview(outcome, session =>
        {
            session.SetRequiredIssuesAcknowledged(true);
            var index = NodeIndex(session, node => node is TableNode table
                && table.HeaderRow is not null
                && table.HeaderRow.SequenceEqual(["Field", "Record"], StringComparer.Ordinal));
            var table = Assert.IsType<TableNode>(session.Draft.Revision.Document.Nodes[index]);
            var rows = table.Rows.Select(row => row.ToArray()).ToArray();
            if (reorderOnly)
            {
                Array.Reverse(rows);
            }
            else
            {
                var date = Assert.Single(rows, row => row[0] == "Date");
                var place = Assert.Single(rows, row => row[0] == "Place");
                (date[1], place[1]) = (place[1], date[1]);
            }

            session.ReplaceNode(index, table with { Rows = rows });
            AssertFreshAcknowledgementRequired(session);
            if (reorderOnly)
            {
                Assert.Empty(Blockers(session));
                AssertTypedApproval(session);
            }
            else
            {
                AssertTypedRefusal(session, "lens.structure");
            }
        });
    }

    private static void AssertFreshAcknowledgementRequired(ReviewSession session)
    {
        Assert.Equal(JobState.AwaitingTeacherReview, session.Machine.State);
        Assert.NotEmpty(session.RequiredAcknowledgements);
        Assert.All(session.RequiredAcknowledgements, issue =>
        {
            Assert.Equal(ValidationSeverity.Warning, issue.Severity);
            Assert.True(issue.RequiresAcknowledgement);
        });
        Assert.False(session.CanApprove);
        Assert.Throws<InvalidOperationException>(() => session.Approve("synthetic-eval@example.invalid", Instant));
        session.SetRequiredIssuesAcknowledged(true);
    }

    private static ApprovedArtifact AssertTypedApproval(ReviewSession session)
    {
        Assert.True(session.CanApprove);
        var revision = session.Draft.Revision;
        var approved = session.Approve("synthetic-eval@example.invalid", Instant);
        Assert.Same(revision, approved.Revision);
        Assert.Same(approved, session.ApprovedResult);
        Assert.Equal(revision.Id, approved.Receipt.ArtifactId);
        Assert.Equal(revision.Number, approved.Receipt.RevisionNumber);
        Assert.Equal(JobState.Approved, session.Machine.State);
        return approved;
    }

    private static ValidationIssue AssertTypedRefusal(ReviewSession session, string code, bool requireSingleIssue = true)
    {
        Assert.False(session.CanApprove);
        var blockers = Blockers(session).ToArray();
        Assert.NotEmpty(blockers);
        Assert.All(blockers, issue => Assert.Equal(code, issue.Code));
        if (requireSingleIssue)
        {
            Assert.Single(blockers);
        }
        Assert.Throws<InvalidOperationException>(() => session.Approve("synthetic-eval@example.invalid", Instant));
        Assert.Null(session.ApprovedResult);
        Assert.Equal(JobState.AwaitingTeacherReview, session.Machine.State);
        return blockers[0];
    }

    private static IEnumerable<ValidationIssue> Blockers(ReviewSession session)
        => session.Issues.Where(issue => issue.Severity == ValidationSeverity.Blocking);

    private static int NodeIndex(ReviewSession session, Func<DocumentNode, bool> predicate)
        => Assert.Single(session.Draft.Revision.Document.Nodes.Select((node, index) => (node, index)),
            item => predicate(item.node)).index;

    private static void InReview(
        ModuleBuildOutcome outcome,
        Action<ReviewSession> evaluate,
        DraftArtifact? draft = null)
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

        var session = new ReviewSession(draft ?? outcome.CreateDraft(), machine, outcome.Validator);
        try
        {
            evaluate(session);
        }
        finally
        {
            if (session.Machine.State == JobState.AwaitingTeacherReview)
            {
                session.Cancel();
            }
        }
    }
}
