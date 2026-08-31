using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Simulated;
using System.Security.Cryptography;

namespace Foundry.Tests.Unit;

public class SessionByteStoreTests
{
    [Fact]
    public void Bytes_round_trip_through_an_opaque_reference()
    {
        var store = new InMemorySessionByteStore();
        var reference = store.Put(new byte[] { 1, 2, 3 });

        Assert.True(store.TryGet(reference, out var content));
        Assert.Equal(new byte[] { 1, 2, 3 }, content.ToArray());
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Empty_content_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new InMemorySessionByteStore().Put(ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public void Release_removes_a_single_reference()
    {
        var store = new InMemorySessionByteStore();
        var kept = store.Put(new byte[] { 1 });
        var released = store.Put(new byte[] { 2 });

        store.Release(released);

        Assert.False(store.TryGet(released, out _));
        Assert.True(store.TryGet(kept, out _));
    }

    [Fact]
    public void Purge_empties_the_session()
    {
        var store = new InMemorySessionByteStore();
        store.Put(new byte[] { 1 });
        store.Put(new byte[] { 2 });

        store.PurgeAll();

        Assert.Equal(0, store.Count);
    }
}

public class CaptureSourceTests
{
    [Fact]
    public async Task An_import_lands_in_the_amber_lane_with_no_path_anywhere()
    {
        var store = new InMemorySessionByteStore();
        var source = new ByteImportCaptureSource(store);

        var envelope = await source.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", "\t\t\t"u8.ToArray()),
            CancellationToken.None);

        Assert.Equal(DataLane.Amber, envelope.Lane);
        Assert.Equal("file-import", envelope.SourceKind);
        Assert.False(envelope.MetadataStripped);
        Assert.True(store.TryGet(envelope.Bytes, out _));
    }

    [Fact]
    public async Task Unsupported_types_and_empty_imports_are_refused()
    {
        var source = new ByteImportCaptureSource(new InMemorySessionByteStore());

        await Assert.ThrowsAsync<ArgumentException>(
            () => source.CaptureAsync(new CaptureRequest("file-import", "application/pdf", new byte[] { 1 }), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(
            () => source.CaptureAsync(new CaptureRequest("file-import", "image/png"), CancellationToken.None));
    }

    [Fact]
    public async Task The_camera_simulator_serves_its_configured_frame()
    {
        var store = new InMemorySessionByteStore();
        var camera = new SimulatedCameraSource(store, new byte[] { 7, 7 });

        var envelope = await camera.CaptureAsync(new CaptureRequest(SimulatedCameraSource.Kind), CancellationToken.None);

        Assert.Equal("camera-simulator", envelope.SourceKind);
        Assert.Equal(DataLane.Amber, envelope.Lane);
        Assert.True(store.TryGet(envelope.Bytes, out var frame));
        Assert.Equal(new byte[] { 7, 7 }, frame.ToArray());
    }
}

public class CaptureSessionTests
{
    [Fact]
    public async Task Authoritative_envelope_copy_is_detached_and_carries_its_identity_and_metadata()
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new RecordingNormalizer(),
            store);
        var captured = await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 10, 20, 30 }),
            CancellationToken.None);

        Assert.True(session.TryCopyAuthoritativeEnvelope(3, out var first));
        Assert.NotNull(first);
        try
        {
            Assert.Same(captured, first.Envelope);
            Assert.Equal(captured.Bytes, first.Envelope.Bytes);
            Assert.Equal(captured.SourceKind, first.Envelope.SourceKind);
            Assert.Equal(captured.MimeType, first.Envelope.MimeType);
            Assert.Equal(captured.PageCount, first.Envelope.PageCount);
            Assert.Equal(captured.Lane, first.Envelope.Lane);
            Assert.Equal(captured.MetadataStripped, first.Envelope.MetadataStripped);
            Assert.Equal(captured.TeacherStatedRights, first.Envelope.TeacherStatedRights);
            Assert.Equal(new byte[] { 10, 20, 30 }, first.Content);

            first.Content[0] = 99;
            Assert.True(session.TryCopyAuthoritativeEnvelope(3, out var second));
            Assert.NotNull(second);
            try
            {
                Assert.NotSame(first.Content, second.Content);
                Assert.Equal(new byte[] { 10, 20, 30 }, second.Content);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(second.Content);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(first.Content);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Authoritative_envelope_copy_refuses_nonpositive_or_undersized_bounds(int maximumByteCount)
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new RecordingNormalizer(),
            store);
        await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);

        Assert.False(session.TryCopyAuthoritativeEnvelope(maximumByteCount, out var copy));
        Assert.Null(copy);
    }

    [Fact]
    public async Task Authoritative_envelope_copy_refuses_an_in_flight_operation()
    {
        var store = new InMemorySessionByteStore();
        var normalizer = new DeferredRotationNormalizer();
        var session = new CaptureSession(new ByteImportCaptureSource(store), normalizer, store);
        await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);
        await session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);

        var rotation = session.NormalizeAsync(
            new NormalizationRequest(RotationDegrees.Rotate90),
            CancellationToken.None);
        await normalizer.RotationStarted;

        Assert.False(session.TryCopyAuthoritativeEnvelope(3, out var copy));
        Assert.Null(copy);

        normalizer.AllowRotation();
        await rotation;
    }

    [Fact]
    public async Task Authoritative_envelope_copy_refuses_missing_and_post_purge_bytes()
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new RecordingNormalizer(),
            store);
        var captured = await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);

        store.Release(captured.Bytes);
        Assert.False(session.TryCopyAuthoritativeEnvelope(3, out var missing));
        Assert.Null(missing);

        Assert.True(session.Cancel());
        Assert.Equal(JobState.TransientSourcesPurged, session.Machine.State);
        Assert.False(session.TryCopyAuthoritativeEnvelope(3, out var purged));
        Assert.Null(purged);
    }

    [Fact]
    public async Task Current_expected_reference_publishes_exactly_once()
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new StoreBackedGenerationNormalizer(store),
            store);
        await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);
        var normalized = await session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);
        var publicationCount = 0;

        var published = session.TryPublishForAuthoritativeEnvelope(
            normalized.Bytes,
            () => publicationCount++);

        Assert.True(published);
        Assert.Equal(1, publicationCount);
    }

    [Fact]
    public async Task Stale_expected_reference_refuses_publication_without_invoking_the_callback()
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new StoreBackedGenerationNormalizer(store),
            store);
        await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);
        var firstGeneration = await session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);
        await session.NormalizeAsync(
            new NormalizationRequest(RotationDegrees.Rotate90),
            CancellationToken.None);
        var publicationCount = 0;

        var published = session.TryPublishForAuthoritativeEnvelope(
            firstGeneration.Bytes,
            () => publicationCount++);

        Assert.False(published);
        Assert.Equal(0, publicationCount);
    }

    [Fact]
    public async Task Current_expected_reference_acquires_normalization_against_that_generation()
    {
        var store = new InMemorySessionByteStore();
        var normalizer = new StoreBackedGenerationNormalizer(store);
        var session = new CaptureSession(new ByteImportCaptureSource(store), normalizer, store);
        await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);
        var firstGeneration = await session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);

        var secondGeneration = await session.NormalizeAsync(
            new NormalizationRequest(RotationDegrees.Rotate90),
            firstGeneration.Bytes,
            CancellationToken.None);

        Assert.Equal(2, normalizer.CallCount);
        Assert.NotEqual(firstGeneration.Bytes, secondGeneration.Bytes);
        Assert.Same(secondGeneration, session.Envelope);
        Assert.Equal(JobState.Normalized, session.Machine.State);
    }

    [Fact]
    public async Task Stale_expected_reference_refuses_normalization_before_calling_the_normalizer()
    {
        var store = new InMemorySessionByteStore();
        var normalizer = new StoreBackedGenerationNormalizer(store);
        var session = new CaptureSession(new ByteImportCaptureSource(store), normalizer, store);
        await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);
        var firstGeneration = await session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);
        var currentGeneration = await session.NormalizeAsync(
            new NormalizationRequest(RotationDegrees.Rotate90),
            CancellationToken.None);

        await Assert.ThrowsAsync<CaptureGenerationChangedException>(
            () => session.NormalizeAsync(
                new NormalizationRequest(Crop: new CropRectangle(0, 0, 1, 1)),
                firstGeneration.Bytes,
                CancellationToken.None));

        Assert.Equal(2, normalizer.CallCount);
        Assert.Same(currentGeneration, session.Envelope);
        Assert.Equal(JobState.Normalized, session.Machine.State);
    }

    [Fact]
    public async Task Stale_expected_confirmation_throws_and_leaves_the_session_normalized()
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new StoreBackedGenerationNormalizer(store),
            store);
        await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);
        var firstGeneration = await session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);
        var currentGeneration = await session.NormalizeAsync(
            new NormalizationRequest(RotationDegrees.Rotate90),
            CancellationToken.None);

        Assert.Throws<CaptureGenerationChangedException>(
            () => session.ConfirmLane(DataLane.Green, firstGeneration.Bytes));

        Assert.Equal(JobState.Normalized, session.Machine.State);
        Assert.Same(currentGeneration, session.Envelope);
        Assert.Equal(DataLane.Amber, currentGeneration.Lane);
    }

    [Fact]
    public async Task Current_expected_confirmation_succeeds()
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new StoreBackedGenerationNormalizer(store),
            store);
        await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);
        var normalized = await session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);

        var confirmed = session.ConfirmLane(DataLane.Green, normalized.Bytes);

        Assert.Equal(JobState.DataLaneConfirmed, session.Machine.State);
        Assert.Equal(normalized.Bytes, confirmed.Bytes);
        Assert.Equal(DataLane.Green, confirmed.Lane);
        Assert.Same(confirmed, session.Envelope);
    }

    [Fact]
    public async Task A_failed_capture_does_not_advance_state_and_can_be_retried()
    {
        var store = new InMemorySessionByteStore();
        var source = new FailsOnceCaptureSource();
        var session = new CaptureSession(source, new RecordingNormalizer(), store);
        var request = new CaptureRequest("synthetic-fails-once", "image/png", new byte[] { 1 });

        await Assert.ThrowsAsync<IOException>(() => session.CaptureAsync(request, CancellationToken.None));
        Assert.Equal(JobState.New, session.Machine.State);
        Assert.Null(session.Envelope);

        var captured = await session.CaptureAsync(request, CancellationToken.None);

        Assert.Equal(JobState.Imported, session.Machine.State);
        Assert.Equal(DataLane.Amber, captured.Lane);
    }

    [Fact]
    public async Task A_capture_source_that_allocates_before_failure_is_purged_and_zeroed()
    {
        var store = new InMemorySessionByteStore();
        var source = new AllocatesThenFailsCaptureSource(store);
        var session = new CaptureSession(source, new RecordingNormalizer(), store);

        await Assert.ThrowsAsync<IOException>(() => session.CaptureAsync(
            new CaptureRequest("synthetic-failure", "image/png", new byte[] { 1 }),
            CancellationToken.None));

        Assert.Equal(JobState.New, session.Machine.State);
        Assert.Null(session.Envelope);
        Assert.Equal(0, store.Count);
        Assert.All(source.HeldBytes.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task A_normalized_capture_can_be_normalized_again_for_rotation()
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new RecordingNormalizer(),
            store);

        await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1 }),
            CancellationToken.None);
        await session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);

        var rotated = await session.NormalizeAsync(
            new NormalizationRequest(RotationDegrees.Rotate90),
            CancellationToken.None);

        Assert.Equal(JobState.Normalized, session.Machine.State);
        Assert.Equal("rotation:90", rotated.TeacherStatedRights);
    }

    [Fact]
    public async Task A_failed_initial_normalization_retains_the_exact_capture_and_can_be_retried()
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new FailsOnceNormalizer(),
            store);

        var captured = await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(
            () => session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None));
        Assert.Equal(JobState.Imported, session.Machine.State);
        Assert.Same(captured, session.Envelope);

        var normalized = await session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);

        Assert.Equal(JobState.Normalized, session.Machine.State);
        Assert.Equal("normalized-after-retry", normalized.TeacherStatedRights);
    }

    [Fact]
    public async Task A_normalizer_that_allocates_before_failure_ends_and_purges_the_session()
    {
        var store = new InMemorySessionByteStore();
        var normalizer = new AllocatesThenFailsNormalizer(store);
        var session = new CaptureSession(new ByteImportCaptureSource(store), normalizer, store);
        var captured = await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);
        Assert.True(store.TryGet(captured.Bytes, out var heldSource));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None));

        Assert.Equal(JobState.TransientSourcesPurged, session.Machine.State);
        Assert.Null(session.Envelope);
        Assert.Equal(0, store.Count);
        Assert.All(heldSource.ToArray(), value => Assert.Equal(0, value));
        Assert.All(normalizer.HeldOutput.ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public async Task A_cancelled_capture_cannot_commit_a_late_result_from_a_source_that_ignores_the_token()
    {
        var store = new InMemorySessionByteStore();
        var source = new DeferredCaptureSource();
        var session = new CaptureSession(source, new RecordingNormalizer(), store);
        using var cancellation = new CancellationTokenSource();

        var pending = session.CaptureAsync(
            new CaptureRequest("synthetic-deferred", "image/png", new byte[] { 1 }),
            cancellation.Token);
        cancellation.Cancel();
        source.Complete();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(JobState.New, session.Machine.State);
        Assert.Null(session.Envelope);
    }

    [Fact]
    public async Task A_cancelled_normalization_cannot_replace_the_capture_when_the_normalizer_ignores_the_token()
    {
        var store = new InMemorySessionByteStore();
        var normalizer = new DeferredNormalizer();
        var session = new CaptureSession(new ByteImportCaptureSource(store), normalizer, store);
        var captured = await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);
        using var cancellation = new CancellationTokenSource();

        var pending = session.NormalizeAsync(new NormalizationRequest(), cancellation.Token);
        cancellation.Cancel();
        normalizer.Complete(captured);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(JobState.Imported, session.Machine.State);
        Assert.Same(captured, session.Envelope);
    }

    [Fact]
    public async Task Cancellation_purges_and_zeroes_the_owned_store_before_becoming_terminal()
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new RecordingNormalizer(),
            store);
        var captured = await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);
        Assert.True(store.TryGet(captured.Bytes, out var heldBytes));

        Assert.True(session.Cancel());

        Assert.Equal(0, store.Count);
        Assert.All(heldBytes.ToArray(), value => Assert.Equal(0, value));
        Assert.Null(session.Envelope);
        Assert.Equal(JobState.TransientSourcesPurged, session.Machine.State);
    }

    [Fact]
    public async Task Standalone_capture_completion_purges_and_zeroes_before_reporting_success()
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new RecordingNormalizer(),
            store);
        var captured = await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 4, 5, 6 }),
            CancellationToken.None);
        Assert.True(store.TryGet(captured.Bytes, out var heldBytes));
        await session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);
        session.ConfirmLane(DataLane.Amber);

        Assert.True(session.CompleteCapture());

        Assert.Equal(0, store.Count);
        Assert.All(heldBytes.ToArray(), value => Assert.Equal(0, value));
        Assert.Null(session.Envelope);
        Assert.Equal(JobState.TransientSourcesPurged, session.Machine.State);
    }

    [Fact]
    public async Task Gate_C_purges_and_zeroes_before_becoming_terminal()
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new RecordingNormalizer(),
            store);
        var captured = await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 7, 8, 9 }),
            CancellationToken.None);
        Assert.True(store.TryGet(captured.Bytes, out var heldBytes));

        var pause = session.InvokeSafetyPause(DistrictPolicy.Offline);

        Assert.Contains("school's", pause.ProcedureText, StringComparison.Ordinal);
        Assert.Equal(0, store.Count);
        Assert.All(heldBytes.ToArray(), value => Assert.Equal(0, value));
        Assert.Null(session.Envelope);
        Assert.Equal(JobState.TransientSourcesPurged, session.Machine.State);
    }

    [Fact]
    public async Task A_failed_purge_is_explicit_and_a_real_retry_reaches_terminal()
    {
        var store = new FailsFirstPurgeStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new RecordingNormalizer(),
            store);
        await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);

        Assert.True(session.Cancel());
        Assert.Equal(JobState.PurgeIncomplete, session.Machine.State);
        Assert.Equal(1, store.Count);

        Assert.True(session.PurgeTransientSources());
        Assert.Equal(0, store.Count);
        Assert.Equal(JobState.TransientSourcesPurged, session.Machine.State);
    }

    [Fact]
    public async Task Cancellation_waits_for_and_purges_a_late_capture_result()
    {
        var store = new InMemorySessionByteStore();
        var source = new StoreBackedDeferredCaptureSource(store);
        var session = new CaptureSession(source, new RecordingNormalizer(), store);
        var pending = session.CaptureAsync(
            new CaptureRequest("synthetic-deferred", "image/png", new byte[] { 1 }),
            CancellationToken.None);

        Assert.True(session.Cancel());
        Assert.Equal(JobState.Cancelled, session.Machine.State);
        source.Complete();

        await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.Equal(0, store.Count);
        Assert.All(source.HeldBytes.ToArray(), value => Assert.Equal(0, value));
        Assert.Equal(JobState.TransientSourcesPurged, session.Machine.State);
    }

    [Fact]
    public async Task Cancellation_after_a_normalizer_allocates_its_result_purges_the_late_output()
    {
        var store = new InMemorySessionByteStore();
        var normalizer = new StoreBackedDeferredNormalizer(store);
        var session = new CaptureSession(new ByteImportCaptureSource(store), normalizer, store);
        await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);

        var pending = session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);
        await normalizer.OutputStored;
        Assert.True(session.Cancel());
        Assert.Equal(JobState.Cancelled, session.Machine.State);
        normalizer.AllowReturn();

        await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.Equal(0, store.Count);
        Assert.All(normalizer.HeldOutput.ToArray(), value => Assert.Equal(0, value));
        Assert.Equal(JobState.TransientSourcesPurged, session.Machine.State);
    }

    [Fact]
    public async Task Lane_confirmation_draft_creation_and_completion_reject_an_in_flight_rotation()
    {
        var store = new InMemorySessionByteStore();
        var normalizer = new DeferredRotationNormalizer();
        var session = new CaptureSession(new ByteImportCaptureSource(store), normalizer, store);
        await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);
        await session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None);

        var rotation = session.NormalizeAsync(
            new NormalizationRequest(RotationDegrees.Rotate90),
            CancellationToken.None);
        await normalizer.RotationStarted;

        Assert.Throws<InvalidOperationException>(() => session.ConfirmLane(DataLane.Green));
        session.Machine.Transition(JobState.DataLaneConfirmed);
        Assert.Throws<InvalidOperationException>(() => session.CreateDraft(
            new ArtifactDocument([new Paragraph("Synthetic fixture")])));
        Assert.Throws<InvalidOperationException>(() => session.CompleteCapture());

        normalizer.AllowRotation();
        await Assert.ThrowsAsync<InvalidOperationException>(() => rotation);
        Assert.True(session.Cancel());
        Assert.Equal(JobState.TransientSourcesPurged, session.Machine.State);
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task A_failed_superseded_reference_release_purges_both_generations_terminally()
    {
        var store = new FailsFirstReleaseStore();
        var normalizer = new StoreBackedReplacingNormalizer(store);
        var session = new CaptureSession(new ByteImportCaptureSource(store), normalizer, store);
        var captured = await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);
        Assert.True(store.TryGet(captured.Bytes, out var heldSource));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None));

        Assert.Equal(JobState.TransientSourcesPurged, session.Machine.State);
        Assert.Null(session.Envelope);
        Assert.Equal(0, store.Count);
        Assert.All(heldSource.ToArray(), value => Assert.Equal(0, value));
        Assert.All(normalizer.HeldOutput.ToArray(), value => Assert.Equal(0, value));
    }

    private sealed class RecordingNormalizer : IDocumentNormalizer
    {
        public Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(source with { TeacherStatedRights = $"rotation:{(int)request.Rotation}" });
        }
    }

    private sealed class StoreBackedGenerationNormalizer(InMemorySessionByteStore store) : IDocumentNormalizer
    {
        private byte _generation;

        public int CallCount { get; private set; }

        public Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            var reference = store.Put(new[] { ++_generation });
            return Task.FromResult(source with
            {
                MetadataStripped = true,
                TeacherStatedRights = $"generation:{_generation}",
                Bytes = reference,
            });
        }
    }

    private sealed class FailsOnceNormalizer : IDocumentNormalizer
    {
        private bool _failed;

        public Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_failed)
            {
                _failed = true;
                throw new ArgumentException("Synthetic corrupt image.", nameof(source));
            }

            return Task.FromResult(source with { TeacherStatedRights = "normalized-after-retry" });
        }
    }

    private sealed class AllocatesThenFailsNormalizer(InMemorySessionByteStore store) : IDocumentNormalizer
    {
        public ReadOnlyMemory<byte> HeldOutput { get; private set; }

        public Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
        {
            var reference = store.Put("dnx"u8.ToArray());
            store.TryGet(reference, out var heldOutput);
            HeldOutput = heldOutput;
            throw new InvalidDataException("Synthetic post-allocation normalization failure.");
        }
    }

    private sealed class FailsOnceCaptureSource : ICaptureSource
    {
        private bool _failed;

        public Task<SourceEnvelope> CaptureAsync(CaptureRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_failed)
            {
                _failed = true;
                throw new IOException("Synthetic capture failure.");
            }

            return Task.FromResult(new SourceEnvelope(
                SourceKind: "synthetic-fails-once",
                MimeType: request.MimeType,
                PageCount: 1,
                Lane: DataLane.Amber,
                MetadataStripped: false,
                TeacherStatedRights: "synthetic test fixture",
                Bytes: SessionByteReference.NewReference()));
        }
    }

    private sealed class DeferredCaptureSource : ICaptureSource
    {
        private readonly TaskCompletionSource<SourceEnvelope> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<SourceEnvelope> CaptureAsync(
            CaptureRequest request,
            CancellationToken cancellationToken)
            => _completion.Task;

        public void Complete()
            => _completion.SetResult(new SourceEnvelope(
                SourceKind: "synthetic-deferred",
                MimeType: "image/png",
                PageCount: 1,
                Lane: DataLane.Amber,
                MetadataStripped: false,
                TeacherStatedRights: "synthetic test fixture",
                Bytes: SessionByteReference.NewReference()));
    }

    private sealed class DeferredNormalizer : IDocumentNormalizer
    {
        private readonly TaskCompletionSource<SourceEnvelope> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
            => _completion.Task;

        public void Complete(SourceEnvelope source)
            => _completion.SetResult(source with
            {
                MetadataStripped = true,
                TeacherStatedRights = "late ignored-token normalization",
            });
    }

    private sealed class AllocatesThenFailsCaptureSource(InMemorySessionByteStore store) : ICaptureSource
    {
        public ReadOnlyMemory<byte> HeldBytes { get; private set; }

        public Task<SourceEnvelope> CaptureAsync(CaptureRequest request, CancellationToken cancellationToken)
        {
            var reference = store.Put("FPZ"u8.ToArray());
            store.TryGet(reference, out var heldBytes);
            HeldBytes = heldBytes;
            throw new IOException("Synthetic post-allocation capture failure.");
        }
    }

    private sealed class StoreBackedDeferredCaptureSource(InMemorySessionByteStore store) : ICaptureSource
    {
        private readonly TaskCompletionSource<SourceEnvelope> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public ReadOnlyMemory<byte> HeldBytes { get; private set; }

        public Task<SourceEnvelope> CaptureAsync(
            CaptureRequest request,
            CancellationToken cancellationToken)
            => _completion.Task;

        public void Complete()
        {
            var reference = store.Put(new byte[] { 10, 20, 30 });
            store.TryGet(reference, out var heldBytes);
            HeldBytes = heldBytes;
            _completion.SetResult(new SourceEnvelope(
                "synthetic-deferred",
                "image/png",
                1,
                DataLane.Amber,
                false,
                string.Empty,
                reference));
        }
    }

    private sealed class StoreBackedDeferredNormalizer(InMemorySessionByteStore store) : IDocumentNormalizer
    {
        private readonly TaskCompletionSource _outputStored = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _allowReturn = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task OutputStored => _outputStored.Task;

        public ReadOnlyMemory<byte> HeldOutput { get; private set; }

        public async Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
        {
            var reference = store.Put("(2<"u8.ToArray());
            store.TryGet(reference, out var heldOutput);
            HeldOutput = heldOutput;
            _outputStored.SetResult();
            await _allowReturn.Task;
            return source with { MetadataStripped = true, Bytes = reference };
        }

        public void AllowReturn() => _allowReturn.SetResult();
    }

    private sealed class DeferredRotationNormalizer : IDocumentNormalizer
    {
        private readonly TaskCompletionSource _rotationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _allowRotation = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RotationStarted => _rotationStarted.Task;

        public async Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
        {
            if (request.Rotation == RotationDegrees.None)
            {
                return source with { MetadataStripped = true };
            }

            _rotationStarted.SetResult();
            await _allowRotation.Task;
            return source with { TeacherStatedRights = "late rotation" };
        }

        public void AllowRotation() => _allowRotation.SetResult();
    }

    private sealed class StoreBackedReplacingNormalizer(FailsFirstReleaseStore store) : IDocumentNormalizer
    {
        public ReadOnlyMemory<byte> HeldOutput { get; private set; }

        public Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
        {
            var reference = store.Put(new byte[] { 11, 22, 33 });
            store.TryGet(reference, out var heldOutput);
            HeldOutput = heldOutput;
            return Task.FromResult(source with { MetadataStripped = true, Bytes = reference });
        }
    }

    private sealed class FailsFirstPurgeStore : ISessionByteStore
    {
        private readonly InMemorySessionByteStore _inner = new();
        private bool _failed;

        public int Count => _inner.Count;

        public SessionByteReference Put(ReadOnlyMemory<byte> content) => _inner.Put(content);

        public bool TryGet(SessionByteReference reference, out ReadOnlyMemory<byte> content)
            => _inner.TryGet(reference, out content);

        public void Release(SessionByteReference reference) => _inner.Release(reference);

        public void PurgeAll()
        {
            if (!_failed)
            {
                _failed = true;
                throw new IOException("Synthetic purge failure.");
            }

            _inner.PurgeAll();
        }
    }

    private sealed class FailsFirstReleaseStore : ISessionByteStore
    {
        private readonly InMemorySessionByteStore _inner = new();
        private bool _failed;

        public int Count => _inner.Count;

        public SessionByteReference Put(ReadOnlyMemory<byte> content) => _inner.Put(content);

        public bool TryGet(SessionByteReference reference, out ReadOnlyMemory<byte> content)
            => _inner.TryGet(reference, out content);

        public void Release(SessionByteReference reference)
        {
            if (!_failed)
            {
                _failed = true;
                throw new IOException("Synthetic release failure.");
            }

            _inner.Release(reference);
        }

        public void PurgeAll() => _inner.PurgeAll();
    }
}

