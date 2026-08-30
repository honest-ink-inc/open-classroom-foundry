// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows.Automation;
using Foundry.Modules.BuiltIn;

namespace Foundry.Tests.UiAutomation;

// Headed proof for the production route from Press Room into the generated
// Built-in Studios surface. The process owns a validated disposable rehearsal
// library through HeadedApp; this test never opens a teacher library or invokes
// an output sink.
public sealed partial class HeadedUiaWalkTests
{
    [HeadedFact]
    public void PressRoom_built_in_studios_expose_every_mode_hold_human_gates_and_complete_real_GateB_over_uia()
    {
        using var app = new HeadedApp("pressroom");

        InvokeButton(app.Window, "Built-in studios…");
        var studio = DialogByTitle(app, app.Window, "built-in module studios", "Built-in studios invoked");
        Assert.Equal(app.Process.Id, studio.Current.ProcessId);

        var doors = ByName(studio, ControlType.List, App.WinForms.UiStrings.ModuleDoors);
        var doorItems = ChildListItems(doors);
        Assert.Equal(10, doorItems.Count);
        Assert.Equal(
            ModuleStudioCatalog.All.Select(door => door.Display.Fallback),
            doorItems.Select(item => item.Current.Name));

        var exposedModes = new List<string>();
        foreach (var door in ModuleStudioCatalog.All)
        {
            SelectListItem(app, doors, door.Display.Fallback, "previous studio mode inspected");
            var modeList = ByName(studio, ControlType.ComboBox, App.WinForms.UiStrings.ModuleMode);
            var value = (ValuePattern)modeList.GetCurrentPattern(ValuePattern.Pattern);

            var firstModeMatches = 0;
            var firstMode = WaitFor(() =>
            {
                var selected = value.Current.Value;
                firstModeMatches = string.Equals(
                    selected,
                    door.Modes[0].Display.Fallback,
                    StringComparison.Ordinal)
                        ? 1
                        : 0;
                return firstModeMatches == 1
                    ? selected
                    : null;
            },
                expectation: $"studio mode '{door.Modes[0].Display.Fallback}' after selecting door '{door.Display.Fallback}'",
                diagnosticSnapshot: () => CachedWaitSnapshot(
                    app.Process,
                    "studio door selection completed",
                    ControlType.ComboBox.ProgrammaticName,
                    candidates: 1,
                    matches: firstModeMatches));
            exposedModes.Add(firstMode);

            if (door.Modes.Count > 1)
            {
                var expand = (ExpandCollapsePattern)modeList.GetCurrentPattern(ExpandCollapsePattern.Pattern);
                var availableModes = ExpandedComboItems(
                    app,
                    modeList,
                    expand,
                    door.Modes.Count,
                    door.Display.Fallback);
                Assert.Equal(
                    door.Modes.Select(mode => mode.Display.Fallback),
                    availableModes.Select(item => item.Current.Name));

                foreach (var mode in door.Modes.Skip(1))
                {
                    exposedModes.Add(SelectComboItem(
                        app,
                        modeList,
                        expand,
                        value,
                        mode.Display.Fallback));
                }

                // Cleanup is useful only after the inspection completed. If a
                // wait above fails, HeadedApp.Dispose owns process cleanup; do
                // not start another UIA wait that can mask the first failure.
                CollapseCombo(app, expand, door.Display.Fallback);
            }
        }

        Assert.Equal(11, exposedModes.Count);
        Assert.Equal(
            ModuleStudioCatalog.All.SelectMany(door => door.Modes).Select(mode => mode.Display.Fallback),
            exposedModes);

        AssertHumanHeldModeCannotBeEnabled(app, studio, "Access Remix");
        AssertHumanHeldModeCannotBeEnabled(app, studio, "Exit Lens");
        AssertHumanHeldModeCannotBeEnabled(app, studio, "Rubric Relay");

        // Return to one catalog-owned synthetic Green starter. Every sink is
        // structurally locked until the actual modal Gate B returns a typed
        // approval for this exact generated revision.
        SelectListItem(app, doors, "Board to Brief", "all studio modes inspected");
        var sinks = new[]
        {
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.WithoutMnemonic(App.WinForms.UiStrings.PrintButton)),
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.WithoutMnemonic(App.WinForms.UiStrings.OpenPrintView)),
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.WithoutMnemonic(App.WinForms.UiStrings.ExportEllipsis)),
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.WithoutMnemonic(App.WinForms.UiStrings.SaveToLibrary)),
        };
        Assert.All(sinks, sink => Assert.False(sink.Current.IsEnabled,
            $"'{sink.Current.Name}' must remain locked before Gate B approval."));

        var review = ByName(
            studio,
            ControlType.Button,
            App.WinForms.UiStrings.WithoutMnemonic(App.WinForms.UiStrings.ReviewAndApprove));
        Assert.True(review.Current.IsEnabled,
            "The catalog-owned synthetic Board to Brief starter must be eligible for review.");
        ((InvokePattern)review.GetCurrentPattern(InvokePattern.Pattern)).Invoke();

        var gateB = DialogByTitle(app, studio, "reviewing a draft", "Review and approve invoked");
        Assert.Equal(app.Process.Id, gateB.Current.ProcessId);
        ApproveGateB(app, studio);

        AwaitStatus(app, studio, "Approved —", "Board to Brief Gate B approval invoked");
        Assert.All(sinks, sink => Assert.True(sink.Current.IsEnabled,
            $"'{sink.Current.Name}' must unlock only after the exact Gate B approval."));
    }

    private static IReadOnlyList<AutomationElement> ChildListItems(AutomationElement list)
        => [.. list.FindAll(
                TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
            .Cast<AutomationElement>()];

    private static IReadOnlyList<AutomationElement> ExpandedComboItems(
        HeadedApp app,
        AutomationElement combo,
        ExpandCollapsePattern expand,
        int expectedCount,
        string doorName)
    {
        var candidateCount = 0;
        var matchCount = 0;
        var expandAttempts = 0;
        return WaitFor<IReadOnlyList<AutomationElement>>(() =>
        {
            var items = combo.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
                .Cast<AutomationElement>()
                .ToList();
            candidateCount = items.Count;
            matchCount = candidateCount == expectedCount ? expectedCount : 0;
            if (matchCount == expectedCount)
            {
                return items;
            }

            // Expand is idempotent. WinForms can acknowledge one UIA request
            // before its standard ComboBox provider exposes the popup items.
            expand.Expand();
            expandAttempts++;
            return null;
        },
            expectation: $"all {expectedCount} modes for door '{doorName}' in the expanded standard combo box",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                $"studio mode expansion requested {expandAttempts} times",
                ControlType.ListItem.ProgrammaticName,
                candidateCount,
                matchCount));
    }

    private static string SelectComboItem(
        HeadedApp app,
        AutomationElement combo,
        ExpandCollapsePattern expand,
        ValuePattern value,
        string modeName)
    {
        var candidateCount = 0;
        var selectedCount = 0;
        var selectionAttempts = 0;
        return WaitFor(() =>
        {
            var selected = value.Current.Value;
            selectedCount = string.Equals(selected, modeName, StringComparison.Ordinal) ? 1 : 0;
            if (selectedCount == 1)
            {
                return selected;
            }

            var items = combo.FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
                .Cast<AutomationElement>()
                .ToList();
            candidateCount = items.Count;
            var item = items.FirstOrDefault(candidate => candidate.Current.Name == modeName);
            if (item is null)
            {
                expand.Expand();
            }
            else
            {
                ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            }

            selectionAttempts++;
            return null;
        },
            expectation: $"selected studio mode '{modeName}' over UI Automation",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                $"studio mode selection requested {selectionAttempts} times",
                ControlType.ListItem.ProgrammaticName,
                candidateCount,
                selectedCount));
    }

    private static void CollapseCombo(
        HeadedApp app,
        ExpandCollapsePattern expand,
        string doorName)
    {
        var collapsed = 0;
        var collapseAttempts = 0;
        _ = WaitFor(() =>
        {
            collapsed = expand.Current.ExpandCollapseState == ExpandCollapseState.Collapsed ? 1 : 0;
            if (collapsed == 1)
            {
                return "collapsed";
            }

            expand.Collapse();
            collapseAttempts++;
            return null;
        },
            expectation: $"the studio mode list for '{doorName}' to collapse",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                $"studio mode collapse requested {collapseAttempts} times",
                ControlType.ComboBox.ProgrammaticName,
                candidates: 1,
                matches: collapsed));
    }

    private static void SelectListItem(
        HeadedApp app,
        AutomationElement list,
        string name,
        string lastTransition)
    {
        var candidateCount = 0;
        var matchCount = 0;
        var selectedCount = 0;
        var selectionAttempts = 0;
        var item = WaitFor(() =>
        {
            var candidates = ChildListItems(list);
            candidateCount = candidates.Count;
            var match = candidates.FirstOrDefault(candidate => candidate.Current.Name == name);
            matchCount = match is null ? 0 : 1;
            return match;
        },
            expectation: $"a list item named '{name}'",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                lastTransition,
                ControlType.ListItem.ProgrammaticName,
                candidateCount,
                matchCount));
        ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        selectionAttempts++;
        WaitFor(() =>
        {
            var candidates = ChildListItems(list);
            candidateCount = candidates.Count;
            var refreshed = candidates.FirstOrDefault(candidate => candidate.Current.Name == name);
            matchCount = refreshed is null ? 0 : 1;
            selectedCount = refreshed is not null
                && ((SelectionItemPattern)refreshed.GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected
                    ? 1
                    : 0;
            if (selectedCount == 1)
            {
                return refreshed;
            }

            if (refreshed is not null)
            {
                // The WinForms UIA provider can acknowledge Select before its
                // ListBox selection has changed. Re-resolve and reissue the
                // same idempotent request inside the existing bounded wait.
                ((SelectionItemPattern)refreshed.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
                selectionAttempts++;
            }

            return null;
        },
            expectation: $"list item '{name}' to become selected over UI Automation",
            diagnosticSnapshot: () => CachedWaitSnapshot(
                app.Process,
                $"studio door selection requested {selectionAttempts} times",
                ControlType.ListItem.ProgrammaticName,
                candidateCount,
                selectedCount));
    }

    private static void AssertHumanHeldModeCannotBeEnabled(
        HeadedApp app,
        AutomationElement studio,
        string doorName)
    {
        var doors = ByName(studio, ControlType.List, App.WinForms.UiStrings.ModuleDoors);
        SelectListItem(app, doors, doorName, "studio door inventory completed");
        var door = ModuleStudioCatalog.All.Single(candidate =>
            string.Equals(candidate.Display.Fallback, doorName, StringComparison.Ordinal));
        var unavailableReason = door.Modes[0].UnavailableReason;
        var expectedStatus = App.WinForms.UiStrings.FormatWithoutMnemonic(
            App.WinForms.UiStrings.StatusModuleUnavailable,
            App.WinForms.UiStrings.Localize(
                unavailableReason!.LocalizationId,
                unavailableReason.Fallback));
        AssertSpokenStatus(studio, doorName, expectedStatus);

        var green = ByName(
            studio,
            ControlType.CheckBox,
            App.WinForms.UiStrings.WithoutMnemonic(App.WinForms.UiStrings.GreenInputAttestation));
        var review = ByName(
            studio,
            ControlType.Button,
            App.WinForms.UiStrings.WithoutMnemonic(App.WinForms.UiStrings.ReviewAndApprove));
        var sinks = new[]
        {
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.WithoutMnemonic(App.WinForms.UiStrings.PrintButton)),
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.WithoutMnemonic(App.WinForms.UiStrings.OpenPrintView)),
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.WithoutMnemonic(App.WinForms.UiStrings.ExportEllipsis)),
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.WithoutMnemonic(App.WinForms.UiStrings.SaveToLibrary)),
        };

        Assert.False(green.Current.IsEnabled,
            $"{doorName}: the Green confirmation must not become an authority-granting keyboard control.");
        var toggle = (TogglePattern)green.GetCurrentPattern(TogglePattern.Pattern);
        Assert.Equal(ToggleState.Off, toggle.Current.ToggleState);
        try
        {
            toggle.Toggle();
        }
        catch (ElementNotEnabledException)
        {
            // Providers may either refuse the disabled action or deliver a
            // no-op. The authority state below must remain held either way.
        }

        Assert.Equal(ToggleState.Off, toggle.Current.ToggleState);
        AssertSpokenStatus(studio, doorName, expectedStatus);

        Assert.False(review.Current.IsEnabled,
            $"{doorName}: Review must remain structurally disabled while human authority is absent.");
        try
        {
            ((InvokePattern)review.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        }
        catch (ElementNotEnabledException)
        {
            // Same UIA-provider variance as the held checkbox above.
        }

        Thread.Sleep(200);
        Assert.False(review.Current.IsEnabled,
            $"{doorName}: invoking a disabled Review control must not change the hold.");
        Assert.Equal(IntPtr.Zero, Win32WindowByTitle(app.Process.Id, "reviewing a draft"));
        Assert.All(sinks, sink => Assert.False(sink.Current.IsEnabled,
            $"{doorName}: '{sink.Current.Name}' must remain locked while the human gate is held."));

    }

    private static void AssertSpokenStatus(
        AutomationElement studio,
        string doorName,
        string expectedStatus)
    {
        var status = studio.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, expectedStatus));
        Assert.NotNull(status);
        Assert.Equal(expectedStatus, status.Current.Name);
        Assert.Equal(
            ControlType.Text,
            status.Current.ControlType);
        Assert.False(
            string.IsNullOrWhiteSpace(status.Current.Name),
            $"{doorName}: the human-hold status must be exposed in the real UI Automation tree.");
    }
}
