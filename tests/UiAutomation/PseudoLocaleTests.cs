// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.IO;
using System.Windows.Automation;
using Foundry.App.WinForms;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Storage;

namespace Foundry.Tests.UiAutomation;

// The pseudo-locale smoke pass (forge item 4): under "ẋẋ" every chrome string
// stretches at least forty percent, is bracketed so truncation confesses, and
// the whole window mirrors right-to-left — so the multilingual seat's week-3
// hour is spent on real language, not on layout defects a machine can catch.
// This assembly runs serialized, so flipping the static locale mode is safe;
// every test restores Neutral in a finally block.

public class PseudoLocaleTests
{
    private static void InPseudo(Action assert)
    {
        UiLocale.Set(UiLocaleMode.Pseudo);
        try
        {
            assert();
        }
        finally
        {
            UiLocale.Set(UiLocaleMode.Neutral);
        }
    }

    [Fact]
    public void Neutral_is_the_default_and_returns_the_exact_english_catalog()
    {
        Assert.Equal(UiLocaleMode.Neutral, UiLocale.Mode);
        Assert.Equal("&Apply edit", UiStrings.ApplyEdit);
        Assert.Equal("en", UiLocale.LanguageTag);
    }

    [Fact]
    public void Pseudo_strings_stretch_forty_percent_bracket_the_ends_and_keep_mnemonics()
        => InPseudo(() =>
        {
            Assert.Equal("ẋẋ", UiLocale.LanguageTag);

            foreach (var (neutral, pseudo) in new[]
            {
                ("&Apply edit", UiStrings.ApplyEdit),
                ("Move &down", UiStrings.MoveDown),
                ("Draft elements", UiStrings.DraftElements),
                ("I saw something concerning — &pause here", UiStrings.SafetyPause),
            })
            {
                Assert.StartsWith("⟦", pseudo, StringComparison.Ordinal);
                Assert.EndsWith("⟧", pseudo, StringComparison.Ordinal);
                Assert.True(pseudo.Length >= neutral.Length * 1.4,
                    $"'{pseudo}' is not 40% longer than '{neutral}'");
            }

            // The mnemonic character survives untransformed: Alt+key still works.
            Assert.Contains("&A", UiStrings.ApplyEdit, StringComparison.Ordinal);
            Assert.Contains("&d", UiStrings.MoveDown, StringComparison.Ordinal);
        });

    [Fact]
    public void Pseudo_transformation_is_deterministic_and_keeps_format_placeholders()
        => InPseudo(() =>
        {
            Assert.Equal(UiStrings.ApproveDescription, UiStrings.ApproveDescription);

            Assert.Contains("{0}", UiStrings.StatusLaneConfirmed, StringComparison.Ordinal);
            Assert.Contains("{1}", UiStrings.NodeHeading, StringComparison.Ordinal);

            // Formatting a pseudo template must not throw and must embed the value.
            Assert.Contains("Amber", UiStrings.Format(UiStrings.StatusLaneConfirmed, "Amber"), StringComparison.Ordinal);
        });

    [Fact]
    public void The_product_name_never_localizes_but_the_phrase_beside_it_does()
        => InPseudo(() =>
        {
            Assert.StartsWith(ProductIdentity.PublicName, UiStrings.ReviewWindowTitle, StringComparison.Ordinal);
            Assert.Contains("⟦", UiStrings.ReviewWindowTitle, StringComparison.Ordinal);
        });

    [Fact]
    public void Every_constructed_surface_exposes_only_catalog_backed_visible_chrome_in_pseudo()
        => InPseudo(() => Sta.Run(() =>
        {
            using var review = UiaHarness.CreateReviewForm();
            using var capture = UiaHarness.CreateCaptureForm();
            using var pressRoom = new PressRoomForm(_ => null);
            using var allAboard = new AllAboardForm(new AppServices.NoAssetsCatalog(), _ => null);
            using var modules = new ModuleStudioForm(_ => null);
            using var preflight = new LoadedProjectPreflightForm(SyntheticLoadedProject());
            using var tile = new TileForm();

            foreach (var form in new Form[]
            {
                review,
                capture,
                pressRoom,
                allAboard,
                modules,
                preflight,
                tile,
            })
            {
                AssertSurfaceChromeIsCatalogBacked(form);
            }

            ExerciseEveryReviewTab(review);
            ExerciseEveryPress(pressRoom);
            ExerciseEveryAllAboardMode(allAboard);
            ExerciseEveryModuleMode(modules);
            ExerciseEveryNodeEditorVariant();
        }));

