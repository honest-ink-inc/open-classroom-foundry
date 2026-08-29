// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Windows.Automation;

namespace Foundry.Tests.UiAutomation;

// The pilot-day dress rehearsal (fourth forge menu, item 1): the complete
// teacher loop — cold start, forge a press, Gate B, approve, save to the
// library, reopen, export the booklet PDF, low-ink variant — as ONE headed
// scenario over real UIA, so a seam that slips is a red test before it is a
// bad morning on pilot day. The print step asserts the gate and the status
// path, never paper: rehearsal machines and CI runners have no printer worth
// trusting. The library lives in a disposable directory the harness is
// pointed at; the teacher's real Documents are never touched. Neither shell
// file dialog is driven: the Save As dialog's name field cannot be committed
// by cross-process automation, and no lookup on the Open dialog earned trust
// under full-suite load (traceability findings 8-9) — so export and reopen
// both go through the Press Room's ctor seams, injected by the harness, and
// the rehearsal asserts only what is ours.
public sealed partial class HeadedUiaWalkTests
{
    private static AutomationElement DialogByTitle(HeadedApp app, string titleFragment)
        => WaitFor(() =>
        {
            var handle = Win32WindowByTitle(app.Process.Id, titleFragment);
            return handle == IntPtr.Zero ? null : AutomationElement.FromHandle(handle);
        });

    private static void InvokeButton(AutomationElement scope, string name)
        => ((InvokePattern)ByName(scope, ControlType.Button, name)
            .GetCurrentPattern(InvokePattern.Pattern)).Invoke();

    /// <summary>Gate B arrives as its own window; approving it is the teacher's signature.</summary>
    private static void ApproveGateB(HeadedApp app)
        => InvokeButton(DialogByTitle(app, "reviewing a draft"), "Approve");

    /// <summary>The speaking status line: waits until it says what the rehearsal expects.</summary>
    private static void AwaitStatus(AutomationElement window, string prefix)
        => WaitFor(() => window.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
            .Cast<AutomationElement>()
            .FirstOrDefault(e => e.Current.Name.StartsWith(prefix, StringComparison.Ordinal)));

    [HeadedFact]
    public void PilotDay_dress_rehearsal_cold_start_to_reopened_booklet_and_low_ink_over_real_uia()
    {
        var library = Path.Combine(
            Path.GetTempPath(),
            "ocf-rehearsal-" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(library);
        try
        {
            RunRehearsal(library);
        }
        finally
        {
            try
            {
                Directory.Delete(library, recursive: true);
            }
            catch (IOException)
            {
                // A straggling handle from the killed process must not turn a green rehearsal red.
            }
        }
    }

    private static void RunRehearsal(string library)
    {
        var booklet = Path.Combine(library, "rehearsal-booklet.pdf");
        using var app = new HeadedApp(
            $"pressroom {App.WinForms.UiaHarness.LibraryRootSwitch} \"{library}\""
            + $" {App.WinForms.UiaHarness.ExportToSwitch} \"{booklet}\"");

        // Cold start: the front door says its name, the status line speaks,
        // and the structural gate holds — nothing prints, exports, or saves
        // before a typed approval exists.
        Assert.Contains(App.WinForms.ProductIdentity.PublicName, app.Window.Current.Name, StringComparison.Ordinal);
        AwaitStatus(app.Window, "Choose a press");
        var export = ByName(app.Window, ControlType.Button, "Export…");
        var save = ByName(app.Window, ControlType.Button, "Save to library");
        var print = ByName(app.Window, ControlType.Button, "Print");
        Assert.False(export.Current.IsEnabled, "Export must be locked before approval");
        Assert.False(save.Current.IsEnabled, "Save to library must be locked before approval");
        Assert.False(print.Current.IsEnabled, "Print must be locked before approval");

        // Forge a press: Flashcards — two sheets at defaults, which the
        // booklet leg of the loop needs.
        var presses = ByName(app.Window, ControlType.List, "Presses");
        ((SelectionItemPattern)WaitFor(() => presses.FindFirst(
                TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "Flashcards")))
            .GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

        InvokeButton(app.Window, "Review and approve…");
        ApproveGateB(app);
        AwaitStatus(app.Window, "Approved —");
        WaitFor(() => export.Current.IsEnabled ? export : null);
        Assert.True(save.Current.IsEnabled, "Approval must unlock saving");
        Assert.True(print.Current.IsEnabled, "Approval must unlock printing — the gate asserted, not paper");

        // Save to the library the harness was pointed at.
        InvokeButton(app.Window, "Save to library");
        AwaitStatus(app.Window, "Saved to the library as");
        var project = WaitFor(() => Directory.EnumerateFiles(library, "*.ocfproj").FirstOrDefault());

        // Reopen: reversibility means a FRESH Gate B review, never an
        // inherited approval — the gate is structural, not hereditary. The
        // reopen goes through the libraryPicker seam the in-process tests
        // always used (the harness resolves the newest fixture project at
        // click time): no lookup on the shell Open dialog earned trust under
        // full-suite load — traceability finding 9 — and Microsoft's chrome
        // is not what this rehearsal guards.
        Assert.EndsWith(".ocfproj", project, StringComparison.Ordinal);
        InvokeButton(app.Window, "Open from library…");
        ApproveGateB(app);
        AwaitStatus(app.Window, "Approved —");

        // Export the booklet PDF from the REOPENED artifact: the round trip
        // must preserve everything the imposer needs. The destination comes
        // from the export seam the harness was launched with (see the header
        // note on why the Save As dialog itself is not drivable).
        InvokeButton(app.Window, "Export…");
        AwaitStatus(app.Window, "Exported to rehearsal-booklet.pdf");
        var bytes = File.ReadAllBytes(booklet);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5), StringComparison.Ordinal);

        // Low-ink variant: the transform applies BEFORE Gate B, so the
        // teacher reviews what will actually print — then approves it like
        // everything else.
        ((TogglePattern)WaitFor(() => app.Window.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
                new PropertyCondition(AutomationElement.NameProperty, "Low ink"))))
            .GetCurrentPattern(TogglePattern.Pattern)).Toggle();
        InvokeButton(app.Window, "Review and approve…");
        ApproveGateB(app);
        AwaitStatus(app.Window, "Approved —");
    }
}
