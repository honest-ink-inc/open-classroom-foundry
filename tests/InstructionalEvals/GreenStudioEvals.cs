using System.Text;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.LessonLoom;
using Foundry.Modules.BuiltIn.ScaffoldSmith;
using Foundry.Modules.BuiltIn.TalkMoves;
using Foundry.Rendering;
using Xunit;

namespace Foundry.Tests.InstructionalEvals;

/// <summary>
/// Release 0.3 evaluation harness growth: the Green planning studio's audience
/// separation — rationale, decisions, and reflections are teacher craft; learners
/// receive the work, never the machinery.
/// </summary>
public class GreenStudioEvals
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static async Task<(string Learner, string Teacher)> RenderBothAsync(ArtifactDocument document, IReadOnlyList<ValidationIssue> issues)
    {
        Assert.False(DocumentValidator.HasBlockingIssues(issues));
        var approved = ApprovalGate.Approve(DraftArtifact.New(document, DataLane.Green), "teacher@example.org", issues, SomeInstant);
        var renderer = new AccessibleHtmlRenderer();

        var learner = Encoding.UTF8.GetString(
            (await renderer.RenderAsync(approved, new RenderRequest(RenderTarget.AccessibleHtml), CancellationToken.None)).Content.Span);
        var teacher = Encoding.UTF8.GetString(
            (await renderer.RenderAsync(approved, new RenderRequest(RenderTarget.AccessibleHtml, RenderAudience.Teacher), CancellationToken.None)).Content.Span);
        return (learner, teacher);
    }

    [Fact]
    public async Task Scaffold_rationale_and_removal_plan_are_teacher_craft_not_learner_content()
    {
        var result = ScaffoldSmithBuilder.BuildPacket(
            "Chapter 9 suspense analysis",
            new LearningTarget("Explain how the author builds suspense.", "Paragraph citing two techniques."),
            ["Names two techniques", "Cites pages"],
            [new ScaffoldSpec("Technique word bank", "Vocabulary retrieval", "Choosing the techniques", "Two analyses without the bank")]);

        var (learner, teacher) = await RenderBothAsync(result.Document, result.Issues);

        Assert.DoesNotContain("Fade when", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("Removal plan", learner, StringComparison.Ordinal);
        Assert.Contains("Technique word bank", learner, StringComparison.Ordinal);
        Assert.Contains("Removal plan", teacher, StringComparison.Ordinal);
        Assert.Contains("Fade when: Two analyses without the bank", teacher, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lesson_contingencies_and_evidence_stay_on_the_teacher_page()
    {
        var result = LessonLoomBuilder.Build(
            "Equivalent fractions",
            new LearningTarget("Model equivalent fractions.", "Exit ticket with two pairs."),
            45,
            [
                new LessonPhase("Launch", 10, "Estimate strips.", "Estimates shared", "Address the common miss"),
                new LessonPhase("Work", 28, "Build pairs."),
                new LessonPhase("Closure", 7, "Exit ticket.", "Tickets in", "Regroup tomorrow"),
            ],
            ["Strip trays"], ["Pre-folded strips"],
            contingencies: ["No device day: strips only, gallery share instead of slides."]);

        var (learner, teacher) = await RenderBothAsync(result.Document, result.Issues);

        Assert.DoesNotContain("Contingency", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("Exit ticket with two pairs.", learner, StringComparison.Ordinal);
        Assert.Contains("Contingency: No device day", teacher, StringComparison.Ordinal);
        Assert.Contains("When you see", teacher, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Discussion_purposes_and_the_equity_reflection_never_reach_learners()
    {
        var result = TalkMovesBuilder.Build(
            "The narrator's secret",
            [new DiscussionQuestion("Was the narrator right?", "Surface competing readings", "Chapter 4 lines")],
            ["Say it", "Write it", "Point to it"],
            "invite", "build", "press", "repair", "synthesize");

        var (learner, teacher) = await RenderBothAsync(result.Document, result.Issues);

        Assert.Contains("Was the narrator right?", learner, StringComparison.Ordinal);
        Assert.Contains(TalkMovesBuilder.PassOption, learner, StringComparison.Ordinal);
        Assert.DoesNotContain("Purpose:", learner, StringComparison.Ordinal);
        Assert.DoesNotContain("Equity reflection", learner, StringComparison.Ordinal);
        Assert.Contains("Equity reflection", teacher, StringComparison.Ordinal);
    }
}
