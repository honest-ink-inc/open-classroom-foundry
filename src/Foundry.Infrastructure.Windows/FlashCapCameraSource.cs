// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using FlashCap;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Infrastructure.Windows;

/// <summary>
/// The live camera behind the same seam the simulator fills, using FlashCap as
/// the kiosk does. One frame per capture, bytes straight into the session
/// store, envelope born Amber like every unknown. Physical-camera behavior
/// (low light, loss and reconnect, rotation) is hardware-bench work by plan
/// §12. Automated tests exercise the byte-ownership and device-lifecycle
/// boundaries through internal seams and stop real hardware at enumeration,
/// because a test that silently photographs the developer's room is not a test,
/// it is a trespass.
/// </summary>
public sealed class FlashCapCameraSource : ICaptureSource
{
    public const string Kind = "camera";
    internal const int MaxCapturedFrameBytes = ImageNormalizer.MaxEncodedImageBytes;

    private readonly ISessionByteStore _store;
    private readonly IFlashCapFrameProvider _frameProvider;

    public FlashCapCameraSource(ISessionByteStore store)
        : this(store, new FlashCapFrameProvider())
    {
    }

    internal FlashCapCameraSource(ISessionByteStore store, IFlashCapFrameProvider frameProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
    }

    public static IReadOnlyList<string> EnumerateCameraNames()
        => [.. new CaptureDevices().EnumerateDescriptors().Select(d => d.Name)];

    public async Task<SourceEnvelope> CaptureAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        byte[]? frame = null;
        SessionByteReference? tentativeReference = null;
        try
        {
            var capturedFrame = await _frameProvider.CaptureFrameAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The camera returned no frame.");

            frame = capturedFrame.Bytes
                ?? throw new InvalidOperationException("The camera returned no frame.");

            cancellationToken.ThrowIfCancellationRequested();

            if (frame.Length == 0)
            {
                throw new InvalidOperationException("The camera returned an empty frame.");
            }

            if (frame.Length > MaxCapturedFrameBytes)
            {
                throw new InvalidDataException(
                    $"The camera frame exceeds the bounded {MaxCapturedFrameBytes}-byte encoded-image contract.");
            }

            tentativeReference = _store.Put(frame);
            cancellationToken.ThrowIfCancellationRequested();

            var envelope = new SourceEnvelope(
                SourceKind: Kind,
                MimeType: capturedFrame.MimeType,
                PageCount: 1,
                Lane: LanePolicy.DefaultForUnknown,
                MetadataStripped: false,
                TeacherStatedRights: string.Empty,
                Bytes: tentativeReference.Value);

            // Ownership transfers only after every fallible/cancellable step has
            // completed. Until then the source must be able to remove its own
            // tentative store allocation even when called without CaptureSession.
            tentativeReference = null;
            return envelope;
        }
        catch (Exception captureFailure)
        {
            if (tentativeReference is { } reference)
            {
                ReleaseTentativeReference(reference, captureFailure);
            }

            throw;
        }
        finally
        {
            if (frame is not null)
            {
                CryptographicOperations.ZeroMemory(frame);
            }
        }
    }

    private void ReleaseTentativeReference(
        SessionByteReference reference,
        Exception captureFailure)
    {
        Exception? releaseFailure;
        try
        {
            _store.Release(reference);
            if (!_store.TryGet(reference, out _))
            {
                return;
            }

            releaseFailure = new InvalidOperationException(
                "The session byte store retained a released camera frame.");
        }
        catch (Exception exception)
        {
            releaseFailure = exception;
        }

        try
        {
            _store.PurgeAll();
        }
        catch (Exception purgeFailure)
        {
            throw new InvalidOperationException(
                "Camera capture failed and its tentative frame could not be purged.",
                new AggregateException(captureFailure, releaseFailure, purgeFailure));
        }

        if (_store.TryGet(reference, out _))
        {
            throw new InvalidOperationException(
                "Camera capture failed and its tentative frame survived release and purge.",
                new AggregateException(captureFailure, releaseFailure));
        }
    }
}

internal sealed record FlashCapCapturedFrame(byte[] Bytes, string MimeType);

