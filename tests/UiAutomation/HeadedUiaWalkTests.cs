// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Automation;
using Foundry.Modules.BuiltIn;

namespace Foundry.Tests.UiAutomation;

// The headed half of the accessibility harness: launch the real app in its
// deterministic --uia-harness fixture mode and read it back through UI
// Automation — the same tree NVDA and Narrator read. What these tests prove is
// exposure and operability over UIA; what speech actually sounds like remains
// the walkthrough's human half.

public sealed partial class HeadedUiaWalkTests
{
    private static partial class Native
    {
        [System.Runtime.InteropServices.LibraryImport("user32.dll")]
        public static partial IntPtr GetTopWindow(IntPtr handle);

        [System.Runtime.InteropServices.LibraryImport("user32.dll")]
        public static partial IntPtr GetWindow(IntPtr handle, uint command);

        [System.Runtime.InteropServices.LibraryImport("user32.dll")]
        public static partial uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

        [System.Runtime.InteropServices.LibraryImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static partial bool IsWindowVisible(IntPtr handle);

        [System.Runtime.InteropServices.LibraryImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static partial bool IsWindow(IntPtr handle);

        [System.Runtime.InteropServices.LibraryImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static partial bool IsWindowEnabled(IntPtr handle);

        [System.Runtime.InteropServices.LibraryImport("user32.dll", StringMarshalling = System.Runtime.InteropServices.StringMarshalling.Utf16)]
        public static partial int GetWindowTextW(IntPtr handle, ref char text, int max);
    }

    /// <summary>First visible top-level window of the process whose title contains the fragment; Win32-level, immune to UIA enumeration gaps.</summary>
    private static IntPtr Win32WindowByTitle(int processId, string titleFragment)
    {
        var handle = Native.GetTopWindow(IntPtr.Zero);
        while (handle != IntPtr.Zero)
        {
            _ = Native.GetWindowThreadProcessId(handle, out var pid);
            if (pid == (uint)processId && Native.IsWindowVisible(handle))
            {
                var buffer = new char[256];
                var length = Native.GetWindowTextW(handle, ref buffer[0], buffer.Length);
                if (new string(buffer, 0, Math.Max(0, length)).Contains(titleFragment, StringComparison.Ordinal))
                {
                    return handle;
                }
            }

            handle = Native.GetWindow(handle, 2); // GW_HWNDNEXT
        }

        return IntPtr.Zero;
    }

    [Flags]
    private enum NativeModalEvidence
    {
        None = 0,
        DialogExists = 1 << 0,
        ExpectedOwnerExists = 1 << 1,
        DialogVisible = 1 << 2,
        DialogEnabled = 1 << 3,
        ExactOwner = 1 << 4,
        ExpectedOwnerVisible = 1 << 5,
        ExpectedOwnerDisabled = 1 << 6,
        SameUiThread = 1 << 7,
        DialogInExpectedProcess = 1 << 8,
        OwnerInExpectedProcess = 1 << 9,
        Complete = DialogExists
            | ExpectedOwnerExists
            | DialogVisible
            | DialogEnabled
            | ExactOwner
            | ExpectedOwnerVisible
            | ExpectedOwnerDisabled
            | SameUiThread
            | DialogInExpectedProcess
            | OwnerInExpectedProcess,
    }

    /// <summary>
    /// Exact read-only proof of the native WinForms modal relationship used
    /// by these controlled ShowDialog routes. The dialog and expected owner
    /// share one process and UI thread; the visible, enabled dialog owns the
    /// disabled but still visible caller through GW_OWNER.
    /// </summary>
    private static NativeModalEvidence ReadNativeModalEvidence(
        IntPtr dialog,
        IntPtr expectedOwner,
        int expectedProcessId)
    {
        var evidence = NativeModalEvidence.None;
        var dialogExists = dialog != IntPtr.Zero && Native.IsWindow(dialog);
        var ownerExists = expectedOwner != IntPtr.Zero && Native.IsWindow(expectedOwner);
        if (dialogExists)
        {
            evidence |= NativeModalEvidence.DialogExists;
        }

        if (ownerExists)
        {
            evidence |= NativeModalEvidence.ExpectedOwnerExists;
        }

        if (dialogExists && Native.IsWindowVisible(dialog))
        {
            evidence |= NativeModalEvidence.DialogVisible;
        }

        if (dialogExists && Native.IsWindowEnabled(dialog))
        {
            evidence |= NativeModalEvidence.DialogEnabled;
        }

        if (dialogExists && ownerExists && Native.GetWindow(dialog, 4) == expectedOwner) // GW_OWNER
        {
            evidence |= NativeModalEvidence.ExactOwner;
        }

        if (ownerExists && Native.IsWindowVisible(expectedOwner))
        {
            evidence |= NativeModalEvidence.ExpectedOwnerVisible;
        }

        if (ownerExists && !Native.IsWindowEnabled(expectedOwner))
        {
            evidence |= NativeModalEvidence.ExpectedOwnerDisabled;
        }

        uint dialogProcess = 0;
        uint ownerProcess = 0;
        var dialogThread = dialogExists
            ? Native.GetWindowThreadProcessId(dialog, out dialogProcess)
            : 0;
        var ownerThread = ownerExists
            ? Native.GetWindowThreadProcessId(expectedOwner, out ownerProcess)
            : 0;
        if (dialogThread != 0 && dialogThread == ownerThread)
        {
            evidence |= NativeModalEvidence.SameUiThread;
        }

        if (dialogExists && dialogProcess == (uint)expectedProcessId)
        {
            evidence |= NativeModalEvidence.DialogInExpectedProcess;
        }

        if (ownerExists && ownerProcess == (uint)expectedProcessId)
        {
            evidence |= NativeModalEvidence.OwnerInExpectedProcess;
        }

        return evidence;
    }

    private static bool HasExactNativeModalRelationship(
        IntPtr dialog,
        IntPtr expectedOwner,
        int expectedProcessId)
        => ReadNativeModalEvidence(dialog, expectedOwner, expectedProcessId) == NativeModalEvidence.Complete;

    private sealed class HeadedApp : IDisposable
    {
        private readonly string? _ownedRehearsalRoot;

        public Process Process { get; }

        public AutomationElement Window { get; }

        public HeadedApp(string harnessMode, string? additionalArguments = null)
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "Foundry.App.WinForms.exe");
            Assert.True(File.Exists(exe), $"App executable not beside the tests: {exe}");

            var arguments = $"--uia-harness {harnessMode}";
            if (string.IsNullOrWhiteSpace(additionalArguments)
                && harnessMode is "pressroom" or "allaboard" or "modules")
            {
                _ownedRehearsalRoot = Path.Combine(
                    Path.GetTempPath(),
                    "ocf-rehearsal-" + Guid.NewGuid().ToString("N"));
                var library = Path.Combine(
                    _ownedRehearsalRoot,
                    Domain.EngineIdentity.EngineVersion,
                    "prepared-library");
                Directory.CreateDirectory(library);
                arguments += $" {App.WinForms.ProjectLibraryRootConfiguration.Switch} \"{library}\"";
            }

            if (!string.IsNullOrWhiteSpace(additionalArguments))
            {
                arguments += $" {additionalArguments}";
            }

            Process = Process.Start(new ProcessStartInfo(exe, arguments))!;
            var matchingWindows = 0;
            Window = WaitFor(() =>
            {
                var found = AutomationElement.RootElement.FindFirst(
                    TreeScope.Children,
                    new PropertyCondition(AutomationElement.ProcessIdProperty, Process.Id));
                matchingWindows = found is null ? 0 : 1;
                return found;
            },
                expectation: $"the top-level window for harness mode '{harnessMode}' (process {Process.Id})",
                diagnosticSnapshot: () => CachedWaitSnapshot(
                    Process,
                    "process launched",
                    ControlType.Window.ProgrammaticName,
                    candidates: null,
                    matches: matchingWindows));
        }

        public void Dispose()
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
                _ = Process.WaitForExit(5000);
            }

