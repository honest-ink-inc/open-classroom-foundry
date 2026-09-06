// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;

namespace Foundry.Tests.Unit;

/// <summary>Synthetic route controls, not admission or real teacher review.</summary>
public sealed class VersionedModuleCatalogTests
{
    [Fact]
    public void Exact_inventory_is_additive_and_unversioned_selection_remains_historical()
    {
        Assert.Equal(11, ModuleStudioCatalog.All.SelectMany(door => door.Modes).Count());
        Assert.Equal(14, ModuleStudioCatalog.AllVersions.Count);
        Assert.Equal(["board-to-brief", "lesson-loom", "source-lens"],
            ModuleStudioCatalog.ReplacementCandidates.Select(mode => mode.Key));
        foreach (var mode in ModuleStudioCatalog.AllVersions)
        {
            Assert.Same(mode, ModuleStudioCatalog.ByModeKey(mode.Key, mode.Recipe.Version));
            var replacement = mode.Recipe.Version == "0.2.0";
            Assert.Equal(replacement ? "0.8.0-alpha" : "0.7.0-alpha", mode.Recipe.MinimumEngineVersion);
            Assert.Equal(replacement ? "0.2" : "0.1", mode.Recipe.EvaluationSuiteVersion);
            if (!replacement)
            {
                Assert.Same(mode, ModuleStudioCatalog.ByModeKey(mode.Key));
            }
        }
    }

    [Theory]
    [InlineData("lesson-loom", null)]
    [InlineData("lesson-loom", "")]
    [InlineData("lesson-loom", " 0.2.0")]
    [InlineData("lesson-loom", "0.2.0 ")]
    [InlineData("lesson-loom", "0.2")]
    [InlineData("lesson-loom", "latest")]
    [InlineData("lesson-loom", "0.3.0")]
    [InlineData("LESSON-LOOM", "0.2.0")]
    [InlineData("lesson-loom ", "0.2.0")]
    [InlineData("access-remix", "0.2.0")]
    public void Unknown_execution_tuples_refuse_without_fallback(string key, string? version)
        => Assert.Throws<ArgumentException>(() => ModuleStudioCatalog.ByModeKey(key, version!));

    [Theory]
    [InlineData("board-to-brief")]
    [InlineData("lesson-loom")]
    [InlineData("source-lens")]
    public void Registered_replacement_keeps_its_own_doors_filename_without_accepting_unknown_modes(string key)
    {
        var historical = ModuleStudioCatalog.ByModeKey(key, "0.1.0");
        var replacement = ModuleStudioCatalog.ByModeKey(key, "0.2.0");
        var door = Assert.Single(ModuleStudioCatalog.All, candidate => candidate.Modes.Contains(historical));
        Assert.Equal(ModulePublicIdentity.FileStemFor(door, historical),
            ModulePublicIdentity.FileStemFor(door, replacement));
        var otherDoor = ModuleStudioCatalog.All.First(candidate => !ReferenceEquals(candidate, door));
        Assert.Throws<ArgumentException>(() => ModulePublicIdentity.FileStemFor(otherDoor, replacement));
        Assert.Throws<ArgumentException>(() => ModulePublicIdentity.FileStemFor(door, replacement with { }));
        Assert.Throws<ArgumentException>(() => ModulePublicIdentity.FileStemFor(door,
            replacement with { Recipe = replacement.Recipe with { Version = "0.3.0" } }));
    }

    [Theory]
    [InlineData("board-to-brief")]
    [InlineData("lesson-loom")]
    [InlineData("source-lens")]
    public void Each_selected_builder_binds_its_actual_manifest_and_preserves_default_document(string key)
    {
        var historical = Build(key, "0.1.0");
        var replacement = Build(key, "0.2.0");
        Assert.Same(ModuleStudioCatalog.ByModeKey(key, "0.1.0").Recipe, historical.Recipe);
        Assert.Same(ModuleStudioCatalog.ByModeKey(key, "0.2.0").Recipe, replacement.Recipe);
        Assert.NotEqual(RecipeContractFingerprint.ComputeSha256(historical.Recipe),
            RecipeContractFingerprint.ComputeSha256(replacement.Recipe));
        Assert.Equal(ArtifactDocumentFingerprint.Compute(historical.Document),
            ArtifactDocumentFingerprint.Compute(replacement.Document));
        Assert.Equal(historical.Issues, replacement.Issues);
        Assert.Equal(historical.Validator.Validate(historical.Document), replacement.Validator.Validate(replacement.Document));
        foreach (var outcome in new[] { historical, replacement })
        {
            var session = Session(outcome);
            Assert.False(session.CanApprove);
            Assert.Throws<InvalidOperationException>(() => session.Approve("synthetic@example.invalid", Instant));
            session.SetRequiredIssuesAcknowledged(true);
            Assert.True(session.CanApprove);
            var approved = session.Approve("synthetic@example.invalid", Instant);
            Assert.Equal(ArtifactDocumentFingerprint.Compute(outcome.Document),
                ArtifactDocumentFingerprint.Compute(approved.Revision.Document));
        }
    }