internal interface IFlashCapFrameProvider
{
    /// <summary>
    /// Transfers exclusive ownership of the returned mutable buffer to the
    /// caller, which must zero it after copying or refusing the frame.
    /// </summary>
    Task<FlashCapCapturedFrame> CaptureFrameAsync(CancellationToken cancellationToken);
}

internal sealed class FlashCapFrameProvider : IFlashCapFrameProvider
{
    private static readonly TimeSpan DefaultDeviceCleanupTimeout = TimeSpan.FromSeconds(5);

    private readonly IFlashCapCaptureDeviceFactory _deviceFactory;
    private readonly TimeSpan _deviceCleanupTimeout;

    public FlashCapFrameProvider()
        : this(new FlashCapCaptureDeviceFactory())
    {
    }

    internal FlashCapFrameProvider(
        IFlashCapCaptureDeviceFactory deviceFactory,
        TimeSpan? deviceCleanupTimeout = null)
    {
        _deviceFactory = deviceFactory ?? throw new ArgumentNullException(nameof(deviceFactory));
        _deviceCleanupTimeout = deviceCleanupTimeout ?? DefaultDeviceCleanupTimeout;
        if (_deviceCleanupTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deviceCleanupTimeout),
                "The camera cleanup timeout must be positive.");
        }
    }

    public async Task<FlashCapCapturedFrame> CaptureFrameAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var frameCompletion = new TaskCompletionSource<byte[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void FrameArrived(byte[] candidate)
        {
            if (candidate is null)
            {
                frameCompletion.TrySetException(
                    new InvalidOperationException("The camera returned no frame."));
                return;
            }

            if (candidate.Length == 0)
            {
                CryptographicOperations.ZeroMemory(candidate);
                frameCompletion.TrySetException(
                    new InvalidOperationException("The camera returned an empty frame."));
                return;
            }

            if (candidate.Length > FlashCapCameraSource.MaxCapturedFrameBytes)
            {
                CryptographicOperations.ZeroMemory(candidate);
                frameCompletion.TrySetException(new InvalidDataException(
                    $"The camera frame exceeds the bounded {FlashCapCameraSource.MaxCapturedFrameBytes}-byte encoded-image contract."));
                return;
            }

            var transferred = false;
            try
            {
                transferred = frameCompletion.TrySetResult(candidate);
            }
            finally
            {
                if (!transferred)
                {
                    CryptographicOperations.ZeroMemory(candidate);
                }
            }
        }

        void FrameFailed(Exception failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            frameCompletion.TrySetException(failure);
        }

        using var cancellationRegistration = cancellationToken.Register(
            () => frameCompletion.TrySetCanceled(cancellationToken));

        FlashCapOpenedDevice? openedDevice = null;
        Task<FlashCapOpenedDevice>? openingDevice = null;
        Task? startingDevice = null;
        byte[]? capturedBytes = null;
        Exception? operationFailure = null;
        var startMayCompleteAfterCleanup = false;

        try
        {
            openingDevice = _deviceFactory.OpenAsync(
                FrameArrived,
                FrameFailed,
                cancellationToken);
            openedDevice = await openingDevice
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("The camera could not be opened.");

            startingDevice = openedDevice.Session.StartAsync(cancellationToken);
            await startingDevice
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
            capturedBytes = await frameCompletion.Task.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            operationFailure = exception;
            // If Start/Open failed before the frame await, make every later
            // callback lose the race (and therefore zero its owned copy).
            // A frame that already won is recovered and zeroed below.
            frameCompletion.TrySetException(exception);

            if (openedDevice is null && openingDevice is not null)
            {
                // WaitAsync can settle the caller even when a driver ignores
                // the cancellation token passed to OpenAsync. Observe that
                // abandoned task and, if it later succeeds, make bounded stop
                // and dispose attempts against the otherwise orphaned session.
                ObserveLateOpen(openingDevice);
            }

            if (startingDevice is not null)
            {
                // A token-ignoring StartAsync can outlive the caller. Its late
                // completion must be observed. When immediate cleanup itself
                // completes, a later successful Start also requires a second
                // stop/dispose pass; otherwise the camera could reactivate
                // after the caller was already told that capture settled.
                startMayCompleteAfterCleanup = !startingDevice.IsCompleted;
            }
        }

        var cleanupResult = openedDevice is null
            ? null
            : await CleanupAsync(openedDevice.Session).ConfigureAwait(false);
        var cleanupFailure = cleanupResult?.Failure;

        if (startingDevice is not null)
        {
            if (startMayCompleteAfterCleanup
                && cleanupResult is { HasPendingOperations: false }
                && openedDevice is not null)
            {
                ObserveLateStartAndCleanup(startingDevice, openedDevice.Session);
            }
            else
            {
                ObserveTaskCompletion(startingDevice);
            }
        }

        if (operationFailure is not null || cleanupFailure is not null)
        {
            if (cleanupFailure is not null)
            {
                // Cleanup may have timed out while callbacks were still live.
                // Close the completion gate before inspecting it so a future
                // callback cannot become an unowned winner.
                frameCompletion.TrySetException(cleanupFailure);
            }

            if (capturedBytes is null && frameCompletion.Task.IsCompletedSuccessfully)
            {
                capturedBytes = frameCompletion.Task.Result;
            }

            if (capturedBytes is not null)
            {
                CryptographicOperations.ZeroMemory(capturedBytes);
            }

            if (operationFailure is not null && cleanupFailure is not null)
            {
                throw new InvalidOperationException(
                    "Camera capture failed, and bounded shutdown could not confirm that the camera stopped and disposed.",
                    new AggregateException(operationFailure, cleanupFailure));
            }

            ExceptionDispatchInfo.Capture(operationFailure ?? cleanupFailure!).Throw();
        }

        return new FlashCapCapturedFrame(capturedBytes!, openedDevice!.MimeType);
    }

    private void ObserveLateOpen(Task<FlashCapOpenedDevice> openingDevice)
        => _ = ObserveLateOpenAsync(openingDevice);

    private async Task ObserveLateOpenAsync(Task<FlashCapOpenedDevice> openingDevice)
    {
        try
        {
            var lateOpenedDevice = await openingDevice.ConfigureAwait(false);
            if (lateOpenedDevice is not null)
            {
                // CleanupAsync captures both failures. Awaiting it here is what
                // observes the late lifecycle; the original caller has already
                // settled and therefore cannot truthfully be told it closed.
                _ = await CleanupAsync(lateOpenedDevice.Session).ConfigureAwait(false);
            }
        }
        catch
        {
            // This detached observer exists solely to consume a late OpenAsync
            // or cleanup failure. There is no logger on this infrastructure
            // seam, and the already-settled capture must not be resurrected.
        }
    }

    private static void ObserveTaskCompletion(Task task)
        => _ = ObserveTaskCompletionAsync(task);

    private static async Task ObserveTaskCompletionAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // The bounded caller already carries the operation/timeout failure.
            // Awaiting here prevents a later driver fault from going unobserved.
        }
    }

    private void ObserveLateStartAndCleanup(
        Task startingDevice,
        IFlashCapCaptureSession session)
        => _ = ObserveLateStartAndCleanupAsync(startingDevice, session);

    private async Task ObserveLateStartAndCleanupAsync(
        Task startingDevice,
        IFlashCapCaptureSession session)
    {
        try
        {
            await startingDevice.ConfigureAwait(false);
        }
        catch
        {
            // A faulted Start may still have changed driver state. The second
            // cleanup attempt below is therefore required on every completion.
        }

        try
        {
            _ = await CleanupAsync(session).ConfigureAwait(false);
        }
        catch
        {
            // Detached cleanup can no longer change the settled caller result;
            // it exists to close a lifecycle that completed out of order.
        }
    }

    private async Task<DeviceCleanupResult> CleanupAsync(IFlashCapCaptureSession session)
    {
        Exception? stopFailure = null;
        Exception? disposeFailure = null;
        Task? stoppingDevice = null;
        Task? disposingDevice = null;

        using (var cleanupCancellation = new CancellationTokenSource(_deviceCleanupTimeout))
        {
            try
            {
                stoppingDevice = session.StopAsync(cleanupCancellation.Token);
                await stoppingDevice
                    .WaitAsync(_deviceCleanupTimeout)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                stopFailure = exception;
                if (stoppingDevice is not null)
                {
                    ObserveTaskCompletion(stoppingDevice);
                }
            }
        }

        try
        {
            disposingDevice = session.DisposeAsync().AsTask();
            await disposingDevice
                .WaitAsync(_deviceCleanupTimeout)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            disposeFailure = exception;
            if (disposingDevice is not null)
            {
                ObserveTaskCompletion(disposingDevice);
            }
        }

        // A timeout proves that our cleanup attempt settled; it does not prove
        // the underlying driver released a shared lifecycle lock or the device.
        var failure = (stopFailure, disposeFailure) switch
        {
            (null, null) => null,
            (not null, null) => stopFailure,
            (null, not null) => disposeFailure,
            _ => new AggregateException(stopFailure, disposeFailure),
        };
        return new DeviceCleanupResult(
            failure,
            HasPendingOperations: stoppingDevice is { IsCompleted: false }
                || disposingDevice is { IsCompleted: false });
    }

    private sealed record DeviceCleanupResult(
        Exception? Failure,
        bool HasPendingOperations);
}

