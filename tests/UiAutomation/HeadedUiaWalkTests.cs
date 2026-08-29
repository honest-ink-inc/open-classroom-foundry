// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.IO;
using System.Windows.Automation;

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

    private sealed class HeadedApp : IDisposable
    {
        public Process Process { get; }

        public AutomationElement Window { get; }

        public HeadedApp(string harnessMode)
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "Foundry.App.WinForms.exe");
            Assert.True(File.Exists(exe), $"App executable not beside the tests: {exe}");

            Process = Process.Start(new ProcessStartInfo(exe, $"--uia-harness {harnessMode}"))!;
            Window = WaitFor(() => AutomationElement.RootElement.FindFirst(
                TreeScope.Children,
                new PropertyCondition(AutomationElement.ProcessIdProperty, Process.Id)));
        }

        public void Dispose()
        {
            if (!Process.HasExited)
            {
                Process.Kill(entireProcessTree: true);
            }

            Process.Dispose();
        }
    }

    private static T WaitFor<T>(Func<T?> probe, int timeoutMs = 20000)
        where T : class
    {
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < timeoutMs)
        {
            if (probe() is { } found)
            {
                return found;
            }

            Thread.Sleep(200);
        }

        throw new TimeoutException("The UI Automation element did not appear in time.");
    }

    private static AutomationElement ByName(AutomationElement scope, ControlType type, string name)
        => WaitFor(() => scope.FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, type),
            new PropertyCondition(AutomationElement.NameProperty, name))));

    [HeadedFact]
    public void Part1_Steps1and2_the_review_window_exposes_only_named_roled_reachable_controls()
    {
        using var app = new HeadedApp("review");

        // Step 1: the title announces the product and the draft state.
        Assert.Contains(App.WinForms.ProductIdentity.PublicName, app.Window.Current.Name, StringComparison.Ordinal);
        Assert.Contains("draft", app.Window.Current.Name, StringComparison.OrdinalIgnoreCase);

        // Step 2: every keyboard-reachable element carries a name — over the
        // real UIA tree, not the WinForms model — and the document order (which
        // is the tab-cycle order) follows the visual logic: splitter, draft
        // list, splitter, editor, issues, then the action buttons. The window's
        // own system menu is OS chrome, excluded.
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
                "Splitter between the draft list and the editor",
                "Draft elements",
                "Splitter between the editor and the validation issues",
                "Selected element text",
                "Validation issues",
                "Apply edit",
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
        var list = ByName(app.Window, ControlType.List, "Draft elements");

        // Step 9: select a step, move it, and find selection on it at its NEW
        // position — the position a screen reader would announce.
        var items = list.FindAll(TreeScope.Children, new PropertyCondition(
            AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().ToList();
        Assert.Equal(5, items.Count); // heading + four steps, fixture-deterministic

        var second = items[2];
        var movedName = second.Current.Name;
        ((SelectionItemPattern)second.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        ((InvokePattern)ByName(app.Window, ControlType.Button, "Move down")
            .GetCurrentPattern(InvokePattern.Pattern)).Invoke();

        var after = WaitFor(() =>
        {
            var refreshed = list.FindAll(TreeScope.Children, new PropertyCondition(
                AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().ToList();
            return refreshed.Count == 5 && refreshed[3].Current.Name == movedName ? refreshed : null;
        });
        Assert.True(((SelectionItemPattern)after[3].GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected,
            "Selection did not follow the moved element to its new position");

        // Step 10: the labeled edit field accepts a change that reads back.
        var editor = ByName(app.Window, ControlType.Edit, "Selected element text");
        ((ValuePattern)editor.GetCurrentPattern(ValuePattern.Pattern)).SetValue("Water each plant twice.");
        ((InvokePattern)ByName(app.Window, ControlType.Button, "Apply edit")
            .GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        WaitFor(() => list.FindFirst(TreeScope.Children, new PropertyCondition(
            AutomationElement.NameProperty, "Paragraph: Water each plant twice.")));

        // Step 11's meaning-of-approval text is asserted in-process on
        // AccessibleDescription; the WinForms UIA provider does not surface it
        // as UIA HelpText, so whether AT actually speaks it stays with the
        // human walkthrough (see the traceability table).
        var approve = ByName(app.Window, ControlType.Button, "Approve");

        // Step 12: approving completes the review — the surface's state change.
        ((InvokePattern)approve.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        Assert.True(app.Process.WaitForExit(10000), "The review did not complete after approval");
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
        var graphPaper = WaitFor(() => presses.FindFirst(TreeScope.Children,
            new PropertyCondition(AutomationElement.NameProperty, "Graph paper")));
        ((SelectionItemPattern)graphPaper.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

        // The click completes immediately (the surface defers its modal to the
        // next message-loop beat precisely so automation is never wedged), and
        // the review dialog arrives as its own window. The legacy UIA client's
        // root enumeration misses windows born after it attached, so the
        // dialog is located by Win32 title walk and bridged via FromHandle.
        ((InvokePattern)ByName(app.Window, ControlType.Button, "Review and approve…")
            .GetCurrentPattern(InvokePattern.Pattern)).Invoke();

        var review = WaitFor(() =>
        {
            var handle = Win32WindowByTitle(app.Process.Id, "reviewing a draft");
            return handle == IntPtr.Zero ? null : AutomationElement.FromHandle(handle);
        });

        ((InvokePattern)ByName(review, ControlType.Button, "Approve")
            .GetCurrentPattern(InvokePattern.Pattern)).Invoke();

        // Approval unlocks the gated actions on the main window.
        var export = ByName(app.Window, ControlType.Button, "Export…");
        WaitFor(() => export.Current.IsEnabled ? export : null);
    }

    [HeadedFact]
    public void Part2_Steps5to7_all_aboard_typed_entry_reaches_approval_over_real_uia()
    {
        using var app = new HeadedApp("allaboard");

        Assert.Contains("All Aboard", app.Window.Current.Name, StringComparison.Ordinal);

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

        var review = WaitFor(() =>
        {
            var handle = Win32WindowByTitle(app.Process.Id, "reviewing a draft");
            return handle == IntPtr.Zero ? null : AutomationElement.FromHandle(handle);
        });

        ((InvokePattern)ByName(review, ControlType.Button, "Approve")
            .GetCurrentPattern(InvokePattern.Pattern)).Invoke();

        var export = ByName(app.Window, ControlType.Button, "Export…");
        WaitFor(() => export.Current.IsEnabled ? export : null);
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
