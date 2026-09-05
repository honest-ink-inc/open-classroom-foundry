// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using System.Text.Json.Serialization;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.BuiltIn.SourceLens;
using Xunit.Abstractions;

namespace Foundry.Tests.Unit;

/// <summary>
/// Wholly synthetic catalog-to-review measurements for constitution #10 and
/// plan section 10.12. These controls establish neither authentic quotation,
/// rights clearance, durable teacher verification, nor recipe compatibility.
/// No approved fixture is passed to any render, save, print, or export sink.
/// </summary>
public sealed class SourceLensReviewIntegrityTests(ITestOutputHelper output)
{
    public enum SyntheticEdit
    {
        Unchanged,
        IdenticalExcerptReplacement,
        ExcerptExpandedTo125,
        ExcerptChangedTo26,
        ExcerptHiddenInTeacherOnlyNotice,
        ExcerptMovedUnderInterpretation,
        DateAndPlaceValuesSwapped,
        SeparateParagraphNoteAdded,
        SeparateTeacherOnlyNoteAdded,
        CitationAlteredWithOriginalInNote,
        ConflictingDuplicateDateLabel,
        DuplicateExcerptAnchor,
        DuplicateSourceMetadataCard,
        TitleAlteredWithOriginalInCitation,
        MetadataMovedUnderInterpretation,
        SeparateParagraphNoteBeforeExcerpt,
        SeparateTeacherOnlyNoteBeforeExcerpt,
        ExcerptSectionMovedIntact,
        MetadataRowsReordered,
        OptionalMetadataNotRecorded,
        OptionalMetadataInventedAfterBuild,
        CitationFormattingPreserved,
        DuplicateSourceTitleAnchor,
        AlteredMetadataWithOriginalUnderOtherTitle,
        IndependentTitledNotesBeforeSource,
        ExtraInventedMetadataRow,
    }

    private static readonly JsonSerializerOptions EvidenceJson = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly DateTimeOffset SyntheticApprovalInstant = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyList<string> ConflictingDateRow = Array.AsReadOnly(["Date", "Synthetic conflicting date"]);
    private static readonly IReadOnlyList<string> InventedMetadataRow = Array.AsReadOnly(["Date ", "Synthetic invented date"]);

