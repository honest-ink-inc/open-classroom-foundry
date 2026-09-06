// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.UiAutomation;

/// <summary>In-process synthetic selection/approval controls; no output is written.</summary>
[Collection(ProjectLibraryRootTestGroup.Name)]
public sealed class RecipeVersionSelectionTests
{
    [Theory]
    [InlineData("board-to-brief")]
    [InlineData("lesson-loom")]
    [InlineData("source-lens")]
    public void Built_in_version_selection_clears_approval_and_saves_the_exact_selected_tuple(string key)
        => Sta.Run(() =>
        {
            var saved = new List<(string Id, string Version)>();
            using var form = new ModuleStudioForm(Approve,
                librarySaver: (artifact, hint, module, recipe, version, catalog, validation, profile) =>
                {
                    Assert.NotNull(artifact);
                    Assert.NotNull(validation);
                    saved.Add((recipe, version));
                    return "synthetic-no-file-written.ocfproj";
                });
            form.Show();
            var doors = Assert.IsType<ListBox>(ReviewSurfaceContractTests.ByName(form, UiStrings.WithoutMnemonic(UiStrings.ModuleDoors)));
            doors.SelectedIndex = ModuleStudioCatalog.All.ToList().FindIndex(door => door.Id == key);
            var selector = VersionSelector(form);
            Assert.Equal("0.1.0", form.SelectedMode!.Recipe.Version);
            Assert.Equal(2, selector.Items.Count);
            Assert.Contains("historical", selector.Items[0]!.ToString()!, StringComparison.Ordinal);
            Assert.Contains("held", selector.Items[1]!.ToString()!, StringComparison.Ordinal);
            Review(form);
            Assert.NotNull(form.ApprovedResult);
            Save(form);
            selector.SelectedIndex = 1;
            Assert.Null(form.ApprovedResult);
            Assert.False(SaveButton(form).Enabled);
            Assert.Equal("0.2.0", form.SelectedMode!.Recipe.Version);
            Review(form);
            Assert.NotNull(form.ApprovedResult);
            Save(form);
            Assert.Equal([(key, "0.1.0"), (key, "0.2.0")], saved);
            selector.SelectedIndex = 0;
            Assert.Null(form.ApprovedResult);
            Assert.False(SaveButton(form).Enabled);
        });

    [Theory]
    [InlineData("bar-chart", "press.charts")]
    [InlineData("goal-post", "press.learner-held")]
    [InlineData("calibration-proof", "press.calibration")]
    [InlineData("flashcards", "press.flashcards")]
    public void Press_version_selection_clears_approval_and_saves_the_exact_selected_tuple(string id, string recipeId)
        => Sta.Run(() =>
        {
            var saved = new List<(string Id, string Version)>();
            using var form = new PressRoomForm(Approve,
                librarySaver: (artifact, hint, module, recipe, version, catalog, validation, profile) =>
                {
                    Assert.NotNull(artifact);
                    saved.Add((recipe, version));
                    return "synthetic-no-file-written.ocfproj";
                });
            form.Show();
            var presses = Assert.IsType<ListBox>(ReviewSurfaceContractTests.ByName(form, UiStrings.WithoutMnemonic(UiStrings.PressList)));
            presses.SelectedIndex = PressRoomCatalog.All.ToList().FindIndex(press => press.Id == id);
            var selector = VersionSelector(form);
            Assert.Equal("0.1.0", form.SelectedPress!.Recipe.Version);
            Assert.Equal(2, selector.Items.Count);
            Review(form);
            Assert.NotNull(form.ApprovedResult);
            Save(form);
            selector.SelectedIndex = 1;
            Assert.Null(form.ApprovedResult);
            Assert.False(SaveButton(form).Enabled);
            Assert.Equal("0.2.0", form.SelectedPress!.Recipe.Version);
            Review(form);
            Save(form);
            Assert.Equal([(recipeId, "0.1.0"), (recipeId, "0.2.0")], saved);
            selector.SelectedIndex = 0;
            Assert.Null(form.ApprovedResult);
        });

    [Fact]
    public void Programmatic_version_change_during_review_cannot_reuse_the_outgoing_approval()
        => Sta.Run(() =>
        {
            ModuleStudioForm? active = null;
            using var form = new ModuleStudioForm(session =>
            {
                var selector = VersionSelector(active!);
                Assert.False(selector.Enabled);
                selector.SelectedIndex = 1;
                return Approve(session);
            });
            active = form;
            form.Show();
            Assert.Equal("board-to-brief", form.SelectedMode!.Key);
            Review(form);
            Assert.Equal("0.2.0", form.SelectedMode!.Recipe.Version);
            Assert.Null(form.ApprovedResult);
            Assert.False(SaveButton(form).Enabled);
        });

    [Fact]
    public void Programmatic_press_version_change_during_review_cannot_reuse_the_outgoing_approval()
        => Sta.Run(() =>
        {
            PressRoomForm? active = null;
            using var form = new PressRoomForm(session =>
            {
                var selector = VersionSelector(active!);
                Assert.False(selector.Enabled);
                selector.SelectedIndex = 1;
                return Approve(session);
            });
            active = form;
            form.Show();
            var presses = Assert.IsType<ListBox>(ReviewSurfaceContractTests.ByName(form,
                UiStrings.WithoutMnemonic(UiStrings.PressList)));
            presses.SelectedIndex = PressRoomCatalog.All.ToList().FindIndex(press => press.Id == "bar-chart");
            Assert.Equal("0.1.0", form.SelectedPress!.Recipe.Version);
            Review(form);
            Assert.Equal("0.2.0", form.SelectedPress!.Recipe.Version);
            Assert.Null(form.ApprovedResult);
            Assert.False(SaveButton(form).Enabled);
        });

    private static ApprovedArtifact Approve(ReviewSession session)
    {
        Assert.False(session.CanApprove);
        session.SetRequiredIssuesAcknowledged(true);
        return session.Approve("synthetic-version-review@example.invalid", new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero));
    }

    private static ComboBox VersionSelector(Control root)
        => Assert.Single(ReviewSurfaceContractTests.Flatten(root).OfType<ComboBox>(), control => control.Name == "recipe-version");

    private static Button SaveButton(Form root)
        => Assert.IsType<Button>(ReviewSurfaceContractTests.ByName(root, UiStrings.WithoutMnemonic(UiStrings.SaveToLibrary)));

    private static void Save(Form root)
    {
        var button = SaveButton(root);
        Assert.True(button.Enabled);
        button.PerformClick();
    }

    private static void Review(Form root)
    {
        var button = Assert.IsType<Button>(ReviewSurfaceContractTests.ByName(root, UiStrings.WithoutMnemonic(UiStrings.ReviewAndApprove)));
        Assert.True(button.Enabled);
        button.PerformClick();
    }
}
