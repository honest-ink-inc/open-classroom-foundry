using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Xunit;

namespace Foundry.Tests.Unit;

/// <summary>Gate C v1 against its design's acceptance criteria (docs/safeguarding/gate-c-design.md).</summary>
public class SafetyGateTests
{
    private static JobStateMachine MachineAt(params JobState[] path)
    {
        var machine = new JobStateMachine();
        foreach (var state in path)
        {
            machine.Transition(state);
        }

        return machine;
    }

    [Theory]
    [InlineData(JobState.Imported)]
    [InlineData(JobState.OutboundPayloadPreviewed)]
    [InlineData(JobState.AwaitingTeacherReview)]
    [InlineData(JobState.Rendered)]
    public void The_adult_can_pause_any_job_in_flight(JobState from)
    {
        Assert.True(JobStateMachine.CanTransition(from, JobState.Blocked));
    }

    [Fact]
    public void A_blocked_job_can_reach_only_the_purge_states()
    {
        foreach (var to in Enum.GetValues<JobState>())
        {
            var allowed = to is JobState.TransientSourcesPurged or JobState.PurgeIncomplete;
            Assert.Equal(allowed, JobStateMachine.CanTransition(JobState.Blocked, to));
        }
    }

    [Fact]
    public void Invocation_blocks_the_job_and_uses_the_district_procedure_when_present()
    {
        var machine = MachineAt(JobState.Imported, JobState.Normalized);
        var policy = DistrictPolicy.Offline with
        {
            SafeguardingProcedureText = "Contact the counselor on duty at extension 4411 and remain with the material.",
        };

        var result = SafetyGate.Invoke(machine, policy);

        Assert.Equal(JobState.Blocked, machine.State);
        Assert.True(result.FromDistrictPolicy);
        Assert.Contains("4411", result.ProcedureText, StringComparison.Ordinal);
    }

    [Fact]
    public void The_fallback_points_to_humans_and_authors_no_safety_instructions()
    {
        var machine = MachineAt(JobState.Imported);

        var result = SafetyGate.Invoke(machine, DistrictPolicy.Offline);

        Assert.False(result.FromDistrictPolicy);
        Assert.Contains("school's", result.ProcedureText, StringComparison.Ordinal);
        Assert.Contains("does not assess, record, or report", result.ProcedureText, StringComparison.Ordinal);
        // The application never claims detection.
        Assert.DoesNotContain("detected", result.ProcedureText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_completed_or_cancelled_job_cannot_be_blocked_after_the_fact()
    {
        Assert.False(JobStateMachine.CanTransition(JobState.Completed, JobState.Blocked));
        Assert.False(JobStateMachine.CanTransition(JobState.Cancelled, JobState.Blocked));
    }
}
