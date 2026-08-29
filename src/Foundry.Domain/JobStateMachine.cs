namespace Foundry.Domain;

/// <summary>The mandatory artifact-job states of implementation plan §6.3, failures explicit.</summary>
public enum JobState
{
    New,
    Imported,
    Normalized,
    DataLaneConfirmed,
    OutboundPayloadPreviewed,
    DraftGenerated,
    SchemaValidated,
    InvariantsValidated,
    AwaitingTeacherReview,
    TeacherEdited,
    Approved,
    Rendered,

    /// <summary>Printed, exported, or deliberately saved.</summary>
    Completed,

    /// <summary>The only terminal state: transient sources verifiably purged.</summary>
    TransientSourcesPurged,

    Blocked,
    Declined,
    Cancelled,
    ProviderFailed,
    ValidationFailed,
    PurgeIncomplete,
}

/// <summary>
/// The transition table is the law: failure is never silent passage, purge follows
/// completion and cancellation alike, and a deterministic authoring path exists
/// with no egress state at all.
/// </summary>
public sealed class JobStateMachine
{
    public JobState State { get; private set; } = JobState.New;

    public static bool IsTerminal(JobState state) => state == JobState.TransientSourcesPurged;

    public static bool CanTransition(JobState from, JobState to)
    {
        if (from == to || IsTerminal(from))
        {
            return false;
        }

        // Cancellation is reachable from anywhere still in flight, but never rewrites history.
        if (to == JobState.Cancelled)
        {
            return from is not (JobState.Completed or JobState.Cancelled);
        }

        return (from, to) switch
        {
            (JobState.New, JobState.Imported) => true,
            (JobState.Imported, JobState.Normalized) => true,
            (JobState.Normalized, JobState.DataLaneConfirmed) => true,

            (JobState.DataLaneConfirmed, JobState.Blocked) => true,
            (JobState.DataLaneConfirmed, JobState.OutboundPayloadPreviewed) => true,
            // Deterministic authoring path: the teacher authors manually, no egress state exists.
            (JobState.DataLaneConfirmed, JobState.DraftGenerated) => true,

            (JobState.OutboundPayloadPreviewed, JobState.Declined) => true,
            (JobState.OutboundPayloadPreviewed, JobState.DraftGenerated) => true,
            (JobState.OutboundPayloadPreviewed, JobState.ProviderFailed) => true,
            (JobState.ProviderFailed, JobState.OutboundPayloadPreviewed) => true,

            (JobState.DraftGenerated, JobState.SchemaValidated) => true,
            (JobState.DraftGenerated, JobState.ValidationFailed) => true,
            (JobState.SchemaValidated, JobState.InvariantsValidated) => true,
            (JobState.SchemaValidated, JobState.ValidationFailed) => true,
            (JobState.InvariantsValidated, JobState.AwaitingTeacherReview) => true,
            (JobState.InvariantsValidated, JobState.ValidationFailed) => true,
            (JobState.ValidationFailed, JobState.OutboundPayloadPreviewed) => true,
            (JobState.ValidationFailed, JobState.Declined) => true,

            (JobState.AwaitingTeacherReview, JobState.TeacherEdited) => true,
            (JobState.AwaitingTeacherReview, JobState.Approved) => true,
            (JobState.AwaitingTeacherReview, JobState.Declined) => true,
            (JobState.TeacherEdited, JobState.AwaitingTeacherReview) => true,
            // A later edit invalidates approval (ADR-004).
            (JobState.Approved, JobState.TeacherEdited) => true,

            (JobState.Approved, JobState.Rendered) => true,
            (JobState.Rendered, JobState.Completed) => true,

            // Purge follows completion, cancellation, decline, and block alike.
            (JobState.Completed, JobState.TransientSourcesPurged) => true,
            (JobState.Completed, JobState.PurgeIncomplete) => true,
            (JobState.Cancelled, JobState.TransientSourcesPurged) => true,
            (JobState.Cancelled, JobState.PurgeIncomplete) => true,
            (JobState.Declined, JobState.TransientSourcesPurged) => true,
            (JobState.Declined, JobState.PurgeIncomplete) => true,
            (JobState.Blocked, JobState.TransientSourcesPurged) => true,
            (JobState.PurgeIncomplete, JobState.TransientSourcesPurged) => true,

            _ => false,
        };
    }

    public bool TryTransition(JobState to)
    {
        if (!CanTransition(State, to))
        {
            return false;
        }

        State = to;
        return true;
    }

    public void Transition(JobState to)
    {
        if (!TryTransition(to))
        {
            throw new InvalidOperationException($"Illegal job transition {State} → {to}.");
        }
    }
}