    private static void AssertSurfaceChromeIsCatalogBacked(Form form)
    {
        if (!form.Visible)
        {
            form.Show();
        }

        System.Windows.Forms.Application.DoEvents();

        // The renderer's forced-RTL discipline, wired into the chrome.
        Assert.Equal(RightToLeft.Yes, form.RightToLeft);
        Assert.True(form.RightToLeftLayout, "Pseudo-locale must mirror the window layout");

        // Every focusable control speaks the pseudo catalog — a bare
        // (unbracketed) name is a string that escaped localization.
        foreach (var control in ReviewSurfaceContractTests.Flatten(form)
            .Where(c => c.TabStop && c.CanSelect))
        {
            Assert.Contains("⟦", control.AccessibilityObject.Name, StringComparison.Ordinal);
        }

        AssertVisibleChromeIsCatalogBacked(form);
        AssertChoiceChromeIsCatalogBacked(form);
    }

    private static void ExerciseEveryReviewTab(ReviewForm form)
    {
        var tabs = ReviewSurfaceContractTests.Flatten(form).OfType<TabControl>().Single();
        Assert.Equal(3, tabs.TabPages.Count);
        for (var index = 0; index < tabs.TabPages.Count; index++)
        {
            tabs.SelectedIndex = index;
            AssertSurfaceChromeIsCatalogBacked(form);
        }
    }