internal sealed record FlashCapOpenedDevice(
    IFlashCapCaptureSession Session,
    string MimeType);

internal interface IFlashCapCaptureDeviceFactory
{
    Task<FlashCapOpenedDevice> OpenAsync(
        Action<byte[]> frameArrived,
        Action<Exception> frameFailed,
        CancellationToken cancellationToken);
}

internal interface IFlashCapCaptureSession : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

internal sealed class FlashCapCaptureDeviceFactory : IFlashCapCaptureDeviceFactory
{
    private const long BitmapEnvelopeAllowanceBytes = 4_096;

    public async Task<FlashCapOpenedDevice> OpenAsync(
        Action<byte[]> frameArrived,
        Action<Exception> frameFailed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frameArrived);
        ArgumentNullException.ThrowIfNull(frameFailed);

        var descriptors = new CaptureDevices().EnumerateDescriptors().ToArray();
        if (descriptors.Length == 0)
        {
            throw new InvalidOperationException("No camera is available on this machine; import an image instead.");
        }

        CaptureDeviceDescriptor? selectedDescriptor = null;
        VideoCharacteristics? selectedCharacteristic = null;
        foreach (var descriptor in descriptors)
        {
            selectedCharacteristic = TrySelectCharacteristic(descriptor.Characteristics);
            if (selectedCharacteristic is not null)
            {
                selectedDescriptor = descriptor;
                break;
            }
        }

