// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.UiAutomation;

/// <summary>Synthetic authoring controls; no real content, files or human approval.</summary>
public sealed class PressRoomGreenInputTests
{
    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.2.0")]
    public void Unconfirmed_text_cannot_enter_review_even_through_the_handler(string version)
        => Sta.Run(() =>
        {
            var reviews = 0;
            DataLane? observedLane = null;
            using var form = new PressRoomForm(session =>
            {
                reviews++;
                observedLane = session.Draft.Revision.Lane;
                return null;
            });
            form.Show();
            Select(form, "flashcards", version);
            Assert.Single(FreeText(form)).Text = "Synthetic term | Synthetic answer";

            Review(form).PerformClick();
            InvokeAuthoringHandler(form);

            Assert.True(reviews == 0,
                $"Unconfirmed synthetic authoring entered review {reviews} times as {observedLane}; expected no review or Green draft.");
            Assert.False(Green(form).Checked);
            Assert.False(Review(form).Enabled);
            Assert.Equal(UiStrings.WithoutMnemonic(UiStrings.StatusModuleGreenRequired), form.StatusText);
            Assert.Equal(form.StatusText, ReviewSurfaceContractTests.Flatten(form).OfType<Label>()
                .Single(label => label.Text == form.StatusText).AccessibilityObject.Name);
            AssertSinksLocked(form);
        });

