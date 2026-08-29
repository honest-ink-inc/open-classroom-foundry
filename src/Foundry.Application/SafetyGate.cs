using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Application;

public sealed record SafetyPauseResult(string ProcedureText, bool FromDistrictPolicy);

/// <summary>
/// Gate C, version 1 (docs/safeguarding/gate-c-design.md): teacher-invoked only.
/// Invocation blocks the job — nothing renders, prints, exports, or transmits
/// from it afterward, and it can reach only the purge states — and returns the
/// district's procedure text for the supervising adult, or a neutral fallback
/// that authors no safety instructions of its own. This function stores nothing
/// and notifies no one: it is a pause and a pointer to the humans whose job this
/// actually is.
/// </summary>
public static class SafetyGate
{
    public const string NeutralFallback =
        "Pause here. Review the original physical or local source directly and follow your school's "
        + "safeguarding procedure, consulting the staff member it names. This application does not "
        + "assess, record, or report safety concerns; those remain human procedures.";

    public static SafetyPauseResult Invoke(JobStateMachine machine, DistrictPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(machine);
        ArgumentNullException.ThrowIfNull(policy);

        machine.Transition(JobState.Blocked);

        return string.IsNullOrWhiteSpace(policy.SafeguardingProcedureText)
            ? new SafetyPauseResult(NeutralFallback, FromDistrictPolicy: false)
            : new SafetyPauseResult(policy.SafeguardingProcedureText, FromDistrictPolicy: true);
    }
}
