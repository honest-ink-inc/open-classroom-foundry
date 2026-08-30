// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.UiAutomation;

// The Press Room's accessibility and flow contract (second forge menu, item 1
// DoD): a teacher reaches an approved artifact from a cold start by keyboard-
// equivalent steps, the structural gate is visible as disabled controls, the
// budget is displayed, and refusals land in the announced status.

public class PressRoomContractTests
{
    private static ApprovedArtifact AutoApprove(ReviewSession session)
    {
        session.SetRequiredIssuesAcknowledged(acknowledged: true);
        return session.Approve(Environment.UserName, DateTimeOffset.UtcNow);
    }

    private static void WithPressRoom(Func<ReviewSession, ApprovedArtifact?> runner, Action<PressRoomForm> assert)
        => Sta.Run(() =>
        {
            using var form = new PressRoomForm(runner);
            form.Show();
            assert(form);
        });

    private static void SelectPress(PressRoomForm form, string id)
    {
        var list = (ListBox)ReviewSurfaceContractTests.ByName(form, "Presses");
        list.SelectedIndex = PressRoomCatalog.All.ToList().FindIndex(d => d.Id == id);
    }

    /// <summary>
    /// A parameter row holds a visual Label AND its named input, and a
    /// NumericUpDown's internal children inherit its name — this picks the
    /// top-level input control itself.
    /// </summary>
    private static Control Input(PressRoomForm form, string accessibleName)
        => ReviewSurfaceContractTests.Flatten(form)
            .Single(c => c is not Label
                && c.Parent is not NumericUpDown
                && c.AccessibilityObject.Name == accessibleName);

    [Fact]
    public void Cold_start_to_typed_approval_press_parameters_review_approve()
        => WithPressRoom(AutoApprove, form =>
        {
            SelectPress(form, "word-search");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            Assert.NotNull(form.ApprovedResult);
            Assert.True(ReviewSurfaceContractTests.ByName(form, "Open print view").Enabled);
            Assert.True(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
            Assert.True(ReviewSurfaceContractTests.ByName(form, "Save to library").Enabled);
        });

    [Fact]
    public void The_structural_gate_is_visible_nothing_unlocks_before_approval()
        => WithPressRoom(AutoApprove, form =>
        {
            Assert.Null(form.ApprovedResult);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Open print view").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Save to library").Enabled);
        });

    [Fact]
    public void Changing_a_press_input_revokes_the_exact_approval_and_every_sink()
        => WithPressRoom(AutoApprove, form =>
        {
            SelectPress(form, "graph-paper");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();
            Assert.NotNull(form.ApprovedResult);

            ((NumericUpDown)Input(form, "Square size (mm)")).Value += 1;

            Assert.Null(form.ApprovedResult);
            Assert.Contains("fresh review", form.StatusText, StringComparison.Ordinal);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Print").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Open print view").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Save to library").Enabled);
        });

    [Fact]
    public void A_press_review_runner_cannot_substitute_another_revision()
        => WithPressRoom(_ =>
        {
            var other = AppServices.SessionOverGreen(
                new ArtifactDocument([new Heading(1, "Different synthetic sheet")]));
            return AutoApprove(other);
        }, form =>
        {
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            Assert.Null(form.ApprovedResult);
            Assert.Contains("without approval", form.StatusText, StringComparison.Ordinal);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
        });

    [Fact]
    public void A_declined_review_unlocks_nothing()
        => WithPressRoom(_ => null, form =>
        {
            SelectPress(form, "graph-paper");
            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            Assert.Null(form.ApprovedResult);
            Assert.False(ReviewSurfaceContractTests.ByName(form, "Export…").Enabled);
        });

    [Fact]
    public void Part2_Step7_a_press_refusal_is_surfaced_in_the_status_not_swallowed()
        => WithPressRoom(AutoApprove, form =>
        {
            SelectPress(form, "word-search");
            var words = (TextBox)Input(form, "Words to hide, one per line");
            words.Text = "two words on one line";

            ((Button)ReviewSurfaceContractTests.ByName(form, "Review and approve…")).PerformClick();

            Assert.Null(form.ApprovedResult);
            Assert.Contains("refused", form.StatusText, StringComparison.Ordinal);
            Assert.Contains("letters only", form.StatusText, StringComparison.Ordinal);
        });

    [Fact]
    public void The_three_minute_budget_is_displayed_as_the_constitution_requires()
        => WithPressRoom(AutoApprove, form =>
        {
            var budget = ReviewSurfaceContractTests.Flatten(form).OfType<Label>()
                .Single(l => l.Text.Contains("budget", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("3", budget.Text, StringComparison.Ordinal);
        });

    [Fact]
    public void Part2_Step4_choosing_a_press_regenerates_its_labeled_parameter_controls()
        => WithPressRoom(AutoApprove, form =>
        {
            SelectPress(form, "graph-paper");
            Assert.IsType<NumericUpDown>(Input(form, "Square size (mm)"));

            SelectPress(form, "grouping-cards");
            Assert.IsType<NumericUpDown>(Input(form, "Group size"));
            Assert.IsType<TextBox>(Input(form,
                "Roster labels, one per line - synthetic or first-name-free only"));
        });

    [Fact]
    public void Part1_Step2_every_focusable_press_room_control_is_named_and_roled()
        => WithPressRoom(AutoApprove, form =>
        {
            SelectPress(form, "bingo-cards");
            var focusable = ReviewSurfaceContractTests.Flatten(form).Where(c => c.TabStop && c.CanSelect).ToList();

            Assert.NotEmpty(focusable);
            Assert.All(focusable, control =>
            {
                Assert.False(string.IsNullOrWhiteSpace(control.AccessibilityObject.Name),
                    $"{control.GetType().Name} is an unnamed focusable control");
                Assert.NotEqual(AccessibleRole.None, control.AccessibilityObject.Role);
            });
        });

    [Fact]
    public void The_press_list_speaks_every_catalog_title()
        => WithPressRoom(AutoApprove, form =>
        {
            var list = (ListBox)ReviewSurfaceContractTests.ByName(form, "Presses");
            Assert.Equal(PressRoomCatalog.All.Count, list.Items.Count);
            Assert.Equal(PressRoomCatalog.All.Select(d => d.Title), list.Items.Cast<string>());
        });
}
