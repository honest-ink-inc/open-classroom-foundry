using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Simulated;
using Foundry.Infrastructure.Windows;
using Xunit;

namespace Foundry.Tests.Integration;

/// <summary>The Days 46–60 cancellation and purge path evidence (plan §14).</summary>
public class CancellationAndPurgeTests
{
    [Fact]
    public async Task A_cancelled_job_still_purges_every_session_byte()
    {
        var store = new InMemorySessionByteStore();
        var camera = new SimulatedCameraSource(store, new byte[] { 1, 2, 3, 4 });
        var machine = new JobStateMachine();

        machine.Transition(JobState.Imported);
        var captured = await camera.CaptureAsync(new CaptureRequest(SimulatedCameraSource.Kind), CancellationToken.None);
        machine.Transition(JobState.Normalized);
        machine.Transition(JobState.DataLaneConfirmed);
        machine.Transition(JobState.OutboundPayloadPreviewed);

        // The teacher cancels mid-flight; purge follows exactly as it would after completion.
        machine.Transition(JobState.Cancelled);
        store.PurgeAll();
        machine.Transition(JobState.TransientSourcesPurged);

        Assert.Equal(0, store.Count);
        Assert.False(store.TryGet(captured.Bytes, out _));
        Assert.True(JobStateMachine.IsTerminal(machine.State));
    }

    [Fact]
    public async Task Normalization_respects_a_cancelled_token_before_touching_bytes()
    {
        var store = new InMemorySessionByteStore();
        var reference = store.Put(new byte[] { 1, 2, 3 });
        var envelope = new SourceEnvelope("file-import", "image/png", 1, DataLane.Amber, false, string.Empty, reference);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ImageNormalizer(store).NormalizeAsync(envelope, new NormalizationRequest(), cancelled.Token));
    }

    [Fact]
    public void An_incomplete_purge_is_explicit_and_the_retry_reaches_the_terminal_state()
    {
        var machine = new JobStateMachine();
        foreach (var state in new[]
        {
            JobState.Imported, JobState.Normalized, JobState.DataLaneConfirmed,
            JobState.DraftGenerated, JobState.SchemaValidated, JobState.InvariantsValidated,
            JobState.AwaitingTeacherReview, JobState.Approved, JobState.Rendered, JobState.Completed,
        })
        {
            machine.Transition(state);
        }

        machine.Transition(JobState.PurgeIncomplete);
        machine.Transition(JobState.TransientSourcesPurged);

        Assert.True(JobStateMachine.IsTerminal(machine.State));
    }
}
