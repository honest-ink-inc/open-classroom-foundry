// SPDX-License-Identifier: GPL-3.0-or-later
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Foundry.Tests.Integration;

/// <summary>
/// Starts one test-owned process in an isolated, hidden Windows console. The
/// isolation lets a separate sender broadcast CTRL+C without signaling the
/// test runner or any unrelated console process.
/// </summary>
internal sealed partial class WindowsConsoleProcess : IDisposable
{
    private const uint CreateNewConsole = 0x00000010;
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint CreateAlways = 2;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint StartfUseShowWindow = 0x00000001;
    private const uint StartfUseStdHandles = 0x00000100;
    private const short SwHide = 0;
    private const uint WaitObject0 = 0;
    private const uint WaitTimeout = 258;
    private const int ErrorInsufficientBuffer = 122;
    private static readonly nuint ProcThreadAttributeHandleList = 0x00020002;

    private readonly SafeFileHandle _processHandle;
    private bool _disposed;

    private WindowsConsoleProcess(SafeFileHandle processHandle, uint processId)
    {
        _processHandle = processHandle;
        ProcessId = processId;
    }

    internal uint ProcessId { get; }

    internal bool HasExited => Wait(TimeSpan.Zero);

    internal int ExitCode
    {
        get
        {
            if (!HasExited)
            {
                throw new InvalidOperationException("The isolated console process is still running.");
            }

            if (!NativeMethods.GetExitCodeProcess(_processHandle, out var exitCode))
            {
                throw LastWin32Exception("Could not read the isolated console process exit code.");
            }

            return unchecked((int)exitCode);
        }
    }

