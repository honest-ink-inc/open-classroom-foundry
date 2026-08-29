using Foundry.Domain;

namespace Foundry.Tests.Unit;

public class JobStateMachineTests
{
    [Fact]
    public void The_model_assisted_happy_path_walks_end_to_end()
    {
        var machine = new JobStateMachine();

        foreach (var state in new[]
        {
            JobState.Imported, JobState.Normalized, JobState.DataLaneConfirmed,
            JobState.OutboundPayloadPreviewed, JobState.DraftGenerated,
            JobState.SchemaValidated, JobState.InvariantsValidated,
            JobState.AwaitingTeacherReview, JobState.Approved, JobState.Rendered,
            JobState.Completed, JobState.TransientSourcesPurged,
        })
        {
            machine.Transition(state);
        }

        Assert.Equal(JobState.TransientSourcesPurged, machine.State);
    }

    [Fact]
    public void The_deterministic_authoring_path_never_touches_an_egress_state()
    {
        Assert.True(JobStateMachine.CanTransition(JobState.DataLaneConfirmed, JobState.DraftGenerated));
    }

    [Fact]
    public void A_later_edit_invalidates_approval()
    {
        Assert.True(JobStateMachine.CanTransition(JobState.Approved, JobState.TeacherEdited));
        Assert.True(JobStateMachine.CanTransition(JobState.TeacherEdited, JobState.AwaitingTeacherReview));
    }

    [Fact]
    public void Restricted_content_blocks_and_still_purges()
    {
        Assert.True(JobStateMachine.CanTransition(JobState.DataLaneConfirmed, JobState.Blocked));
        Assert.True(JobStateMachine.CanTransition(JobState.Blocked, JobState.TransientSourcesPurged));
    }

    [Theory]
    [InlineData(JobState.Imported)]
    [InlineData(JobState.OutboundPayloadPreviewed)]
    [InlineData(JobState.AwaitingTeacherReview)]
    [InlineData(JobState.Rendered)]
    public void Cancellation_is_reachable_from_work_in_flight(JobState from)
    {
        Assert.True(JobStateMachine.CanTransition(from, JobState.Cancelled));
    }

    [Fact]
    public void Purge_follows_cancellation_exactly_as_it_follows_completion()
    {
        Assert.True(JobStateMachine.CanTransition(JobState.Cancelled, JobState.TransientSourcesPurged));
        Assert.True(JobStateMachine.CanTransition(JobState.Completed, JobState.TransientSourcesPurged));
    }

    [Fact]
    public void Completion_cannot_be_cancelled_after_the_fact()
    {
        Assert.False(JobStateMachine.CanTransition(JobState.Completed, JobState.Cancelled));
    }

    [Fact]
    public void Nothing_leaves_the_terminal_state()
    {
        foreach (var to in Enum.GetValues<JobState>())
        {
            Assert.False(JobStateMachine.CanTransition(JobState.TransientSourcesPurged, to));
        }
    }

    [Fact]
    public void Provider_failure_permits_an_explicit_retry()
    {
        Assert.True(JobStateMachine.CanTransition(JobState.OutboundPayloadPreviewed, JobState.ProviderFailed));
        Assert.True(JobStateMachine.CanTransition(JobState.ProviderFailed, JobState.OutboundPayloadPreviewed));
    }

    [Fact]
    public void An_incomplete_purge_is_explicit_and_retryable()
    {
        Assert.True(JobStateMachine.CanTransition(JobState.Completed, JobState.PurgeIncomplete));
        Assert.True(JobStateMachine.CanTransition(JobState.PurgeIncomplete, JobState.TransientSourcesPurged));
    }

    [Fact]
    public void Illegal_transitions_throw_rather_than_pass_silently()
    {
        var machine = new JobStateMachine();

        Assert.Throws<InvalidOperationException>(() => machine.Transition(JobState.Approved));
        Assert.Equal(JobState.New, machine.State);
    }

    [Fact]
    public void A_state_never_transitions_to_itself()
    {
        Assert.False(JobStateMachine.CanTransition(JobState.New, JobState.New));
    }
}
