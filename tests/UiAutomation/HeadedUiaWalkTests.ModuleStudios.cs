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
        var studio = DialogByTitle(app, "built-in module studios");
        Assert.Equal(app.Process.Id, studio.Current.ProcessId);
        Assert.True(((WindowPattern)studio.GetCurrentPattern(WindowPattern.Pattern)).Current.IsModal,
            "Press Room must open the production Built-in Studios modal, not a harness substitute.");

        var doors = ByName(studio, ControlType.List, App.WinForms.UiStrings.ModuleDoors);
        var doorItems = ChildListItems(doors);
        Assert.Equal(10, doorItems.Count);
        Assert.Equal(
            ModuleStudioCatalog.All.Select(door => door.Display.Fallback),
            doorItems.Select(item => item.Current.Name));

        var exposedModes = new List<string>();
        foreach (var door in ModuleStudioCatalog.All)
        {
            SelectListItem(doors, door.Display.Fallback);
            var modeList = ByName(studio, ControlType.ComboBox, App.WinForms.UiStrings.ModuleMode);
            var value = (ValuePattern)modeList.GetCurrentPattern(ValuePattern.Pattern);

            var firstMode = WaitFor(() =>
            {
                var selected = value.Current.Value;
                return string.Equals(selected, door.Modes[0].Display.Fallback, StringComparison.Ordinal)
                    ? selected
                    : null;
            }, expectation: $"studio mode '{door.Modes[0].Display.Fallback}' after selecting door '{door.Display.Fallback}'");
            exposedModes.Add(firstMode);

            if (door.Modes.Count > 1)
            {
                var expand = (ExpandCollapsePattern)modeList.GetCurrentPattern(ExpandCollapsePattern.Pattern);
                expand.Expand();
                try
                {
                    var availableModes = WaitFor(() =>
                    {
                        var items = modeList.FindAll(
                                TreeScope.Descendants,
                                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
                            .Cast<AutomationElement>()
                            .ToList();
                        return items.Count == door.Modes.Count ? items : null;
                    }, expectation: $"all {door.Modes.Count} modes for door '{door.Display.Fallback}' in the expanded standard combo box");
                    Assert.Equal(
                        door.Modes.Select(mode => mode.Display.Fallback),
                        availableModes.Select(item => item.Current.Name));

                    foreach (var mode in door.Modes.Skip(1))
                    {
                        var item = Assert.Single(
                            availableModes,
                            candidate => string.Equals(candidate.Current.Name, mode.Display.Fallback, StringComparison.Ordinal));
                        ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
                        exposedModes.Add(WaitFor(() =>
                        {
                            var selected = value.Current.Value;
                            return string.Equals(selected, mode.Display.Fallback, StringComparison.Ordinal)
                                ? selected
                                : null;
                        }, expectation: $"selected studio mode '{mode.Display.Fallback}' over UI Automation"));
                    }
                }
                finally
                {
                    expand.Collapse();
                }
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
        SelectListItem(doors, "Board to Brief");
        var sinks = new[]
        {
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.PrintButton),
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.OpenPrintView),
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.ExportEllipsis),
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.SaveToLibrary),
        };
        Assert.All(sinks, sink => Assert.False(sink.Current.IsEnabled,
            $"'{sink.Current.Name}' must remain locked before Gate B approval."));

        var review = ByName(studio, ControlType.Button, App.WinForms.UiStrings.ReviewAndApprove);
        Assert.True(review.Current.IsEnabled,
            "The catalog-owned synthetic Board to Brief starter must be eligible for review.");
        ((InvokePattern)review.GetCurrentPattern(InvokePattern.Pattern)).Invoke();

        var gateB = DialogByTitle(app, "reviewing a draft");
        Assert.Equal(app.Process.Id, gateB.Current.ProcessId);
        Assert.True(((WindowPattern)gateB.GetCurrentPattern(WindowPattern.Pattern)).Current.IsModal,
            "Gate B must be the real production modal review window.");
        ApproveGateB(app);

        AwaitStatus(studio, "Approved —");
        Assert.All(sinks, sink => Assert.True(sink.Current.IsEnabled,
            $"'{sink.Current.Name}' must unlock only after the exact Gate B approval."));
    }

    private static IReadOnlyList<AutomationElement> ChildListItems(AutomationElement list)
        => [.. list.FindAll(
                TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem))
            .Cast<AutomationElement>()];

    private static void SelectListItem(AutomationElement list, string name)
    {
        var item = WaitFor(() => list.FindFirst(
                TreeScope.Children,
                new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                    new PropertyCondition(AutomationElement.NameProperty, name))),
            expectation: $"a list item named '{name}'");
        ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        WaitFor(() =>
        {
            var refreshed = list.FindFirst(
                TreeScope.Children,
                new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                    new PropertyCondition(AutomationElement.NameProperty, name)));
            return refreshed is not null
                && ((SelectionItemPattern)refreshed.GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected
                    ? refreshed
                    : null;
        }, expectation: $"list item '{name}' to become selected over UI Automation");
    }

    private static void AssertHumanHeldModeCannotBeEnabled(
        HeadedApp app,
        AutomationElement studio,
        string doorName)
    {
        var doors = ByName(studio, ControlType.List, App.WinForms.UiStrings.ModuleDoors);
        SelectListItem(doors, doorName);
        var door = ModuleStudioCatalog.All.Single(candidate =>
            string.Equals(candidate.Display.Fallback, doorName, StringComparison.Ordinal));
        var unavailableReason = door.Modes[0].UnavailableReason;
        var expectedStatus = App.WinForms.UiStrings.Format(
            App.WinForms.UiStrings.StatusModuleUnavailable,
            App.WinForms.UiStrings.Localize(
                unavailableReason!.LocalizationId,
                unavailableReason.Fallback));
        AssertSpokenStatus(studio, doorName, expectedStatus);

        var green = ByName(
            studio,
            ControlType.CheckBox,
            App.WinForms.UiStrings.GreenInputAttestation);
        var review = ByName(studio, ControlType.Button, App.WinForms.UiStrings.ReviewAndApprove);
        var sinks = new[]
        {
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.PrintButton),
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.OpenPrintView),
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.ExportEllipsis),
            ByName(studio, ControlType.Button, App.WinForms.UiStrings.SaveToLibrary),
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
