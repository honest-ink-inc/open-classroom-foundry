// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Domain;
using Xunit;

namespace Foundry.Tests.UiAutomation;

// The in-process half of the accessibility harness (ADR-002): what WinForms
// will hand to UI Automation, asserted as data — accessible names, roles, tab
// order, mnemonics, and the standard-controls rule. Test names carry the
// walkthrough-script step they encode (docs/accessibility/nvda-walkthrough-
// script.md); the mapping lives in docs/accessibility/uia-harness-traceability.md.
// What only a human ear can judge — actual speech — stays with the walkthrough.

public class ReviewSurfaceContractTests
{
    private static void WithReviewForm(Action<ReviewForm> assert) => Sta.Run(() =>
    {
        using var form = UiaHarness.CreateReviewForm();
        form.Show();
        assert(form);
    });

    [Fact]
    public void Part1_Step1_the_window_title_announces_the_product_and_the_draft_state()
        => WithReviewForm(form =>
        {
            Assert.Contains(ProductIdentity.PublicName, form.Text, StringComparison.Ordinal);
            Assert.Contains("draft", form.Text, StringComparison.OrdinalIgnoreCase);
        });

    [Fact]
    public void Part1_Step2_every_focusable_control_announces_a_name_and_a_role_with_no_unnamed_pane()
        => WithReviewForm(form =>
        {
            // WinForms insists SplitContainers stay focusable (keyboard resize),
            // so the "no unnamed pane" rule is: everything selectable is named.
            var focusable = Flatten(form).Where(c => c.TabStop && c.CanSelect).ToList();

            Assert.NotEmpty(focusable);
            Assert.All(focusable, control =>
            {
                Assert.False(string.IsNullOrWhiteSpace(control.AccessibilityObject.Name),
                    $"{control.GetType().Name} is an unnamed focusable control — the 'unnamed pane' failure of walkthrough step 2");
                Assert.NotEqual(AccessibleRole.None, control.AccessibilityObject.Role);
            });
        });

    [Fact]
    public void Part1_Step2_the_action_buttons_tab_in_their_visual_order()
        => WithReviewForm(form => Assert.Equal(
            ["Apply edit", "Remove element", "Move up", "Move down", "Approve", "Reject"],
            Flatten(form).OfType<FlowLayoutPanel>().Single().Controls
                .Cast<Control>()
                .OrderBy(c => c.TabIndex)
                .Select(c => c.AccessibilityObject.Name)
                .ToList()));

    [Fact]
    public void Part2_Step7_a_blank_step_surfaces_an_announced_issue_and_disables_approval()
        => Sta.Run(() =>
        {
            using var form = new ReviewForm(SessionOver(
                new Paragraph("Water each plant once."),
                new Paragraph("   ")));
            form.Show();

            var issues = (ListBox)ByName(form, "Validation issues");
            Assert.Contains(issues.Items.Cast<string>(), i => i.Contains("no text", StringComparison.Ordinal));

            // A disabled approval is announced as unavailable — the gate is audible.
            Assert.False(ByName(form, "Approve").Enabled);
        });

    [Fact]
    public void Part3_Step9_moving_a_step_keeps_selection_on_it_so_the_new_position_is_announced()
        => WithReviewForm(form =>
        {
            var list = (ListBox)ByName(form, "Draft elements");
            list.SelectedIndex = 1;
            var moved = (string)list.Items[1]!;

            ((Button)ByName(form, "Move down")).PerformClick();

            // Selection follows the moved element: the list's announced position
            // ("3 of 5") is the element's NEW position, which is step 9's demand.
            Assert.Equal(2, list.SelectedIndex);
            Assert.Equal(moved, list.Items[2]);
        });

    [Fact]
    public void Part3_Step10_the_edit_field_is_labeled_and_an_edit_reads_back_from_the_list()
        => WithReviewForm(form =>
        {
            var list = (ListBox)ByName(form, "Draft elements");
            var editor = (TextBox)ByName(form, "Selected element text");
            list.SelectedIndex = 2;
            Assert.Equal("Fill it to the line.", editor.Text);

            editor.Text = "Fill it exactly to the line.";
            ((Button)ByName(form, "Apply edit")).PerformClick();

            Assert.Equal("Paragraph: Fill it exactly to the line.", list.Items[2]);
        });

    [Fact]
    public void Part3_Step11_the_approval_control_states_what_approval_means()
        => WithReviewForm(form =>
        {
            var approve = ByName(form, "Approve");
            Assert.Contains("named approval", approve.AccessibleDescription, StringComparison.Ordinal);
            Assert.Contains("revision", approve.AccessibleDescription, StringComparison.Ordinal);
        });

    [Fact]
    public void Part3_Step12_approving_produces_the_typed_artifact_and_closes_as_accepted()
        => WithReviewForm(form =>
        {
            ((Button)ByName(form, "Approve")).PerformClick();

            Assert.NotNull(form.Result);
            Assert.Equal(DialogResult.OK, form.DialogResult);
        });