    [Fact]
    public void Every_current_catalog_text_field_and_restore_to_default_require_fresh_confirmation()
        => Sta.Run(() =>
        {
            var reviews = 0;
            var measuredFields = 0;
            using var form = new PressRoomForm(_ => { reviews++; return null; });
            form.Show();
            foreach (var definition in PressRoomCatalog.AllVersions)
            {
                Select(form, definition.Id, definition.Recipe.Version);
                Assert.True(Green(form).Checked);
                var fields = FreeText(form);
                var inputPanel = Assert.IsType<TableLayoutPanel>(Green(form).Parent);
                Assert.True(inputPanel.AutoScroll);
                Assert.Equal(2, inputPanel.GetColumnSpan(Green(form)));
                Assert.Equal(definition.Parameters.Count + 1, inputPanel.RowCount);
                Assert.Equal(inputPanel.RowCount, inputPanel.RowStyles.Count);
                Assert.All(fields, field => Assert.Same(inputPanel, field.Parent));
                Assert.Equal(definition.Parameters.Count(parameter => parameter is TextParameter or LinesParameter), fields.Length);
                foreach (var field in fields)
                {
                    var original = field.Text;
                    field.Text = original + " synthetic";
                    Assert.False(Green(form).Checked);
                    Assert.False(Review(form).Enabled);
                    AssertSinksLocked(form);
                    field.Text = original;
                    Assert.False(Green(form).Checked);
                    Assert.False(Review(form).Enabled);
                    Green(form).Checked = true;
                    Assert.True(Review(form).Enabled);
                    AssertSinksLocked(form);
                    measuredFields++;
                }
            }

            Assert.True(measuredFields > 1, "The closed catalog must exercise both Text and Lines inputs.");
            Assert.Equal(0, reviews);
        });

    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.2.0")]
    public void Text_edit_revokes_approval_and_confirmation_alone_unlocks_no_sink(string version)
        => Sta.Run(() =>
        {
            using var form = new PressRoomForm(Approve);
            form.Show();
            Select(form, "flashcards", version);
            Review(form).PerformClick();
            Assert.NotNull(form.ApprovedResult);
            var old = form.ApprovedResult;

            Assert.Single(FreeText(form)).Text = "Synthetic term | Synthetic replacement";
            AssertSinksLocked(form);
            Assert.False(Green(form).Checked);
            Green(form).Checked = true;
            AssertSinksLocked(form);
            Review(form).PerformClick();
            Assert.NotNull(form.ApprovedResult);
            Assert.NotSame(old, form.ApprovedResult);
            Assert.Equal(DataLane.Green, form.ApprovedResult.Revision.Lane);

            Green(form).Checked = false;
            AssertSinksLocked(form);
            Green(form).Checked = true;
            AssertSinksLocked(form);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bounded_settings_preserve_lane_but_revoke_approval(bool initiallyConfirmed)
        => Sta.Run(() =>
        {
            using var form = new PressRoomForm(Approve);
            form.Show();
            Select(form, "graph-paper", "0.1.0");
            Green(form).Checked = initiallyConfirmed;
            if (initiallyConfirmed)
            {
                Review(form).PerformClick();
                Assert.NotNull(form.ApprovedResult);
            }

            var number = ReviewSurfaceContractTests.Flatten(form).OfType<NumericUpDown>().First();
            number.Value += number.Increment;
            Assert.Equal(initiallyConfirmed, Green(form).Checked);
            AssertSinksLocked(form);
            var ink = Assert.IsType<CheckBox>(ReviewSurfaceContractTests.ByName(form,
                UiStrings.WithoutMnemonic(UiStrings.LowInkToggle)));
            ink.Checked = !ink.Checked;
            Assert.Equal(initiallyConfirmed, Green(form).Checked);
            Assert.Equal(initiallyConfirmed, Review(form).Enabled);
            AssertSinksLocked(form);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Programmatic_changes_during_review_cannot_revive_the_old_approval(bool editText)
        => Sta.Run(() =>
        {
            PressRoomForm? active = null;
            var calls = 0;
            using var form = new PressRoomForm(session =>
            {
                calls++;
                Assert.False(Green(active!).Enabled);
                InvokeAuthoringHandler(active!);
                Assert.Equal(1, calls);
                if (editText)
                {
                    var field = Assert.Single(FreeText(active!));
                    var original = field.Text;
                    field.Text = "Synthetic changed term | Synthetic changed answer";
                    field.Text = original;
                    Assert.False(Green(active!).Checked);
                }
                else
                {
                    Green(active!).Checked = false;
                }

                Green(active!).Checked = true;
                return Approve(session);
            });
            active = form;
            form.Show();
            Select(form, "flashcards", "0.2.0");
            Review(form).PerformClick();
            Assert.Equal(1, calls);
            AssertSinksLocked(form);
        });

    [Fact]
    public void Only_fresh_catalog_construction_restores_known_defaults_and_no_selection_revokes_them()
        => Sta.Run(() =>
        {
            using var form = new PressRoomForm(Approve);
            form.Show();
            Select(form, "flashcards", "0.1.0");
            Assert.Single(FreeText(form)).Text = "Synthetic term | Synthetic changed answer";
            Assert.False(Green(form).Checked);
            Select(form, "graph-paper", "0.1.0");
            Assert.True(Green(form).Checked);
            AssertSinksLocked(form);
            Review(form).PerformClick();
            Assert.NotNull(form.ApprovedResult);
            Presses(form).SelectedIndex = -1;
            Assert.False(Green(form).Checked);
            Assert.False(Review(form).Enabled);
            AssertSinksLocked(form);
        });

    internal static void ConfirmSyntheticInputs(PressRoomForm form) => Green(form).Checked = true;

    private static ApprovedArtifact Approve(ReviewSession session)
    {
        session.SetRequiredIssuesAcknowledged(true);
        return session.Approve("synthetic-press-review@example.invalid",
            new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero));
    }

    private static CheckBox Green(PressRoomForm form)
        => Assert.Single(ReviewSurfaceContractTests.Flatten(form).OfType<CheckBox>(),
            control => control.Name == "press-green-input");

    private static Button Review(PressRoomForm form)
        => Assert.IsType<Button>(ReviewSurfaceContractTests.ByName(form,
            UiStrings.WithoutMnemonic(UiStrings.ReviewAndApprove)));

    private static ListBox Presses(PressRoomForm form)
        => Assert.IsType<ListBox>(ReviewSurfaceContractTests.ByName(form,
            UiStrings.WithoutMnemonic(UiStrings.PressList)));

    private static TextBox[] FreeText(PressRoomForm form)
        => [.. ReviewSurfaceContractTests.Flatten(form).OfType<TextBox>().Where(control => control.Parent is not NumericUpDown)];

    private static void Select(PressRoomForm form, string id, string version)
    {
        Presses(form).SelectedIndex = PressRoomCatalog.All.ToList().FindIndex(definition => definition.Id == id);
        var versions = Assert.Single(ReviewSurfaceContractTests.Flatten(form).OfType<ComboBox>(),
            control => control.Name == "recipe-version");
        versions.SelectedIndex = PressRoomCatalog.AllVersions.Where(definition => definition.Id == id)
            .ToList().FindIndex(definition => definition.Recipe.Version == version);
        Assert.Equal(version, form.SelectedPress!.Recipe.Version);
    }

    private static void InvokeAuthoringHandler(PressRoomForm form)
        => typeof(PressRoomForm).GetMethod("ReviewAndApprove", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, null);

    private static void AssertSinksLocked(PressRoomForm form)
    {
        Assert.Null(form.ApprovedResult);
        foreach (var caption in new[] { UiStrings.PrintButton, UiStrings.OpenPrintView,
            UiStrings.ExportEllipsis, UiStrings.SaveToLibrary, UiStrings.TileForWall })
        {
            Assert.False(ReviewSurfaceContractTests.ByName(form, UiStrings.WithoutMnemonic(caption)).Enabled);
        }
    }
}
