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

                var floorHosts = new Dictionary<Form, FloorHost>();
                try
                {
                    foreach (var surface in surfaces)
                    {
                        var floor = PrepareAtFloor(surface, scale);
                        floorHosts.Add(surface, floor);
                        AssertFloor(floor);
                    }

                    ExerciseEveryPressAtFloor(floorHosts[pressRoom]);
                    ExerciseEveryAllAboardModeAtFloor(floorHosts[allAboard]);
                    ExerciseEveryModuleModeAtFloor(floorHosts[modules]);
                    ExerciseEveryNodeEditorVariantAtFloor(scale);
                }
                finally
                {
                    foreach (var host in floorHosts.Values)
                    {
                        host.Dispose();
                    }
                }
            }
            finally
            {
                UiLocale.Set(UiLocaleMode.Neutral);
            }
        });

    [Fact]
    public void Floor_assertion_catches_non_scrollable_and_clipped_scroll_viewports()
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
            using var host = PrepareAtFloor(form, 1.0f, maximize: false);

            var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(host));
            Assert.Contains("Synthetic clipped control", failure.Message, StringComparison.Ordinal);

            using var scrollForm = new Form { Size = new Size(400, 300) };
            var clippedScroller = new Panel
            {
                AutoScroll = true,
                Location = new Point(300, 80),
                Size = new Size(200, 100),
            };
            clippedScroller.Controls.Add(new Button
            {
                Text = "Synthetic scroll-owned control",
                AccessibleName = "Synthetic scroll-owned control",
                Location = new Point(250, 20),
                Size = new Size(180, 30),
            });
            scrollForm.Controls.Add(clippedScroller);
            using var scrollHost = PrepareAtFloor(scrollForm, 1.0f, maximize: false);

            var scrollFailure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(scrollHost));
            Assert.Contains("Synthetic scroll-owned control", scrollFailure.Message, StringComparison.Ordinal);
        });

    private static void ExerciseEveryPressAtFloor(FloorHost floor)
    {
        var list = Descendants(floor.ClientCanvas).OfType<ListBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.PressList);
        for (var index = 0; index < list.Items.Count; index++)
        {
            list.SelectedIndex = index;
            FlushLayout(floor.ClientCanvas);
            AssertFloor(floor);
        }
    }

    private static void ExerciseEveryAllAboardModeAtFloor(FloorHost floor)
    {
        var modes = Descendants(floor.ClientCanvas).OfType<ComboBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.OutputMode);
        for (var index = 0; index < modes.Items.Count; index++)
        {
            modes.SelectedIndex = index;
            FlushLayout(floor.ClientCanvas);
            AssertFloor(floor);
        }
    }

    private static void ExerciseEveryModuleModeAtFloor(FloorHost floor)
    {
        var doors = Descendants(floor.ClientCanvas).OfType<ListBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.ModuleDoors);
        var modes = Descendants(floor.ClientCanvas).OfType<ComboBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.ModuleMode);

        for (var doorIndex = 0; doorIndex < doors.Items.Count; doorIndex++)
        {
            doors.SelectedIndex = doorIndex;
            for (var modeIndex = 0; modeIndex < modes.Items.Count; modeIndex++)
            {
                modes.SelectedIndex = modeIndex;
                FlushLayout(floor.ClientCanvas);
                AssertFloor(floor);
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
            using var host = PrepareAtFloor(editor, scale);
            AssertFloor(host);
            if (node is VectorGraphic)
            {
                var primitives = Descendants(host.ClientCanvas).OfType<ListBox>()
                    .Single(control => control.AccessibilityObject.Name == UiStrings.EditorVectorPrimitives);
                for (var index = 0; index < primitives.Items.Count; index++)
                {
                    primitives.SelectedIndex = index;
                    FlushLayout(host.ClientCanvas);
                    AssertFloor(host);
                }
            }
        }
    }

    private static FloorHost PrepareAtFloor(Form form, float scale, bool maximize = true)
    {
        Assert.False(
            form.IsHandleCreated,
            $"{form.GetType().Name} created a native window before the hermetic floor layout.");
        Assert.True(float.IsFinite(scale) && scale > 0, $"Invalid floor scale: {scale}.");

        var requested = maximize
            ? FloorWorkingArea.Size
            : new Size(
                Math.Min(form.Width, FloorWorkingArea.Width),
                Math.Min(form.Height, FloorWorkingArea.Height));

        var nonClient = new Size(
            form.Width - form.ClientSize.Width,
            form.Height - form.ClientSize.Height);
        var requestedClient = new Size(
            requested.Width - nonClient.Width,
            requested.Height - nonClient.Height);
        Assert.True(
            requestedClient.Width > 0 && requestedClient.Height > 0,
            $"{form.GetType().Name} has an invalid floor client area: {requestedClient}.");
        var logicalClient = new Size(
            Math.Max(1, (int)Math.Round(requestedClient.Width / scale, MidpointRounding.AwayFromZero)),
            Math.Max(1, (int)Math.Round(requestedClient.Height / scale, MidpointRounding.AwayFromZero)));

        // A hosted Windows runner can expose a desktop narrower than the
        // product's 1366 px floor. A top-level Form silently clamps to that
        // host desktop's MaxWindowTrackSize, which turns this contract into a
        // test of the runner (the 30 Aug 2026 hosted runs measured 1028 px of
        // client width). Lay out the Form's live control tree in an orphaned
        // client canvas whose size is the physical floor minus that Form's
        // measured non-client frame. Panels are not constrained by the host
        // desktop, while docking, anchoring, events, accessibility objects,
        // and every nested production control remain real.
        var host = new FloorClientCanvas(
            form.RightToLeft == RightToLeft.Yes && form.RightToLeftLayout)
        {
            AccessibleName = form.GetType().Name,
            AutoScroll = form.AutoScroll,
            BackColor = form.BackColor,
            ClientSize = logicalClient,
            Enabled = form.Enabled,
            Font = form.Font,
            ForeColor = form.ForeColor,
            Padding = form.Padding,
            RightToLeft = form.RightToLeft,
        };
        try
        {
            var children = form.Controls.Cast<Control>()
                .Select(control => (Control: control, Index: form.Controls.GetChildIndex(control)))
                .OrderBy(item => item.Index)
                .ToArray();
            form.SuspendLayout();
            host.SuspendLayout();
            foreach (var child in children)
            {
                host.Controls.Add(child.Control);
            }

            foreach (var child in children)
            {
                host.Controls.SetChildIndex(child.Control, child.Index);
                Assert.Equal(child.Index, host.Controls.GetChildIndex(child.Control));
            }

            form.ResumeLayout(performLayout: false);
            host.ResumeLayout(performLayout: true);
            FlushLayout(host);
            if (scale != 1.0f)
            {
                host.Scale(new SizeF(scale, scale));
            }

            host.ClientSize = requestedClient;
            FlushLayout(host);
            host.CreateControl();
            FlushLayout(host);
            Assert.Equal(requestedClient, host.ClientSize);
            Assert.Equal(
                requested,
                new Size(
                    host.ClientSize.Width + nonClient.Width,
                    host.ClientSize.Height + nonClient.Height));
            Assert.False(
                form.IsHandleCreated,
                $"{form.GetType().Name} created a native window during the hermetic floor layout.");
            return new FloorHost(form, host, new Rectangle(FloorWorkingArea.Location, requested));
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    private static void AssertFloor(FloorHost floor)
    {
        var form = floor.Surface;
        Assert.True(
            FloorWorkingArea.Contains(floor.OuterBounds),
            $"{form.GetType().Name} extends beyond the 1366 x 768 floor working area: {floor.OuterBounds}.");

        var candidates = Descendants(floor.ClientCanvas)
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
                IsFullyVisibleOrScrollable(control, floor.ClientCanvas),
                $"{form.GetType().Name}: '{name}' ({control.GetType().Name}) is clipped " +
                "inside a non-scrollable viewport at the 1366 x 768 floor. " +
                BoundsTrace(control));
        }

        foreach (var status in Descendants(floor.ClientCanvas).OfType<Label>()
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

    private static bool IsFullyVisibleOrScrollable(Control control, Control root)
    {
        var rectangle = control.RectangleToScreen(control.ClientRectangle);
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            var viewport = parent.RectangleToScreen(parent.ClientRectangle);
            if (!viewport.Contains(rectangle))
            {
                if (parent is not ScrollableControl { AutoScroll: true }
                    || viewport.Width <= 0
                    || viewport.Height <= 0)
                {
                    return false;
                }

                // The control can be brought into this owning viewport, but
                // that viewport must itself remain reachable through every
                // outer non-scrollable ancestor.
                rectangle = viewport;
            }

            if (ReferenceEquals(parent, root))
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

    private sealed class FloorHost(Form surface, Panel clientCanvas, Rectangle outerBounds) : IDisposable
    {
        public Form Surface { get; } = surface;

        public Panel ClientCanvas { get; } = clientCanvas;

        public Rectangle OuterBounds { get; } = outerBounds;

        public void Dispose() => ClientCanvas.Dispose();
    }

    private sealed class FloorClientCanvas(bool mirrorLayout) : Panel
    {
        private const int WsExRight = 0x00001000;
        private const int WsExRtlReading = 0x00002000;
        private const int WsExLeftScrollbar = 0x00004000;
        private const int WsExNoInheritLayout = 0x00100000;
        private const int WsExLayoutRtl = 0x00400000;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                if (mirrorLayout)
                {
                    parameters.ExStyle |= WsExLayoutRtl | WsExNoInheritLayout;
                    parameters.ExStyle &= ~(WsExRight | WsExRtlReading | WsExLeftScrollbar);
                }

                return parameters;
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
