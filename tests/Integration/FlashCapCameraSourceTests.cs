// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using FlashCap;
using FlashCap.Utilities;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Infrastructure.Windows;

namespace Foundry.Tests.Integration;

[Collection(BoundedNativeLifecycleTestGroup.Name)]
public class FlashCapCameraSourceTests
{
    [Fact]
    public async Task A_captured_frame_is_copied_into_the_session_store_and_the_acquisition_buffer_is_zeroed()
    {
        var frame = new byte[] { 0xFF, 0xD8, 1, 2, 3, 0xFF, 0xD9 };
        var expected = frame.ToArray();
        var store = new InMemorySessionByteStore();
        var source = new FlashCapCameraSource(store, new StubFrameProvider(frame));

        var envelope = await source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            CancellationToken.None);

        Assert.Equal(FlashCapCameraSource.Kind, envelope.SourceKind);
        Assert.Equal("image/jpeg", envelope.MimeType);
        Assert.Equal(DataLane.Amber, envelope.Lane);
        Assert.False(envelope.MetadataStripped);
        Assert.True(store.TryGet(envelope.Bytes, out var stored));
        Assert.Equal(expected, stored.ToArray());
        Assert.True(Array.TrueForAll(frame, static value => value == 0));
    }

    [Fact]
    public async Task An_oversized_frame_is_refused_and_zeroed_before_the_store_is_called()
    {
        var frame = new byte[FlashCapCameraSource.MaxCapturedFrameBytes + 1];
        Array.Fill(frame, (byte)0xA5);
        var store = new RecordingStore();
        var source = new FlashCapCameraSource(store, new StubFrameProvider(frame));

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            CancellationToken.None));

        Assert.Contains("bounded", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, store.PutCalls);
        Assert.True(Array.TrueForAll(frame, static value => value == 0));
    }

    [Fact]
    public async Task A_store_failure_still_zeroes_the_acquisition_buffer()
    {
        var frame = new byte[] { 9, 8, 7, 6 };
        var store = new RecordingStore(new IOException("synthetic store failure"));
        var source = new FlashCapCameraSource(store, new StubFrameProvider(frame));

        var exception = await Assert.ThrowsAsync<IOException>(() => source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            CancellationToken.None));

        Assert.Equal("synthetic store failure", exception.Message);
        Assert.Equal(1, store.PutCalls);
        Assert.True(Array.TrueForAll(frame, static value => value == 0));
    }

    [Fact]
    public async Task Cancellation_after_acquisition_refuses_storage_and_zeroes_the_frame()
    {
        var frame = new byte[] { 4, 3, 2, 1 };
        using var cancellation = new CancellationTokenSource();
        var store = new RecordingStore();
        var provider = new StubFrameProvider(frame, cancellation.Cancel);
        var source = new FlashCapCameraSource(store, provider);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            cancellation.Token));

        Assert.Equal(1, provider.CaptureCalls);
        Assert.Equal(0, store.PutCalls);
        Assert.True(Array.TrueForAll(frame, static value => value == 0));
    }

    [Fact]
    public async Task Cancellation_during_store_releases_the_tentative_copy_and_zeroes_the_frame()
    {
        var frame = new byte[] { 7, 6, 5, 4 };
        using var cancellation = new CancellationTokenSource();
        var store = new CancellingStore(cancellation);
        var source = new FlashCapCameraSource(store, new StubFrameProvider(frame));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            cancellation.Token));

        Assert.Equal(1, store.PutCalls);
        Assert.Equal(1, store.ReleaseCalls);
        Assert.Equal(0, store.Count);
        Assert.True(Array.TrueForAll(store.LastStoredBuffer!, static value => value == 0));
        Assert.True(Array.TrueForAll(frame, static value => value == 0));
    }

    [Fact]
    public async Task Cancellation_before_any_frame_settles_and_stops_then_disposes_the_device()
    {
        using var cancellation = new CancellationTokenSource();
        var device = new FakeCaptureDevice();
        var source = new FlashCapCameraSource(
            new RecordingStore(),
            new FlashCapFrameProvider(device));

        var capture = source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            cancellation.Token);

        await device.Started.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            _ = await capture.WaitAsync(TimeSpan.FromSeconds(5));
        });

        Assert.Equal(["Open", "Start", "Stop", "Dispose"], device.Events);
        Assert.False(device.StopObservedCancelledToken);
    }

    [Fact]
    public async Task Cancellation_settles_when_open_ignores_its_token_and_never_completes()
    {
        using var cancellation = new CancellationTokenSource();
        var device = new DelayedOpenCaptureDevice();
        var source = new FlashCapCameraSource(
            new RecordingStore(),
            new FlashCapFrameProvider(device));

        var capture = source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            cancellation.Token);

        await device.OpenCalled.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            _ = await capture.WaitAsync(TimeSpan.FromSeconds(5));
        });

        Assert.Equal(["Open"], device.Events);
    }

    [Fact]
    public async Task A_late_successful_open_is_observed_then_stopped_and_disposed()
    {
        using var cancellation = new CancellationTokenSource();
        var device = new DelayedOpenCaptureDevice();
        var source = new FlashCapCameraSource(
            new RecordingStore(),
            new FlashCapFrameProvider(device));

        var capture = source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            cancellation.Token);

        await device.OpenCalled.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            _ = await capture.WaitAsync(TimeSpan.FromSeconds(5));
        });

        device.CompleteOpen();
        await device.Disposed.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["Open", "Stop", "Dispose"], device.Events);
    }

    [Fact]
    public async Task Cancellation_settles_when_start_ignores_its_token_and_completed_shutdown_is_observed()
    {
        using var cancellation = new CancellationTokenSource();
        var device = new FakeCaptureDevice(startNeverCompletes: true);
        var source = new FlashCapCameraSource(
            new RecordingStore(),
            new FlashCapFrameProvider(device));

        var capture = source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            cancellation.Token);

        await device.Started.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            _ = await capture.WaitAsync(TimeSpan.FromSeconds(5));
        });

        Assert.Equal(["Open", "Start", "Stop", "Dispose"], device.Events);
        Assert.False(device.StopObservedCancelledToken);
    }

    [Fact]
    public async Task A_late_successful_start_is_stopped_and_disposed_again_after_immediate_cleanup()
    {
        using var cancellation = new CancellationTokenSource();
        var device = new LateCompletingStartCaptureDevice();
        var source = new FlashCapCameraSource(
            new RecordingStore(),
            new FlashCapFrameProvider(device));

        var capture = source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            cancellation.Token);

        await device.StartCalled.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            _ = await capture.WaitAsync(TimeSpan.FromSeconds(5));
        });

        Assert.Equal(["Open", "Start", "Stop", "Dispose"], device.Events);

        device.CompleteStartAfterCleanup();
        await device.SecondDispose.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(device.IsActive);
        Assert.Equal(
            ["Open", "Start", "Stop", "Dispose", "StartCompleted", "Stop", "Dispose"],
            device.Events);
    }

    [Fact]
    public async Task A_shared_lifecycle_lock_can_prevent_confirmed_shutdown_but_capture_still_settles_bounded()
    {
        using var cancellation = new CancellationTokenSource();
        var device = new SerializedHungCaptureDevice();
        var source = new FlashCapCameraSource(
            new RecordingStore(),
            new FlashCapFrameProvider(device, TimeSpan.FromMilliseconds(50)));

        var capture = source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            cancellation.Token);

        await device.StartHoldingLifecycleLock.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                _ = await capture.WaitAsync(TimeSpan.FromSeconds(5));
            });

            Assert.Contains("could not confirm", exception.Message, StringComparison.Ordinal);
            Assert.Contains("StopAttempt", device.Events);
            Assert.Contains("DisposeAttempt", device.Events);
            Assert.DoesNotContain("StopCompleted", device.Events);
            Assert.DoesNotContain("DisposeCompleted", device.Events);
        }
        finally
        {
            // Release the synthetic driver only after proving that the caller
            // settled without claiming device closure. This drains detached
            // observers so the test itself leaves no unfinished lifecycle work.
            device.ReleaseHungStart();
            await device.CleanupDrained.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task A_frame_that_loses_the_one_shot_race_is_zeroed()
    {
        var winningFrame = new byte[] { 1, 2, 3, 4 };
        var losingFrame = new byte[] { 5, 6, 7, 8 };
        var expected = winningFrame.ToArray();
        var device = new FakeCaptureDevice(
            frames: [winningFrame, losingFrame],
            mimeType: "image/png");
        var store = new InMemorySessionByteStore();
        var source = new FlashCapCameraSource(store, new FlashCapFrameProvider(device));

        var envelope = await source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            CancellationToken.None);

        Assert.Equal("image/png", envelope.MimeType);
        Assert.True(store.TryGet(envelope.Bytes, out var stored));
        Assert.Equal(expected, stored.ToArray());
        Assert.True(Array.TrueForAll(winningFrame, static value => value == 0));
        Assert.True(Array.TrueForAll(losingFrame, static value => value == 0));
        Assert.Equal(["Open", "Start", "Stop", "Dispose"], device.Events);
    }

    [Fact]
    public void A_borrowed_pool_segment_is_zeroed_before_its_lease_is_released()
    {
        var pooled = new byte[] { 0xA0, 1, 2, 3, 0xB0 };
        byte[]? delivered = null;
        Exception? failure = null;
        var releaseObservedZero = false;

        FlashCapCaptureDeviceFactory.CopyAndDeliverBorrowedFrame(
            () => new ArraySegment<byte>(pooled, 1, 3),
            () => releaseObservedZero = pooled[1] == 0 && pooled[2] == 0 && pooled[3] == 0,
            frame => delivered = frame,
            exception => failure = exception);

        Assert.True(releaseObservedZero);
        Assert.Equal([0xA0, 0, 0, 0, 0xB0], pooled);
        Assert.Equal([1, 2, 3], delivered);
        Assert.Null(failure);
        Array.Clear(delivered!);
    }

    [Fact]
    public void A_borrowed_pool_segment_and_owned_copy_are_zeroed_when_delivery_fails()
    {
        var pooled = new byte[] { 9, 8, 7, 6 };
        byte[]? delivered = null;
        Exception? failure = null;
        var releaseObservedZero = false;

        FlashCapCaptureDeviceFactory.CopyAndDeliverBorrowedFrame(
            () => new ArraySegment<byte>(pooled),
            () => releaseObservedZero = Array.TrueForAll(pooled, static value => value == 0),
            frame =>
            {
                delivered = frame;
                throw new IOException("synthetic delivery failure");
            },
            exception => failure = exception);

        Assert.True(releaseObservedZero);
        Assert.True(Array.TrueForAll(pooled, static value => value == 0));
        Assert.NotNull(delivered);
        Assert.True(Array.TrueForAll(delivered, static value => value == 0));
        Assert.IsType<IOException>(failure);
    }

    [Fact]
    public async Task A_frame_delivered_before_start_fails_is_recovered_and_zeroed()
    {
        var frame = new byte[] { 3, 1, 4, 1 };
        var device = new FakeCaptureDevice(
            frames: [frame],
            startFailure: new IOException("synthetic start failure"));
        var store = new RecordingStore();
        var source = new FlashCapCameraSource(store, new FlashCapFrameProvider(device));

        var exception = await Assert.ThrowsAsync<IOException>(() => source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            CancellationToken.None));

        Assert.Equal("synthetic start failure", exception.Message);
        Assert.Equal(0, store.PutCalls);
        Assert.True(Array.TrueForAll(frame, static value => value == 0));
        Assert.Equal(["Open", "Start", "Stop", "Dispose"], device.Events);
    }

    [Fact]
    public async Task A_stop_failure_after_a_frame_zeroes_it_and_still_disposes_the_device()
    {
        var frame = new byte[] { 9, 7, 5, 3 };
        var device = new FakeCaptureDevice(
            frames: [frame],
            stopFailure: new IOException("synthetic stop failure"));
        var store = new RecordingStore();
        var source = new FlashCapCameraSource(store, new FlashCapFrameProvider(device));

        var exception = await Assert.ThrowsAsync<IOException>(() => source.CaptureAsync(
            new CaptureRequest(FlashCapCameraSource.Kind),
            CancellationToken.None));

        Assert.Equal("synthetic stop failure", exception.Message);
        Assert.Equal(0, store.PutCalls);
        Assert.True(Array.TrueForAll(frame, static value => value == 0));
        Assert.Equal(["Open", "Start", "Stop", "Dispose"], device.Events);
    }

    [Fact]
    public async Task A_stop_that_ignores_cancellation_is_bounded_disposes_and_zeroes_the_frame()
    {
        var frame = new byte[] { 8, 6, 4, 2 };
        var device = new FakeCaptureDevice(
            frames: [frame],
            stopNeverCompletes: true);
        var store = new RecordingStore();
        var source = new FlashCapCameraSource(
            store,
            new FlashCapFrameProvider(device, TimeSpan.FromMilliseconds(50)));

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            _ = await source.CaptureAsync(
                    new CaptureRequest(FlashCapCameraSource.Kind),
                    CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
        });

        Assert.Equal(0, store.PutCalls);
        Assert.True(Array.TrueForAll(frame, static value => value == 0));
        Assert.Equal(["Open", "Start", "Stop", "Dispose"], device.Events);
    }

    [Fact]
    public async Task A_dispose_that_never_completes_is_bounded_and_zeroes_the_frame()
    {
        var frame = new byte[] { 2, 4, 6, 8 };
        var device = new FakeCaptureDevice(
            frames: [frame],
            disposeNeverCompletes: true);
        var store = new RecordingStore();
        var source = new FlashCapCameraSource(
            store,
            new FlashCapFrameProvider(device, TimeSpan.FromMilliseconds(50)));

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            _ = await source.CaptureAsync(
                    new CaptureRequest(FlashCapCameraSource.Kind),
                    CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5));
        });

        Assert.Equal(0, store.PutCalls);
        Assert.True(Array.TrueForAll(frame, static value => value == 0));
        Assert.Equal(["Open", "Start", "Stop", "Dispose"], device.Events);
    }

    [Fact]
    public void Characteristic_selection_rejects_unknown_and_over_budget_modes_and_prefers_jpeg_then_png()
    {
        var bitmap = Characteristic(PixelFormats.RGB24, 640, 480);
        var png = Characteristic(PixelFormats.PNG, 800, 600);
        var jpeg = Characteristic(PixelFormats.JPEG, 320, 240);

        var selected = FlashCapCaptureDeviceFactory.SelectCharacteristic(
        [
            Characteristic(PixelFormats.Unknown, 640, 480),
            Characteristic(PixelFormats.JPEG, ImageNormalizer.MaxImageDimension + 1, 480),
            Characteristic(PixelFormats.JPEG, 6_000, 5_000),
            Characteristic(PixelFormats.RGB24, 3_840, 2_160),
            bitmap,
            png,
            jpeg,
        ]);

        Assert.Same(jpeg, selected);
        Assert.Same(
            png,
            FlashCapCaptureDeviceFactory.SelectCharacteristic([bitmap, png]));
    }

    [Fact]
    public void Characteristic_selection_rejects_raw_auto_modes_whose_bmp_cannot_fit_the_encoded_limit()
    {
        var smallerRawMode = Characteristic(PixelFormats.YUYV, 1_280, 720);

        var selected = FlashCapCaptureDeviceFactory.SelectCharacteristic(
        [
            Characteristic(PixelFormats.RGB24, 3_840, 2_160),
            smallerRawMode,
        ]);

        Assert.Same(smallerRawMode, selected);
        Assert.Equal("image/bmp", FlashCapCaptureDeviceFactory.MimeTypeForAuto(selected.PixelFormat));
    }

    [Fact]
    public void Characteristic_selection_refuses_a_camera_with_no_bounded_supported_mode()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            FlashCapCaptureDeviceFactory.SelectCharacteristic(
            [
                Characteristic(PixelFormats.Unknown, 640, 480),
                Characteristic(PixelFormats.PNG, 0, 480),
                Characteristic(PixelFormats.JPEG, 6_000, 5_000),
            ]));

        Assert.Contains("no supported capture characteristic", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PixelFormats.JPEG, "image/jpeg")]
    [InlineData(PixelFormats.PNG, "image/png")]
    [InlineData(PixelFormats.RGB24, "image/bmp")]
    [InlineData(PixelFormats.YUYV, "image/bmp")]
    public void Auto_transcoding_reports_the_mime_type_flashcap_actually_returns(
        PixelFormats pixelFormat,
        string expectedMimeType)
        => Assert.Equal(
            expectedMimeType,
            FlashCapCaptureDeviceFactory.MimeTypeForAuto(pixelFormat));

    private static VideoCharacteristics Characteristic(
        PixelFormats pixelFormat,
        int width,
        int height)
        => new(pixelFormat, width, height, Fraction.Create(30));

    private sealed class StubFrameProvider(
        byte[] frame,
        Action? beforeReturn = null,
        string mimeType = "image/jpeg") : IFlashCapFrameProvider
    {
        public int CaptureCalls { get; private set; }

        public Task<FlashCapCapturedFrame> CaptureFrameAsync(CancellationToken cancellationToken)
        {
            CaptureCalls++;
            beforeReturn?.Invoke();
            return Task.FromResult(new FlashCapCapturedFrame(frame, mimeType));
        }
    }

    private sealed class FakeCaptureDevice(
        IReadOnlyList<byte[]>? frames = null,
        string mimeType = "image/jpeg",
        Exception? startFailure = null,
        Exception? stopFailure = null,
        Exception? disposeFailure = null,
        bool startNeverCompletes = false,
        bool stopNeverCompletes = false,
        bool disposeNeverCompletes = false) : IFlashCapCaptureDeviceFactory, IFlashCapCaptureSession
    {
        private readonly List<string> _events = [];
        private readonly TaskCompletionSource _neverCompletes = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _started = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private Action<byte[]>? _frameArrived;

        public IReadOnlyList<string> Events => _events;

        public Task Started => _started.Task;

        public bool StopObservedCancelledToken { get; private set; }

        public Task<FlashCapOpenedDevice> OpenAsync(
            Action<byte[]> frameArrived,
            Action<Exception> frameFailed,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _events.Add("Open");
            _frameArrived = frameArrived;
            return Task.FromResult(new FlashCapOpenedDevice(this, mimeType));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _events.Add("Start");
            _started.TrySetResult();

            if (startNeverCompletes)
            {
                return _neverCompletes.Task;
            }

            foreach (var frame in frames ?? [])
            {
                _frameArrived!(frame);
            }

            return startFailure is null
                ? Task.CompletedTask
                : Task.FromException(startFailure);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _events.Add("Stop");
            StopObservedCancelledToken = cancellationToken.IsCancellationRequested;
            if (stopNeverCompletes)
            {
                return _neverCompletes.Task;
            }

            return stopFailure is null
                ? Task.CompletedTask
                : Task.FromException(stopFailure);
        }

        public ValueTask DisposeAsync()
        {
            _events.Add("Dispose");
            if (disposeNeverCompletes)
            {
                return new ValueTask(_neverCompletes.Task);
            }

            if (disposeFailure is not null)
            {
                throw disposeFailure;
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class DelayedOpenCaptureDevice : IFlashCapCaptureDeviceFactory, IFlashCapCaptureSession
    {
        private readonly ConcurrentQueue<string> _events = new();
        private readonly TaskCompletionSource<FlashCapOpenedDevice> _opening = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _openCalled = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _disposed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<string> Events => [.. _events];

        public Task OpenCalled => _openCalled.Task;

        public Task Disposed => _disposed.Task;

        public Task<FlashCapOpenedDevice> OpenAsync(
            Action<byte[]> frameArrived,
            Action<Exception> frameFailed,
            CancellationToken cancellationToken)
        {
            _events.Enqueue("Open");
            _openCalled.TrySetResult();
            return _opening.Task;
        }

        public void CompleteOpen()
            => _opening.TrySetResult(new FlashCapOpenedDevice(this, "image/jpeg"));

        public Task StartAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("A late open must not be started.");

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _events.Enqueue("Stop");
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _events.Enqueue("Dispose");
            _disposed.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class LateCompletingStartCaptureDevice :
        IFlashCapCaptureDeviceFactory,
        IFlashCapCaptureSession
    {
        private readonly ConcurrentQueue<string> _events = new();
        private readonly TaskCompletionSource _startCalled = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _startCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondDispose = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;
        private int _disposeCount;

        public IReadOnlyList<string> Events => [.. _events];

        public Task StartCalled => _startCalled.Task;

        public Task SecondDispose => _secondDispose.Task;

        public bool IsActive => Volatile.Read(ref _active) != 0;

        public Task<FlashCapOpenedDevice> OpenAsync(
            Action<byte[]> frameArrived,
            Action<Exception> frameFailed,
            CancellationToken cancellationToken)
        {
            _events.Enqueue("Open");
            return Task.FromResult(new FlashCapOpenedDevice(this, "image/jpeg"));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _events.Enqueue("Start");
            _startCalled.TrySetResult();
            return _startCompletion.Task;
        }

        public void CompleteStartAfterCleanup()
        {
            Volatile.Write(ref _active, 1);
            _events.Enqueue("StartCompleted");
            _startCompletion.TrySetResult();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _events.Enqueue("Stop");
            Volatile.Write(ref _active, 0);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _events.Enqueue("Dispose");
            Volatile.Write(ref _active, 0);
            if (Interlocked.Increment(ref _disposeCount) == 2)
            {
                _secondDispose.TrySetResult();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class SerializedHungCaptureDevice : IFlashCapCaptureDeviceFactory, IFlashCapCaptureSession
    {
        private readonly ConcurrentQueue<string> _events = new();
        private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
        private readonly TaskCompletionSource _startHoldingLifecycleLock = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseStart = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _cleanupDrained = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _completedCleanupOperations;

        public IReadOnlyList<string> Events => [.. _events];

        public Task StartHoldingLifecycleLock => _startHoldingLifecycleLock.Task;

        public Task CleanupDrained => _cleanupDrained.Task;

        public Task<FlashCapOpenedDevice> OpenAsync(
            Action<byte[]> frameArrived,
            Action<Exception> frameFailed,
            CancellationToken cancellationToken)
        {
            _events.Enqueue("Open");
            return Task.FromResult(new FlashCapOpenedDevice(this, "image/jpeg"));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _events.Enqueue("StartAttempt");
            await _lifecycleLock.WaitAsync(CancellationToken.None);
            _events.Enqueue("StartHoldingLock");
            _startHoldingLifecycleLock.TrySetResult();
            try
            {
                await _releaseStart.Task;
            }
            finally
            {
                _lifecycleLock.Release();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _events.Enqueue("StopAttempt");
            await _lifecycleLock.WaitAsync(CancellationToken.None);
            try
            {
                _events.Enqueue("StopCompleted");
            }
            finally
            {
                _lifecycleLock.Release();
                MarkCleanupOperationCompleted();
            }
        }

        public async ValueTask DisposeAsync()
        {
            _events.Enqueue("DisposeAttempt");
            await _lifecycleLock.WaitAsync(CancellationToken.None);
            try
            {
                _events.Enqueue("DisposeCompleted");
            }
            finally
            {
                _lifecycleLock.Release();
                MarkCleanupOperationCompleted();
            }
        }

        public void ReleaseHungStart()
            => _releaseStart.TrySetResult();

        private void MarkCleanupOperationCompleted()
        {
            if (Interlocked.Increment(ref _completedCleanupOperations) == 2)
            {
                _cleanupDrained.TrySetResult();
            }
        }
    }

    private sealed class RecordingStore(Exception? putFailure = null) : ISessionByteStore
    {
        public int Count => 0;

        public int PutCalls { get; private set; }

        public SessionByteReference Put(ReadOnlyMemory<byte> content)
        {
            PutCalls++;
            if (putFailure is not null)
            {
                throw putFailure;
            }

            return SessionByteReference.NewReference();
        }

        public bool TryGet(SessionByteReference reference, out ReadOnlyMemory<byte> content)
        {
            content = default;
            return false;
        }

        public void Release(SessionByteReference reference)
        {
        }

        public void PurgeAll()
        {
        }
    }

    private sealed class CancellingStore(CancellationTokenSource cancellation) : ISessionByteStore
    {
        private readonly InMemorySessionByteStore _inner = new();

        public int Count => _inner.Count;

        public int PutCalls { get; private set; }

        public int ReleaseCalls { get; private set; }

        public byte[]? LastStoredBuffer { get; private set; }

        public SessionByteReference Put(ReadOnlyMemory<byte> content)
        {
            PutCalls++;
            LastStoredBuffer = content.ToArray();
            var reference = _inner.Put(LastStoredBuffer);
            cancellation.Cancel();
            return reference;
        }

        public bool TryGet(SessionByteReference reference, out ReadOnlyMemory<byte> content)
            => _inner.TryGet(reference, out content);

        public void Release(SessionByteReference reference)
        {
            ReleaseCalls++;
            _inner.Release(reference);
            if (LastStoredBuffer is not null)
            {
                Array.Clear(LastStoredBuffer);
            }
        }

        public void PurgeAll()
        {
            _inner.PurgeAll();
            if (LastStoredBuffer is not null)
            {
                Array.Clear(LastStoredBuffer);
            }
        }
    }
}
