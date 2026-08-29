using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.LessonLoom;
using Foundry.Modules.BuiltIn.ScaffoldSmith;
using Foundry.Modules.BuiltIn.TalkMoves;

namespace Foundry.Tests.Unit;

public class ScaffoldSmithTests
{
    private static readonly LearningTarget Target = new(
        "Explain how the author builds suspense across a chapter.",
        "A written paragraph citing two suspense techniques with page references.");

    [Fact]
    public void A_packet_keeps_the_target_criteria_and_optional_supports_visible()
    {
        var result = ScaffoldSmithBuilder.BuildPacket(
            "Chapter 9 suspense analysis",
            Target,
            ["Names two techniques", "Cites page numbers", "Explains the effect on the reader"],
            [new ScaffoldSpec("Technique word bank", "Vocabulary retrieval", "Choosing and justifying the techniques", "Two analyses without the bank")],
            hintLadder: ["Reread the last page of the chapter.", "Where does the author slow time down?"],
            vocabularyBank: ["foreshadowing", "cliffhanger", "pacing"]);

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));

        var text = string.Join('\n', DocumentText.CollectStrings(result.Document));
        Assert.Contains("Names two techniques", text, StringComparison.Ordinal);
        Assert.Contains("optional", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Removal plan", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_scaffold_without_its_fade_criterion_blocks()
    {
        var result = ScaffoldSmithBuilder.BuildPacket(
            "Task", Target, ["Criterion"],
            [new ScaffoldSpec("Word bank", "Vocabulary retrieval", "Choosing techniques", "  ")]);

        Assert.Contains(result.Issues, i => i.Code == "scaffold.rationale");
    }

    [Fact]
    public void One_hint_is_not_a_ladder()
    {
        var result = ScaffoldSmithBuilder.BuildPacket(
            "Task", Target, ["Criterion"],
            [new ScaffoldSpec("Bank", "Barrier", "Demand", "Fade")],
            hintLadder: ["Only hint"]);

        Assert.Contains(result.Issues, i => i.Code == "scaffold.ladder");
    }

    [Fact]
    public void The_task_entry_preset_carries_taskdocks_whole_shape()
    {
        var result = ScaffoldSmithBuilder.BuildTaskEntry(
            "Science fair display board",
            ["Tri-fold board", "Printed photos", "Glue stick"],
            "Write your project title on a strip of paper.",
            ["Lay out the sections before gluing.", "Glue the title and photos.", "Add your captions."],
            ["Ask your table partner.", "Check the example board.", "Raise your hand."],
            "All five sections are glued down and readable from one meter away.");

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));
        Assert.Contains(result.Document.Nodes.OfType<Card>(), c => c.Title == "First");
        Assert.Contains(result.Document.Nodes.OfType<Card>(), c => c.Title == "Done means");
        Assert.Contains(
            result.Document.Nodes.OfType<TeacherOnlyNotice>(),
            n => n.Text.Contains("ADR-005", StringComparison.Ordinal) && n.Text.Contains("fade", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Task_entry_without_a_help_route_is_a_wall_and_blocks()
    {
        var result = ScaffoldSmithBuilder.BuildTaskEntry(
            "Task", [], "First action", ["Chunk"], [], "Done");

        Assert.Contains(result.Issues, i => i.Code == "task-entry.help");
    }

    [Fact]
    public void The_two_recipes_include_the_discoverable_task_entry_identity()
    {
        Assert.Equal(2, ScaffoldSmithBuilder.Recipes.Count);
        Assert.Contains(ScaffoldSmithBuilder.Recipes, r => r.Id == "scaffold-smith.task-entry"
            && r.InstructionalPurpose.Contains("Task breakdown", StringComparison.Ordinal));
    }
}

public class LessonLoomTests
{
    private static readonly LearningTarget Target = new(
        "Model equivalent fractions with strips.",
        "Exit ticket: two equivalent pairs modeled and named.");

    private static IReadOnlyList<LessonPhase> SoundPhases() =>
    [
        new LessonPhase("Launch", 8, "Estimate which strip is longer."),
        new LessonPhase("Model", 12, "Fold and compare strips with a partner.", "Half the pairs found 2/4 = 1/2", "Pull the strip tray and re-model with thirds"),
        new LessonPhase("Practice", 18, "Build three equivalent pairs."),
        new LessonPhase("Closure", 7, "Complete the exit ticket.", "Exit tickets collected", "Tomorrow's warm-up regroups by the miss pattern"),
    ];

    [Fact]
    public void A_feasible_lesson_builds_with_a_decision_table()
    {
        var result = LessonLoomBuilder.Build(
            "Equivalent fractions with strips", Target, 45, SoundPhases(),
            ["Fraction strip trays", "Exit tickets"], ["Strips pre-folded for two students", "Sentence frame on the board"]);

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));
        Assert.Equal(2, result.Document.Nodes.OfType<TableNode>().Count());
    }

    [Fact]
    public void Minutes_that_do_not_sum_block_with_the_arithmetic_shown()
    {
        var result = LessonLoomBuilder.Build(
            "Lesson", Target, 50, SoundPhases(), ["x"], ["y"]);

        var issue = Assert.Single(result.Issues, i => i.Code == "loom.timing");
        Assert.Contains("45", issue.Message, StringComparison.Ordinal);
        Assert.Contains("50", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_check_without_a_response_is_theater_and_blocks()
    {
        var phases = new List<LessonPhase>(SoundPhases()) { [1] = new LessonPhase("Model", 12, "Fold strips.", "Half found it", null) };

        var result = LessonLoomBuilder.Build("Lesson", Target, 45, phases, ["x"], ["y"]);

        Assert.Contains(result.Issues, i => i.Code == "loom.check-response");
    }

    [Fact]
    public void Fewer_than_two_checks_blocks_and_closure_must_produce_evidence()
    {
        var oneCheck = new List<LessonPhase>
        {
            new("Launch", 10, "Warm up."),
            new("Work", 25, "Practice."),
            new("Closure", 10, "Exit ticket.", "Tickets in", "Regroup tomorrow"),
        };
        Assert.Contains(LessonLoomBuilder.Build("L", Target, 45, oneCheck, ["x"], ["y"]).Issues,
            i => i.Code == "loom.checks");

        var noClosureCheck = new List<LessonPhase>
        {
            new("Launch", 10, "Warm up.", "Estimates shared", "Address the common miss"),
            new("Work", 25, "Practice.", "Circulate", "Pull a small group"),
            new("Closure", 10, "Pack up."),
        };
        Assert.Contains(LessonLoomBuilder.Build("L", Target, 45, noClosureCheck, ["x"], ["y"]).Issues,
            i => i.Code == "loom.closure");
    }

    [Fact]
    public void Decisions_use_the_shared_instructional_contract()
    {
        var decisions = LessonLoomBuilder.Decisions(SoundPhases());

        Assert.Equal(2, decisions.Count);
        Assert.IsType<InstructionalDecision>(decisions[0]);
        Assert.Equal("Half the pairs found 2/4 = 1/2", decisions[0].WhenYouSee);
    }
}

public class TalkMovesTests
{
    private static IReadOnlyList<DiscussionQuestion> Questions() =>
    [
        new DiscussionQuestion(
            "Was the narrator right to keep the secret?",
            "Surface competing readings before the evidence pass",
            "Page references from chapter 4"),
    ];

    [Fact]
    public void A_discussion_plan_appends_pass_and_carries_all_five_move_families()
    {
        var result = TalkMovesBuilder.Build(
            "The narrator's secret", Questions(),
            ["Say it aloud", "Write it on your card", "Point to the line in the text"],
            "What do you think, and who haven't we heard from - in any mode?",
            "Who can add to that idea?",
            "Where in the text do you see that?",
            "Say back what you heard before you disagree.",
            "Who can pull our two readings together?");

        Assert.False(DocumentValidator.HasBlockingIssues(result.Issues));

        var modes = result.Document.Nodes.OfType<UnorderedList>().First(l => l.Items.Contains(TalkMovesBuilder.PassOption));
        Assert.Equal(4, modes.Items.Count);

        var facilitation = Assert.Single(
            result.Document.Nodes.OfType<TeacherOnlyNotice>(),
            n => n.Text.StartsWith("Facilitation moves", StringComparison.Ordinal));
        Assert.Contains("Press for evidence", facilitation.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Fewer_than_three_participation_modes_blocks()
    {
        var result = TalkMovesBuilder.Build(
            "Topic", Questions(), ["Speak", "Write"], "i", "b", "p", "r", "s");

        Assert.Contains(result.Issues, i => i.Code == "talk.modes");
    }

    [Fact]
    public void A_question_without_purpose_or_evidence_blocks()
    {
        var result = TalkMovesBuilder.Build(
            "Topic",
            [new DiscussionQuestion("Why?", "  ", "Text evidence")],
            ["a", "b", "c"], "i", "b", "p", "r", "s");

        Assert.Contains(result.Issues, i => i.Code == "talk.question-map");
    }

    [Fact]
    public void A_missing_move_family_blocks_by_name()
    {
        var result = TalkMovesBuilder.Build(
            "Topic", Questions(), ["a", "b", "c"], "i", "b", "  ", "r", "s");

        Assert.Contains(result.Issues, i => i.Code == "talk.moves" && i.Message.Contains("press for evidence", StringComparison.Ordinal));
    }
}
