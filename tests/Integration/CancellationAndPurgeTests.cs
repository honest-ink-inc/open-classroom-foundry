using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;

namespace Foundry.Tests.Integration;

/// <summary>The Days 46–60 cancellation and purge path evidence (plan §14).</summary>
public class CancellationAndPurgeTests
{
    [Fact]
    public async Task A_cancelled_job_still_purges_every_session_byte()
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new PassThroughNormalizer(),
            store);
        var captured = await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3, 4 }),
            CancellationToken.None);
        Assert.True(store.TryGet(captured.Bytes, out var heldBytes));

        Assert.True(session.Cancel());

        Assert.Equal(0, store.Count);
        Assert.False(store.TryGet(captured.Bytes, out _));
        Assert.All(heldBytes.ToArray(), value => Assert.Equal(0, value));
        Assert.True(JobStateMachine.IsTerminal(session.Machine.State));
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
    public async Task An_incomplete_purge_is_explicit_and_the_retry_reaches_the_terminal_state()
    {
        var store = new FailsFirstPurgeStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new PassThroughNormalizer(),
            store);
        await session.CaptureAsync(
            new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", new byte[] { 1, 2, 3 }),
            CancellationToken.None);

        Assert.True(session.Cancel());
        Assert.Equal(JobState.PurgeIncomplete, session.Machine.State);
        Assert.Equal(1, store.Count);

        Assert.True(session.PurgeTransientSources());
        Assert.Equal(0, store.Count);
        Assert.True(JobStateMachine.IsTerminal(session.Machine.State));
    }

    private sealed class PassThroughNormalizer : IDocumentNormalizer
    {
        public Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(source);
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
}
