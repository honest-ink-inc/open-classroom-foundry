// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Foundry.Tools.ProjectUpgradeHost;

/// <summary>
/// Makes this host's cooperative cancellation actually reachable.
///
/// <para>Windows keeps a per-process "ignore CTRL+C" flag, set by
/// <c>SetConsoleCtrlHandler(NULL, TRUE)</c>. Two properties of that flag matter here:
/// a child process <b>inherits it at creation</b>, and the console subsystem consults
/// it <b>ahead of every registered handler</b>. So a host launched by any parent that
/// set the flag is silently uncancellable — <c>GenerateConsoleCtrlEvent</c> reports
/// success, the event reaches the console, and no handler in this process ever runs.
/// Nothing about the failure is visible from inside the handler, because the handler
/// is never called.</para>
///
/// <para>That is sighting S-21, diagnosed 11 September 2026. It reproduced on demand
/// on a workstation whose test-runner chain carried the flag, and never on a hosted
/// runner whose chain did not, which is exactly the shape an inherited process flag
/// produces. The host declares that a delivered CTRL+C runs the compatibility
/// service's all-or-nothing cleanup through its token; clearing an inherited ignore
/// is what makes that declaration true regardless of who launched it.</para>
/// </summary>
internal static partial class ConsoleCancellation
{
    /// <summary>
    /// Clears an inherited ignore so this process's own handler governs CTRL+C.
    /// Returns false when the clear failed; the caller proceeds either way, because a
    /// host that cannot clear the flag is no worse off than one that never tried.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static bool RestoreInheritedCancellation()
        => NativeMethods.SetConsoleCtrlHandler(IntPtr.Zero, add: false);

    private static partial class NativeMethods
    {
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetConsoleCtrlHandler(
            IntPtr handlerRoutine,
            [MarshalAs(UnmanagedType.Bool)] bool add);
    }
}
