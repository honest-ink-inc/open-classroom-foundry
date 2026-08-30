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
    private static AutomationElement DialogByTitle(
        HeadedApp app,
        AutomationElement expectedOwner,
        string titleFragment,
        string lastTransition)
    {
        var expectedOwnerHandle = new IntPtr(expectedOwner.Current.NativeWindowHandle);
        Assert.NotEqual(IntPtr.Zero, expectedOwnerHandle);
        var dialogMatches = 0;
        var nativeModalMatches = 0;
        var nativeModalEvidence = NativeModalEvidence.None;
        return WaitFor(() =>
        {
            var handle = Win32WindowByTitle(app.Process.Id, titleFragment);
            dialogMatches = handle == IntPtr.Zero ? 0 : 1;
            if (dialogMatches == 0)
            {
                nativeModalMatches = 0;
                nativeModalEvidence = NativeModalEvidence.None;
                return null;
            }

            nativeModalEvidence = ReadNativeModalEvidence(
                handle,
                expectedOwnerHandle,
                app.Process.Id);
            nativeModalMatches = nativeModalEvidence == NativeModalEvidence.Complete ? 1 : 0;
            return nativeModalMatches == 1 ? AutomationElement.FromHandle(handle) : null;
        },
            expectation: $"an enabled dialog owned by the disabled expected window whose title contains '{titleFragment}'",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                $"{lastTransition}; native modal evidence={nativeModalEvidence}",
                ControlType.Window.ProgrammaticName,
                candidates: dialogMatches,
                matches: nativeModalMatches));
    }

    private static void InvokeButton(AutomationElement scope, string name)
        => ((InvokePattern)ByName(scope, ControlType.Button, name)
            .GetCurrentPattern(InvokePattern.Pattern)).Invoke();

    /// <summary>Gate B arrives as its own window; approving it is the teacher's signature.</summary>
    private static void ApproveGateB(HeadedApp app, AutomationElement expectedOwner)
    {
        var dialog = DialogByTitle(app, expectedOwner, "reviewing a draft", "Review and approve invoked");
        var approve = ByName(
            dialog,
            ControlType.Button,
            "Approve",
            app.Process,
            "Gate B modal observed");

        var acknowledgementCandidates = 0;
        var acknowledgementMatches = 0;
        var approveEnabled = 0;
        var acknowledgementOrReadyApproval = WaitFor(() =>
        {
            var candidates = dialog.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox))
                .Cast<AutomationElement>()
                .ToList();
            acknowledgementCandidates = candidates.Count;
            var acknowledgement = candidates.FirstOrDefault(candidate =>
                candidate.Current.Name == "I have reviewed the non-dismissable warnings");
            acknowledgementMatches = acknowledgement is null ? 0 : 1;
            approveEnabled = approve.Current.IsEnabled ? 1 : 0;
            return acknowledgement ?? (approveEnabled == 1 ? approve : null);
        },
            expectation: "the Gate B warning acknowledgement or a ready approval",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                "Gate B modal observed",
                "warning acknowledgement or enabled approval",
                acknowledgementCandidates + 1,
                acknowledgementMatches + approveEnabled));

        if (acknowledgementOrReadyApproval.Current.ControlType == ControlType.CheckBox)
        {
            var toggle = (TogglePattern)acknowledgementOrReadyApproval.GetCurrentPattern(TogglePattern.Pattern);
            var initialState = toggle.Current.ToggleState;
            Assert.True(
                initialState is ToggleState.Off or ToggleState.On,
                "The Gate B warning acknowledgement must expose a determinate toggle state.");
            if (initialState == ToggleState.Off)
            {
                // Toggle is not idempotent: issue it once, then observe only.
                toggle.Toggle();
            }

            var acknowledgedCandidates = 0;
            var acknowledgedMatches = initialState == ToggleState.On ? 1 : 0;
            _ = WaitFor(() =>
            {
                var candidates = dialog.FindAll(
                        TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox))
                    .Cast<AutomationElement>()
                    .ToList();
                acknowledgedCandidates = candidates.Count;
                var refreshed = candidates.FirstOrDefault(candidate =>
                    candidate.Current.Name == "I have reviewed the non-dismissable warnings");
                acknowledgedMatches = refreshed is not null
                    && ((TogglePattern)refreshed.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState == ToggleState.On
                        ? 1
                        : 0;
                return acknowledgedMatches == 1 ? refreshed : null;
            },
                expectation: "the Gate B warning acknowledgement to read back as selected",
                diagnosticSnapshot: () => CachedWaitSnapshot(
                    app.Process,
                    "Gate B warning acknowledgement toggled once",
                    ControlType.CheckBox.ProgrammaticName,
                    candidates: acknowledgedCandidates,
                    matches: acknowledgedMatches));
        }

        var approvalCandidates = 0;
        var approvalMatches = 0;
        var readyApprove = WaitFor(() =>
        {
            var candidates = dialog.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
                .Cast<AutomationElement>()
                .ToList();
            approvalCandidates = candidates.Count;
            var candidate = candidates.FirstOrDefault(button => button.Current.Name == "Approve");
            approvalMatches = candidate is not null && candidate.Current.IsEnabled ? 1 : 0;
            return approvalMatches == 1 ? candidate : null;
        },
            expectation: "the Gate B approval to become enabled",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                acknowledgementMatches == 1
                    ? "Gate B warning acknowledgement observed selected"
                    : "Gate B modal requires no warning acknowledgement",
                ControlType.Button.ProgrammaticName,
                approvalCandidates,
                approvalMatches));

        // Approval is side-effecting and may close the modal: invoke exactly once.
        ((InvokePattern)readyApprove.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    private static void CompleteLoadedProjectPreflight(HeadedApp app)
    {
        var preflight = DialogByTitle(app, app.Window, "reopened project data-lane preflight", "Open from library invoked");
        var exactDocument = ByName(preflight, ControlType.Edit, "Exact loaded semantic document");
        var exactValue = (ValuePattern)exactDocument.GetCurrentPattern(ValuePattern.Pattern);
        Assert.True(exactValue.Current.IsReadOnly);
        Assert.Contains("Exact semantic document SHA-256", exactValue.Current.Value, StringComparison.Ordinal);

        foreach (var statement in new[]
        {
            "This exact project's content is generic, teacher-created, staged, openly licensed, public-domain, or otherwise authorized for this use.",
            "It contains no student work, handwriting, faces, voices, identifying or linkable learner information, or personalized family communication.",
            "It contains no IEP/504, diagnosis, medical, counseling, behavioral, disciplinary, custody, private schedule, individualized AAC or communication, safety-disclosure, or recipient-list material.",
        })
        {
            ((TogglePattern)ByName(preflight, ControlType.CheckBox, statement)
                .GetCurrentPattern(TogglePattern.Pattern)).Toggle();
        }

        InvokeButton(preflight, "Continue to exact review");
    }

    /// <summary>The speaking status line: waits until it says what the rehearsal expects.</summary>
    private static void AwaitStatus(
        HeadedApp app,
        AutomationElement window,
        string prefix,
        string lastTransition)
    {
        var statusCandidates = 0;
        var statusMatches = 0;
        _ = WaitFor(() =>
        {
            var candidates = window.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
                .Cast<AutomationElement>()
                .ToList();
            statusCandidates = candidates.Count;
            var match = candidates.FirstOrDefault(e =>
                e.Current.Name.StartsWith(prefix, StringComparison.Ordinal));
            statusMatches = match is null ? 0 : 1;
            return match;
        },
            expectation: $"a speaking status beginning with '{prefix}'",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                lastTransition,
                ControlType.Text.ProgrammaticName,
                statusCandidates,
                statusMatches));
    }

    [HeadedFact]
    public void PilotDay_dress_rehearsal_cold_start_to_reopened_booklet_and_low_ink_over_real_uia()
    {
        var rehearsalRoot = Path.Combine(
            Path.GetTempPath(),
            "ocf-rehearsal-" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
        var library = Path.Combine(rehearsalRoot, Domain.EngineIdentity.EngineVersion, "prepared-library");
        Directory.CreateDirectory(library);
        try
        {
            RunRehearsal(library);
        }
        finally
        {
            try
            {
                Directory.Delete(rehearsalRoot, recursive: true);
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
            "pressroom",
            $"{App.WinForms.ProjectLibraryRootConfiguration.Switch} \"{library}\""
            + $" {App.WinForms.UiaHarness.ExportToSwitch} \"{booklet}\"");

        // Cold start: the front door says its name, the status line speaks,
        // and the structural gate holds — nothing prints, exports, or saves
        // before a typed approval exists.
        Assert.Contains(App.WinForms.ProductIdentity.PublicName, app.Window.Current.Name, StringComparison.Ordinal);
        AwaitStatus(app, app.Window, "Choose a press", "Press Room opened");
        var export = ByName(app.Window, ControlType.Button, "Export…");
        var save = ByName(app.Window, ControlType.Button, "Save to library");
        var print = ByName(app.Window, ControlType.Button, "Print");
        Assert.False(export.Current.IsEnabled, "Export must be locked before approval");
        Assert.False(save.Current.IsEnabled, "Save to library must be locked before approval");
        Assert.False(print.Current.IsEnabled, "Print must be locked before approval");

        // Forge a press: Flashcards — two sheets at defaults, which the
        // booklet leg of the loop needs.
        var presses = ByName(app.Window, ControlType.List, "Presses");
        var flashcardMatches = 0;
        ((SelectionItemPattern)WaitFor(() =>
            {
                var match = presses.FindFirst(
                    TreeScope.Children,
                    new PropertyCondition(AutomationElement.NameProperty, "Flashcards"));
                flashcardMatches = match is null ? 0 : 1;
                return match;
            },
                expectation: "the Flashcards press item",
                diagnosticSnapshot: () => CachedWaitSnapshot(
                    app.Process,
                    "Press Room opened",
                    ControlType.ListItem.ProgrammaticName,
                    candidates: null,
                    matches: flashcardMatches))
            .GetCurrentPattern(SelectionItemPattern.Pattern)).Select();

        InvokeButton(app.Window, "Review and approve…");
        ApproveGateB(app, app.Window);
        AwaitStatus(app, app.Window, "Approved —", "Initial Gate B approval invoked");
        var exportMatches = 0;
        _ = WaitFor(() =>
        {
            exportMatches = export.Current.IsEnabled ? 1 : 0;
            return exportMatches == 1 ? export : null;
        },
            expectation: "Export to unlock after the initial PilotDay approval",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                "Initial Gate B approval completed",
                ControlType.Button.ProgrammaticName,
                candidates: null,
                matches: exportMatches));
        Assert.True(save.Current.IsEnabled, "Approval must unlock saving");
        Assert.True(print.Current.IsEnabled, "Approval must unlock printing — the gate asserted, not paper");

        // Save to the library the harness was pointed at.
        InvokeButton(app.Window, "Save to library");
        AwaitStatus(app, app.Window, "Saved to the library as", "Save to library invoked");
        var projectCandidates = 0;
        var project = WaitFor(() =>
        {
            var candidates = Directory.EnumerateFiles(library, "*.ocfproj").Take(2).ToList();
            projectCandidates = candidates.Count;
            return candidates.FirstOrDefault();
        },
            expectation: "one prepared project in the disposable PilotDay library",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                "Save to library completed",
                "prepared project file",
                projectCandidates,
                matches: projectCandidates == 0 ? 0 : 1));

        // Reopen: reversibility means a FRESH Gate B review, never an
        // inherited approval — the gate is structural, not hereditary. The
        // reopen goes through the libraryPicker seam the in-process tests
        // always used (the harness resolves the newest fixture project at
        // click time): no lookup on the shell Open dialog earned trust under
        // full-suite load — traceability finding 9 — and Microsoft's chrome
        // is not what this rehearsal guards.
        Assert.EndsWith(".ocfproj", project, StringComparison.Ordinal);
        InvokeButton(app.Window, "Open from library…");
        CompleteLoadedProjectPreflight(app);
        ApproveGateB(app, app.Window);
        AwaitStatus(app, app.Window, "Approved —", "Reopened-project Gate B approval invoked");

        // Export the booklet PDF from the REOPENED artifact: the round trip
        // must preserve everything the imposer needs. The destination comes
        // from the export seam the harness was launched with (see the header
        // note on why the Save As dialog itself is not drivable).
        InvokeButton(app.Window, "Export…");
        AwaitStatus(
            app,
            app.Window,
            "Exported to rehearsal-booklet.pdf",
            "Reopened-project Export invoked");
        var bytes = File.ReadAllBytes(booklet);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5), StringComparison.Ordinal);

        // Low-ink variant: the transform applies BEFORE Gate B, so the
        // teacher reviews what will actually print — then approves it like
        // everything else.
        var lowInkMatches = 0;
        ((TogglePattern)WaitFor(() =>
            {
                var match = app.Window.FindFirst(TreeScope.Descendants, new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
                    new PropertyCondition(AutomationElement.NameProperty, "Low ink")));
                lowInkMatches = match is null ? 0 : 1;
                return match;
            },
                expectation: "the Low ink checkbox",
                diagnosticSnapshot: () => CachedWaitSnapshot(
                    app.Process,
                    "Reopened-project export completed",
                    ControlType.CheckBox.ProgrammaticName,
                    candidates: null,
                    matches: lowInkMatches))
            .GetCurrentPattern(TogglePattern.Pattern)).Toggle();
        InvokeButton(app.Window, "Review and approve…");
        ApproveGateB(app, app.Window);
        AwaitStatus(app, app.Window, "Approved —", "Low-ink Gate B approval invoked");
    }
}