    private static void ExerciseEveryPress(PressRoomForm form)
    {
        var presses = ReviewSurfaceContractTests.Flatten(form).OfType<ListBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.PressList);
        for (var index = 0; index < presses.Items.Count; index++)
        {
            presses.SelectedIndex = index;
            AssertSurfaceChromeIsCatalogBacked(form);
        }
    }

    private static void ExerciseEveryAllAboardMode(AllAboardForm form)
    {
        var modes = ReviewSurfaceContractTests.Flatten(form).OfType<ComboBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.OutputMode);
        for (var index = 0; index < modes.Items.Count; index++)
        {
            modes.SelectedIndex = index;
            AssertSurfaceChromeIsCatalogBacked(form);
        }
    }

    private static void ExerciseEveryModuleMode(ModuleStudioForm form)
    {
        var doors = ReviewSurfaceContractTests.Flatten(form).OfType<ListBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.ModuleDoors);
        var modes = ReviewSurfaceContractTests.Flatten(form).OfType<ComboBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.ModuleMode);

        for (var doorIndex = 0; doorIndex < doors.Items.Count; doorIndex++)
        {
            doors.SelectedIndex = doorIndex;
            for (var modeIndex = 0; modeIndex < modes.Items.Count; modeIndex++)
            {
                modes.SelectedIndex = modeIndex;
                AssertSurfaceChromeIsCatalogBacked(form);
            }
        }
    }

    private static void ExerciseEveryNodeEditorVariant()
    {
        DocumentNode[] nodes =
        [
            new Heading(2, "Synthetic heading"),
            new Paragraph("Synthetic paragraph."),
            new OrderedSteps(["Synthetic first step", "Synthetic second step"]),
            new UnorderedList(["Synthetic first item", "Synthetic second item"]),
            new ChoiceSet(["Synthetic choice A", "Synthetic choice B"]),
            new TableNode(["Synthetic heading A", "Synthetic heading B"], [["A1", "B1"]]),
            new Card("Synthetic card", "Synthetic card body."),
            new ImageReference(new AssetId("symbol.synthetic-pseudo"), "Synthetic symbol"),
            new BilingualPair("Synthetic source", "Synthetic target", "en", "es"),
            new EvidenceLink("Synthetic claim", "authorized:synthetic-line-1"),
            new Citation("Synthetic citation"),
            new TeacherOnlyNotice("Synthetic teacher-only notice"),
            new StepRow(
                "Synthetic step row",
                new ImageReference(new AssetId("symbol.synthetic-step"), "Synthetic step symbol"),
                "Fila sintética",
                "en",
                "es"),
            new PageBreak(),
            new VectorGraphic(
                210,
                297,
                [
                    new LineSeg(10, 10, 100, 20, 0.35, Dashed: true),
                    new CircleShape(40, 50, 12, 0.5, Filled: false),
                    new RectShape(70, 80, 60, 35, 0.4, Filled: true),
                    new TextLabel(105, 140, "Synthetic vector label", 5, TextAnchor.Middle),
                ],
                "Synthetic complex vector sheet"),
        ];

        foreach (var node in nodes)
        {
            using var editor = new NodeEditorForm(node);
            AssertSurfaceChromeIsCatalogBacked(editor);
            if (node is not VectorGraphic)
            {
                continue;
            }

            var primitives = ReviewSurfaceContractTests.Flatten(editor).OfType<ListBox>()
                .Single(control => control.AccessibilityObject.Name == UiStrings.EditorVectorPrimitives);
            for (var index = 0; index < primitives.Items.Count; index++)
            {
                primitives.SelectedIndex = index;
                AssertSurfaceChromeIsCatalogBacked(editor);
            }
        }
    }

    private static void AssertVisibleChromeIsCatalogBacked(Form form)
    {
        Assert.Contains("⟦", form.Text, StringComparison.Ordinal);

        foreach (var control in ReviewSurfaceContractTests.Flatten(form))
        {
            if (control is Label or GroupBox or ButtonBase or TabPage
                && !string.IsNullOrWhiteSpace(control.Text))
            {
                Assert.True(
                    control.Text.Contains('⟦'),
                    $"{form.GetType().Name} contains visible {control.GetType().Name} chrome " +
                    $"outside the catalog: '{control.Text}'.");
            }

            if (control is DataGridView grid)
            {
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    if (!string.IsNullOrWhiteSpace(column.HeaderText))
                    {
                        Assert.True(
                            column.HeaderText.Contains('⟦'),
                            $"{form.GetType().Name} contains a grid header outside the catalog: " +
                            $"'{column.HeaderText}'.");
                    }
                }
            }
        }
    }

    private static void AssertChoiceChromeIsCatalogBacked(Form form)
    {
        foreach (var combo in ReviewSurfaceContractTests.Flatten(form).OfType<ComboBox>())
        {
            foreach (var item in combo.Items.Cast<object>())
            {
                var text = item.ToString();
                Assert.False(string.IsNullOrWhiteSpace(text));
                Assert.Contains("⟦", text, StringComparison.Ordinal);
            }
        }

        var catalogListNames = new HashSet<string>(StringComparer.Ordinal)
        {
            UiStrings.PressList,
            UiStrings.ModuleDoors,
            UiStrings.EditorVectorPrimitives,
        };
        foreach (var list in ReviewSurfaceContractTests.Flatten(form).OfType<ListBox>()
            .Where(control => control.AccessibilityObject.Name is { } name
                && catalogListNames.Contains(name)))
        {
            foreach (var item in list.Items.Cast<object>())
            {
                var text = item.ToString();
                Assert.False(string.IsNullOrWhiteSpace(text));
                Assert.Contains("⟦", text, StringComparison.Ordinal);
            }
        }
    }

    private static LoadedProject SyntheticLoadedProject()
    {
        var document = new ArtifactDocument(
            [new Heading(1, "Synthetic exact pseudo fixture"), new Paragraph("No learner data.")],
            "en");
        var manifest = new ProjectManifest(
            EngineIdentity.ProjectSchemaVersion,
            Guid.Parse("8d92db3d-1a4c-4a31-aa23-5a0b04157da0"),
            "synthetic-module",
            "0.0.0",
            "synthetic-recipe",
            "0.0.0",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            DataLane.Green,
            "teacher-managed",
            "en",
            null,
            EngineIdentity.EngineVersion,
            "artifact.json",
            [],
            ArtifactPurpose.Unknown);
        return new LoadedProject(manifest, document, null, null);
    }

    [HeadedFact]
    public void The_pseudo_locale_switch_reaches_the_real_chrome_end_to_end()
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "Foundry.App.WinForms.exe");
        using var process = Process.Start(new ProcessStartInfo(exe, $"{UiaHarness.Switch} review {UiLocale.PseudoSwitch}"))!;
        try
        {
            AutomationElement? window = null;
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 20000 && window is null)
            {
                Thread.Sleep(200);
                window = AutomationElement.RootElement.FindFirst(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id));
            }

            Assert.NotNull(window);
            Assert.StartsWith(ProductIdentity.PublicName, window.Current.Name, StringComparison.Ordinal);
            Assert.Contains("⟦", window.Current.Name, StringComparison.Ordinal);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }
}
