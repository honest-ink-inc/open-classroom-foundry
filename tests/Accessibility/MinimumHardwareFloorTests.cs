// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.BoardToBrief;
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
    private static readonly Type[] DirectSurfaceFloorTypes =
    [
        typeof(ReviewForm),
        typeof(CaptureForm),
        typeof(BoardToBriefIntakeForm),
        typeof(PressRoomForm),
        typeof(AllAboardForm),
        typeof(ModuleStudioForm),
        typeof(LoadedProjectPreflightForm),
        typeof(TileForm),
    ];
    private static readonly Type[] SpecializedSurfaceFloorTypes = [typeof(NodeEditorForm)];

    [Fact]
    public void Every_shipped_form_type_has_a_deliberate_floor_scenario()
    {
        var shipped = typeof(PressRoomForm).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Form).IsAssignableFrom(type))
            .OrderBy(TypeName, StringComparer.Ordinal)
            .ToArray();
        var covered = DirectSurfaceFloorTypes.Concat(SpecializedSurfaceFloorTypes)
            .OrderBy(TypeName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(shipped, covered);
    }

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
                var boardFixture = CreateBoardIntakeFloorFixture();
                using var boardIntake = boardFixture.Form;
                using var pressRoom = new PressRoomForm(
                    reviewRunner: _ => null,
                    libraryPicker: () => null,
                    loadedProjectPreflight: _ => null);
                using var allAboard = new AllAboardForm(AppServices.SymbolCatalog(), _ => null);
                using var modules = new ModuleStudioForm(reviewRunner: _ => null);
                using var preflight = new LoadedProjectPreflightForm(SyntheticLoadedProject());
                using var tile = new TileForm();

                var surfaces = new Form[]
                {
                    review,
                    capture,
                    boardIntake,
                    pressRoom,
                    allAboard,
                    modules,
                    preflight,
                    tile,
                };
                Assert.Equal(
                    DirectSurfaceFloorTypes.OrderBy(TypeName, StringComparer.Ordinal),
                    surfaces.Select(surface => surface.GetType()).OrderBy(TypeName, StringComparer.Ordinal));

                var floorHosts = new Dictionary<Form, FloorHost>();
                try
                {
                    foreach (var surface in surfaces)
                    {
                        var floor = PrepareAtFloor(surface, scale);
                        floorHosts.Add(surface, floor);
                        AssertFloor(floor);
                    }

                    ExerciseEveryReviewTabAtFloor(floorHosts[review]);
                    ExerciseBoardIntakeStagesAtFloor(
                        floorHosts[boardIntake],
                        boardFixture.Session);
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

    [Fact]
    public void Floor_assertion_requires_a_real_scroll_path_and_accepts_one_that_exposes_the_control()
        => RunSta(() =>
        {
            using (var scrollableForm = new Form { Size = new Size(400, 300) })
            {
                var scrollable = new Panel
                {
                    AutoScroll = true,
                    Location = new Point(20, 20),
                    Size = new Size(160, 100),
                };
                scrollable.Controls.Add(new Button
                {
                    AccessibleName = "Synthetic reachable scrolled control",
                    Location = new Point(250, 20),
                    Size = new Size(120, 30),
                    Text = "Synthetic reachable",
                });
                scrollableForm.Controls.Add(scrollable);
                using var scrollableHost = PrepareAtFloor(scrollableForm, 1.0f, maximize: false);
                var originalScroll = scrollable.AutoScrollPosition;

                AssertFloor(scrollableHost);
                Assert.Equal(originalScroll, scrollable.AutoScrollPosition);
            }

            using var unreachableForm = new Form { Size = new Size(400, 300) };
            var oneWayScroller = new Panel
            {
                AutoScroll = true,
                AutoScrollMinSize = new Size(400, 100),
                Location = new Point(20, 20),
                Size = new Size(160, 100),
            };
            oneWayScroller.Controls.Add(new Button
            {
                AccessibleName = "Synthetic unreachable scrolled control",
                Location = new Point(-200, 20),
                Size = new Size(120, 30),
                Text = "Synthetic unreachable",
            });
            unreachableForm.Controls.Add(oneWayScroller);
            using var unreachableHost = PrepareAtFloor(unreachableForm, 1.0f, maximize: false);

            var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(unreachableHost));
            Assert.Contains("Synthetic unreachable scrolled control", failure.Message, StringComparison.Ordinal);
        });

    [Fact]
    public void Floor_assertion_catches_clipped_noninteractive_chrome()
        => RunSta(() =>
        {
            using (var labelForm = SyntheticChromeForm())
            {
                labelForm.Controls.Add(new Label
                {
                    AutoSize = false,
                    Location = new Point(20, 20),
                    Size = new Size(120, 14),
                    Text = "Synthetic label that needs more than one visible line",
                });
                using var labelHost = PrepareAtFloor(labelForm, 1.0f, maximize: false);
                var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(labelHost));
                Assert.Contains("Synthetic label", failure.Message, StringComparison.Ordinal);
            }

            using (var groupForm = SyntheticChromeForm())
            {
                groupForm.Controls.Add(new GroupBox
                {
                    AccessibleName = "Synthetic clipped group",
                    Location = new Point(20, 20),
                    Size = new Size(90, 80),
                    Text = "Synthetic clipped group caption",
                });
                using var groupHost = PrepareAtFloor(groupForm, 1.0f, maximize: false);
                var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(groupHost));
                Assert.Contains("Synthetic clipped group caption", failure.Message, StringComparison.Ordinal);
            }

            using (var tabForm = SyntheticChromeForm())
            {
                var tabs = new TabControl
                {
                    AccessibleName = "Synthetic tabs",
                    Location = new Point(20, 20),
                    Size = new Size(150, 100),
                };
                tabs.TabPages.AddRange(
                [
                    new TabPage("Synthetic first tab caption that cannot fit"),
                    new TabPage("Synthetic second tab caption that cannot fit"),
                ]);
                tabForm.Controls.Add(tabs);
                using var tabHost = PrepareAtFloor(tabForm, 1.0f, maximize: false);
                var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(tabHost));
                Assert.Contains("Synthetic first tab caption", failure.Message, StringComparison.Ordinal);
            }

            using (var gridForm = SyntheticChromeForm())
            {
                var grid = new DataGridView
                {
                    AccessibleName = "Synthetic grid",
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    ColumnHeadersHeight = 20,
                    ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                    Location = new Point(20, 20),
                    Size = new Size(180, 100),
                };
                grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = "Synthetic grid header that needs several lines",
                });
                gridForm.Controls.Add(grid);
                using var gridHost = PrepareAtFloor(gridForm, 1.0f, maximize: false);
                var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(gridHost));
                Assert.Contains("Synthetic grid header", failure.Message, StringComparison.Ordinal);
            }

            using (var rowHeaderForm = SyntheticChromeForm())
            {
                var grid = new DataGridView
                {
                    AccessibleName = "Synthetic row-header grid",
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    Location = new Point(20, 20),
                    RowHeadersWidth = 42,
                    RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing,
                    Size = new Size(180, 100),
                };
                grid.RowHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value" });
                var row = grid.Rows[grid.Rows.Add("Synthetic value")];
                row.HeaderCell.Value = "Synthetic row header that cannot fit";
                row.Height = 22;
                rowHeaderForm.Controls.Add(grid);
                using var rowHeaderHost = PrepareAtFloor(rowHeaderForm, 1.0f, maximize: false);
                var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(rowHeaderHost));
                Assert.Contains("Synthetic row header", failure.Message, StringComparison.Ordinal);
            }
        });

    [Fact]
    public void Floor_assertion_allows_intentional_wrapping_and_ellipsis()
        => RunSta(() =>
        {
            using var form = SyntheticChromeForm();
            form.Controls.Add(new Label
            {
                AutoSize = false,
                Location = new Point(20, 20),
                Size = new Size(150, 70),
                Text = "Synthetic label that intentionally wraps onto enough visible lines",
            });
            form.Controls.Add(new Label
            {
                AutoEllipsis = true,
                AutoSize = false,
                Location = new Point(20, 100),
                Size = new Size(80, 24),
                Text = "Synthetic deliberately ellipsized label",
            });
            var grid = new DataGridView
            {
                AccessibleName = "Synthetic ellipsized grid",
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Location = new Point(190, 20),
                Size = new Size(170, 105),
            };
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Synthetic deliberately ellipsized grid header",
            });
            form.Controls.Add(grid);

            using var host = PrepareAtFloor(form, 1.0f, maximize: false);
            AssertFloor(host);
        });

    [Fact]
    public void Floor_assertion_requires_list_item_text_to_fit_or_have_a_horizontal_reading_path()
        => RunSta(() =>
        {
            const string LongSafeguard =
                "Synthetic safeguard text is deliberately wider than its list and must remain fully readable.";

            using (var clippedForm = SyntheticChromeForm())
            {
                var clipped = new ListBox
                {
                    AccessibleName = "Synthetic clipped safeguards",
                    Location = new Point(20, 20),
                    Size = new Size(150, 100),
                };
                clipped.Items.Add(LongSafeguard);
                clippedForm.Controls.Add(clipped);
                using var clippedHost = PrepareAtFloor(clippedForm, 1.0f, maximize: false);

                var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(clippedHost));
                Assert.Contains(LongSafeguard, failure.Message, StringComparison.Ordinal);
                Assert.Contains("horizontal reading path", failure.Message, StringComparison.Ordinal);
            }

            using (var verticalScrollForm = SyntheticChromeForm())
            {
                var verticalScroll = new ListBox
                {
                    AccessibleName = "Synthetic vertical-scroll safeguards",
                    IntegralHeight = false,
                    Location = new Point(20, 20),
                    Size = new Size(150, 100),
                };
                var widthWithoutScrollBar = Math.Max(0, verticalScroll.ClientSize.Width - 4);
                var widthWithScrollBar = Math.Max(
                    0,
                    widthWithoutScrollBar - SystemInformation.VerticalScrollBarWidth);
                var boundaryText = "i";
                while (TextRenderer.MeasureText(
                           boundaryText,
                           verticalScroll.Font,
                           Size.Empty,
                           TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width
                       <= widthWithScrollBar)
                {
                    boundaryText += "i";
                }

                var boundaryWidth = TextRenderer.MeasureText(
                    boundaryText,
                    verticalScroll.Font,
                    Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                Assert.InRange(boundaryWidth, widthWithScrollBar + 1, widthWithoutScrollBar);
                verticalScroll.Items.Add(boundaryText);
                for (var index = 0; index < 30; index++)
                {
                    verticalScroll.Items.Add($"Synthetic item {index}");
                }

                verticalScrollForm.Controls.Add(verticalScroll);
                using var verticalScrollHost = PrepareAtFloor(verticalScrollForm, 1.0f, maximize: false);

                var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(verticalScrollHost));
                Assert.Contains(boundaryText, failure.Message, StringComparison.Ordinal);
                Assert.Contains("horizontal reading path", failure.Message, StringComparison.Ordinal);
            }

            using var readableForm = SyntheticChromeForm();
            var readable = new ListBox
            {
                AccessibleName = "Synthetic readable safeguards",
                HorizontalScrollbar = true,
                Location = new Point(20, 20),
                Size = new Size(150, 100),
            };
            readable.Items.Add(LongSafeguard);
            readableForm.Controls.Add(readable);
            using var readableHost = PrepareAtFloor(readableForm, 1.0f, maximize: false);

            AssertFloor(readableHost);
        });

    private static void ExerciseEveryReviewTabAtFloor(FloorHost floor)
    {
        var tabs = Descendants(floor.ClientCanvas).OfType<TabControl>().Single();
        Assert.Equal(3, tabs.TabPages.Count);
        for (var index = 0; index < tabs.TabPages.Count; index++)
        {
            tabs.SelectedIndex = index;
            FlushLayout(floor.ClientCanvas);
            AssertFloor(floor);
        }
    }

    private static BoardIntakeFloorFixture CreateBoardIntakeFloorFixture()
    {
        var store = new FailsFirstFloorPurgeStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new FloorPassThroughNormalizer(),
            store);
        session.CaptureAsync(
                new CaptureRequest(
                    ByteImportCaptureSource.Kind,
                    "image/png",
                    new byte[] { 1, 2, 3 }),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        session.NormalizeAsync(new NormalizationRequest(), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        session.ConfirmLane(DataLane.Green);

        var form = new BoardToBriefIntakeForm(
            store,
            session,
            new FloorOcrService(),
            DistrictPolicy.Offline,
            captureRunner: _ => DialogResult.Cancel,
            noticePresenter: (_, _, _) => { });
        return new BoardIntakeFloorFixture(form, session);
    }

    private static void ExerciseBoardIntakeStagesAtFloor(
        FloorHost floor,
        CaptureSession session)
    {
        var manual = Descendants(floor.ClientCanvas).OfType<TextBox>()
            .Single(control => control.Name == BoardToBriefIntakeForm.ManualInputName);
        manual.Text = "Synthetic board title" + Environment.NewLine
            + "Open the synthetic notebook.";
        Descendants(floor.ClientCanvas).OfType<Button>()
            .Single(control => control.Name == BoardToBriefIntakeForm.UseManualName)
            .PerformClick();
        AssertFloor(floor);

        var accept = Descendants(floor.ClientCanvas).OfType<Button>()
            .Single(control => control.Name == BoardToBriefIntakeForm.AcceptCandidateName);
        Assert.True(accept.Enabled, "The floor fixture did not enter the unresolved-token stage.");
        accept.PerformClick();
        AssertFloor(floor);
        accept.PerformClick();

        var roles = Descendants(floor.ClientCanvas).OfType<DataGridView>()
            .Single(control => control.Name == BoardToBriefIntakeForm.RoleGridName);
        Assert.Equal(2, roles.Rows.Count);
        roles.Rows[0].Cells[1].Value = BriefRole.Title;
        roles.Rows[1].Cells[1].Value = BriefRole.Step;
        AssertFloor(floor);

        var moveDown = Descendants(floor.ClientCanvas).OfType<Button>()
            .Single(control => control.Name == BoardToBriefIntakeForm.MoveDownName);
        var moveUp = Descendants(floor.ClientCanvas).OfType<Button>()
            .Single(control => control.Name == BoardToBriefIntakeForm.MoveUpName);
        roles.CurrentCell = roles.Rows[0].Cells[0];
        moveDown.PerformClick();
        moveUp.PerformClick();
        AssertFloor(floor);

        Descendants(floor.ClientCanvas).OfType<Button>()
            .Single(control => control.Name == BoardToBriefIntakeForm.FinishName)
            .PerformClick();
        Assert.Equal(JobState.PurgeIncomplete, session.Machine.State);
        AssertFloor(floor);

        var retry = Descendants(floor.ClientCanvas).OfType<Button>()
            .Single(control => control.Name == BoardToBriefIntakeForm.RetryPurgeName);
        Assert.True(retry.Visible, "The first synthetic purge refusal did not expose recovery.");
        Assert.True(retry.Enabled, "The exposed purge-recovery action was not enabled.");
        retry.PerformClick();
        Assert.Equal(JobState.TransientSourcesPurged, session.Machine.State);
    }

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

            if (control is ListBox list)
            {
                AssertListItemTextIsReadable(form, list);
            }
        }

        foreach (var chrome in Descendants(floor.ClientCanvas)
                     .Where(control => control.Visible && IsNoninteractiveChrome(control)))
        {
            Assert.True(
                IsFullyVisibleOrScrollable(chrome, floor.ClientCanvas),
                $"{form.GetType().Name}: noninteractive {chrome.GetType().Name} chrome is clipped " +
                "inside a non-scrollable viewport at the 1366 x 768 floor. " +
                BoundsTrace(chrome));

            switch (chrome)
            {
                case Label label:
                    AssertLabelTextFits(form, label);
                    break;
                case GroupBox group:
                    AssertGroupCaptionFits(form, group);
                    break;
                case TabControl tabs:
                    AssertTabHeadersFit(form, tabs);
                    break;
                case DataGridView grid:
                    AssertGridHeadersFit(form, grid);
                    break;
            }
        }
    }

    private static bool IsReachabilitySurface(Control control)
        => control.TabStop
            || control is ButtonBase or ComboBox or ListBox or TextBoxBase or NumericUpDown
            || control.AccessibilityObject.Role == AccessibleRole.StatusBar;

    private static bool IsNoninteractiveChrome(Control control)
        => control is Label or GroupBox or TabControl or DataGridView;

    private static void AssertListItemTextIsReadable(Form form, ListBox list)
    {
        var verticalScrollBarWidth = list.ScrollAlwaysVisible
            || (!list.MultiColumn && (long)list.ItemHeight * list.Items.Count > list.ClientSize.Height)
                ? SystemInformation.VerticalScrollBarWidth
                : 0;
        var availableWidth = Math.Max(0, list.ClientSize.Width - 4 - verticalScrollBarWidth);
        foreach (var item in list.Items.Cast<object>())
        {
            var text = item.ToString() ?? "";
            if (text.Length == 0)
            {
                continue;
            }

            var required = TextRenderer.MeasureText(
                text,
                list.Font,
                Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            Assert.True(
                required.Width <= availableWidth || list.HorizontalScrollbar,
                $"{form.GetType().Name}: list item '{text}' requires {required.Width}px, but its " +
                $"visible list offers {availableWidth}px and has no horizontal reading path.");
        }
    }

    private static void AssertLabelTextFits(Form form, Label label)
    {
        if (label.Text.Length == 0 || label.AutoEllipsis)
        {
            return;
        }

        var proposedWidth = Math.Max(1, label.ClientSize.Width);
        var preferred = label.GetPreferredSize(new Size(proposedWidth, 0));
        Assert.True(
            preferred.Height <= label.ClientSize.Height,
            $"{form.GetType().Name}: label '{label.Text}' requires {preferred.Height}px when wrapped " +
            $"to its {proposedWidth}px client width, but only {label.ClientSize.Height}px is visible.");
    }

    private static void AssertGroupCaptionFits(Form form, GroupBox group)
    {
        if (group.Text.Length == 0)
        {
            return;
        }

        var caption = WithoutMnemonics(group.Text);
        var required = TextRenderer.MeasureText(
            caption,
            group.Font,
            Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        var availableWidth = Math.Max(0, group.ClientSize.Width - 16);
        Assert.True(
            required.Width <= availableWidth,
            $"{form.GetType().Name}: group caption '{caption}' requires {required.Width}px, " +
            $"but only {availableWidth}px is available without clipping.");
    }

    private static void AssertTabHeadersFit(Form form, TabControl tabs)
    {
        for (var index = 0; index < tabs.TabPages.Count; index++)
        {
            var page = tabs.TabPages[index];
            var header = tabs.GetTabRect(index);
            Assert.True(
                tabs.ClientRectangle.Contains(header),
                $"{form.GetType().Name}: tab header '{WithoutMnemonics(page.Text)}' is outside " +
                $"the visible tab strip: header={header},client={tabs.ClientRectangle}.");

            var caption = WithoutMnemonics(page.Text);
            var required = TextRenderer.MeasureText(
                caption,
                tabs.Font,
                Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            var availableWidth = Math.Max(0, header.Width - (tabs.Padding.X * 2));
            Assert.True(
                required.Width <= availableWidth,
                $"{form.GetType().Name}: tab header '{caption}' requires {required.Width}px, " +
                $"but its visible header offers {availableWidth}px.");
        }
    }

    private static void AssertGridHeadersFit(Form form, DataGridView grid)
    {
        if (grid.ColumnHeadersVisible)
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                if (!column.Visible || string.IsNullOrEmpty(column.HeaderText))
                {
                    continue;
                }

                AssertGridHeaderTextFits(
                    form,
                    grid,
                    column.HeaderText,
                    column.HeaderCell.InheritedStyle,
                    grid.GetCellDisplayRectangle(column.Index, -1, cutOverflow: false),
                    "column");
            }
        }

        if (!grid.RowHeadersVisible)
        {
            return;
        }

        foreach (DataGridViewRow row in grid.Rows)
        {
            var text = Convert.ToString(
                row.HeaderCell.Value,
                System.Globalization.CultureInfo.InvariantCulture);
            if (!row.Visible || string.IsNullOrEmpty(text))
            {
                continue;
            }

            AssertGridHeaderTextFits(
                form,
                grid,
                text,
                row.HeaderCell.InheritedStyle,
                grid.GetCellDisplayRectangle(-1, row.Index, cutOverflow: false),
                "row");
        }
    }

    private static void AssertGridHeaderTextFits(
        Form form,
        DataGridView grid,
        string text,
        DataGridViewCellStyle style,
        Rectangle header,
        string kind)
    {
        // A no-wrap header explicitly opts into the grid's native ellipsis.
        // Wrapped headers, by contrast, must have enough header height for
        // every line; otherwise the grid silently cuts off its own label.
        if (style.WrapMode == DataGridViewTriState.False)
        {
            return;
        }

        var available = new Size(
            Math.Max(1, header.Width - style.Padding.Horizontal - 8),
            Math.Max(0, header.Height - style.Padding.Vertical - 4));
        var required = TextRenderer.MeasureText(
            text,
            style.Font ?? grid.Font,
            new Size(available.Width, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        Assert.True(
            required.Height <= available.Height,
            $"{form.GetType().Name}: grid {kind} header '{text}' requires {required.Height}px " +
            $"when wrapped to {available.Width}px, but its visible header offers {available.Height}px.");
    }

    private static string WithoutMnemonics(string text)
    {
        var value = new System.Text.StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '&')
            {
                value.Append(text[index]);
                continue;
            }

            if (index + 1 < text.Length && text[index + 1] == '&')
            {
                value.Append('&');
                index++;
            }
        }

        return value.ToString();
    }

    private static bool IsFullyVisibleOrScrollable(Control control, Control root)
    {
        var rectangle = control.RectangleToScreen(control.ClientRectangle);
        var scrollTarget = control;
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            var viewport = parent.RectangleToScreen(parent.ClientRectangle);
            if (!viewport.Contains(rectangle))
            {
                if (parent is not ScrollableControl { AutoScroll: true } scrollable
                    || viewport.Width <= 0
                    || viewport.Height <= 0
                    || !CanScrollIntoView(scrollable, scrollTarget))
                {
                    return false;
                }

                // ScrollControlIntoView proved the target can be exposed in
                // this owning viewport. The viewport itself must still remain
                // reachable through every outer non-scrollable ancestor.
                rectangle = viewport;
                scrollTarget = parent;
            }

            if (ReferenceEquals(parent, root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanScrollIntoView(ScrollableControl owner, Control target)
    {
        var original = owner.AutoScrollPosition;
        try
        {
            owner.ScrollControlIntoView(target);
            owner.PerformLayout();
            var viewport = owner.RectangleToScreen(owner.ClientRectangle);
            var exposed = target.RectangleToScreen(target.ClientRectangle);
            return exposed.Width > 0
                && exposed.Height > 0
                && AxisIsExposed(exposed.Left, exposed.Width, viewport.Left, viewport.Width)
                && AxisIsExposed(exposed.Top, exposed.Height, viewport.Top, viewport.Height);
        }
        finally
        {
            // Reachability inspection must not leave one candidate scrolled
            // into view at the expense of the candidates asserted after it.
            owner.AutoScrollPosition = new Point(-original.X, -original.Y);
            owner.PerformLayout();
        }
    }

    private static bool AxisIsExposed(
        int targetStart,
        int targetLength,
        int viewportStart,
        int viewportLength)
    {
        var targetEnd = (long)targetStart + targetLength;
        var viewportEnd = (long)viewportStart + viewportLength;
        return targetLength <= viewportLength
            ? targetStart >= viewportStart && targetEnd <= viewportEnd
            : targetStart < viewportEnd && targetEnd > viewportStart;
    }

    private static string TypeName(Type type) => type.FullName ?? type.Name;

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

    private static Form SyntheticChromeForm()
    {
        var form = new Form { Size = new Size(400, 300) };
        form.Controls.Add(new Button
        {
            AccessibleName = "Synthetic reachable sentinel",
            Location = new Point(20, 220),
            Size = new Size(120, 30),
            Text = "Synthetic sentinel",
        });
        return form;
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

    private sealed record BoardIntakeFloorFixture(
        BoardToBriefIntakeForm Form,
        CaptureSession Session);

    private sealed class FloorPassThroughNormalizer : IDocumentNormalizer
    {
        public Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(source with { MetadataStripped = true });
        }
    }

    private sealed class FloorOcrService : IOcrService
    {
        public Task<OcrResult> RecognizeAsync(
            SourceEnvelope source,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new OcrResult(
            [
                new OcrToken("Synthetic", 0) { ConfidenceAvailable = false },
            ]));
        }
    }

    private sealed class FailsFirstFloorPurgeStore : ISessionByteStore
    {
        private readonly InMemorySessionByteStore _inner = new();
        private bool _failNextPurge = true;

        public int Count => _inner.Count;

        public SessionByteReference Put(ReadOnlyMemory<byte> content) => _inner.Put(content);

        public bool TryGet(SessionByteReference reference, out ReadOnlyMemory<byte> content)
            => _inner.TryGet(reference, out content);

        public void Release(SessionByteReference reference) => _inner.Release(reference);

        public void PurgeAll()
        {
            if (_failNextPurge)
            {
                _failNextPurge = false;
                throw new IOException("Synthetic first purge refusal for floor-state proof.");
            }

            _inner.PurgeAll();
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
