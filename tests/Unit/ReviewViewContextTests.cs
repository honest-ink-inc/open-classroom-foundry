// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Tests.Unit;

public sealed class ReviewViewContextTests
{
    [Fact]
    public void Exact_source_and_preview_profile_stay_bound_across_draft_edits()
    {
        var source = new ReviewSourceContext(
            "Synthetic verified transcription",
            "Exact source line.\nSecond line.");
        var request = new RenderRequest(
            RenderTarget.AccessibleHtml,
            RenderAudience.Teacher,
            TextScalePercent: 175,
            TargetLanguageFirst: true);
        var context = new ReviewViewContext(request, source);
        var session = new ReviewSession(
            DraftArtifact.New(
                new ArtifactDocument([new Paragraph("Initial draft")]),
                DataLane.Green),
            MachineAtReview(),
            new DefaultArtifactValidator(),
            context);

        var initialRevision = session.Draft.Revision;
        session.ReplaceNode(0, new Paragraph("Edited draft"));

        Assert.NotSame(initialRevision, session.Draft.Revision);
        Assert.Same(context, session.ViewContext);
        Assert.Same(source, session.ViewContext.Source);
        Assert.Equal(request, session.ViewContext.PreviewRequest);
        Assert.Equal("Exact source line.\nSecond line.", session.ViewContext.Source!.ExactText);
    }

    [Fact]
    public void Manual_default_represents_no_source_and_rejects_non_html_or_invalid_profiles()
    {
        Assert.Null(ReviewViewContext.ManualDefault.Source);
        Assert.Equal(RenderTarget.PrintHtml, ReviewViewContext.ManualDefault.PreviewRequest.Target);
        Assert.Equal(RenderAudience.Learner, ReviewViewContext.ManualDefault.PreviewRequest.Audience);
        Assert.Equal(100, ReviewViewContext.ManualDefault.PreviewRequest.TextScalePercent);
        Assert.False(ReviewViewContext.ManualDefault.PreviewRequest.TargetLanguageFirst);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReviewViewContext(new RenderRequest(RenderTarget.PrintPdf)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReviewViewContext(new RenderRequest(
                RenderTarget.PrintHtml,
                RenderAudience.Learner,
                TextScalePercent: 99)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReviewViewContext(new RenderRequest(
                RenderTarget.PrintHtml,
                RenderAudience.Learner,
                TextScalePercent: 201)));
    }

    private static JobStateMachine MachineAtReview()
    {
        var machine = new JobStateMachine();
        foreach (var state in new[]
        {
            JobState.Imported,
            JobState.Normalized,
            JobState.DataLaneConfirmed,
            JobState.DraftGenerated,
            JobState.SchemaValidated,
            JobState.InvariantsValidated,
            JobState.AwaitingTeacherReview,
        })
        {
            machine.Transition(state);
        }

        return machine;
    }
}
