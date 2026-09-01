// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

var lockObservationTimeout = TimeSpan.FromSeconds(15);

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
_ = NativeMethods.FreeConsole();
if (!NativeMethods.AttachConsole(targetProcessId))
{
    Console.Error.WriteLine("console-signal.attach-failed");
    return 3;
}

try
{
    if (!NativeMethods.SetConsoleCtrlHandler(IntPtr.Zero, add: true))
    {
        Console.Error.WriteLine("console-signal.ignore-failed");
        return 4;
    }

    var observationWatch = Stopwatch.StartNew();
    while (!IsHeldBatchLock(batchLockPath))
    {
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

        if (observationWatch.Elapsed >= lockObservationTimeout)
        {
            Console.Error.WriteLine("console-signal.lock-observation-timeout");
            return 5;
        }

        Thread.Sleep(TimeSpan.FromMilliseconds(2));
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
    _ = NativeMethods.FreeConsole();
}

static bool IsHeldBatchLock(string path)
{
    try
    {
        using var unexpectedOpen = File.OpenHandle(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return false;
    }
    catch (IOException exception) when ((exception.HResult & 0xFFFF) == NativeMethods.ErrorSharingViolation)
    {
        return true;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        return false;
    }
}

internal static partial class NativeMethods
{
    internal const uint CtrlCEvent = 0;
    internal const int ErrorSharingViolation = 32;
    internal const uint Synchronize = 0x00100000;
    internal const uint WaitObject0 = 0;
    internal const uint WaitTimeout = 258;

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