    internal static WindowsConsoleProcess Start(
        string applicationPath,
        IReadOnlyList<string> arguments,
        string standardOutputPath,
        string standardErrorPath,
        string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationPath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(standardOutputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(standardErrorPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The isolated console process exercise requires Windows.");
        }

        using var standardInput = CreateInheritableFile(
            "NUL",
            GenericRead,
            FileShareRead | FileShareWrite,
            OpenExisting);
        using var standardOutput = CreateInheritableFile(
            standardOutputPath,
            GenericWrite,
            FileShareRead | FileShareWrite,
            CreateAlways);
        using var standardError = CreateInheritableFile(
            standardErrorPath,
            GenericWrite,
            FileShareRead | FileShareWrite,
            CreateAlways);

        using var inheritedHandles = new ProcessThreadHandleList(
            standardInput,
            standardOutput,
            standardError);
        var startup = new StartupInfoEx
        {
            StartupInfo = new StartupInfo
            {
                Size = Marshal.SizeOf<StartupInfoEx>(),
                Flags = StartfUseShowWindow | StartfUseStdHandles,
                ShowWindow = SwHide,
                StandardInput = standardInput.DangerousGetHandle(),
                StandardOutput = standardOutput.DangerousGetHandle(),
                StandardError = standardError.DangerousGetHandle(),
            },
            AttributeList = inheritedHandles.AttributeList,
        };
        var commandLine = (string.Join(
            ' ',
            new[] { applicationPath }.Concat(arguments).Select(QuoteArgument)) + '\0').ToCharArray();

        if (!NativeMethods.CreateProcess(
                applicationPath,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                inheritHandles: true,
                CreateNewConsole | ExtendedStartupInfoPresent,
                IntPtr.Zero,
                workingDirectory,
                ref startup,
                out var processInformation))
        {
            throw LastWin32Exception("Could not start the isolated console process.");
        }

        using var threadHandle = new SafeFileHandle(processInformation.Thread, ownsHandle: true);
        return new WindowsConsoleProcess(
            new SafeFileHandle(processInformation.Process, ownsHandle: true),
            processInformation.ProcessId);
    }

    internal bool Wait(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var milliseconds = timeout == Timeout.InfiniteTimeSpan
            ? uint.MaxValue
            : checked((uint)Math.Clamp(Math.Ceiling(timeout.TotalMilliseconds), 0, uint.MaxValue - 1));
        var result = NativeMethods.WaitForSingleObject(_processHandle, milliseconds);
        return result switch
        {
            WaitObject0 => true,
            WaitTimeout => false,
            _ => throw LastWin32Exception("Could not wait for the isolated console process."),
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Exception? cleanupFailure = null;
        try
        {
            if (!Wait(TimeSpan.Zero))
            {
                if (!NativeMethods.TerminateProcess(_processHandle, 1))
                {
                    var terminateFailure = LastWin32Exception("Could not terminate the isolated console process.");
                    if (!Wait(TimeSpan.Zero))
                    {
                        cleanupFailure = terminateFailure;
                    }
                }

                if (cleanupFailure is null && !Wait(TimeSpan.FromSeconds(5)))
                {
                    cleanupFailure = new TimeoutException(
                        "The isolated console process did not exit within five seconds after forced termination.");
                }
            }
        }
        catch (Exception exception)
        {
            cleanupFailure = exception;
        }
        finally
        {
            _disposed = true;
            _processHandle.Dispose();
        }

        if (cleanupFailure is not null)
        {
            throw cleanupFailure;
        }
    }

    /// <summary>
    /// Creates a test-owned inheritable handle that must not cross the exact
    /// handle list supplied by <see cref="Start"/>. A caller can close this
    /// handle and delete the file while the child is alive to prove that the
    /// child did not inherit the unrelated handle.
    /// </summary>
    internal static SafeFileHandle CreateInheritanceProbe(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return CreateInheritableFile(
            path,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            CreateAlways);
    }

    private static SafeFileHandle CreateInheritableFile(
        string path,
        uint desiredAccess,
        uint shareMode,
        uint creationDisposition)
    {
        var security = new SecurityAttributes
        {
            Length = Marshal.SizeOf<SecurityAttributes>(),
            InheritHandle = true,
        };
        var handle = NativeMethods.CreateFile(
            path,
            desiredAccess,
            shareMode,
            ref security,
            creationDisposition,
            FileAttributeNormal,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw LastWin32Exception("Could not create an isolated console stream.");
        }

        return handle;
    }

    private static string QuoteArgument(string argument)
    {
        if (argument.Contains('"', StringComparison.Ordinal))
        {
            throw new ArgumentException("A console-process argument cannot contain a quote.", nameof(argument));
        }

        return $"\"{argument}\"";
    }

    private static Win32Exception LastWin32Exception(string message)
        => new(Marshal.GetLastWin32Error(), message);

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        internal int Length;
        internal IntPtr SecurityDescriptor;

        [MarshalAs(UnmanagedType.Bool)]
        internal bool InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        internal int Size;
        internal IntPtr Reserved;
        internal IntPtr Desktop;
        internal IntPtr Title;
        internal int X;
        internal int Y;
        internal int XSize;
        internal int YSize;
        internal int XCountChars;
        internal int YCountChars;
        internal int FillAttribute;
        internal uint Flags;
        internal short ShowWindow;
        internal short ReservedByteCount;
        internal IntPtr ReservedBytes;
        internal IntPtr StandardInput;
        internal IntPtr StandardOutput;
        internal IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfoEx
    {
        internal StartupInfo StartupInfo;
        internal IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        internal IntPtr Process;
        internal IntPtr Thread;
        internal uint ProcessId;
        internal uint ThreadId;
    }

    private sealed class ProcessThreadHandleList : IDisposable
    {
        private readonly IntPtr _handles;
        private readonly bool _attributeListInitialized;
        private bool _disposed;

        internal ProcessThreadHandleList(params SafeFileHandle[] handles)
        {
            ArgumentNullException.ThrowIfNull(handles);
            if (handles.Length == 0 || handles.Any(handle => handle is null || handle.IsInvalid || handle.IsClosed))
            {
                throw new ArgumentException("The inherited console handle list is invalid.", nameof(handles));
            }

            nuint attributeListSize = 0;
            var firstCallSucceeded = NativeMethods.InitializeProcThreadAttributeList(
                IntPtr.Zero,
                attributeCount: 1,
                flags: 0,
                ref attributeListSize);
            var sizingError = Marshal.GetLastWin32Error();
            if (firstCallSucceeded || sizingError != ErrorInsufficientBuffer || attributeListSize == 0)
            {
                throw new Win32Exception(
                    sizingError,
                    "Could not size the isolated console process handle list.");
            }

            AttributeList = Marshal.AllocHGlobal(checked((nint)attributeListSize));
            try
            {
                if (!NativeMethods.InitializeProcThreadAttributeList(
                        AttributeList,
                        attributeCount: 1,
                        flags: 0,
                        ref attributeListSize))
                {
                    throw LastWin32Exception("Could not initialize the isolated console process handle list.");
                }

                _attributeListInitialized = true;
                _handles = Marshal.AllocHGlobal(checked(handles.Length * IntPtr.Size));
                for (var index = 0; index < handles.Length; index++)
                {
                    Marshal.WriteIntPtr(_handles, index * IntPtr.Size, handles[index].DangerousGetHandle());
                }

                if (!NativeMethods.UpdateProcThreadAttribute(
                        AttributeList,
                        flags: 0,
                        ProcThreadAttributeHandleList,
                        _handles,
                        checked((nuint)(handles.Length * IntPtr.Size)),
                        IntPtr.Zero,
                        IntPtr.Zero))
                {
                    throw LastWin32Exception("Could not constrain the isolated console process handle list.");
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal IntPtr AttributeList { get; private set; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_attributeListInitialized)
            {
                NativeMethods.DeleteProcThreadAttributeList(AttributeList);
            }

            if (_handles != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_handles);
            }

            if (AttributeList != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(AttributeList);
                AttributeList = IntPtr.Zero;
            }

            _disposed = true;
        }
    }

    private static partial class NativeMethods
    {
        // CreateProcess mutates its command buffer and the stream declarations use
        // SafeHandle marshalling. Match the repository's established runtime-
        // marshalling exception for signatures that are not LibraryImport-safe.
#pragma warning disable SYSLIB1054
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            ref SecurityAttributes securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcess(
            string applicationName,
            [In, Out] char[] commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string currentDirectory,
            ref StartupInfoEx startupInfo,
            out ProcessInformation processInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool InitializeProcThreadAttributeList(
            IntPtr attributeList,
            int attributeCount,
            uint flags,
            ref nuint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateProcThreadAttribute(
            IntPtr attributeList,
            uint flags,
            nuint attribute,
            IntPtr value,
            nuint size,
            IntPtr previousValue,
            IntPtr returnSize);

        [DllImport("kernel32.dll")]
        internal static extern void DeleteProcThreadAttributeList(IntPtr attributeList);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetExitCodeProcess(SafeFileHandle process, out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TerminateProcess(SafeFileHandle process, uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(SafeFileHandle handle, uint milliseconds);
#pragma warning restore SYSLIB1054
    }
}