    [Fact]
    public void Historical_checked_builder_overflow_and_replacement_timing_refusal_are_distinct()
    {
        const string phases = "Launch|2147483647|Synthetic launch.|Synthetic check|Synthetic response\nWork|2147483647|Synthetic work.||\nClosure|47|Synthetic closure.|Synthetic closure check|Synthetic closure response";
        Assert.Throws<OverflowException>(() => Build("lesson-loom", "0.1.0", values => values["phases"] = phases));
        var replacement = Build("lesson-loom", "0.2.0", values => values["phases"] = phases);
        Assert.Contains(replacement.Issues, issue => issue.Code == "loom.timing"
            && issue.Severity == ValidationSeverity.Blocking
            && issue.Message.Contains("4294967341", StringComparison.Ordinal));
        var session = Session(replacement);
        session.SetRequiredIssuesAcknowledged(true);
        Assert.False(session.CanApprove);
        Assert.Throws<InvalidOperationException>(() => session.Approve("synthetic@example.invalid", Instant));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Blank_evidence_remains_an_outgoing_defect_and_is_refused_only_by_replacement(string evidence)
    {
        var historical = Build("lesson-loom", "0.1.0", values => values["evidence"] = evidence);
        var replacement = Build("lesson-loom", "0.2.0", values => values["evidence"] = evidence);
        Assert.DoesNotContain(historical.Issues, issue => issue.Code == "loom.evidence");
        Assert.Contains(replacement.Issues, issue => issue.Code == "loom.evidence"
            && issue.Severity == ValidationSeverity.Blocking);
        Assert.Equal(ArtifactDocumentFingerprint.Compute(historical.Document),
            ArtifactDocumentFingerprint.Compute(replacement.Document));
    }

    [Theory]
    [InlineData("0.1.0", true)]
    [InlineData("0.2.0", false)]
    public void Reviewed_timing_keeps_the_selected_arithmetic_through_fresh_acknowledgement(string version, bool allows)
    {
        var outcome = Build("lesson-loom", version);
        var session = Session(outcome);
        var indexed = Assert.Single(outcome.Document.Nodes.Select((node, index) => (node, index)),
            item => item.node is TableNode table && table.HeaderRow!.SequenceEqual(["Phase", "Minutes", "Learners are doing"]));
        var phaseTable = (TableNode)indexed.node;
        string[] minutes = ["2147483647", "2147483647", "47"];
        session.SetRequiredIssuesAcknowledged(true);
        session.ReplaceNode(indexed.index, phaseTable with
        {
            Rows = [.. phaseTable.Rows.Select((row, index) => (IReadOnlyList<string>)[row[0], minutes[index], row[2]])],
        });
        Assert.False(session.CanApprove);
        session.SetRequiredIssuesAcknowledged(true);
        Assert.Equal(allows, session.CanApprove);
        Assert.Equal(!allows, session.Issues.Any(issue => issue.Code == "loom.timing" && issue.Severity == ValidationSeverity.Blocking));
        if (allows)
        {
            Assert.NotNull(session.Approve("synthetic@example.invalid", Instant));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => session.Approve("synthetic@example.invalid", Instant));
        }
    }

    [Theory]
    [InlineData("board-to-brief", "0.1.0", true)]
    [InlineData("board-to-brief", "0.2.0", false)]
    [InlineData("source-lens", "0.1.0", true)]
    [InlineData("source-lens", "0.2.0", false)]
    public void Moving_a_required_public_fact_into_teacher_notes_uses_the_selected_review_contract(string key, string version, bool allows)
    {
        var outcome = Build(key, version, values =>
        {
            if (key == "board-to-brief")
            {
                values["lines"] = "Synthetic brief|title\nJune 10|date\nRead the synthetic card.|step";
                values["locked-fields"] = "date|June 10";
            }
        });
        var indexed = Assert.Single(outcome.Document.Nodes.Select((node, index) => (node, index)), item => item.node is Paragraph);
        var session = Session(outcome);
        session.SetRequiredIssuesAcknowledged(true);
        session.ReplaceNode(indexed.index, new TeacherOnlyNotice(((Paragraph)indexed.node).Text));
        Assert.False(session.CanApprove);
        session.SetRequiredIssuesAcknowledged(true);
        Assert.Equal(allows, session.CanApprove);
        if (allows)
        {
            Assert.NotNull(session.Approve("synthetic@example.invalid", Instant));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => session.Approve("synthetic@example.invalid", Instant));
        }
    }

    private static readonly DateTimeOffset Instant = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);

    private static ModuleBuildOutcome Build(string key, string version, Action<Dictionary<string, object?>>? edit = null)
    {
        var mode = ModuleStudioCatalog.ByModeKey(key, version);
        var values = ModuleStudioCatalog.Defaults(mode);
        edit?.Invoke(values);
        return mode.Build!(new ModuleInputValues(values));
    }

    private static ReviewSession Session(ModuleBuildOutcome outcome)
    {
        var machine = new JobStateMachine();
        foreach (var state in new[] { JobState.Imported, JobState.Normalized, JobState.DataLaneConfirmed,
            JobState.DraftGenerated, JobState.SchemaValidated, JobState.InvariantsValidated, JobState.AwaitingTeacherReview })
        {
            machine.Transition(state);
        }

        return new ReviewSession(outcome.CreateDraft(), machine, outcome.Validator);
    }
}