public class SimulatedSinkTests
{
    private static readonly DateTimeOffset SomeInstant = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);

    private static ApprovedArtifact Approved(DataLane lane)
        => ApprovalGate.Approve(
            DraftArtifact.New(new ArtifactDocument([new Paragraph("Ten-frame practice")]), lane),
            "teacher@example.org",
            [],
            SomeInstant);

    [Fact]
    public async Task The_virtual_printer_records_the_receipt_of_what_it_printed()
    {
        var printer = new VirtualPrintSink();
        var artifact = Approved(DataLane.Green);

        await printer.PrintAsync(artifact, new PrintRequest("virtual", Duplex: false, Copies: 2), CancellationToken.None);

        var job = Assert.Single(printer.Jobs);
        Assert.Equal(artifact.Receipt, job.Receipt);
        Assert.Equal(2, job.Request.Copies);
    }

    [Fact]
    public async Task The_recording_exporter_records_the_receipt()
    {
        var exporter = new RecordingExporter();

        await exporter.ExportAsync(Approved(DataLane.Green), new ExportRequest(RenderTarget.PrintPdf, "exports"), CancellationToken.None);

        Assert.Single(exporter.Exports);
    }

    [Fact]
    public async Task The_project_store_refuses_anything_above_the_green_lane()
    {
        var store = new RecordingProjectStore();

        await store.SaveGreenProjectAsync(Approved(DataLane.Green), new ProjectSaveRequest("library"), CancellationToken.None);
        Assert.Single(store.Saves);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.SaveGreenProjectAsync(Approved(DataLane.Amber), new ProjectSaveRequest("library"), CancellationToken.None));
        Assert.Single(store.Saves);
    }
}
