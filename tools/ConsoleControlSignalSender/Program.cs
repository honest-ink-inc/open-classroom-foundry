// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

var readinessTimeout = TimeSpan.FromSeconds(15);
var readinessPollInterval = TimeSpan.FromMilliseconds(2);

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("console-signal.unsupported-platform");
    return 2;
}

if (args.Length != 2
    || !uint.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out var targetProcessId)
    || targetProcessId == 0
    || string.IsNullOrWhiteSpace(args[1])
    || !Path.IsPathFullyQualified(args[1]))
{
    Console.Error.WriteLine("console-signal.invalid-target");
    return 2;
}

string batchLockPath;
try
{
    batchLockPath = Path.GetFullPath(args[1]);
}
catch (Exception exception) when (
    exception is ArgumentException
        or NotSupportedException
        or IOException
        or System.Security.SecurityException
        or UnauthorizedAccessException)
{
    Console.Error.WriteLine("console-signal.invalid-target");
    return 2;
}

using var targetProcess = NativeMethods.OpenProcess(
    NativeMethods.Synchronize,
    inheritHandle: false,
    targetProcessId);
if (targetProcess.IsInvalid)
{
    Console.Error.WriteLine("console-signal.target-open-failed");
    return 3;
}

// This executable is test support. It detaches from any inherited console,
// joins the isolated console created for the target process, ignores the
// broadcast in this sender, observes the batch lock while its exclusive handle
// is still held, and then delivers one real CTRL+C console event. Keeping this
// wait in the already-attached sender minimizes—but cannot eliminate—the race
// between the filesystem observation and asynchronous console delivery.
var detachedFromInheritedConsole = NativeMethods.FreeConsole();
var detachError = detachedFromInheritedConsole ? 0 : Marshal.GetLastPInvokeError();
var readinessWatch = Stopwatch.StartNew();
var attachedToTarget = false;
try
{
    var attachment = ConsoleAttachmentReadiness.Wait(
        () =>
        {
            var attached = NativeMethods.AttachConsole(targetProcessId);
            attachedToTarget |= attached;
            return new ConsoleAttachmentAttempt(
                attached,
                attached ? 0 : Marshal.GetLastPInvokeError());
        },
        () =>
        {
            var waitResult = NativeMethods.WaitForSingleObject(targetProcess, milliseconds: 0);
            return new ConsoleTargetObservation(
                waitResult,
                waitResult == NativeMethods.WaitFailed ? Marshal.GetLastPInvokeError() : 0);
        },
        () => readinessWatch.ElapsedMilliseconds,
        () => Thread.Sleep(readinessPollInterval),
        checked((long)readinessTimeout.TotalMilliseconds));
    if (attachment.Outcome != ConsoleAttachmentOutcome.Attached)
    {
        var detachReceipt = detachedFromInheritedConsole ? "true" : "false";
        var attachmentReceipt =
            $"attempts={attachment.Attempts.ToString(CultureInfo.InvariantCulture)}; lastAttachError={attachment.LastAttachError.ToString(CultureInfo.InvariantCulture)}; attachedToTarget={attachment.AttachedToTarget.ToString().ToLowerInvariant()}; inheritedConsoleDetached={detachReceipt}; inheritedDetachError={detachError.ToString(CultureInfo.InvariantCulture)}; targetWait={TargetWaitReceipt(attachment.LastTargetObservation)}; targetWaitError={attachment.LastTargetObservation.ErrorCode.ToString(CultureInfo.InvariantCulture)}; maxPollGapMs={attachment.MaximumPollGapMilliseconds.ToString(CultureInfo.InvariantCulture)}; elapsedMs={attachment.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)}";
        if (attachment.Outcome == ConsoleAttachmentOutcome.TargetExited)
        {
            Console.Error.WriteLine($"console-signal.target-exited; phase=attach; {attachmentReceipt}");
            return 5;
        }

        if (attachment.Outcome == ConsoleAttachmentOutcome.TargetWatchFailed)
        {
            Console.Error.WriteLine($"console-signal.target-watch-failed; phase=attach; {attachmentReceipt}");
            return 5;
        }

        if (attachment.Outcome == ConsoleAttachmentOutcome.TimedOut)
        {
            Console.Error.WriteLine($"console-signal.attach-timeout; {attachmentReceipt}");
            return 3;
        }

        Console.Error.WriteLine($"console-signal.attach-failed; {attachmentReceipt}");
        return 3;
    }

    if (!NativeMethods.SetConsoleCtrlHandler(IntPtr.Zero, add: true))
    {
        Console.Error.WriteLine("console-signal.ignore-failed");
        return 4;
    }

    var attachmentElapsedMilliseconds = readinessWatch.ElapsedMilliseconds;
    var observationAttempts = 0;
    var missingObservations = 0;
    var openableObservations = 0;
    var accessRefusedObservations = 0;
    var otherIoObservations = 0;
    var lastObservationElapsedMilliseconds = attachmentElapsedMilliseconds;
    var maximumPollGapMilliseconds = 0L;
    var lastLockState = BatchLockState.Missing;
    while (true)
    {
        var observationElapsedMilliseconds = readinessWatch.ElapsedMilliseconds;
        maximumPollGapMilliseconds = Math.Max(
            maximumPollGapMilliseconds,
            observationElapsedMilliseconds - lastObservationElapsedMilliseconds);
        lastObservationElapsedMilliseconds = observationElapsedMilliseconds;
        observationAttempts++;
        lastLockState = ObserveBatchLock(batchLockPath);
        if (lastLockState == BatchLockState.Held)
        {
            break;
        }

        if (lastLockState == BatchLockState.Missing)
        {
            missingObservations++;
        }
        else if (lastLockState == BatchLockState.Openable)
        {
            openableObservations++;
        }
        else if (lastLockState == BatchLockState.AccessRefused)
        {
            accessRefusedObservations++;
        }
        else
        {
            otherIoObservations++;
        }

        var targetWait = NativeMethods.WaitForSingleObject(targetProcess, milliseconds: 0);
        if (targetWait == NativeMethods.WaitObject0)
        {
            Console.Error.WriteLine("console-signal.target-exited");
            return 5;
        }

        if (targetWait != NativeMethods.WaitTimeout)
        {
            Console.Error.WriteLine("console-signal.target-watch-failed");
            return 5;
        }

        if (readinessWatch.Elapsed >= readinessTimeout)
        {
            Console.Error.WriteLine(
                $"console-signal.lock-observation-timeout; target=running; lastLockState={LockStateReceipt(lastLockState)}; attempts={observationAttempts.ToString(CultureInfo.InvariantCulture)}; missing={missingObservations.ToString(CultureInfo.InvariantCulture)}; openable={openableObservations.ToString(CultureInfo.InvariantCulture)}; accessRefused={accessRefusedObservations.ToString(CultureInfo.InvariantCulture)}; otherIo={otherIoObservations.ToString(CultureInfo.InvariantCulture)}; maxPollGapMs={maximumPollGapMilliseconds.ToString(CultureInfo.InvariantCulture)}; attachAttempts={attachment.Attempts.ToString(CultureInfo.InvariantCulture)}; attachElapsedMs={attachmentElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)}; elapsedMs={readinessWatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)}");
            return 5;
        }

        Thread.Sleep(readinessPollInterval);
    }

    var deliveryWait = NativeMethods.WaitForSingleObject(targetProcess, milliseconds: 0);
    if (deliveryWait == NativeMethods.WaitObject0)
    {
        Console.Error.WriteLine("console-signal.target-exited");
        return 5;
    }

    if (deliveryWait != NativeMethods.WaitTimeout)
    {
        Console.Error.WriteLine("console-signal.target-watch-failed");
        return 5;
    }

    if (!NativeMethods.GenerateConsoleCtrlEvent(NativeMethods.CtrlCEvent, processGroupId: 0))
    {
        Console.Error.WriteLine("console-signal.delivery-failed");
        return 6;
    }

    // GenerateConsoleCtrlEvent is asynchronous. Keep the sender attached and
    // ignoring CTRL+C briefly so the target handler can begin cooperatively.
    Thread.Sleep(TimeSpan.FromMilliseconds(250));
    return 0;
}
finally
{
    if (attachedToTarget)
    {
        _ = NativeMethods.FreeConsole();
    }
}

