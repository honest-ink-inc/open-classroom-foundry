using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Application;

/// <summary>
/// The capture-side presenter (ADR-002): capture → normalize → teacher lane
/// confirmation, with Gate C invokable at any moment and RC-18 lane inheritance
/// at the exit. The teacher's lane confirmation is an attestation — staged
/// materials and empty environments qualify as Green because the teacher says
/// so and owns saying so (plan §4); everything downstream then inherits, and a
/// flow-computed draft can never fall below its sources.
/// </summary>
public sealed class CaptureSession(ICaptureSource source, IDocumentNormalizer normalizer)
{
    public JobStateMachine Machine { get; } = new();

    public SourceEnvelope? Envelope { get; private set; }

    public async Task<SourceEnvelope> CaptureAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        Machine.Transition(JobState.Imported);
        Envelope = await source.CaptureAsync(request, cancellationToken).ConfigureAwait(false);
        return Envelope;
    }

    public async Task<SourceEnvelope> NormalizeAsync(NormalizationRequest request, CancellationToken cancellationToken)
    {
        var envelope = Envelope ?? throw new InvalidOperationException("Nothing has been captured.");
        Envelope = await normalizer.NormalizeAsync(envelope, request, cancellationToken).ConfigureAwait(false);
        Machine.Transition(JobState.Normalized);
        return Envelope;
    }

    /// <summary>The teacher attests the lane; the attestation is theirs to make and theirs to answer for.</summary>
    public SourceEnvelope ConfirmLane(DataLane teacherConfirmedLane)
    {
        var envelope = Envelope ?? throw new InvalidOperationException("Nothing has been captured.");
        Envelope = envelope with { Lane = teacherConfirmedLane };
        Machine.Transition(JobState.DataLaneConfirmed);
        return Envelope;
    }

    /// <summary>Gate C: the adult's pause, available from any in-flight state.</summary>
    public SafetyPauseResult InvokeSafetyPause(DistrictPolicy policy)
        => SafetyGate.Invoke(Machine, policy);

    /// <summary>RC-18 at the exit: the draft's lane is computed from the confirmed envelope, never hand-passed.</summary>
    public DraftArtifact CreateDraft(ArtifactDocument document)
    {
        var envelope = Envelope ?? throw new InvalidOperationException("Nothing has been captured.");
        if (Machine.State != JobState.DataLaneConfirmed)
        {
            throw new InvalidOperationException("Confirm the lane before creating a draft.");
        }

        return DraftFactory.CreateFromSources(document, [envelope]);
    }
}
