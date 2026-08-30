// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Storage;

namespace Foundry.Tests.UiAutomation;

/// <summary>
/// In-process contract for the one generated surface behind all built-in
/// module doors. Fixtures are synthetic; Access Remix exercises a real Green
/// project save/reopen and a fresh Gate B pass without any learner material.
/// </summary>
[Collection(ProjectLibraryRootTestGroup.Name)]
public sealed class ModuleStudioContractTests : IDisposable
{
    private static readonly DateTimeOffset ApprovalInstant = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] AmberModeKeys = ["exit-lens", "rubric-relay"];
    private readonly string _originalLibraryRoot = AppServices.LibraryRoot;
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "ocf-module-studio-tests",
        Guid.NewGuid().ToString("N"),
        EngineIdentity.EngineVersion,
        "projects");

    public ModuleStudioContractTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        AppServices.LibraryRoot = _temporaryDirectory;
    }

    public void Dispose()
    {
        AppServices.LibraryRoot = _originalLibraryRoot;
        try
        {
            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temporary test-output cleanup is best-effort.
        }
    }

    [Fact]
    public void The_surface_shows_exactly_ten_doors_and_eleven_modes_including_both_scaffold_modes()
        => WithForm(GateRespectingApprove, form =>
        {
            var doors = DoorList(form);
            Assert.Equal(10, doors.Items.Count);
            Assert.Equal(
                ModuleStudioCatalog.All.Select(door => door.Display.Fallback),
                doors.Items.Cast<object>().Select(item => item.ToString()));

            var visibleModes = new List<string>();
            foreach (var door in ModuleStudioCatalog.All)
            {
                SelectDoor(form, door.Id);
                visibleModes.AddRange(ModeList(form).Items.Cast<object>().Select(item => item.ToString()!));
            }

            Assert.Equal(11, visibleModes.Count);
            Assert.Contains("Scaffold packet", visibleModes, StringComparer.Ordinal);
            Assert.Contains("Task entry", visibleModes, StringComparer.Ordinal);
            Assert.Equal(11, visibleModes.Distinct(StringComparer.Ordinal).Count());
        });

    [Fact]
    public void The_press_room_carries_the_built_in_studios_door()
        => Sta.Run(() =>
        {
            using var form = new PressRoomForm(_ => null);
            form.Show();

            Assert.IsType<Button>(ReviewSurfaceContractTests.ByName(form, "Built-in studios…"));
        });

    [Fact]
    public void Every_mode_uses_named_roled_standard_controls_only()
        => WithForm(GateRespectingApprove, form =>
        {
            foreach (var door in ModuleStudioCatalog.All)
            {
                foreach (var mode in door.Modes)
                {
                    SelectMode(form, mode.Key);
                    var controls = ReviewSurfaceContractTests.Flatten(form).ToList();

                    Assert.All(controls, control => Assert.Equal(
                        typeof(Control).Assembly,
                        control.GetType().Assembly));

                    var focusable = controls.Where(control => control.TabStop && control.CanSelect).ToList();
                    Assert.NotEmpty(focusable);
                    Assert.All(focusable, control =>
                    {
                        Assert.False(string.IsNullOrWhiteSpace(control.AccessibilityObject.Name),
                            $"{mode.Key}: {control.GetType().Name} is an unnamed focusable control.");
                        Assert.NotEqual(AccessibleRole.None, control.AccessibilityObject.Role);
                    });
                }
            }
        });

    [Fact]
    public void All_eight_synthetic_modes_cold_start_to_typed_approval_with_catalog_defaults()
        => WithForm(GateRespectingApprove, form =>
        {
            var syntheticModes = ModuleStudioCatalog.All.SelectMany(door => door.Modes)
                .Where(mode => mode.DefaultsAreSynthetic)
                .ToList();
            Assert.Equal(8, syntheticModes.Count);

            foreach (var mode in syntheticModes)
            {
                SelectMode(form, mode.Key);
                ReviewButton(form).PerformClick();

                Assert.NotNull(form.ApprovedResult);
                Assert.IsType<ApprovedArtifact>(form.ApprovedResult);
                Assert.Equal(DataLane.Green, form.ApprovedResult.Revision.Lane);
                Assert.Equal(ArtifactPurpose.ClassroomSupport, form.ApprovedResult.Revision.Purpose);
                Assert.Contains("Approved", form.StatusText, StringComparison.Ordinal);
                AssertSinks(form, enabled: true);
            }
        });

    [Fact]
    public void Representative_field_changes_and_every_output_option_revoke_approval_and_all_sinks()
        => WithForm(GateRespectingApprove, form =>
        {
            AssertMutationRevokes(form, "board-to-brief", () =>
                ((TextBox)FieldControl(form, "Document language")).Text = "fr");

            AssertMutationRevokes(form, "board-to-brief", () =>
            {
                var table = (DataGridView)FieldControl(form, "Verified lines and roles");
                table.Rows[0].Cells[0].Value = "Tuesday";
            });

            AssertMutationRevokes(form, "lesson-loom", () =>
                ((NumericUpDown)FieldControl(form, "Total minutes")).Value = 46);

            AssertMutationRevokes(form, "source-lens", () =>
                ((CheckBox)FieldControl(form, "Transcript verified by teacher")).Checked = false);

            AssertMutationRevokes(form, "source-lens", () =>
                ((TextBox)FieldControl(form, "Verified excerpt")).AppendText(" Synthetic edit."));

            SelectMode(form, "board-to-brief");
            ApproveAndAssertSinks(form);
            OutputAudience(form).SelectedIndex = 1;
            AssertApprovalRevoked(form);

            ApproveAndAssertSinks(form);
            TextScale(form).Value = 125;
            AssertApprovalRevoked(form);

            ApproveAndAssertSinks(form);
            TargetLanguageFirst(form).Checked = true;
            AssertApprovalRevoked(form);
        });

    [Fact]
    public void Free_text_edits_clear_both_lane_and_intended_use_until_the_teacher_reclassifies_them()
        => WithForm(GateRespectingApprove, form =>
        {
            SelectMode(form, "source-lens");
            var excerpt = (TextBox)FieldControl(form, "Verified excerpt");
            excerpt.AppendText(" Synthetic teacher-authored addition.");

            var confirmation = GreenConfirmation(form);
            var purpose = PurposeClassification(form);
            Assert.False(confirmation.Checked);
            Assert.False(purpose.Checked);
            Assert.False(ReviewButton(form).Enabled);
            Assert.Contains("remain Amber", form.StatusText, StringComparison.Ordinal);

            confirmation.Checked = true;
            Assert.False(ReviewButton(form).Enabled);
            Assert.Contains("intended use", form.StatusText, StringComparison.OrdinalIgnoreCase);
            purpose.Checked = true;
            Assert.True(ReviewButton(form).Enabled);
            ReviewButton(form).PerformClick();
            Assert.NotNull(form.ApprovedResult);
        });

    [Fact]
    public void Builder_blockers_never_open_review_or_unlock_outputs()
        => Sta.Run(() =>
        {
            var reviews = 0;
            using var form = new ModuleStudioForm(session =>
            {
                reviews++;
                return GateRespectingApprove(session);
            });
            form.Show();
            SelectMode(form, "source-lens");
            ((CheckBox)FieldControl(form, "Transcript verified by teacher")).Checked = false;

            ReviewButton(form).PerformClick();

            Assert.Equal(0, reviews);
            Assert.Null(form.ApprovedResult);
            Assert.Contains("blocking issue", form.StatusText, StringComparison.Ordinal);
            AssertSinks(form, enabled: false);
        });

    [Fact]
    public void Malformed_keyboard_language_tag_is_refused_before_GateB_opens()
        => Sta.Run(() =>
        {
            var reviews = 0;
            using var form = new ModuleStudioForm(session =>
            {
                reviews++;
                return GateRespectingApprove(session);
            });
            form.Show();
            SelectMode(form, "directions-duet");
            ((TextBox)FieldControl(form, "Source language")).Text = "e n";
            GreenConfirmation(form).Checked = true;
            PurposeClassification(form).Checked = true;

            Assert.True(ReviewButton(form).Enabled);
            ReviewButton(form).PerformClick();

            Assert.Equal(0, reviews);
            Assert.Null(form.ApprovedResult);
            Assert.Contains("structurally valid language tag", form.StatusText, StringComparison.Ordinal);
            AssertSinks(form, enabled: false);
        });

    [Fact]
    public void Exit_and_Rubric_are_visible_but_build_is_disabled_and_the_written_district_gate_is_spoken()
        => WithForm(GateRespectingApprove, form =>
        {
            foreach (var modeKey in AmberModeKeys)
            {
                SelectMode(form, modeKey);

                Assert.False(ReviewButton(form).Enabled);
                Assert.False(((FlowLayoutPanel)ReviewSurfaceContractTests.ByName(form, "Module inputs")).Enabled);
                Assert.Contains("Written district authorization is required", form.StatusText, StringComparison.Ordinal);

                var confirmation = GreenConfirmation(form);
                confirmation.Checked = true;
                Assert.False(confirmation.Checked);
                Assert.False(ReviewButton(form).Enabled);
                Assert.Contains("Written district authorization is required", form.StatusText, StringComparison.Ordinal);

                var speakingStatus = ReviewSurfaceContractTests.Flatten(form).OfType<Label>()
                    .Single(label => string.Equals(label.Text, form.StatusText, StringComparison.Ordinal));
                Assert.Equal(form.StatusText, speakingStatus.AccessibilityObject.Name);

                var notes = (ListBox)ReviewSurfaceContractTests.ByName(form, "Module notes and safeguards");
                Assert.Contains(notes.Items.Cast<string>(), note =>
                    note.Contains("Written district authorization is required", StringComparison.Ordinal));
                AssertSinks(form, enabled: false);
            }
        });

    [Fact]
    public void A_saved_package_is_reclassified_and_freshly_reviewed_but_its_purpose_remains_unknown_to_Access()
    {
        var saved = SaveSyntheticGreenProject();
        Sta.Run(() =>
        {
            var reviewDrafts = new List<ArtifactId>();
            ApprovedArtifact? Review(ReviewSession session)
            {
                reviewDrafts.Add(session.Draft.Revision.Id);
                return GateRespectingApprove(session);
            }

            using var form = new ModuleStudioForm(
                Review,
                () => saved.Path,
                loadedProjectPreflight: ConfirmSyntheticLoadedProject);
            form.Show();
            SelectMode(form, "access-remix");

            Assert.False(ReviewButton(form).Enabled);
            Assert.Null(form.ApprovedResult);
            Assert.Contains("Green confirmation", form.StatusText, StringComparison.Ordinal);
            AssertSinks(form, enabled: false);

            ClickAndDrain(ActionButton(form, "Choose approved project…"));
            Assert.Single(reviewDrafts);
            Assert.NotEqual(saved.Artifact.Revision.Id, reviewDrafts[0]);
            Assert.Contains("fresh named review", form.StatusText, StringComparison.Ordinal);
            Assert.Null(form.ApprovedResult);

            var operation = Assert.IsType<ComboBox>(FieldControl(form, "Layout operation"));
            Assert.Equal(["Chunk steps", "One step per panel"],
                operation.Items.Cast<object>().Select(item => item.ToString()));
            operation.SelectedIndex = operation.Items.Cast<object>().ToList()
                .FindIndex(item => string.Equals(item.ToString(), "One step per panel", StringComparison.Ordinal));

            ReviewButton(form).PerformClick();

            Assert.Single(reviewDrafts);
            Assert.Null(form.ApprovedResult);
            Assert.Contains("requires an approved classroom-support purpose", form.StatusText, StringComparison.Ordinal);
            AssertSinks(form, enabled: false);
        });
    }

    [Fact]
    public void Access_accepts_only_the_current_exact_in_memory_classroom_support_approval()
        => Sta.Run(() =>
        {
            using var form = new ModuleStudioForm(GateRespectingApprove);
            form.Show();

            SelectMode(form, "board-to-brief");
            ApproveAndAssertSinks(form);
            var trustedSource = form.ApprovedResult;

            SelectMode(form, "access-remix");
            Assert.Equal(
                "Current exact approval selected",
                ReviewSurfaceContractTests.Flatten(form).OfType<TextBox>().Single(text => text.ReadOnly).Text);
            GreenConfirmation(form).Checked = true;

            var operation = Assert.IsType<ComboBox>(FieldControl(form, "Layout operation"));
            operation.SelectedIndex = operation.Items.Cast<object>().ToList()
                .FindIndex(item => string.Equals(item.ToString(), "One step per panel", StringComparison.Ordinal));
            ReviewButton(form).PerformClick();

            Assert.NotNull(trustedSource);
            Assert.NotNull(form.ApprovedResult);
            Assert.NotSame(trustedSource, form.ApprovedResult);
            Assert.Equal(ArtifactPurpose.ClassroomSupport, form.ApprovedResult.Revision.Purpose);
            AssertSinks(form, enabled: true);
        });

    [Fact]
    public void A_declined_fresh_source_review_leaves_Access_locked()
    {
        var saved = SaveSyntheticGreenProject();
        Sta.Run(() =>
        {
            var reviewCalls = 0;
            using var form = new ModuleStudioForm(
                _ =>
                {
                    reviewCalls++;
                    return null;
                },
                () => saved.Path,
                loadedProjectPreflight: ConfirmSyntheticLoadedProject);
            form.Show();
            SelectMode(form, "access-remix");

            ClickAndDrain(ActionButton(form, "Choose approved project…"));

            Assert.Equal(1, reviewCalls);
            Assert.Null(form.ApprovedResult);
            Assert.Contains("not approved", form.StatusText, StringComparison.Ordinal);
            Assert.Contains("remains locked", form.StatusText, StringComparison.Ordinal);
            Assert.Equal("No approved project selected",
                ReviewSurfaceContractTests.Flatten(form).OfType<TextBox>().Single(text => text.ReadOnly).Text);
            AssertSinks(form, enabled: false);
        });
    }

    [Theory]
    [InlineData(ArtifactPurpose.Unknown)]
    [InlineData(ArtifactPurpose.FormalOrHighStakesAssessment)]
    public void Access_cannot_promote_an_unknown_or_assessment_project_from_the_keyboard(ArtifactPurpose purpose)
    {
        var saved = SaveSyntheticGreenProject(purpose);
        Sta.Run(() =>
        {
            using var form = new ModuleStudioForm(
                GateRespectingApprove,
                () => saved.Path,
                loadedProjectPreflight: ConfirmSyntheticLoadedProject);
            form.Show();
            SelectMode(form, "access-remix");

            ClickAndDrain(ActionButton(form, "Choose approved project…"));
            Assert.True(GreenConfirmation(form).Checked);

            ReviewButton(form).PerformClick();

            Assert.Null(form.ApprovedResult);
            Assert.Contains("requires an approved classroom-support purpose", form.StatusText, StringComparison.Ordinal);
            AssertSinks(form, enabled: false);
        });
    }

    [Fact]
    public void A_review_runner_cannot_substitute_a_different_approved_revision()
        => Sta.Run(() =>
        {
            static ApprovedArtifact? Substitute(ReviewSession _)
            {
                var other = AppServices.SessionOverGreen(
                    new ArtifactDocument([new Heading(1, "Different synthetic draft")]),
                    ArtifactPurpose.ClassroomSupport);
                return GateRespectingApprove(other);
            }

            using var form = new ModuleStudioForm(Substitute);
            form.Show();
            ReviewButton(form).PerformClick();

            Assert.Null(form.ApprovedResult);
            Assert.Contains("without approval", form.StatusText, StringComparison.Ordinal);
            AssertSinks(form, enabled: false);
        });

    [Fact]
    public void A_form_state_change_during_review_invalidates_the_returned_approval()
        => Sta.Run(() =>
        {
            ModuleStudioForm? form = null;
            ApprovedArtifact? ChangeThenApprove(ReviewSession session)
            {
                TextScale(form!).Value = 125;
                return GateRespectingApprove(session);
            }

            using (form = new ModuleStudioForm(ChangeThenApprove))
            {
                form.Show();
                ReviewButton(form).PerformClick();

                Assert.Null(form.ApprovedResult);
                Assert.Contains("without approval", form.StatusText, StringComparison.Ordinal);
                AssertSinks(form, enabled: false);
            }
        });

    [Fact]
    public void Export_accepts_only_recipe_declared_HTML_and_forwards_audience_scale_and_language_order()
        => Sta.Run(() =>
        {
            Directory.CreateDirectory(_temporaryDirectory);
            ModuleStudioForm.ExportChoice? nextExport = null;
            using var form = new ModuleStudioForm(
                GateRespectingApprove,
                exportPicker: () => nextExport);
            form.Show();
            SelectMode(form, "directions-duet");

            OutputAudience(form).SelectedIndex = OutputAudience(form).Items.Cast<object>().ToList()
                .FindIndex(item => string.Equals(item.ToString(), "Learner copy", StringComparison.Ordinal));
            TextScale(form).Value = 175;
            TargetLanguageFirst(form).Checked = true;
            ReviewButton(form).PerformClick();
            Assert.NotNull(form.ApprovedResult);

            var mode = ModuleStudioCatalog.ByModeKey("directions-duet");
            Assert.Equal([RenderTarget.AccessibleHtml, RenderTarget.PrintHtml], mode.Recipe.SupportedExports);

            var accessiblePath = Path.Combine(_temporaryDirectory, "directions-accessible.html");
            nextExport = new ModuleStudioForm.ExportChoice(accessiblePath, RenderTarget.AccessibleHtml);
            ClickAndDrain(ExportButton(form));
            var accessible = File.ReadAllText(accessiblePath);
            Assert.Contains("body { font-size: 175%; }", accessible, StringComparison.Ordinal);
            Assert.DoesNotContain("<aside class=\"teacher-only\"", accessible, StringComparison.Ordinal);
            Assert.True(
                accessible.IndexOf("Abra la carpeta 3.", StringComparison.Ordinal)
                    < accessible.IndexOf("Open folder 3.", StringComparison.Ordinal),
                "Target-language-first must put the approved target text before its source text.");

            var printPath = Path.Combine(_temporaryDirectory, "directions-print.html");
            nextExport = new ModuleStudioForm.ExportChoice(printPath, RenderTarget.PrintHtml);
            ClickAndDrain(ExportButton(form));
            Assert.Contains("@page { margin: 12mm; }", File.ReadAllText(printPath), StringComparison.Ordinal);

            var refusedPath = Path.Combine(_temporaryDirectory, "directions.png");
            nextExport = new ModuleStudioForm.ExportChoice(refusedPath, RenderTarget.Png);
            ClickAndDrain(ExportButton(form));
            Assert.False(File.Exists(refusedPath));
            Assert.Contains("refused", form.StatusText, StringComparison.Ordinal);
            Assert.Contains(nameof(RenderTarget.Png), form.StatusText, StringComparison.Ordinal);
        });

    private static ApprovedArtifact? GateRespectingApprove(ReviewSession session)
    {
        session.SetRequiredIssuesAcknowledged(acknowledged: true);
        return session.CanApprove
            ? session.Approve("Synthetic test teacher", ApprovalInstant)
            : null;
    }

    private static LoadedProjectGreenConfirmation ConfirmSyntheticLoadedProject(LoadedProject loaded)
        => AppServices.ConfirmLoadedProjectGreen(
            loaded,
            new LoadedProjectGreenChecklist(
                IsGreenQualifyingContent: true,
                HasNoLearnerLinkedContent: true,
                HasNoRestrictedContent: true));

    private static void WithForm(Func<ReviewSession, ApprovedArtifact?> review, Action<ModuleStudioForm> assert)
        => Sta.Run(() =>
        {
            using var form = new ModuleStudioForm(review);
            form.Show();
            assert(form);
        });

    private static void SelectDoor(ModuleStudioForm form, string doorId)
    {
        var index = ModuleStudioCatalog.All.ToList().FindIndex(door => door.Id == doorId);
        Assert.True(index >= 0, $"Unknown door '{doorId}'.");
        DoorList(form).SelectedIndex = index;
    }

    private static void SelectMode(ModuleStudioForm form, string modeKey)
    {
        var door = ModuleStudioCatalog.All.Single(candidate => candidate.Modes.Any(mode => mode.Key == modeKey));
        SelectDoor(form, door.Id);
        var index = door.Modes.ToList().FindIndex(mode => mode.Key == modeKey);
        ModeList(form).SelectedIndex = index;
        Assert.Equal(modeKey, form.SelectedMode?.Key);
    }

    private static ListBox DoorList(ModuleStudioForm form)
        => (ListBox)ReviewSurfaceContractTests.ByName(form, "Module doors");

    private static ComboBox ModeList(ModuleStudioForm form)
        => ReviewSurfaceContractTests.Flatten(form).OfType<ComboBox>()
            .Single(combo => combo.AccessibilityObject.Name == "Studio mode");

    private static Control FieldControl(ModuleStudioForm form, string accessibleName)
        => ReviewSurfaceContractTests.Flatten(form)
            .Single(control => control.Parent is GroupBox
                && control.AccessibilityObject.Name == accessibleName);

    private static ComboBox OutputAudience(ModuleStudioForm form)
        => ReviewSurfaceContractTests.Flatten(form).OfType<ComboBox>()
            .Single(combo => combo.AccessibilityObject.Name == "Output audience");

    private static NumericUpDown TextScale(ModuleStudioForm form)
        => ReviewSurfaceContractTests.Flatten(form).OfType<NumericUpDown>()
            .Single(spinner => spinner.AccessibilityObject.Name == "Text scale percent");

    private static CheckBox TargetLanguageFirst(ModuleStudioForm form)
        => ReviewSurfaceContractTests.Flatten(form).OfType<CheckBox>()
            .Single(check => WithoutMnemonic(check.Text) == "Target-language first");

    private static CheckBox GreenConfirmation(ModuleStudioForm form)
        => ReviewSurfaceContractTests.Flatten(form).OfType<CheckBox>()
            .Single(check => WithoutMnemonic(check.Text).StartsWith(
                "I confirm these inputs are staged",
                StringComparison.Ordinal));

    private static Button ReviewButton(ModuleStudioForm form)
        => ActionButton(form, "Review and approve…");

    private static Button ExportButton(ModuleStudioForm form)
        => ActionButton(form, "Export…");

    private static IReadOnlyList<Control> Sinks(ModuleStudioForm form)
        =>
        [
            ActionButton(form, "Print"),
            ActionButton(form, "Open print view"),
            ExportButton(form),
            ActionButton(form, "Save to library"),
        ];

    private static Button ActionButton(ModuleStudioForm form, string visibleText)
        => ReviewSurfaceContractTests.Flatten(form).OfType<Button>()
            .Single(button => WithoutMnemonic(button.Text) == visibleText);

    private static CheckBox PurposeClassification(ModuleStudioForm form)
        => ReviewSurfaceContractTests.Flatten(form).OfType<CheckBox>()
            .Single(check => check.AccessibilityObject.Name ==
                "This artifact is classroom support, not a formal or high-stakes assessment.");

    private static string WithoutMnemonic(string text)
        => text.Replace("&&", "", StringComparison.Ordinal).Replace("&", "", StringComparison.Ordinal);

    private static void AssertSinks(ModuleStudioForm form, bool enabled)
        => Assert.All(Sinks(form), sink => Assert.Equal(enabled, sink.Enabled));

    private static void ApproveAndAssertSinks(ModuleStudioForm form)
    {
        ReviewButton(form).PerformClick();
        Assert.NotNull(form.ApprovedResult);
        AssertSinks(form, enabled: true);
    }

    private static void AssertMutationRevokes(ModuleStudioForm form, string modeKey, Action mutate)
    {
        if (string.Equals(form.SelectedMode?.Key, modeKey, StringComparison.Ordinal))
        {
            var resetMode = ModuleStudioCatalog.All
                .SelectMany(door => door.Modes)
                .First(mode => mode.IsBuildAvailable && !string.Equals(mode.Key, modeKey, StringComparison.Ordinal));
            SelectMode(form, resetMode.Key);
        }

        SelectMode(form, modeKey);
        ApproveAndAssertSinks(form);
        mutate();
        AssertApprovalRevoked(form);
    }

    private static void AssertApprovalRevoked(ModuleStudioForm form)
    {
        Assert.Null(form.ApprovedResult);
        Assert.True(
            form.StatusText.Contains("fresh review", StringComparison.Ordinal)
                || form.StatusText.Contains("Green confirmation", StringComparison.Ordinal),
            form.StatusText);
        AssertSinks(form, enabled: false);
    }

    private static void ClickAndDrain(Button button)
    {
        button.PerformClick();
        System.Windows.Forms.Application.DoEvents();
    }

    private (string Path, ApprovedArtifact Artifact) SaveSyntheticGreenProject(
        ArtifactPurpose purpose = ArtifactPurpose.ClassroomSupport)
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var document = new ArtifactDocument(
        [
            new Heading(1, "Synthetic cleanup routine"),
            new StepRow("Place the practice cards in the tray."),
            new StepRow("Return the synthetic marker to the bin."),
            new StepRow("Check the sample table."),
        ],
        "en");
        var artifact = ApprovalGate.Approve(
            DraftArtifact.New(document, DataLane.Green, purpose),
            "Synthetic test teacher",
            DocumentValidator.Validate(document),
            ApprovalInstant);

        AppServices.SaveToLibrary(
            artifact,
            "synthetic-access-source",
            "synthetic-test",
            "synthetic.access-source",
            "0.1.0",
            new AppServices.NoAssetsCatalog());

        return (Assert.Single(Directory.GetFiles(_temporaryDirectory, $"*{OcfprojProjectStore.Extension}")), artifact);
    }
}
