// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Domain;
using Foundry.Storage;

namespace Foundry.Tests.UiAutomation;

// The All Aboard typed-steps surface (second forge menu, item 2): walkthrough
// part 2 encoded where automation honestly can. The wall stands in the tests
// too — everything here exercises the ratified 0.1 slice and nothing else.

public class AllAboardContractTests
{
    private static JsonAssetCatalog Catalog()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OpenClassroomFoundry.slnx")))
        {
            dir = dir.Parent;
        }

        return new JsonAssetCatalog(Path.Combine(dir!.FullName, "assets", "symbols"));
    }

    private static ApprovedArtifact? GateRespectingApprove(ReviewSession session)
        => session.CanApprove ? session.Approve(Environment.UserName, DateTimeOffset.UtcNow) : null;

    private static void WithForm(Action<AllAboardForm> assert)
        => Sta.Run(() =>
        {
            using var form = new AllAboardForm(Catalog(), GateRespectingApprove);
            form.Show();
            assert(form);
        });

    /// <summary>Each row holds a visual Label plus its named input; this picks the input.</summary>
    private static Control Input(AllAboardForm form, string accessibleName)
        => ReviewSurfaceContractTests.Flatten(form)
            .Single(c => c is not Label && c.AccessibilityObject.Name == accessibleName);

    private static TextBox Title(AllAboardForm form)
        => (TextBox)Input(form, "Task title");

    private static TextBox Step(AllAboardForm form, int number)
        => (TextBox)Input(form, $"Step {number} text");

    private static ComboBox Symbol(AllAboardForm form, int number)
        => (ComboBox)Input(form, $"Step {number} symbol");

    [Fact]
    public void Part2_Step5_a_title_and_four_typed_steps_reach_typed_approval()
        => WithForm(form =>
        {
            Title(form).Text = "Watering the class plants";
            Step(form, 1).Text = "Pick up the watering can.";
            Step(form, 2).Text = "Fill it to the line.";
            Step(form, 3).Text = "Water each plant once.";
            Step(form, 4).Text = "Put the can back.";

            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            Assert.NotNull(form.ApprovedResult);
            var nodes = form.ApprovedResult.Revision.Document.Nodes;
            Assert.Equal("Watering the class plants", Assert.IsType<Heading>(nodes[0]).Text);
            Assert.Equal(4, nodes.OfType<StepRow>().Count());
            Assert.True(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
        });

    [Fact]
    public void Part2_Step6_symbols_are_chosen_by_meaning_never_by_filename()
        => WithForm(form =>
        {
            var symbols = Symbol(form, 1);

            // The picker speaks meanings from provenance records — no "image",
            // no ".svg", no filename anywhere in what a screen reader hears.
            Assert.Equal("(no symbol)", symbols.Items[0]);
            Assert.True(symbols.Items.Count > 1, "The shipped pack must appear in the picker");
            Assert.All(symbols.Items.Cast<string>(), item =>
            {
                Assert.DoesNotContain(".svg", item, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("image", item, StringComparison.OrdinalIgnoreCase);
            });

            Title(form).Text = "Getting help";
            Step(form, 1).Text = "Raise your hand.";
            Step(form, 2).Text = "Wait for the teacher.";
            Step(form, 3).Text = "Say what you need.";
            symbols.SelectedIndex = symbols.Items.IndexOf("Help");

            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            Assert.NotNull(form.ApprovedResult);
            var first = form.ApprovedResult.Revision.Document.Nodes.OfType<StepRow>().First();
            Assert.NotNull(first.Symbol);
            Assert.False(string.IsNullOrWhiteSpace(first.Symbol.AltText));
        });

    [Fact]
    public void Part2_Step7_too_few_steps_is_refused_in_the_speaking_status()
        => WithForm(form =>
        {
            Title(form).Text = "Short";
            Step(form, 1).Text = "Only one.";
            Step(form, 2).Text = "Only two.";

            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            Assert.Null(form.ApprovedResult);
            Assert.Contains("3 to 8", form.StatusText, StringComparison.Ordinal);
        });

    [Fact]
    public void Part2_Step7_a_symbol_on_a_blank_step_fails_review_not_silently()
        => WithForm(form =>
        {
            Title(form).Text = "Blank step";
            Step(form, 1).Text = "First step.";
            Step(form, 2).Text = "Second step.";
            Symbol(form, 3).SelectedIndex = 1; // symbol chosen, text left blank

            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            // The blank step flows into review, where structural validation
            // blocks approval — the gate speaks, nothing is silently dropped.
            Assert.Null(form.ApprovedResult);
            Assert.Contains("without approval", form.StatusText, StringComparison.Ordinal);
        });

    [Fact]
    public void Every_focusable_all_aboard_control_is_named_and_roled()
        => WithForm(form => Assert.All(
            ReviewSurfaceContractTests.Flatten(form).Where(c => c.TabStop && c.CanSelect),
            control =>
            {
                Assert.False(string.IsNullOrWhiteSpace(control.AccessibilityObject.Name),
                    $"{control.GetType().Name} is an unnamed focusable control");
                Assert.NotEqual(AccessibleRole.None, control.AccessibilityObject.Role);
            }));

    [Fact]
    public void The_press_room_carries_the_all_aboard_door()
        => Sta.Run(() =>
        {
            using var pressRoom = new PressRoomForm(_ => null);
            Assert.NotNull(ReviewSurfaceContractTests.ByName(pressRoom, "All Aboard task strip…"));
        });
}