            Process.Dispose();
            if (_ownedRehearsalRoot is not null)
            {
                try
                {
                    Directory.Delete(_ownedRehearsalRoot, recursive: true);
                }
                catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
                {
                    // The process is already gone; temp cleanup must not hide
                    // the headed assertion that owns this fixture.
                }
            }
        }
    }

    private static T WaitFor<T>(
        Func<T?> probe,
        int timeoutMs = 20000,
        string? expectation = null,
        Func<string>? diagnosticSnapshot = null,
        [CallerArgumentExpression(nameof(probe))] string? probeExpression = null)
        where T : class
    {
        var clock = Stopwatch.StartNew();
        var probes = 0;
        while (clock.ElapsedMilliseconds < timeoutMs)
        {
            probes++;
            if (probe() is { } found)
            {
                return found;
            }

            Thread.Sleep(200);
        }

        var awaited = expectation ?? probeExpression ?? "the requested UI Automation condition";
        var snapshot = CaptureDiagnosticSnapshot(diagnosticSnapshot);
        throw new TimeoutException(
            $"Timed out after {clock.ElapsedMilliseconds} ms and {probes} probes waiting for {awaited}. "
            + $"Content-free diagnostic snapshot: {snapshot}.");
    }

    private static string CaptureDiagnosticSnapshot(Func<string>? diagnosticSnapshot)
    {
        if (diagnosticSnapshot is null)
        {
            return "not supplied";
        }

        try
        {
            // This delegate formats counters cached by the probe and may read
            // the owning local process state. It must not traverse UIA or the
            // filesystem after the owning wait expires.
            return diagnosticSnapshot();
        }
        catch (Exception failure)
        {
            // Never copy a provider message that may contain authored UI text.
            return $"unavailable ({failure.GetType().Name})";
        }
    }

    private static string CachedWaitSnapshot(
        Process process,
        string lastTransition,
        string expectedType,
        int? candidates,
        int? matches)
    {
        var candidateCount = candidates?.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ?? "not measured";
        var matchCount = matches?.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ?? "not measured";
        return $"last transition={lastTransition}; process={ProcessState(process)}; "
            + $"expected={expectedType}; candidates={candidateCount}; matches={matchCount}";
    }

    private static string ProcessState(Process process)
    {
        try
        {
            return process.HasExited
                ? $"exited with code {process.ExitCode}"
                : "running";
        }
        catch (InvalidOperationException)
        {
            return "state unavailable";
        }
    }

    private static AutomationElement ByName(
        AutomationElement scope,
        ControlType type,
        string name,
        Process? process = null,
        string? lastTransition = null)
    {
        var candidateCount = 0;
        var matchCount = 0;
        return WaitFor(() =>
        {
            if (process is null)
            {
                return scope.FindFirst(TreeScope.Descendants, new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, type),
                    new PropertyCondition(AutomationElement.NameProperty, name)));
            }

            var candidates = scope.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, type))
                .Cast<AutomationElement>()
                .ToList();
            candidateCount = candidates.Count;
            var match = candidates.FirstOrDefault(candidate => candidate.Current.Name == name);
            matchCount = match is null ? 0 : 1;
            return match;
        },
            expectation: $"a {type.ProgrammaticName} named '{name}'",
            diagnosticSnapshot: process is null
                ? null
                : () => CachedWaitSnapshot(
                    process,
                    lastTransition ?? "named control requested",
                    type.ProgrammaticName,
                    candidateCount,
                    matchCount));
    }

    [Fact]
    public void WaitFor_timeout_retains_bounded_content_free_diagnostics()
    {
        var retained = Assert.Throws<TimeoutException>(() => WaitFor<object>(
            () => null,
            timeoutMs: 0,
            expectation: "the synthetic condition",
            diagnosticSnapshot: () =>
                "last transition=synthetic action; process=running; expected=Button; candidates=0; matches=0"));
        Assert.Contains("0 probes", retained.Message, StringComparison.Ordinal);
        Assert.Contains("last transition=synthetic action", retained.Message, StringComparison.Ordinal);

        var sanitized = Assert.Throws<TimeoutException>(() => WaitFor<object>(
            () => null,
            timeoutMs: 0,
            expectation: "the synthetic condition",
            diagnosticSnapshot: () => throw new InvalidOperationException("authored text must not escape")));
        Assert.Contains("unavailable (InvalidOperationException)", sanitized.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("authored text must not escape", sanitized.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Native_modal_proof_requires_the_exact_visible_disabled_owner()
        => Sta.Run(() =>
        {
            using var owner = new Form();
            using var otherOwner = new Form();
            using var dialog = new Form();
            owner.Show();
            otherOwner.Show();

            Assert.False(HasExactNativeModalRelationship(
                dialog.Handle,
                owner.Handle,
                Environment.ProcessId));
            dialog.Shown += (_, _) =>
            {
                Assert.True(HasExactNativeModalRelationship(
                    dialog.Handle,
                    owner.Handle,
                    Environment.ProcessId));
                Assert.False(HasExactNativeModalRelationship(
                    dialog.Handle,
                    otherOwner.Handle,
                    Environment.ProcessId));
                dialog.DialogResult = DialogResult.Cancel;
            };

            Assert.Equal(DialogResult.Cancel, dialog.ShowDialog(owner));
            Assert.True(Native.IsWindowEnabled(owner.Handle));
            Assert.True(Native.IsWindowEnabled(otherOwner.Handle));
        });

    [HeadedFact]
    public void Part1_Steps1and2_the_review_window_exposes_only_named_roled_reachable_controls()
    {
        using var app = new HeadedApp("review");

        // Step 1: the title announces the product and the draft state.
        Assert.Contains(App.WinForms.ProductIdentity.PublicName, app.Window.Current.Name, StringComparison.Ordinal);
        Assert.Contains("draft", app.Window.Current.Name, StringComparison.OrdinalIgnoreCase);

        // Step 2: every keyboard-reachable element carries a name — over the
        // real UIA tree, not the WinForms model — and the document order (which
        // is the tab-cycle order) follows the visual logic: the review tabs and
        // selected tab, splitter, draft list, splitter, editor, issues, then the
        // action buttons. The window's own system menu is OS chrome, excluded.
        var focusable = app.Window.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.IsKeyboardFocusableProperty, true))
            .Cast<AutomationElement>()
            .Where(e => e.Current.ControlType != ControlType.MenuBar
                && e.Current.ControlType != ControlType.MenuItem
                && e.Current.ControlType != ControlType.ListItem)
            .ToList();

        Assert.All(focusable, element => Assert.False(
            string.IsNullOrWhiteSpace(element.Current.Name),
            $"Unnamed keyboard-focusable {element.Current.ControlType.ProgrammaticName} — the 'unnamed pane' failure of walkthrough step 2"));

        Assert.Equal(
            [
                App.WinForms.UiStrings.ReviewWindowTitle,
                App.WinForms.UiStrings.ReviewElementsTab.Replace("&", string.Empty, StringComparison.Ordinal),
                "Splitter between the draft list and the editor",
                "Draft elements",
                "Splitter between the editor and the validation issues",
                "Selected element text",
                "Validation issues",
                "Selected validation issue detail",
                App.WinForms.UiStrings.ReviewElementsTab,
                App.WinForms.UiStrings.SourceComparisonTab,
                App.WinForms.UiStrings.VisualPreviewTab,
                "Apply edit",
                "Edit element…",
                "Remove element",
                "Move up",
                "Move down",
                "Approve",
                "Reject",
            ],
            focusable.Select(e => e.Current.Name).ToList());
    }

    [HeadedFact]
    public void Part3_Steps9to12_move_edit_and_approve_operate_through_uia_patterns()
    {
        using var app = new HeadedApp("review");
        var list = ByName(
            app.Window,
            ControlType.List,
            "Draft elements",
            app.Process,
            "Part3 review opened");

        // Step 9: select a step, move it, and find selection on it at its NEW
        // position — the position a screen reader would announce.
        var items = list.FindAll(TreeScope.Children, new PropertyCondition(
            AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().ToList();
        Assert.Equal(5, items.Count); // heading + four steps, fixture-deterministic

        var second = items[2];
        var movedName = second.Current.Name;
        ((SelectionItemPattern)second.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        ((InvokePattern)ByName(
                app.Window,
                ControlType.Button,
                "Move down",
                app.Process,
                "Part3 step item selected")
            .GetCurrentPattern(InvokePattern.Pattern)).Invoke();

        var moveCandidates = 0;
        var moveMatches = 0;
        var after = WaitFor(() =>
        {
            var refreshed = list.FindAll(TreeScope.Children, new PropertyCondition(
                AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().ToList();
            moveCandidates = refreshed.Count;
            moveMatches = refreshed.Count == 5 && refreshed[3].Current.Name == movedName ? 1 : 0;
            return moveMatches == 1 ? refreshed : null;
        },
            expectation: "the moved item in position four during Part3 step 9",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                "Move down invoked",
                ControlType.ListItem.ProgrammaticName,
                moveCandidates,
                moveMatches));
        Assert.True(((SelectionItemPattern)after[3].GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected,
            "Selection did not follow the moved element to its new position");

        // Step 10: the labeled edit field accepts a change that reads back.
        var editor = ByName(
            app.Window,
            ControlType.Edit,
            "Selected element text",
            app.Process,
            "Part3 move completed");
        ((ValuePattern)editor.GetCurrentPattern(ValuePattern.Pattern)).SetValue("Water each plant twice.");
        ((InvokePattern)ByName(
                app.Window,
                ControlType.Button,
                "Apply edit",
                app.Process,
                "Part3 edited text entered")
            .GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        var editCandidates = 0;
        var editMatches = 0;
        WaitFor(() =>
        {
            var refreshed = list.FindAll(TreeScope.Children, new PropertyCondition(
                AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().ToList();
            editCandidates = refreshed.Count;
            var match = refreshed.FirstOrDefault(candidate =>
                candidate.Current.Name == "Paragraph: Water each plant twice.");
            editMatches = match is null ? 0 : 1;
            return match;
        },
            expectation: "the edited paragraph during Part3 step 10",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                "Apply edit invoked",
                ControlType.ListItem.ProgrammaticName,
                editCandidates,
                editMatches));

        // Step 11's meaning-of-approval text is asserted in-process on
        // AccessibleDescription; the WinForms UIA provider does not surface it
        // as UIA HelpText, so whether AT actually speaks it stays with the
        // human walkthrough (see the traceability table).
        var approve = ByName(
            app.Window,
            ControlType.Button,
            "Approve",
            app.Process,
            "Part3 edited paragraph observed");

        // Step 12: approving completes the review — the surface's state change.
        const int unexpectedFailure = unchecked((int)0x8000FFFF);
        int? approvalInvokeFailure = null;
        try
        {
            // Approval is non-idempotent. Invoke exactly once even when the
            // provider loses the destroyed final HWND while returning.
            ((InvokePattern)approve.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        }
        catch (System.Runtime.InteropServices.COMException failure)
            when (failure.HResult == unexpectedFailure)
        {
            approvalInvokeFailure = failure.HResult;
        }

        var processExitMatches = 0;
        var exited = WaitFor(() =>
        {
            processExitMatches = app.Process.HasExited ? 1 : 0;
            return processExitMatches == 1 ? app.Process : null;
        },
            expectation: "the Part3 review process to exit after approval",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                approvalInvokeFailure.HasValue
                    ? $"Part3 approval Invoke returned HRESULT 0x{approvalInvokeFailure.Value:X8}; no retry issued"
                    : "Part3 approval invoked",
                "process exit",
                candidates: 1,
                matches: processExitMatches));
        Assert.True(
            exited.ExitCode == 0,
            approvalInvokeFailure.HasValue
                ? $"Part3 approval Invoke returned HRESULT 0x{approvalInvokeFailure.Value:X8}; process exited with code {exited.ExitCode}; no retry issued."
                : $"Part3 approval process exited with code {exited.ExitCode}.");
    }

    [HeadedFact]
    public void PressRoom_cold_start_press_review_approve_over_real_uia()
    {
        using var app = new HeadedApp("pressroom");

        // The main window is the product's front door and says so.
        Assert.Contains(App.WinForms.ProductIdentity.PublicName, app.Window.Current.Name, StringComparison.Ordinal);

        // Every keyboard-reachable control is named — no anonymous pane.
        var focusable = app.Window.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.IsKeyboardFocusableProperty, true))
            .Cast<AutomationElement>()
            .Where(e => e.Current.ControlType != ControlType.MenuBar
                && e.Current.ControlType != ControlType.MenuItem
                && e.Current.ControlType != ControlType.ListItem)
            .ToList();
        Assert.All(focusable, element => Assert.False(
            string.IsNullOrWhiteSpace(element.Current.Name),
            $"Unnamed keyboard-focusable {element.Current.ControlType.ProgrammaticName}"));

        // Choose a press, review it, approve it — the whole road to a typed
        // approval, driven through the same tree a screen reader uses.
        var presses = ByName(app.Window, ControlType.List, "Presses");
        var pressMatches = 0;
        var graphPaper = WaitFor(() =>
        {
            var match = presses.FindFirst(TreeScope.Children,
                new PropertyCondition(AutomationElement.NameProperty, "Graph paper"));
            pressMatches = match is null ? 0 : 1;
            return match;
        },
            expectation: "the Graph paper press item",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                "Press Room opened",
                ControlType.ListItem.ProgrammaticName,
                candidates: null,
                matches: pressMatches));
        ((SelectionItemPattern)graphPaper.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

        // The click completes immediately (the surface defers its modal to the
        // next message-loop beat precisely so automation is never wedged), and
        // the review dialog arrives as its own window. The legacy UIA client's
        // root enumeration misses windows born after it attached, so the
        // dialog is located by Win32 title walk and bridged via FromHandle.
        ((InvokePattern)ByName(app.Window, ControlType.Button, "Review and approve…")
            .GetCurrentPattern(InvokePattern.Pattern)).Invoke();

        ApproveGateB(app, app.Window);

        // Approval unlocks the gated actions on the main window.
        var export = ByName(app.Window, ControlType.Button, "Export…");
        var exportMatches = 0;
        WaitFor(() =>
        {
            exportMatches = export.Current.IsEnabled ? 1 : 0;
            return exportMatches == 1 ? export : null;
        },
            expectation: "Export to unlock after Press Room approval",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                "Gate B approval invoked",
                ControlType.Button.ProgrammaticName,
                candidates: null,
                matches: exportMatches));
    }

    [HeadedFact]
    public void Part2_Steps5to7_all_aboard_typed_entry_reaches_approval_over_real_uia()
    {
        using var app = new HeadedApp("allaboard");

        Assert.Contains(
            ModulePublicIdentity.VisualSupport.DisplayName,
            app.Window.Current.Name,
            StringComparison.Ordinal);

        ((ValuePattern)ByName(app.Window, ControlType.Edit, "Task title")
            .GetCurrentPattern(ValuePattern.Pattern)).SetValue("Watering the class plants");
        ((ValuePattern)ByName(app.Window, ControlType.Edit, "Step 1 text")
            .GetCurrentPattern(ValuePattern.Pattern)).SetValue("Pick up the watering can.");
        ((ValuePattern)ByName(app.Window, ControlType.Edit, "Step 2 text")
            .GetCurrentPattern(ValuePattern.Pattern)).SetValue("Fill it to the line.");
        ((ValuePattern)ByName(app.Window, ControlType.Edit, "Step 3 text")
            .GetCurrentPattern(ValuePattern.Pattern)).SetValue("Water each plant once.");

        ((InvokePattern)ByName(app.Window, ControlType.Button, "Review and approve…")
            .GetCurrentPattern(InvokePattern.Pattern)).Invoke();

        ApproveGateB(app, app.Window);

        var export = ByName(app.Window, ControlType.Button, "Export…");
        var exportMatches = 0;
        WaitFor(() =>
        {
            exportMatches = export.Current.IsEnabled ? 1 : 0;
            return exportMatches == 1 ? export : null;
        },
            expectation: $"Export to unlock after {ModulePublicIdentity.VisualSupport.DisplayName} approval",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                "Gate B approval invoked",
                ControlType.Button.ProgrammaticName,
                candidates: null,
                matches: exportMatches));
    }

    [HeadedFact]
    public void Part4_Steps14and15_the_capture_window_speaks_lane_meaning_state_and_the_safety_pause()
    {
        using var app = new HeadedApp("capture");

        var amber = ByName(app.Window, ControlType.RadioButton, "May include learners or their work — keep Amber");
        var green = ByName(app.Window, ControlType.RadioButton, "Staged materials or empty space — Green (my attestation)");

        // Step 14: name, role, and checked state all discoverable over UIA.
        Assert.True(((SelectionItemPattern)amber.GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected,
            "Amber is the default lane and its checked state must be exposed");
        Assert.False(((SelectionItemPattern)green.GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected);

        // Step 15: the safety pause is present and names its full purpose.
        var pause = ByName(app.Window, ControlType.Button, "I saw something concerning — pause here");
        Assert.True(pause.Current.IsKeyboardFocusable, "Gate C must be keyboard reachable — an invisible Gate C is a failed Gate C");
    }
}
