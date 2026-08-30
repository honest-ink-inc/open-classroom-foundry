// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.App.WinForms;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Storage;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Foundry.Tests.Accessibility;

/// <summary>
/// Executable evidence for the minimum-hardware covenant. 1366 x 768 is a
/// non-breaking floor, not the product's design canvas. Forty pixels are
/// deliberately reserved for taskbar/window-manager chrome, so the asserted
/// application working area is the more conservative 1366 x 728.
/// </summary>
public sealed class MinimumHardwareFloorTests
{
    private static readonly Rectangle FloorWorkingArea = new(0, 0, 1366, 728);

    [Theory]
    [InlineData(UiLocaleMode.Neutral, 1.0f)]
    [InlineData(UiLocaleMode.Pseudo, 1.25f)]
    public void Every_shipped_surface_keeps_controls_and_status_reachable_at_the_floor(
        UiLocaleMode locale,
        float scale)
        => RunSta(() =>
        {
            UiLocale.Set(locale);
            try
            {
                using var review = UiaHarness.CreateReviewForm();
                using var capture = UiaHarness.CreateCaptureForm();
                using var pressRoom = new PressRoomForm(
                    reviewRunner: _ => null,
                    libraryPicker: () => null,
                    loadedProjectPreflight: _ => null);
                using var allAboard = new AllAboardForm(AppServices.SymbolCatalog(), _ => null);
                using var modules = new ModuleStudioForm(
                    reviewRunner: _ => null,
                    libraryPicker: () => null,
                    loadedProjectPreflight: _ => null);
                using var preflight = new LoadedProjectPreflightForm(SyntheticLoadedProject());
                using var tile = new TileForm();

                var surfaces = new Form[]
                {
                    review,
                    capture,
                    pressRoom,
                    allAboard,
                    modules,
                    preflight,
                    tile,
                };

                foreach (var surface in surfaces)
                {
                    PrepareAtFloor(surface, scale);
                    AssertFloor(surface);
                }

                ExerciseEveryPressAtFloor(pressRoom);
                ExerciseEveryAllAboardModeAtFloor(allAboard);
                ExerciseEveryModuleModeAtFloor(modules);
                ExerciseEveryNodeEditorVariantAtFloor(scale);
            }
            finally
            {
                UiLocale.Set(UiLocaleMode.Neutral);
            }
        });