static BatchLockState ObserveBatchLock(string path)
{
    try
    {
        using var unexpectedOpen = File.OpenHandle(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return BatchLockState.Openable;
    }
    catch (IOException exception) when ((exception.HResult & 0xFFFF) == NativeMethods.ErrorSharingViolation)
    {
        return BatchLockState.Held;
    }
    catch (IOException exception) when ((exception.HResult & 0xFFFF) is NativeMethods.ErrorFileNotFound or NativeMethods.ErrorPathNotFound)
    {
        return BatchLockState.Missing;
    }

    catch (UnauthorizedAccessException)
    {
        return BatchLockState.AccessRefused;
    }

    catch (IOException exception)
    {
        return new BatchLockState(exception.HResult & 0xFFFF);
    }
}

static string LockStateReceipt(BatchLockState state)
{
    if (state == BatchLockState.Missing)
    {
        return "missing";
    }

    if (state == BatchLockState.Openable)
    {
        return "openable";
    }

    if (state == BatchLockState.Held)
    {
        return "held";
    }

    if (state == BatchLockState.AccessRefused)
    {
        return "access-refused";
    }

    return $"io-error-{state.ErrorCode.ToString(CultureInfo.InvariantCulture)}";
}

static string TargetWaitReceipt(ConsoleTargetObservation observation)
{
    if (observation.WaitResult == NativeMethods.WaitTimeout)
    {
        return "running";
    }

    if (observation.WaitResult == NativeMethods.WaitObject0)
    {
        return "exited";
    }

    if (observation.WaitResult == NativeMethods.WaitFailed)
    {
        return "failed";
    }

    return $"unexpected-{observation.WaitResult.ToString(CultureInfo.InvariantCulture)}";
}

internal readonly record struct BatchLockState(int ErrorCode)
{
    internal static BatchLockState Missing { get; } = new(NativeMethods.ErrorFileNotFound);

    internal static BatchLockState Openable { get; } = new(0);

    internal static BatchLockState Held { get; } = new(NativeMethods.ErrorSharingViolation);

    internal static BatchLockState AccessRefused { get; } = new(NativeMethods.ErrorAccessDenied);
}

internal static class ConsoleAttachmentReadiness
{
    internal static ConsoleAttachmentResult Wait(
        Func<ConsoleAttachmentAttempt> tryAttach,
        Func<ConsoleTargetObservation> observeTarget,
        Func<long> elapsedMilliseconds,
        Action delay,
        long timeoutMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(tryAttach);
        ArgumentNullException.ThrowIfNull(observeTarget);
        ArgumentNullException.ThrowIfNull(elapsedMilliseconds);
        ArgumentNullException.ThrowIfNull(delay);
        if (timeoutMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeoutMilliseconds),
                timeoutMilliseconds,
                "The console-attachment readiness timeout must be positive.");
        }

        var initialElapsed = elapsedMilliseconds();
        if (initialElapsed < 0)
        {
            throw new InvalidOperationException("The console-attachment clock cannot start below zero.");
        }

        var lastPollAt = initialElapsed;
        var maximumPollGapMilliseconds = 0L;
        var attempts = 0;
        var lastAttachError = 0;
        var lastTargetObservation = ConsoleTargetObservation.Unobserved;

        long ReadElapsed()
        {
            var current = elapsedMilliseconds();
            if (current < lastPollAt)
            {
                throw new InvalidOperationException("The console-attachment clock must be monotonic.");
            }

            maximumPollGapMilliseconds = Math.Max(
                maximumPollGapMilliseconds,
                current - lastPollAt);
            lastPollAt = current;
            return current;
        }

        ConsoleAttachmentResult Result(
            ConsoleAttachmentOutcome outcome,
            bool attachedToTarget,
            long elapsed)
        {
            return new(
                outcome,
                lastAttachError,
                attempts,
                attachedToTarget,
                lastTargetObservation,
                maximumPollGapMilliseconds,
                elapsed);
        }

        ConsoleAttachmentResult? ObserveTargetAndClassify(
            bool attachedToTarget,
            ConsoleAttachmentOutcome runningOutcome)
        {
            lastTargetObservation = observeTarget();
            var elapsed = ReadElapsed();
            if (lastTargetObservation.WaitResult == NativeMethods.WaitObject0)
            {
                return Result(ConsoleAttachmentOutcome.TargetExited, attachedToTarget, elapsed);
            }

            if (lastTargetObservation.WaitResult != NativeMethods.WaitTimeout)
            {
                return Result(ConsoleAttachmentOutcome.TargetWatchFailed, attachedToTarget, elapsed);
            }

            if (elapsed >= timeoutMilliseconds)
            {
                return Result(ConsoleAttachmentOutcome.TimedOut, attachedToTarget, elapsed);
            }

            return runningOutcome == ConsoleAttachmentOutcome.Attached
                ? null
                : Result(runningOutcome, attachedToTarget, elapsed);
        }

        while (true)
        {
            var elapsed = ReadElapsed();
            if (elapsed >= timeoutMilliseconds)
            {
                return ObserveTargetAndClassify(
                    attachedToTarget: false,
                    ConsoleAttachmentOutcome.TimedOut)!.Value;
            }

            attempts = checked(attempts + 1);
            var attempt = tryAttach();
            elapsed = ReadElapsed();
            if (attempt.Attached)
            {
                lastAttachError = 0;
                if (elapsed < timeoutMilliseconds)
                {
                    return Result(ConsoleAttachmentOutcome.Attached, attachedToTarget: true, elapsed);
                }

                return ObserveTargetAndClassify(
                    attachedToTarget: true,
                    ConsoleAttachmentOutcome.TimedOut)!.Value;
            }

            lastAttachError = attempt.ErrorCode;
            var runningOutcome = lastAttachError == NativeMethods.ErrorInvalidHandle
                ? ConsoleAttachmentOutcome.Attached
                : ConsoleAttachmentOutcome.AttachFailed;
            var classified = ObserveTargetAndClassify(
                attachedToTarget: false,
                runningOutcome);
            if (classified.HasValue)
            {
                return classified.Value;
            }

            delay();
        }
    }
}