    [Theory]
    [InlineData(SyntheticEdit.Unchanged, false)]
    [InlineData(SyntheticEdit.IdenticalExcerptReplacement, false)]
    [InlineData(SyntheticEdit.ExcerptExpandedTo125, true)]
    [InlineData(SyntheticEdit.ExcerptChangedTo26, true)]
    [InlineData(SyntheticEdit.ExcerptHiddenInTeacherOnlyNotice, true)]
    [InlineData(SyntheticEdit.ExcerptMovedUnderInterpretation, true)]
    [InlineData(SyntheticEdit.DateAndPlaceValuesSwapped, true)]
    [InlineData(SyntheticEdit.SeparateParagraphNoteAdded, false)]
    [InlineData(SyntheticEdit.SeparateTeacherOnlyNoteAdded, false)]
    [InlineData(SyntheticEdit.CitationAlteredWithOriginalInNote, true)]
    [InlineData(SyntheticEdit.ConflictingDuplicateDateLabel, true)]
    [InlineData(SyntheticEdit.DuplicateExcerptAnchor, true)]
    [InlineData(SyntheticEdit.DuplicateSourceMetadataCard, true)]
    [InlineData(SyntheticEdit.TitleAlteredWithOriginalInCitation, true)]
    [InlineData(SyntheticEdit.MetadataMovedUnderInterpretation, true)]
    [InlineData(SyntheticEdit.SeparateParagraphNoteBeforeExcerpt, false)]
    [InlineData(SyntheticEdit.SeparateTeacherOnlyNoteBeforeExcerpt, false)]
    [InlineData(SyntheticEdit.ExcerptSectionMovedIntact, false)]
    [InlineData(SyntheticEdit.MetadataRowsReordered, false)]
    [InlineData(SyntheticEdit.OptionalMetadataNotRecorded, false)]
    [InlineData(SyntheticEdit.OptionalMetadataInventedAfterBuild, true)]
    [InlineData(SyntheticEdit.CitationFormattingPreserved, false)]
    [InlineData(SyntheticEdit.DuplicateSourceTitleAnchor, true)]
    [InlineData(SyntheticEdit.AlteredMetadataWithOriginalUnderOtherTitle, true)]
    [InlineData(SyntheticEdit.IndependentTitledNotesBeforeSource, false)]
    [InlineData(SyntheticEdit.ExtraInventedMetadataRow, true)]
    public void Source_review_preserves_exact_source_roles_and_accepts_separate_notes(
        SyntheticEdit edit,
        bool expectedBlocker)
    {
        var mode = ModuleStudioCatalog.ByModeKey("source-lens");
        var values = SyntheticValues(mode);
        if (edit is SyntheticEdit.OptionalMetadataNotRecorded or SyntheticEdit.OptionalMetadataInventedAfterBuild)
        {
            values["place"] = "";
            values["audience"] = " \t";
            values["provenance"] = "";
        }
        else if (edit == SyntheticEdit.CitationFormattingPreserved)
        {
            foreach (var key in new[] { "creator", "title", "date", "type", "provenance" })
            {
                values[key] = " " + values[key] + ". ";
            }
        }

        var outcome = Assert.IsType<Func<ModuleInputValues, ModuleBuildOutcome>>(mode.Build)(new ModuleInputValues(values));
        var session = new ReviewSession(outcome.CreateDraft(), MachineAtReview(), outcome.Validator);
        var baselineDocument = session.Draft.Revision.Document;
        var baselineHash = ArtifactDocumentFingerprint.Compute(baselineDocument);
        session.SetRequiredIssuesAcknowledged(true);
        output.WriteLine("SOURCE_ROLE_BASELINE " + JsonSerializer.Serialize(new
        {
            Scope = "Wholly synthetic source-role control; no sink or human verification",
            Edit = edit,
            Inputs = values,
            Recipe = new { outcome.Recipe.Id, outcome.Recipe.Version, outcome.Recipe.OutputSchemaId, outcome.Recipe.EvaluationSuiteVersion },
            Document = baselineDocument,
            DocumentHash = baselineHash,
            session.Issues,
            session.CanApprove,
        }, EvidenceJson));
        Assert.False(DocumentValidator.HasBlockingIssues(session.Issues));
        Assert.NotEmpty(session.RequiredAcknowledgements);
        Assert.True(session.CanApprove);
        Assert.Equal("25", Assert.Single(baselineDocument.Nodes.OfType<Paragraph>()).Text);
        if (edit is SyntheticEdit.OptionalMetadataNotRecorded or SyntheticEdit.OptionalMetadataInventedAfterBuild)
        {
            var metadata = baselineDocument.Nodes.OfType<TableNode>().First();
            foreach (var field in new[] { "Place", "Audience", "Provenance" })
            {
                Assert.Equal(SourceLensBuilder.NotRecorded, Assert.Single(metadata.Rows, row => row[0] == field)[1]);
            }
        }
        else if (edit == SyntheticEdit.CitationFormattingPreserved)
        {
            Assert.Equal(
                "Synthetic creator A. Synthetic source-role fixture. Synthetic date A. Synthetic token record. Wholly generated source-role control; no external source.",
                Assert.Single(baselineDocument.Nodes.OfType<Citation>()).Text);
        }

        session = ApplySyntheticEdit(edit, outcome, session);
        if (edit == SyntheticEdit.Unchanged)
        {
            // The unchanged control also performs an explicit fresh acknowledgement.
            session.SetRequiredIssuesAcknowledged(false);
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
            approved = session.Approve("synthetic-source-review@example.invalid", SyntheticApprovalInstant);
        }
        catch (InvalidOperationException failure)
        {
            refusal = failure;
        }

        output.WriteLine("SOURCE_ROLE_OBSERVATION " + JsonSerializer.Serialize(new
        {
            Scope = "Wholly synthetic source-role control; no sink or human verification",
            Edit = edit,
            ExpectedBlocker = expectedBlocker,
            EditRoute = UsesEditedDraft(edit)
                ? "DraftArtifact.WithEditedDocument followed by a fresh ReviewSession; no UI append operation is asserted"
                : "Existing ReviewSession replacement/movement or unchanged revision",
            BaselineDocumentHash = baselineHash,
            attemptedRevision.Document,
            DocumentHash = ArtifactDocumentFingerprint.Compute(attemptedRevision.Document),
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

        try
        {
            Assert.False(beforeAcknowledgement);
            Assert.NotEmpty(requiredAcknowledgements);
            Assert.Equal(directIssues, session.Issues);
            var actualBlockingCodes = session.Issues.Where(issue => issue.Severity == ValidationSeverity.Blocking)
                .Select(issue => issue.Code).ToArray();
            var description = $"Synthetic edit {edit}: blocking codes=[{string.Join(", ", actualBlockingCodes)}]; " +
                $"CanApprove after fresh acknowledgement={canApproveAtAttempt}; typed approval returned={approved is not null}; " +
                $"refusal={refusal?.Message ?? "None"}.";
            if (expectedBlocker)
            {
                Assert.True(session.Issues.Any(issue => issue.Code == "lens.structure"
                    && issue.Severity == ValidationSeverity.Blocking),
                    "Expected blocking lens.structure for a changed source value or role. " + description);
                Assert.False(canApproveAtAttempt, description);
                Assert.Null(approved);
                Assert.NotNull(refusal);
                Assert.Equal(JobState.AwaitingTeacherReview, session.Machine.State);
            }
            else
            {
                Assert.Empty(actualBlockingCodes);
                Assert.True(canApproveAtAttempt, description);
                Assert.Null(refusal);
                Assert.NotNull(approved);
                Assert.Same(attemptedRevision, approved.Revision);
                Assert.Equal(JobState.Approved, session.Machine.State);
            }

            if (edit is SyntheticEdit.Unchanged or SyntheticEdit.IdenticalExcerptReplacement)
            {
                Assert.Equal(baselineHash, ArtifactDocumentFingerprint.Compute(attemptedRevision.Document));
            }
        }
        finally
        {
            // These controls own no asynchronous operation, native child, or sink.
            // End any refused in-memory review without inventing an approval.
            if (session.Machine.State == JobState.AwaitingTeacherReview)
            {
                session.Cancel();
            }
        }
    }

    private static Dictionary<string, object?> SyntheticValues(ModuleModeDefinition mode)
    {
        var values = ModuleStudioCatalog.Defaults(mode);
        values["creator"] = "Synthetic creator A";
        values["title"] = "Synthetic source-role fixture";
        values["date"] = "Synthetic date A";
        values["type"] = "Synthetic token record";
        values["rights"] = "Wholly synthetic repository fixture; GPL-3.0-or-later";
        values["place"] = "Synthetic place B";
        values["audience"] = "Synthetic workshop audience";
        values["provenance"] = "Wholly generated source-role control; no external source";
        values["excerpt"] = "25";
        values["transcript-verified"] = "true";
        values["sourcing"] = "Synthetic sourcing prompt.";
        values["context"] = "Synthetic context prompt.";
        values["close-reading"] = "Synthetic close-reading prompt.";
        values["corroboration"] = "Synthetic corroboration prompt.";
        values["bounded-interpretation"] = "Synthetic interpretation prompt.";
        values["observation-rows"] = "2";
        values["language"] = "en";
        return values;
    }

    private static ReviewSession ApplySyntheticEdit(SyntheticEdit edit, ModuleBuildOutcome outcome, ReviewSession session)
    {
        var document = session.Draft.Revision.Document;
        var excerptIndex = NodeIndex(document, node => node is Paragraph { Text: "25" });
        switch (edit)
        {
            case SyntheticEdit.Unchanged:
                break;
            case SyntheticEdit.IdenticalExcerptReplacement:
            case SyntheticEdit.OptionalMetadataNotRecorded:
            case SyntheticEdit.CitationFormattingPreserved:
                session.ReplaceNode(excerptIndex, new Paragraph("25"));
                break;
            case SyntheticEdit.ExcerptExpandedTo125:
                session.ReplaceNode(excerptIndex, new Paragraph("125"));
                break;
            case SyntheticEdit.ExcerptChangedTo26:
                session.ReplaceNode(excerptIndex, new Paragraph("26"));
                break;
            case SyntheticEdit.ExcerptHiddenInTeacherOnlyNotice:
                session.ReplaceNode(excerptIndex, new TeacherOnlyNotice("25"));
                break;
            case SyntheticEdit.ExcerptMovedUnderInterpretation:
                var interpretationIndex = NodeIndex(document, node => node is Heading { Level: 2, Text: "Interpretation, within bounds" });
                Assert.True(excerptIndex < interpretationIndex);
                // Removal shifts the later heading back one; this destination
                // therefore inserts the unchanged excerpt just after that heading.
                session.MoveNode(excerptIndex, interpretationIndex);
                break;
            case SyntheticEdit.DateAndPlaceValuesSwapped:
            case SyntheticEdit.ConflictingDuplicateDateLabel:
            case SyntheticEdit.DuplicateSourceMetadataCard:
            case SyntheticEdit.MetadataMovedUnderInterpretation:
            case SyntheticEdit.MetadataRowsReordered:
            case SyntheticEdit.OptionalMetadataInventedAfterBuild:
            case SyntheticEdit.AlteredMetadataWithOriginalUnderOtherTitle:
            case SyntheticEdit.ExtraInventedMetadataRow:
                var tableIndex = NodeIndex(document, node => node is TableNode table
                    && table.HeaderRow is not null
                    && table.HeaderRow.SequenceEqual(["Field", "Record"], StringComparer.Ordinal));
                var sourceTable = Assert.IsType<TableNode>(document.Nodes[tableIndex]);
                if (edit == SyntheticEdit.ExtraInventedMetadataRow)
                {
                    session.ReplaceNode(tableIndex, new TableNode(sourceTable.HeaderRow,
                        [.. sourceTable.Rows, InventedMetadataRow]));
                    break;
                }

                if (edit == SyntheticEdit.AlteredMetadataWithOriginalUnderOtherTitle)
                {
                    var withOtherSource = document.Nodes.ToList();
                    withOtherSource[tableIndex] = new TableNode(sourceTable.HeaderRow,
                        [.. sourceTable.Rows.Select(IReadOnlyList<string> (row) => row[0] == "Date"
                            ? [row[0], "Synthetic altered date"] : row)]);
                    withOtherSource.Add(new Heading(1, "Synthetic other source"));
                    withOtherSource.Add(sourceTable);
                    return ReviewEditedNodes(session, outcome, withOtherSource);
                }

                if (edit == SyntheticEdit.ConflictingDuplicateDateLabel)
                {
                    session.ReplaceNode(tableIndex, new TableNode(sourceTable.HeaderRow,
                        [.. sourceTable.Rows, ConflictingDateRow]));
                    break;
                }

                if (edit == SyntheticEdit.DuplicateSourceMetadataCard)
                {
                    var duplicated = document.Nodes.ToList();
                    duplicated.Insert(tableIndex + 1, sourceTable);
                    return ReviewEditedNodes(session, outcome, duplicated);
                }

                if (edit == SyntheticEdit.MetadataMovedUnderInterpretation)
                {
                    var destination = NodeIndex(document, node => node is Heading { Level: 2, Text: "Interpretation, within bounds" });
                    session.MoveNode(tableIndex, destination);
                    break;
                }

                if (edit == SyntheticEdit.MetadataRowsReordered)
                {
                    session.ReplaceNode(tableIndex, new TableNode(sourceTable.HeaderRow, [.. sourceTable.Rows.Reverse()]));
                    break;
                }

                if (edit == SyntheticEdit.OptionalMetadataInventedAfterBuild)
                {
                    session.ReplaceNode(tableIndex, new TableNode(sourceTable.HeaderRow,
                        [.. sourceTable.Rows.Select(IReadOnlyList<string> (row) => row[0] == "Place"
                            ? [row[0], "Synthetic invented place"] : row)]));
                    break;
                }

                var date = Assert.Single(sourceTable.Rows, row => row[0] == "Date")[1];
                var place = Assert.Single(sourceTable.Rows, row => row[0] == "Place")[1];
                session.ReplaceNode(tableIndex, new TableNode(sourceTable.HeaderRow,
                    [.. sourceTable.Rows.Select(IReadOnlyList<string> (row) => row[0] switch
                    {
                        "Date" => [row[0], place],
                        "Place" => [row[0], date],
                        _ => row,
                    })]));
                break;
            case SyntheticEdit.SeparateParagraphNoteAdded:
            case SyntheticEdit.SeparateTeacherOnlyNoteAdded:
            case SyntheticEdit.SeparateParagraphNoteBeforeExcerpt:
            case SyntheticEdit.SeparateTeacherOnlyNoteBeforeExcerpt:
                DocumentNode note = edit is SyntheticEdit.SeparateParagraphNoteAdded or SyntheticEdit.SeparateParagraphNoteBeforeExcerpt
                    ? new Paragraph("Synthetic separate teacher-authored inquiry note.")
                    : new TeacherOnlyNotice("Synthetic separate teacher-only inquiry note.");
                var withNote = document.Nodes.ToList();
                var noteIndex = edit is SyntheticEdit.SeparateParagraphNoteBeforeExcerpt or SyntheticEdit.SeparateTeacherOnlyNoteBeforeExcerpt
                    ? NodeIndex(document, node => node is Heading { Level: 2, Text: "Teacher-provided source excerpt" })
                    : withNote.Count;
                withNote.Insert(noteIndex, note);
                return ReviewEditedNodes(session, outcome, withNote);
            case SyntheticEdit.CitationAlteredWithOriginalInNote:
                var citationIndex = NodeIndex(document, node => node is Citation);
                var originalCitation = Assert.IsType<Citation>(document.Nodes[citationIndex]);
                session.ReplaceNode(citationIndex, new Citation("Synthetic altered citation."));
                return ReviewEditedNodes(session, outcome,
                    [.. session.Draft.Revision.Document.Nodes, new TeacherOnlyNotice(originalCitation.Text)]);
            case SyntheticEdit.DuplicateExcerptAnchor:
                var withAnchor = document.Nodes.ToList();
                withAnchor.Add(new Heading(2, "Teacher-provided source excerpt"));
                withAnchor.Add(new Paragraph("Synthetic competing excerpt."));
                withAnchor.Add(new Citation("Synthetic competing citation."));
                return ReviewEditedNodes(session, outcome, withAnchor);
            case SyntheticEdit.TitleAlteredWithOriginalInCitation:
                session.ReplaceNode(NodeIndex(document, node => node is Heading { Level: 1 }),
                    new Heading(1, "Synthetic altered source title"));
                break;
            case SyntheticEdit.DuplicateSourceTitleAnchor:
                return ReviewEditedNodes(session, outcome,
                    [.. document.Nodes, document.Nodes[NodeIndex(document, node => node is Heading { Level: 1 })]]);
            case SyntheticEdit.IndependentTitledNotesBeforeSource:
                return ReviewEditedNodes(session, outcome,
                    [new Heading(1, "Synthetic teacher notes"), new Paragraph("Synthetic independent note."), .. document.Nodes]);
            case SyntheticEdit.ExcerptSectionMovedIntact:
                var sourceHeadingIndex = NodeIndex(document, node => node is Heading { Level: 2, Text: "Teacher-provided source excerpt" });
                for (var index = 0; index < 3; index++)
                {
                    session.MoveNode(sourceHeadingIndex, document.Nodes.Count - 1);
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(edit), edit, "Unknown synthetic edit.");
        }

        return session;
    }

    private static bool UsesEditedDraft(SyntheticEdit edit)
        => edit is SyntheticEdit.SeparateParagraphNoteAdded or SyntheticEdit.SeparateTeacherOnlyNoteAdded
            or SyntheticEdit.SeparateParagraphNoteBeforeExcerpt or SyntheticEdit.SeparateTeacherOnlyNoteBeforeExcerpt
            or SyntheticEdit.CitationAlteredWithOriginalInNote or SyntheticEdit.DuplicateExcerptAnchor
            or SyntheticEdit.DuplicateSourceMetadataCard or SyntheticEdit.DuplicateSourceTitleAnchor
            or SyntheticEdit.AlteredMetadataWithOriginalUnderOtherTitle or SyntheticEdit.IndependentTitledNotesBeforeSource;

    private static ReviewSession ReviewEditedNodes(
        ReviewSession session,
        ModuleBuildOutcome outcome,
        IReadOnlyList<DocumentNode> nodes)
    {
        var editedDraft = session.Draft.WithEditedDocument(new ArtifactDocument(nodes, session.Draft.Revision.Document.Language));
        session.Cancel();
        return new ReviewSession(editedDraft, MachineAtReview(), outcome.Validator);
    }

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