        if (selectedDescriptor is null || selectedCharacteristic is null)
        {
            throw new InvalidOperationException(
                "No camera reports a supported capture characteristic within the bounded image contract.");
        }

        void callback(PixelBufferScope bufferScope)
        {
            CopyAndDeliverFrame(bufferScope, frameArrived, frameFailed);
        }

        var device = await selectedDescriptor.OpenAsync(
            selectedCharacteristic,
            TranscodeFormats.Auto,
            callback,
            cancellationToken).ConfigureAwait(false);

        return new FlashCapOpenedDevice(
            new FlashCapCaptureSession(device),
            MimeTypeForAuto(selectedCharacteristic.PixelFormat));
    }

    internal static VideoCharacteristics SelectCharacteristic(
        IEnumerable<VideoCharacteristics> characteristics)
        => TrySelectCharacteristic(characteristics)
            ?? throw new InvalidOperationException(
                "The camera reports no supported capture characteristic within the bounded image contract.");

    internal static string MimeTypeForAuto(PixelFormats pixelFormat)
        => pixelFormat switch
        {
            PixelFormats.JPEG => "image/jpeg",
            PixelFormats.PNG => "image/png",
            PixelFormats.Unknown => throw new ArgumentOutOfRangeException(
                nameof(pixelFormat),
                pixelFormat,
                "An unknown pixel format cannot be captured."),
            _ => "image/bmp",
        };

    private static VideoCharacteristics? TrySelectCharacteristic(
        IEnumerable<VideoCharacteristics> characteristics)
    {
        ArgumentNullException.ThrowIfNull(characteristics);

        VideoCharacteristics? selected = null;
        var selectedRank = int.MaxValue;
        foreach (var characteristic in characteristics)
        {
            if (!IsWithinImageBounds(characteristic))
            {
                continue;
            }

            var rank = characteristic.PixelFormat switch
            {
                PixelFormats.JPEG => 0,
                PixelFormats.PNG => 1,
                _ => 2,
            };

            if (rank < selectedRank)
            {
                selected = characteristic;
                selectedRank = rank;
            }
        }

        return selected;
    }

    private static bool IsWithinImageBounds(VideoCharacteristics? characteristic)
    {
        if (characteristic is null
            || characteristic.PixelFormat == PixelFormats.Unknown
            || characteristic.Width is <= 0 or > ImageNormalizer.MaxImageDimension
            || characteristic.Height is <= 0 or > ImageNormalizer.MaxImageDimension
            || characteristic.Width > ImageNormalizer.MaxDecodedPixels / characteristic.Height)
        {
            return false;
        }

        if (characteristic.PixelFormat is PixelFormats.JPEG or PixelFormats.PNG)
        {
            return true;
        }

        // FlashCap's Auto transcode produces BMP for every other accepted raw
        // mode. Admit only a mode whose conservative 32-bit bitmap plus header
        // allowance can still satisfy the 16 MiB encoded-frame boundary.
        var pixelCount = (long)characteristic.Width * characteristic.Height;
        return (pixelCount * 4) + BitmapEnvelopeAllowanceBytes
            <= FlashCapCameraSource.MaxCapturedFrameBytes;
    }

    private static void CopyAndDeliverFrame(
        PixelBufferScope bufferScope,
        Action<byte[]> frameArrived,
        Action<Exception> frameFailed)
        => CopyAndDeliverBorrowedFrame(
            bufferScope.Buffer.ReferImage,
            bufferScope.ReleaseNow,
            frameArrived,
            frameFailed);

    internal static void CopyAndDeliverBorrowedFrame(
        Func<ArraySegment<byte>> referImage,
        Action release,
        Action<byte[]> frameArrived,
        Action<Exception> frameFailed)
    {
        ArgumentNullException.ThrowIfNull(referImage);
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(frameArrived);
        ArgumentNullException.ThrowIfNull(frameFailed);

        ArraySegment<byte> borrowedFrame = default;
        var borrowedFrameAcquired = false;
        byte[]? ownedFrame = null;
        Exception? captureFailure = null;

        try
        {
            borrowedFrame = referImage();
            borrowedFrameAcquired = true;
            if (borrowedFrame.Count == 0)
            {
                throw new InvalidOperationException("The camera returned an empty frame.");
            }

            if (borrowedFrame.Count > FlashCapCameraSource.MaxCapturedFrameBytes)
            {
                throw new InvalidDataException(
                    $"The camera frame exceeds the bounded {FlashCapCameraSource.MaxCapturedFrameBytes}-byte encoded-image contract.");
            }

            ownedFrame = new ReadOnlySpan<byte>(
                borrowedFrame.Array!,
                borrowedFrame.Offset,
                borrowedFrame.Count).ToArray();
        }
        catch (Exception exception)
        {
            captureFailure = exception;
        }

        if (borrowedFrameAcquired)
        {
            try
            {
                // ReferImage returns a borrowed pooled segment. Scrub that
                // exact segment while the lease is still held, on success and
                // every refusal path, before ReleaseNow returns it to the pool.
                CryptographicOperations.ZeroMemory(borrowedFrame.AsSpan());
            }
            catch (Exception zeroFailure)
            {
                captureFailure = captureFailure is null
                    ? zeroFailure
                    : new AggregateException(captureFailure, zeroFailure);
            }
        }

        try
        {
            release();
        }
        catch (Exception releaseFailure)
        {
            captureFailure = captureFailure is null
                ? releaseFailure
                : new AggregateException(captureFailure, releaseFailure);
        }

        if (captureFailure is not null)
        {
            if (ownedFrame is not null)
            {
                CryptographicOperations.ZeroMemory(ownedFrame);
            }

            frameFailed(captureFailure);
            return;
        }

        try
        {
            frameArrived(ownedFrame!);
            ownedFrame = null;
        }
        catch (Exception deliveryFailure)
        {
            if (ownedFrame is not null)
            {
                CryptographicOperations.ZeroMemory(ownedFrame);
            }

            frameFailed(deliveryFailure);
        }
    }

    private sealed class FlashCapCaptureSession(CaptureDevice device) : IFlashCapCaptureSession
    {
        public Task StartAsync(CancellationToken cancellationToken)
            => device.StartAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken)
            => device.StopAsync(cancellationToken);

        public ValueTask DisposeAsync()
            => new(device.DisposeAsync());
    }
}