internal readonly record struct ConsoleAttachmentAttempt(bool Attached, int ErrorCode);

internal readonly record struct ConsoleTargetObservation(uint WaitResult, int ErrorCode)
{
    internal static ConsoleTargetObservation Unobserved { get; } = new(uint.MaxValue - 1, 0);
}

internal readonly record struct ConsoleAttachmentResult(
    ConsoleAttachmentOutcome Outcome,
    int LastAttachError,
    int Attempts,
    bool AttachedToTarget,
    ConsoleTargetObservation LastTargetObservation,
    long MaximumPollGapMilliseconds,
    long ElapsedMilliseconds);

internal enum ConsoleAttachmentOutcome
{
    Attached,
    AttachFailed,
    TargetExited,
    TargetWatchFailed,
    TimedOut,
}

internal static partial class NativeMethods
{
    internal const uint CtrlCEvent = 0;
    internal const int ErrorFileNotFound = 2;
    internal const int ErrorPathNotFound = 3;
    internal const int ErrorAccessDenied = 5;
    internal const int ErrorInvalidHandle = 6;
    internal const int ErrorSharingViolation = 32;
    internal const uint Synchronize = 0x00100000;
    internal const uint WaitObject0 = 0;
    internal const uint WaitTimeout = 258;
    internal const uint WaitFailed = uint.MaxValue;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial SafeFileHandle OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AttachConsole(uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FreeConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GenerateConsoleCtrlEvent(uint controlEvent, uint processGroupId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetConsoleCtrlHandler(IntPtr handlerRoutine, [MarshalAs(UnmanagedType.Bool)] bool add);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint WaitForSingleObject(SafeFileHandle handle, uint milliseconds);
}