    [Fact]
    public void Mnemonics_are_unique_so_every_action_has_one_unambiguous_access_key()
        => Sta.Run(() =>
        {
            using var review = UiaHarness.CreateReviewForm();
            using var capture = UiaHarness.CreateCaptureForm();

            foreach (var form in new Form[] { review, capture })
            {
                var mnemonics = Flatten(form)
                    .OfType<ButtonBase>().Cast<Control>()
                    .Concat(Flatten(form).OfType<RadioButton>())
                    .Distinct()
                    .Select(c => Mnemonic(c.Text))
                    .OfType<char>()
                    .ToList();

                Assert.Equal(mnemonics.Count, mnemonics.Distinct().Count());
            }
        });

    [Fact]
    public void ADR002_standard_controls_only_no_custom_control_without_its_own_peer()
        => Sta.Run(() =>
        {
            using var review = UiaHarness.CreateReviewForm();
            using var capture = UiaHarness.CreateCaptureForm();

            foreach (var form in new Form[] { review, capture })
            {
                Assert.All(Flatten(form), control => Assert.Equal(
                    typeof(Control).Assembly,
                    control.GetType().Assembly));
            }
        });

    internal static ReviewSession SessionOver(params DocumentNode[] nodes)
    {
        var machine = new JobStateMachine();
        foreach (var state in new[]
        {
            JobState.Imported, JobState.Normalized, JobState.DataLaneConfirmed,
            JobState.DraftGenerated, JobState.SchemaValidated, JobState.InvariantsValidated,
            JobState.AwaitingTeacherReview,
        })
        {
            machine.Transition(state);
        }

        return new ReviewSession(
            DraftArtifact.New(new ArtifactDocument(nodes), DataLane.Green),
            machine,
            new DefaultArtifactValidator(),
            new DomainApprovalGate());
    }

    internal static IReadOnlyList<Control> TabStops(Form form)
    {
        var stops = new List<Control>();
        Control? current = form;
        while ((current = form.GetNextControl(current, forward: true)) is not null)
        {
            if (current.TabStop && current.CanSelect)
            {
                stops.Add(current);
            }
        }

        return stops;
    }

    internal static Control ByName(Form form, string accessibleName)
        => Flatten(form).Single(c => c.AccessibilityObject.Name == accessibleName);

    internal static IEnumerable<Control> Flatten(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    private static char? Mnemonic(string text)
    {
        var index = text.Replace("&&", "  ", StringComparison.Ordinal).IndexOf('&', StringComparison.Ordinal);
        return index >= 0 && index + 1 < text.Length ? char.ToLowerInvariant(text[index + 1]) : null;
    }
}

public class CaptureSurfaceContractTests
{
    private static void WithCaptureForm(Action<CaptureForm> assert) => Sta.Run(() =>
    {
        using var form = UiaHarness.CreateCaptureForm();
        form.Show();
        assert(form);
    });

    [Fact]
    public void Part1_Step2_every_capture_tab_stop_announces_a_name_and_a_role()
        => WithCaptureForm(form => Assert.All(
            ReviewSurfaceContractTests.TabStops(form),
            control =>
            {
                Assert.False(string.IsNullOrWhiteSpace(control.AccessibilityObject.Name));
                Assert.NotEqual(AccessibleRole.None, control.AccessibilityObject.Role);
            }));

    [Fact]
    public void Part4_Step14_the_lane_radios_carry_their_full_meaning_in_the_accessible_name()
        => WithCaptureForm(form =>
        {
            var radios = ReviewSurfaceContractTests.Flatten(form).OfType<RadioButton>().ToList();
            Assert.Equal(2, radios.Count);

            var green = Assert.Single(radios, r => r.AccessibilityObject.Name!.Contains("Green", StringComparison.Ordinal));
            var amber = Assert.Single(radios, r => r.AccessibilityObject.Name!.Contains("Amber", StringComparison.Ordinal));

            // Meaning in the name itself, not only in adjacent visual text.
            Assert.Contains("Staged materials", green.AccessibilityObject.Name, StringComparison.Ordinal);
            Assert.Contains("learners", amber.AccessibilityObject.Name, StringComparison.Ordinal);
        });

    [Fact]
    public void Part4_Step15_the_safety_pause_is_reachable_within_a_few_tabs_and_names_its_purpose()
        => WithCaptureForm(form =>
        {
            var stops = ReviewSurfaceContractTests.TabStops(form);
            var pauseIndex = stops.ToList().FindIndex(c =>
                c.AccessibilityObject.Name?.Contains("pause", StringComparison.OrdinalIgnoreCase) == true);

            Assert.InRange(pauseIndex, 0, 5); // "reachable in ≤ a few tabs"
            Assert.Contains("concerning", stops[pauseIndex].AccessibilityObject.Name, StringComparison.Ordinal);
        });
}
