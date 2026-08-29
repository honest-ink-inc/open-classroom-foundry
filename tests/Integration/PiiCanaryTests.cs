using System.Text;
using System.Text.Json;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Simulated;
using Xunit;

namespace Foundry.Tests.Integration;

/// <summary>
/// The synthetic PII-canary suite of plan §4/§7: a distinctive marker travels the
/// pipeline and must be findable nowhere after purge — not in the session store,
/// not in diagnostics, not in anything the virtual bench persisted.
/// </summary>
public class PiiCanaryTests
{
    private const string Canary = "CANARY-9Q4Z-ZEPHYRINE-QUILL-8842";

    [Fact]
    public async Task After_purge_the_canary_is_gone_from_the_session_and_absent_from_diagnostics()
    {
        var store = new InMemorySessionByteStore();
        var sink = new InMemoryDiagnosticsSink();
        var camera = new SimulatedCameraSource(store, Encoding.UTF8.GetBytes(Canary));

        var envelope = await camera.CaptureAsync(new CaptureRequest(SimulatedCameraSource.Kind), CancellationToken.None);
        sink.Record(new DiagnosticEvent("job.captured", "success", ModuleId: "all-aboard", MediaClass: "image"));
        sink.Record(new DiagnosticEvent("job.state-changed", "cancelled", FromState: JobState.DataLaneConfirmed, ToState: JobState.Cancelled));

        store.PurgeAll();
        sink.Record(new DiagnosticEvent("job.purged", "success"));

        Assert.Equal(0, store.Count);
        Assert.False(store.TryGet(envelope.Bytes, out _));

        var serializedDiagnostics = JsonSerializer.Serialize(sink.Events);
        Assert.DoesNotContain(Canary, serializedDiagnostics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ZEPHYRINE", serializedDiagnostics, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_canary_cannot_be_smuggled_through_any_diagnostic_field()
    {
        var sink = new InMemoryDiagnosticsSink();
        var sound = new DiagnosticEvent("job.state-changed", "success");

        Assert.Throws<InvalidOperationException>(() => sink.Record(sound with { EventCode = Canary }));
        Assert.Throws<InvalidOperationException>(() => sink.Record(sound with { RecipeId = Canary }));
        Assert.Throws<InvalidOperationException>(() => sink.Record(sound with { ModuleId = Canary }));
        Assert.Throws<InvalidOperationException>(() => sink.Record(sound with { MediaClass = Canary }));
        Assert.Empty(sink.Events);
    }

    [Fact]
    public async Task An_amber_artifact_carrying_the_canary_is_refused_persistence_by_the_bench()
    {
        var document = new ArtifactDocument([new Paragraph($"Response summary mentioning {Canary}.")]);
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Amber), "teacher@example.org", [],
            new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));

        var projectStore = new RecordingProjectStore();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => projectStore.SaveGreenProjectAsync(artifact, new ProjectSaveRequest("summary"), CancellationToken.None));
        Assert.Empty(projectStore.Saves);
    }
}