    [Fact]
    public void Floor_assertion_catches_a_non_scrollable_offscreen_control()
        => RunSta(() =>
        {
            using var form = new Form { Size = new Size(400, 300) };
            var clipped = new Button
            {
                Text = "Synthetic clipped control",
                AccessibleName = "Synthetic clipped control",
                Location = new Point(390, 20),
                Size = new Size(180, 30),
            };
            form.Controls.Add(clipped);
            PrepareAtFloor(form, 1.0f, maximize: false);

            var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(form));
            Assert.Contains("Synthetic clipped control", failure.Message, StringComparison.Ordinal);
        });

    private static void ExerciseEveryPressAtFloor(PressRoomForm form)
    {
        var list = Descendants(form).OfType<ListBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.PressList);
        for (var index = 0; index < list.Items.Count; index++)
        {
            list.SelectedIndex = index;
            FlushLayout(form);
            AssertFloor(form);
        }
    }

    private static void ExerciseEveryAllAboardModeAtFloor(AllAboardForm form)
    {
        var modes = Descendants(form).OfType<ComboBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.OutputMode);
        for (var index = 0; index < modes.Items.Count; index++)
        {
            modes.SelectedIndex = index;
            FlushLayout(form);
            AssertFloor(form);
        }
    }

    private static void ExerciseEveryModuleModeAtFloor(ModuleStudioForm form)
    {
        var doors = Descendants(form).OfType<ListBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.ModuleDoors);
        var modes = Descendants(form).OfType<ComboBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.ModuleMode);

        for (var doorIndex = 0; doorIndex < doors.Items.Count; doorIndex++)
        {
            doors.SelectedIndex = doorIndex;
            for (var modeIndex = 0; modeIndex < modes.Items.Count; modeIndex++)
            {
                modes.SelectedIndex = modeIndex;
                FlushLayout(form);
                AssertFloor(form);
            }
        }
    }

    private static void ExerciseEveryNodeEditorVariantAtFloor(float scale)
    {
        DocumentNode[] nodes =
        [
            new Heading(2, "A deliberately longer synthetic heading at the hardware floor"),
            new Paragraph("A deliberately longer synthetic paragraph verifies that the multiline editor remains reachable without using learner data."),
            new OrderedSteps(["First synthetic step", "Second synthetic step", "Third synthetic step"]),
            new UnorderedList(["First synthetic item", "Second synthetic item", "Third synthetic item"]),
            new ChoiceSet(["Synthetic choice A", "Synthetic choice B", "Synthetic choice C"]),
            new TableNode(
                ["Synthetic heading A", "Synthetic heading B", "Synthetic heading C", "Synthetic heading D"],
                [
                    ["Synthetic cell A1", "Synthetic cell B1", "Synthetic cell C1", "Synthetic cell D1"],
                    ["Synthetic cell A2", "Synthetic cell B2", "Synthetic cell C2", "Synthetic cell D2"],
                ]),
            new Card("Synthetic card title", "Synthetic card body with enough text to exercise the multiline field."),
            new ImageReference(new AssetId("symbol.synthetic-floor"), "Synthetic floor symbol"),
            new BilingualPair("Synthetic source sentence.", "Oración sintética de destino.", "en", "es"),
            new EvidenceLink("Synthetic claim", "authorized:synthetic-line-1"),
            new Citation("Synthetic citation"),
            new TeacherOnlyNotice("Synthetic teacher-only notice"),
            new StepRow(
                "Synthetic step row",
                new ImageReference(new AssetId("symbol.synthetic-step"), "Synthetic step symbol"),
                "Fila de paso sintética",
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
            PrepareAtFloor(editor, scale);
            AssertFloor(editor);
            if (node is VectorGraphic)
            {
                var primitives = Descendants(editor).OfType<ListBox>()
                    .Single(control => control.AccessibilityObject.Name == UiStrings.EditorVectorPrimitives);
                for (var index = 0; index < primitives.Items.Count; index++)
                {
                    primitives.SelectedIndex = index;
                    FlushLayout(editor);
                    AssertFloor(editor);
                }
            }
        }
    }

    private static void PrepareAtFloor(Form form, float scale, bool maximize = true)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.ShowInTaskbar = false;
        form.Opacity = 0;
        if (scale != 1.0f)
        {
            form.Scale(new SizeF(scale, scale));
        }

        var requested = maximize
            ? FloorWorkingArea.Size
            : new Size(
                Math.Min(form.Width, FloorWorkingArea.Width),
                Math.Min(form.Height, FloorWorkingArea.Height));
        form.Bounds = new Rectangle(FloorWorkingArea.Location, requested);
        form.Show();
        FlushLayout(form);
    }

    private static void AssertFloor(Form form)
    {
        Assert.True(
            FloorWorkingArea.Contains(form.Bounds),
            $"{form.GetType().Name} extends beyond the 1366 x 768 floor working area: {form.Bounds}.");

        var candidates = Descendants(form)
            .Where(control => control.Visible && IsReachabilitySurface(control))
            .ToArray();
        Assert.NotEmpty(candidates);

        foreach (var control in candidates)
        {
            var name = control.AccessibilityObject.Name;
            Assert.False(
                string.IsNullOrWhiteSpace(name),
                $"{form.GetType().Name} contains an unnamed reachable {control.GetType().Name}.");
            Assert.True(
                IsFullyVisibleOrScrollable(control, form),
                $"{form.GetType().Name}: '{name}' ({control.GetType().Name}) is clipped " +
                "inside a non-scrollable viewport at the 1366 x 768 floor. " +
                BoundsTrace(control));
        }

        foreach (var status in Descendants(form).OfType<Label>()
                     .Where(label => label.Visible && label.Dock == DockStyle.Bottom && label.Text.Length > 0))
        {
            var preferred = status.GetPreferredSize(new Size(status.ClientSize.Width, 0));
            Assert.True(
                preferred.Height <= status.ClientSize.Height,
                $"{form.GetType().Name}: status '{status.Text}' requires {preferred.Height}px " +
                $"but its non-scrollable status line is {status.ClientSize.Height}px high.");
        }
    }

    private static bool IsReachabilitySurface(Control control)
        => control.TabStop
            || control is ButtonBase or ComboBox or ListBox or TextBoxBase or NumericUpDown
            || control.AccessibilityObject.Role == AccessibleRole.StatusBar;

    private static bool IsFullyVisibleOrScrollable(Control control, Form form)
    {
        var rectangle = control.RectangleToScreen(control.ClientRectangle);
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            var viewport = parent.RectangleToScreen(parent.ClientRectangle);
            if (!viewport.Contains(rectangle))
            {
                return parent is ScrollableControl { AutoScroll: true }
                    && viewport.Width > 0
                    && viewport.Height > 0;
            }

            if (ReferenceEquals(parent, form))
            {
                return true;
            }
        }

        return false;
    }

    private static string BoundsTrace(Control control)
    {
        var trace = new List<string>();
        var rectangle = control.RectangleToScreen(control.ClientRectangle);
        trace.Add($"control={rectangle}");
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            var viewport = parent.RectangleToScreen(parent.ClientRectangle);
            trace.Add($"{parent.GetType().Name}={viewport},autoScroll=" +
                (parent is ScrollableControl scrollable && scrollable.AutoScroll));
        }

        return string.Join("; ", trace);
    }

    private static void FlushLayout(Control root)
    {
        root.PerformLayout();
        foreach (var control in Descendants(root))
        {
            control.PerformLayout();
        }

        System.Windows.Forms.Application.DoEvents();
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static LoadedProject SyntheticLoadedProject()
    {
        var document = new ArtifactDocument(
            [new Heading(1, "Synthetic exact floor fixture"), new Paragraph("No learner data.")],
            "en");
        var manifest = new ProjectManifest(
            EngineIdentity.ProjectSchemaVersion,
            Guid.Parse("2ae639c8-863f-4d8d-b654-b991021374f6"),
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

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw failure;
        }
    }
}
