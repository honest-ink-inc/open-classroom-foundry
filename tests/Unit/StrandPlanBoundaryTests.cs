// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.BuiltIn.LessonLoom;

namespace Foundry.Tests.Unit;

public sealed class StrandPlanBoundaryTests
{
    [Fact]
    public void Large_phase_minutes_reach_a_timing_refusal_instead_of_overflowing_the_builder()
    {
        var mode = ModuleStudioCatalog.ByModeKey("lesson-loom");
        var values = ModuleStudioCatalog.Defaults(mode);
        values["phases"] = "Launch|2147483647|Synthetic launch work.|Synthetic launch check|Synthetic response\nWork|2147483647|Synthetic work.||\nClosure|47|Synthetic closure work.|Synthetic closure check|Synthetic closure response";

        var outcome = mode.Build!(new ModuleInputValues(values));

        Assert.Contains(outcome.Issues, issue => issue.Code == "loom.timing"
            && issue.Severity == ValidationSeverity.Blocking
            && issue.Message.Contains("4294967341", StringComparison.Ordinal));
    }

    [Fact]
    public void Reviewed_phase_minutes_cannot_wrap_around_to_the_original_available_time()
    {
        var mode = ModuleStudioCatalog.ByModeKey("lesson-loom");
        var outcome = mode.Build!(new ModuleInputValues(ModuleStudioCatalog.Defaults(mode)));
        var phaseTable = Assert.Single(outcome.Document.Nodes.OfType<TableNode>(), table =>
            table.HeaderRow!.SequenceEqual(["Phase", "Minutes", "Learners are doing"], StringComparer.Ordinal));
        var minutes = new[] { "2147483647", "2147483647", "47" };
        var editedTable = phaseTable with
        {
            Rows = [.. phaseTable.Rows.Select((row, index) =>
                (IReadOnlyList<string>)[row[0], minutes[index], row[2]])],
        };
        var edited = new ArtifactDocument([.. outcome.Document.Nodes.Select(node =>
            ReferenceEquals(node, phaseTable) ? editedTable : node)], outcome.Document.Language);

        Assert.DoesNotContain(DocumentValidator.Validate(edited), issue => issue.Severity == ValidationSeverity.Blocking);
        Assert.Contains(outcome.Validator.Validate(edited), issue =>
            issue.Code == "loom.timing" && issue.Severity == ValidationSeverity.Blocking);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    public void A_required_learning_evidence_value_cannot_be_replaced_by_its_generated_heading(string evidence)
    {
        var mode = ModuleStudioCatalog.ByModeKey("lesson-loom");
        Assert.True(Assert.Single(mode.Fields, field => field.Key == "evidence").IsRequired);
        var values = ModuleStudioCatalog.Defaults(mode);
        values["evidence"] = evidence;

        var outcome = mode.Build!(new ModuleInputValues(values));

        Assert.Contains(outcome.Issues, issue =>
            issue.Code == "loom.evidence" && issue.Severity == ValidationSeverity.Blocking);
        Assert.Contains(outcome.Validator.Validate(outcome.Document), issue =>
            issue.Code == "loom.evidence" && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void A_direct_builder_keeps_exact_large_totals_when_they_really_match()
    {
        var result = LessonLoomBuilder.Build(
            "Synthetic boundary lesson",
            new LearningTarget("Synthetic target", "Synthetic evidence"),
            int.MaxValue,
            [
                new LessonPhase("Launch", int.MaxValue - 1, "Synthetic work", "Synthetic check", "Synthetic response"),
                new LessonPhase("Closure", 1, "Synthetic closure", "Synthetic closure check", "Synthetic closure response"),
            ],
            ["Synthetic material"],
            ["Synthetic access route"]);

        Assert.DoesNotContain(result.Issues, issue => issue.Severity == ValidationSeverity.Blocking);
    }
}
