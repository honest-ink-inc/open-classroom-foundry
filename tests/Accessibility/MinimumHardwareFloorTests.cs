// SPDX-License-Identifier: GPL-3.0-or-later
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Foundry.App.WinForms;
using Foundry.Application;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn.BoardToBrief;
using Foundry.Storage;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Foundry.Tests.Accessibility;

/// <summary>
/// Hermetic client-layout evidence for the minimum-hardware covenant.
/// 1366 x 768 is a practical functional floor: essential controls and status
/// must remain reachable, but reflow, wrapping, and real scroll paths are valid.
/// It is not the product's fixed design canvas. The
/// asserted profiles reserve either 40 pixels (the historical reference) or
/// 48 pixels (an ordinary measured Windows taskbar), so the application must
/// remain reachable in both 1366 x 728 and 1366 x 720 working areas.
/// The pseudo 125 and 200 percent and neutral 200 percent cases stretch the live
/// control tree hermetically; they are not physical-monitor, DeviceDpi,
/// non-client-DPI, shown-window, assistive-technology, or contrast proof.
/// The reviewed-catalog cases use exact-hash-pinned synthetic LTR and RTL
/// fixtures solely to exercise the floor oracle. They are not protected-seat
/// review evidence and do not activate a production catalog.
/// </summary>
public sealed class MinimumHardwareFloorTests
{
    private static readonly string FloorPdfExportName = new string('W', 251) + ".pdf";
    private static readonly string FloorHtmlExportName = new string('W', 250) + ".html";
    private const string FloorExportRefusal =
        "Synthetic floor destination became unavailable after export began.";
    private const string FloorNormalizationRefusal =
        "Synthetic floor normalization refusal.";
    private const string ReviewedFloorMarker = " ⟬F⟭";
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
    private static readonly string[] SyntheticInteractiveKinds = ["button", "check box", "radio button"];

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

    [Fact]
    public void Deferred_export_cleanup_preserves_the_original_failure_and_settles_the_attempt()
        => AssertDeferredExportFailureIsIsolated();

    [Fact]
    public void Hermetic_content_scale_changes_inherited_and_authored_font_metrics_once()
        => RunSta(() =>
        {
            var unscaled = MeasureHermeticContentScale(1.0f);
            var doubled = MeasureHermeticContentScale(2.0f);

            Assert.Equal(unscaled.ClientSize, doubled.ClientSize);
            Assert.Equal(unscaled.InheritedFontSize, unscaled.ScaledInheritedFontSize);
            Assert.Equal(unscaled.AuthoredFontSize, unscaled.ScaledAuthoredFontSize);
            Assert.Equal(unscaled.InheritedTextSize, unscaled.ScaledInheritedTextSize);
            Assert.Equal(unscaled.AuthoredTextSize, unscaled.ScaledAuthoredTextSize);

            Assert.Equal(
                doubled.InheritedFontSize * 2,
                doubled.ScaledInheritedFontSize,
                precision: 3);
            Assert.Equal(
                doubled.AuthoredFontSize * 2,
                doubled.ScaledAuthoredFontSize,
                precision: 3);
            AssertApproximatelyDoubled(
                doubled.InheritedTextSize,
                doubled.ScaledInheritedTextSize,
                "inherited");
            AssertApproximatelyDoubled(
                doubled.AuthoredTextSize,
                doubled.ScaledAuthoredTextSize,
                "authored");
        });

    [Fact]
    public void Board_default_size_absorbs_ambient_typography_growth_without_manufactured_scrollbars()
        => RunSta(() =>
        {
            UiLocale.Set(UiLocaleMode.Neutral);
            var fixture = CreateBoardIntakeFloorFixture();
            using var ambientFont = new Font(
                "Segoe UI",
                10.25f,
                FontStyle.Regular,
                GraphicsUnit.Point);
            Assert.Equal("Segoe UI", ambientFont.FontFamily.Name);
            Assert.Equal(10.25f, ambientFont.SizeInPoints, precision: 2);
            using var form = fixture.Form;
            form.Font = ambientFont;
            using var floor = PrepareAtFloor(
                form,
                scale: 1.0f,
                maximize: false,
                new Rectangle(0, 0, 1366, 728));

            AssertBoardDefaultUsesItsAvailableViewport(floor);
        });

    [Theory]
    [InlineData(UiLocaleMode.Neutral, 1.0f, 728)]
    [InlineData(UiLocaleMode.Pseudo, 1.25f, 728)]
    [InlineData(UiLocaleMode.Neutral, 1.0f, 720)]
    [InlineData(UiLocaleMode.Pseudo, 1.25f, 720)]
    [InlineData(UiLocaleMode.Neutral, 2.0f, 728)]
    [InlineData(UiLocaleMode.Neutral, 2.0f, 720)]
    [InlineData(UiLocaleMode.Pseudo, 2.0f, 728)]
    [InlineData(UiLocaleMode.Pseudo, 2.0f, 720)]
    public void Every_shipped_surface_keeps_controls_and_status_reachable_at_the_floor(
        UiLocaleMode locale,
        float scale,
        int workingHeight)
        => RunSta(() =>
        {
            UiLocale.Set(locale);
            try
            {
                ExerciseEveryShippedSurfaceAtFloor(scale, workingHeight);
            }
            finally
            {
                UiLocale.Set(UiLocaleMode.Neutral);
            }
        });

    [Theory]
    [InlineData("ltr", 1.0f, 720)]
    [InlineData("rtl", 1.0f, 720)]
    [InlineData("ltr", 2.0f, 728)]
    [InlineData("rtl", 2.0f, 728)]
    [InlineData("ltr", 2.0f, 720)]
    [InlineData("rtl", 2.0f, 720)]
    public void Exact_test_pinned_reviewed_catalog_keeps_every_surface_reachable_at_the_floor(
        string direction,
        float scale,
        int workingHeight)
        => RunSta(() =>
        {
            Assert.Empty(UiCatalogDeployment.ApprovedCatalogSha256);
            using var catalog = CreateReviewedFloorCatalog(direction);
            try
            {
                UiLocale.ConfigureForTest(
                    [UiLocale.CatalogSwitch, catalog.Path],
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase) { catalog.Sha256 });
                Assert.Equal(UiLocaleMode.ReviewedCatalog, UiLocale.Mode);
                Assert.Contains("not protected-seat evidence", UiLocale.ActiveReview?.ReviewerName, StringComparison.Ordinal);
                Assert.EndsWith(ReviewedFloorMarker, UiStrings.PressList, StringComparison.Ordinal);

                ExerciseEveryShippedSurfaceAtFloor(scale, workingHeight);
            }
            finally
            {
                UiLocale.Set(UiLocaleMode.Neutral);
                Assert.Empty(UiCatalogDeployment.ApprovedCatalogSha256);
            }
        });

    private static void ExerciseEveryShippedSurfaceAtFloor(float scale, int workingHeight)
    {
        var workingArea = new Rectangle(0, 0, 1366, workingHeight);
        using var review = UiaHarness.CreateReviewForm();
        using var capture = UiaHarness.CreateCaptureForm();
        var boardFixture = CreateBoardIntakeFloorFixture();
        using var boardIntake = boardFixture.Form;
        var pressExport = new DeferredFloorExport();
        using var pressRoom = new PressRoomForm(
            reviewRunner: ApproveSyntheticFloorArtifact,
            libraryPicker: () => null,
            exportPicker: () => new PressRoomForm.ExportChoice(FloorPdfExportName, 1),
            loadedProjectPreflight: _ => null,
            pdfExporter: (_, _, _, cancellationToken) =>
                pressExport.RunAsync(cancellationToken));
        var allAboardExport = new DeferredFloorExport();
        using var allAboard = new AllAboardForm(
            AppServices.SymbolCatalog(),
            reviewRunner: ApproveSyntheticFloorArtifact,
            exportPicker: () => new AllAboardForm.ExportChoice(FloorPdfExportName, 1),
            pdfExporter: (_, _, _, cancellationToken) =>
                allAboardExport.RunAsync(cancellationToken));
        var moduleExport = new DeferredFloorExport();
        using var modules = new ModuleStudioForm(
            reviewRunner: ApproveSyntheticFloorArtifact,
            exportPicker: () => new ModuleStudioForm.ExportChoice(
                FloorHtmlExportName,
                RenderTarget.AccessibleHtml),
            exportWriter: (_, _, cancellationToken) =>
                moduleExport.RunAsync(cancellationToken),
            printViewOpener: null,
            boardIntakeRunner: null,
            exportWorkRunner: RunFloorExportWork);
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
                var useActualOuterSize = surface is LoadedProjectPreflightForm or TileForm;
                var originalOuterSize = surface.Size;
                var floor = PrepareAtFloor(
                    surface,
                    scale,
                    maximize: !useActualOuterSize,
                    workingArea);
                floorHosts.Add(surface, floor);
                if (useActualOuterSize)
                {
                    Assert.Equal(originalOuterSize, floor.OuterBounds.Size);
                }

                AssertFloor(floor);
            }

            AssertEssentialVisualViewport<PictureBox>(
                floorHosts[capture],
                UiStrings.CapturePreviewAccessibleName);
            AssertEssentialVisualViewport<PictureBox>(
                floorHosts[boardIntake],
                UiStrings.BoardIntakeSourceImage);

            if (scale == 1.0f && workingHeight == 728)
            {
                AssertBoardDefaultUsesItsAvailableViewport(floorHosts[boardIntake]);
                AssertLoadedPreflightExpandsItsDocumentRow(floorHosts[preflight]);
            }

            ExerciseEveryReviewTabAtFloor(floorHosts[review]);
            ExerciseBoardIntakeStagesAtFloor(
                floorHosts[boardIntake],
                boardFixture.Session);
            ExercisePressExportStatesAtFloor(floorHosts[pressRoom], pressExport);
            ExerciseEveryPressAtFloor(floorHosts[pressRoom]);
            ExerciseAllAboardExportStatesAtFloor(
                floorHosts[allAboard],
                allAboardExport);
            ExerciseEveryAllAboardModeAtFloor(floorHosts[allAboard]);
            ExerciseModuleExportStatesAtFloor(floorHosts[modules], moduleExport);
            ExerciseEveryModuleModeAtFloor(floorHosts[modules]);
            ExerciseCaptureRecoveryStatesAtFloor(scale, workingArea);
            ExerciseEveryNodeEditorVariantAtFloor(scale, workingArea);
        }
        finally
        {
            foreach (var host in floorHosts.Values)
            {
                host.Dispose();
            }
        }
    }

    private static void AssertBoardDefaultUsesItsAvailableViewport(FloorHost floor)
    {
        var floorSize = floor.ClientCanvas.ClientSize;
        try
        {
            floor.ClientCanvas.ClientSize = floor.Surface.ClientSize;
            FlushLayout(floor.ClientCanvas);

            var comparison = Descendants(floor.ClientCanvas).OfType<TableLayoutPanel>()
                .Single(control => control.ColumnCount == 2
                    && control.RowCount == 3
                    && control.MinimumSize.Height == 350);
            var viewport = Assert.IsType<Panel>(comparison.Parent);
            var trace = BoardDefaultViewportTrace(floor, comparison, viewport);
            Assert.False(
                viewport.HorizontalScroll.Visible,
                "Board to Brief manufactured a horizontal body scrollbar at its neutral default size. " + trace);
            Assert.False(
                viewport.VerticalScroll.Visible,
                "Board to Brief manufactured a vertical body scrollbar at its neutral default size. " + trace);
        }
        finally
        {
            floor.ClientCanvas.ClientSize = floorSize;
            FlushLayout(floor.ClientCanvas);
        }

        AssertFloor(floor);
    }

    private static string BoardDefaultViewportTrace(
        FloorHost floor,
        TableLayoutPanel comparison,
        Panel viewport)
    {
        var body = Assert.IsType<TableLayoutPanel>(viewport.Parent);
        var roles = Assert.IsType<GroupBox>(body.GetControlFromPosition(0, 2));
        var outerActions = floor.ClientCanvas.Controls.OfType<FlowLayoutPanel>().Single();
        var status = floor.ClientCanvas.Controls.OfType<Label>()
            .Single(control => string.Equals(
                control.Name,
                BoardToBriefIntakeForm.StatusName,
                StringComparison.Ordinal));
        var intro = Descendants(body).OfType<Label>()
            .Single(control => string.Equals(
                control.AccessibleName,
                UiStrings.WithoutMnemonic(UiStrings.BoardIntakeIntroduction),
                StringComparison.Ordinal));
        return $"surfaceClient={floor.Surface.ClientSize}; hostClient={floor.ClientCanvas.ClientSize}; " +
            $"font={floor.ClientCanvas.Font.Name}/{floor.ClientCanvas.Font.SizeInPoints}; " +
            $"bodyClient={body.ClientSize}; bodyRows=[{string.Join(',', body.GetRowHeights())}]; " +
            $"introBounds={intro.Bounds}; rolesBounds={roles.Bounds}; rolesMinimum={roles.MinimumSize}; " +
            $"rolesMargin={roles.Margin}; actionsBounds={outerActions.Bounds}; statusBounds={status.Bounds}; " +
            $"viewportClient={viewport.ClientSize}; viewportDisplay={viewport.DisplayRectangle}; " +
            $"comparisonBounds={comparison.Bounds}; comparisonMinimum={comparison.MinimumSize}.";
    }

    private static void AssertLoadedPreflightExpandsItsDocumentRow(FloorHost floor)
    {
        var floorSize = floor.ClientCanvas.ClientSize;
        var exactDocument = Descendants(floor.ClientCanvas).OfType<TextBox>()
            .Single(control => control.Multiline && control.ReadOnly);
        var continueButton = ButtonByCaption(floor, UiStrings.ContinueToExactReview);
        var buttons = Assert.IsType<FlowLayoutPanel>(continueButton.Parent);
        var compactDocumentHeight = exactDocument.Height;
        var compactButtonHeight = buttons.Height;
        try
        {
            floor.ClientCanvas.ClientSize = new Size(1184, 861);
            FlushLayout(floor.ClientCanvas);

            Assert.True(
                exactDocument.Height > compactDocumentHeight,
                "Loaded-project exact-document content did not absorb surplus window height.");
            Assert.Equal(compactButtonHeight, buttons.Height);
        }
        finally
        {
            floor.ClientCanvas.ClientSize = floorSize;
            FlushLayout(floor.ClientCanvas);
        }

        AssertFloor(floor);
    }

    private static ReviewedFloorCatalogFile CreateReviewedFloorCatalog(string direction)
    {
        Assert.True(
            direction is "ltr" or "rtl",
            $"Unsupported synthetic floor direction: {direction}.");
        var root = JsonNode.Parse(UiCatalogInventory.CreateTemplateJson())!.AsObject();
        root["languageTag"] = "en-US";
        root["direction"] = direction;
        var review = root["review"]!.AsObject();
        review["status"] = UiCatalogInventory.ReviewedStatus;
        review["reviewerName"] = "Synthetic automated floor fixture — not protected-seat evidence";
        review["reviewerRole"] = UiCatalogInventory.RequiredReviewerRole;
        review["reviewedAtUtc"] = "2026-08-31T12:00:00Z";
        var provenance = root["provenance"]!.AsObject();
        provenance["catalogId"] = $"synthetic-floor-{direction}";
        provenance["creator"] = "Automated test fixture";
        provenance["source"] = "Generated in memory for this floor test only";
        provenance["license"] = "GPL-3.0-or-later test fixture";
        provenance["modificationHistory"] =
            new JsonArray("Appended a diagnostic floor marker to neutral fixture text");

        var strings = root["strings"]!.AsObject();
        foreach (var pair in UiCatalogInventory.NeutralStrings)
        {
            strings[pair.Key] = pair.Value + ReviewedFloorMarker;
        }

        return ReviewedFloorCatalogFile.FromJson(
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

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
    public void Floor_assertion_requires_nonzero_reachable_essential_visual_viewports()
        => RunSta(() =>
        {
            using (var zeroAreaForm = SyntheticChromeForm())
            {
                zeroAreaForm.Controls.Add(new PictureBox
                {
                    AccessibleName = "Synthetic zero-area visual viewport",
                    Location = new Point(20, 20),
                    Size = new Size(160, 0),
                });
                using var zeroAreaHost = PrepareAtFloor(zeroAreaForm, 1.0f, maximize: false);

                var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
                    () => AssertFloor(zeroAreaHost));
                Assert.Contains(
                    "Synthetic zero-area visual viewport",
                    failure.Message,
                    StringComparison.Ordinal);
                Assert.Contains("nonzero client area", failure.Message, StringComparison.Ordinal);
            }

            using (var clippedForm = SyntheticChromeForm())
            {
                clippedForm.Controls.Add(new WebBrowser
                {
                    AccessibleName = "Synthetic clipped visual viewport",
                    Location = new Point(360, 20),
                    Size = new Size(160, 120),
                });
                using var clippedHost = PrepareAtFloor(clippedForm, 1.0f, maximize: false);

                var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
                    () => AssertFloor(clippedHost));
                Assert.Contains(
                    "Synthetic clipped visual viewport",
                    failure.Message,
                    StringComparison.Ordinal);
                Assert.Contains("clipped", failure.Message, StringComparison.Ordinal);
            }
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
                    // Wider than the viewport: both ends must be reachable at
                    // different real scroll positions, not merely overlap it.
                    Size = new Size(240, 30),
                    Text = "Synthetic reachable",
                });
                scrollableForm.Controls.Add(scrollable);
                using var scrollableHost = PrepareAtFloor(scrollableForm, 1.0f, maximize: false);
                var originalScroll = scrollable.AutoScrollPosition;

                AssertFloor(scrollableHost);
                Assert.Equal(originalScroll, scrollable.AutoScrollPosition);
            }

            using (var nestedForm = new Form { Size = new Size(400, 300) })
            {
                var outer = new Panel
                {
                    AutoScroll = true,
                    AutoScrollMinSize = new Size(440, 320),
                    Location = new Point(20, 20),
                    Size = new Size(180, 120),
                };
                var inner = new Panel
                {
                    AutoScroll = true,
                    AutoScrollMinSize = new Size(540, 240),
                    Location = new Point(190, 130),
                    Size = new Size(200, 140),
                };
                inner.Controls.Add(new Button
                {
                    AccessibleName = "Synthetic nested oversized control",
                    Location = new Point(250, 20),
                    Size = new Size(240, 30),
                    Text = "Synthetic nested reachable",
                });
                outer.Controls.Add(inner);
                nestedForm.Controls.Add(outer);
                using var nestedHost = PrepareAtFloor(nestedForm, 1.0f, maximize: false);
                outer.AutoScrollPosition = new Point(80, 60);
                inner.AutoScrollPosition = new Point(110, 25);
                var originalOuterScroll = outer.AutoScrollPosition;
                var originalInnerScroll = inner.AutoScrollPosition;

                AssertFloor(nestedHost);

                Assert.Equal(originalOuterScroll, outer.AutoScrollPosition);
                Assert.Equal(originalInnerScroll, inner.AutoScrollPosition);
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

            using var partialForm = new Form { Size = new Size(400, 300) };
            var partialScroller = new Panel
            {
                AutoScroll = true,
                Location = new Point(20, 20),
                Size = new Size(160, 100),
            };
            partialScroller.Controls.Add(new Button
            {
                AccessibleName = "Synthetic partially unreachable oversized control",
                Location = new Point(-20, 20),
                Size = new Size(200, 30),
                Text = "Synthetic wide control",
            });
            partialForm.Controls.Add(partialScroller);
            using var partialHost = PrepareAtFloor(partialForm, 1.0f, maximize: false);

            var partialFailure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(partialHost));
            Assert.Contains(
                "Synthetic partially unreachable oversized control",
                partialFailure.Message,
                StringComparison.Ordinal);

            using var zeroAreaForm = new Form { Size = new Size(400, 300) };
            var zeroAreaScroller = new Panel
            {
                AutoScroll = true,
                Location = new Point(20, 20),
                Size = new Size(160, 100),
            };
            zeroAreaScroller.Controls.Add(new Button
            {
                AccessibleName = "Synthetic zero-height oversized control",
                Location = new Point(250, 20),
                Size = new Size(240, 0),
                Text = "Synthetic zero-height control",
            });
            zeroAreaForm.Controls.Add(zeroAreaScroller);
            using var zeroAreaHost = PrepareAtFloor(zeroAreaForm, 1.0f, maximize: false);

            var zeroAreaFailure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
                () => AssertFloor(zeroAreaHost));
            Assert.Contains(
                "Synthetic zero-height oversized control",
                zeroAreaFailure.Message,
                StringComparison.Ordinal);
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

    [Fact]
    public void Floor_assertion_requires_interactive_captions_to_fit_their_visible_controls()
        => RunSta(() =>
        {
            static ButtonBase ClippedButtonBase(string kind)
            {
                return kind switch
                {
                    "button" => new Button(),
                    "check box" => new CheckBox(),
                    "radio button" => new RadioButton(),
                    _ => throw new InvalidOperationException($"Unknown synthetic button kind: {kind}."),
                };
            }

            foreach (var kind in SyntheticInteractiveKinds)
            {
                using var clippedForm = SyntheticChromeForm();
                var clipped = ClippedButtonBase(kind);
                clipped.AccessibleName = $"Synthetic clipped {kind}";
                clipped.AutoSize = false;
                clipped.Location = new Point(20, 20);
                clipped.Size = new Size(72, 24);
                clipped.Text = $"Synthetic {kind} caption that cannot fit";
                clippedForm.Controls.Add(clipped);
                using var clippedHost = PrepareAtFloor(clippedForm, 1.0f, maximize: false);

                var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(clippedHost));
                Assert.Contains(clipped.Text, failure.Message, StringComparison.Ordinal);
            }

            using (var constrainedAutoSizeForm = SyntheticChromeForm())
            {
                var constraint = new Panel
                {
                    Location = new Point(20, 20),
                    Size = new Size(90, 30),
                };
                var constrained = new Button
                {
                    AccessibleName = "Synthetic constrained auto-size button",
                    AutoSize = true,
                    Dock = DockStyle.Fill,
                    Text = "Synthetic constrained auto-size caption cannot fit",
                };
                constraint.Controls.Add(constrained);
                constrainedAutoSizeForm.Controls.Add(constraint);
                using var constrainedHost = PrepareAtFloor(
                    constrainedAutoSizeForm,
                    1.0f,
                    maximize: false);

                var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
                    () => AssertFloor(constrainedHost));
                Assert.Contains(constrained.Text, failure.Message, StringComparison.Ordinal);
            }

            using (var literalAmpersandForm = SyntheticChromeForm())
            {
                const string Caption = "A&B&C&D&E&F&G&H";
                var mnemonicWidth = TextRenderer.MeasureText(
                    WithoutMnemonics(Caption),
                    literalAmpersandForm.Font,
                    Size.Empty,
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.NoPrefix).Width;
                var literalAmpersands = new Button
                {
                    AccessibleName = "Synthetic clipped literal ampersands",
                    AutoSize = false,
                    Location = new Point(20, 20),
                    Size = new Size(
                        mnemonicWidth + (SystemInformation.Border3DSize.Width * 2) + 4,
                        30),
                    Text = Caption,
                    UseMnemonic = false,
                };
                literalAmpersandForm.Controls.Add(literalAmpersands);
                using var literalAmpersandHost = PrepareAtFloor(
                    literalAmpersandForm,
                    1.0f,
                    maximize: false);

                var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(
                    () => AssertFloor(literalAmpersandHost));
                Assert.Contains(Caption, failure.Message, StringComparison.Ordinal);
            }

            using (var wrappedForm = SyntheticChromeForm())
            {
                wrappedForm.Controls.Add(new Button
                {
                    AccessibleName = "Synthetic readable wrapped button",
                    AutoSize = false,
                    Location = new Point(20, 20),
                    Size = new Size(140, 80),
                    Text = "Synthetic readable button caption wraps across several visible lines",
                });
                using var wrappedHost = PrepareAtFloor(wrappedForm, 1.0f, maximize: false);

                AssertFloor(wrappedHost);
            }

            using var readableForm = SyntheticChromeForm();
            readableForm.Controls.Add(new Button
            {
                AccessibleName = "Synthetic readable button",
                AutoSize = true,
                Location = new Point(20, 20),
                Text = "Synthetic readable button",
            });
            readableForm.Controls.Add(new CheckBox
            {
                AccessibleName = "Synthetic readable check box",
                AutoSize = true,
                Location = new Point(20, 60),
                Text = "Synthetic readable check box",
            });
            readableForm.Controls.Add(new RadioButton
            {
                AccessibleName = "Synthetic readable radio button",
                AutoSize = true,
                Location = new Point(20, 100),
                Text = "Synthetic readable radio button",
            });
            using var readableHost = PrepareAtFloor(readableForm, 1.0f, maximize: false);

            AssertFloor(readableHost);
        });

    [Fact]
    public void Floor_assertion_requires_combo_items_to_fit_or_have_a_dropdown_reading_path()
        => RunSta(() =>
        {
            const string LongChoice =
                "Synthetic choice that needs a wider explicit dropdown reading path";

            using (var clippedForm = SyntheticChromeForm())
            {
                var clipped = new ComboBox
                {
                    AccessibleName = "Synthetic clipped choices",
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(20, 20),
                    Width = 110,
                };
                clipped.Items.Add(LongChoice);
                clipped.SelectedIndex = 0;
                clippedForm.Controls.Add(clipped);
                using var clippedHost = PrepareAtFloor(clippedForm, 1.0f, maximize: false);

                var failure = Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertFloor(clippedHost));
                Assert.Contains(LongChoice, failure.Message, StringComparison.Ordinal);
                Assert.Contains("dropdown reading path", failure.Message, StringComparison.Ordinal);
            }

            using var readableForm = SyntheticChromeForm();
            var readable = new ComboBox
            {
                AccessibleName = "Synthetic readable choices",
                DropDownStyle = ComboBoxStyle.DropDownList,
                DropDownWidth = 380,
                Location = new Point(20, 20),
                Width = 110,
            };
            readable.Items.Add(LongChoice);
            readable.SelectedIndex = 0;
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
            if (index == 2)
            {
                AssertEssentialVisualViewport<WebBrowser>(
                    floor,
                    UiStrings.UnapprovedVisualPreview);
            }
        }
    }

    private static void ExerciseCaptureRecoveryStatesAtFloor(float scale, Rectangle workingArea)
    {
        var store = new FailsFirstFloorPurgeStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new RefusingFloorNormalizer(),
            store);
        session.CaptureAsync(
                new CaptureRequest(
                    ByteImportCaptureSource.Kind,
                    "image/png",
                    new byte[] { 1, 2, 3 }),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        using var form = new CaptureForm(
            session,
            DistrictPolicy.Offline,
            safetyPausePresenter: _ => { });
        using var floor = PrepareAtFloor(form, scale, maximize: true, workingArea);
        var retryNormalization = ButtonByCaption(floor, UiStrings.RetryNormalization);
        Assert.True(retryNormalization.Enabled);

        retryNormalization.PerformClick();
        PumpUntil(
            () => !form.OperationPending,
            "The synthetic normalization refusal did not settle.");

        Assert.Equal(JobState.Imported, session.Machine.State);
        Assert.Equal(
            UiStrings.FormatWithoutMnemonic(
                UiStrings.StatusNormalizationRetry,
                FloorNormalizationRefusal),
            StatusLabel(floor, "CaptureStatus").Text);
        AssertFloor(floor);

        var safetyPause = ButtonByCaption(floor, UiStrings.SafetyPause);
        Assert.True(safetyPause.Enabled);
        safetyPause.PerformClick();

        Assert.Equal(JobState.PurgeIncomplete, session.Machine.State);
        var retryPurge = ButtonByCaption(floor, UiStrings.RetrySecurePurge);
        Assert.True(retryPurge.Visible);
        Assert.True(retryPurge.Enabled);
        Assert.Equal(
            UiStrings.WithoutMnemonic(UiStrings.StatusPurgeIncomplete),
            StatusLabel(floor, "CaptureStatus").Text);
        FlushLayout(floor.ClientCanvas);
        AssertFloor(floor);
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

    private static void ExercisePressExportStatesAtFloor(
        FloorHost floor,
        DeferredFloorExport exporter)
    {
        var form = Assert.IsType<PressRoomForm>(floor.Surface);
        ButtonByCaption(floor, UiStrings.ReviewAndApprove).PerformClick();
        Assert.NotNull(form.ApprovedResult);

        ExerciseExportLifecycleAtFloor(
            floor,
            exporter,
            form.ExportAsync,
            () => form.StatusText,
            FloorPdfExportName,
            "Press Room");
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

    private static void ExerciseAllAboardExportStatesAtFloor(
        FloorHost floor,
        DeferredFloorExport exporter)
    {
        var form = Assert.IsType<AllAboardForm>(floor.Surface);
        var textInputs = Descendants(floor.ClientCanvas).OfType<TextBox>()
            .Where(control => control.Visible)
            .ToArray();
        Assert.NotEmpty(textInputs);
        for (var index = 0; index < textInputs.Length; index++)
        {
            textInputs[index].Text = $"Synthetic floor task text {index + 1}.";
        }

        ButtonByCaption(floor, UiStrings.ReviewAndApprove).PerformClick();
        var approved = Assert.IsType<ApprovedArtifact>(form.ApprovedResult);

        ExerciseExportLifecycleAtFloor(
            floor,
            exporter,
            () => form.ExportAsync(approved),
            () => form.StatusText,
            FloorPdfExportName,
            "SequenceSlate");
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

    private static void ExerciseModuleExportStatesAtFloor(
        FloorHost floor,
        DeferredFloorExport exporter)
    {
        var form = Assert.IsType<ModuleStudioForm>(floor.Surface);
        var review = ButtonByCaption(floor, UiStrings.ReviewAndApprove);
        Assert.True(review.Enabled, "The synthetic initial Module Studio mode was not reviewable.");
        review.PerformClick();
        Assert.NotNull(form.ApprovedResult);

        ExerciseExportLifecycleAtFloor(
            floor,
            exporter,
            form.ExportAsync,
            () => form.StatusText,
            FloorHtmlExportName,
            "Module Studio");
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

    private static void AssertExportBusyAtFloor(
        FloorHost floor,
        string status,
        string destinationName)
    {
        Assert.Equal(
            UiStrings.FormatWithoutMnemonic(UiStrings.StatusExporting, destinationName),
            status);
        Assert.True(ButtonByCaption(floor, UiStrings.CancelExport).Enabled);
        Assert.False(ButtonByCaption(floor, UiStrings.ExportEllipsis).Enabled);
        AssertStatusAccessibilityText(floor, status);
        FlushLayout(floor.ClientCanvas);
        AssertFloor(floor);
    }

    private static void ExerciseExportLifecycleAtFloor(
        FloorHost floor,
        DeferredFloorExport exporter,
        Func<Task> beginExport,
        Func<string> status,
        string destinationName,
        string surfaceName)
    {
        Assert.Equal(255, destinationName.Length);
        Assert.Equal(destinationName, Path.GetFileName(destinationName));
        Assert.Equal(-1, destinationName.IndexOfAny(Path.GetInvalidFileNameChars()));

        Task? activeExport = null;
        try
        {
            BeginFloorExport(
                floor,
                exporter,
                beginExport,
                work => activeExport = work,
                status,
                destinationName,
                surfaceName,
                expectedAttempt: 1);
            ButtonByCaption(floor, UiStrings.CancelExport).PerformClick();
            PumpUntil(
                () => activeExport!.IsCompleted,
                $"The {surfaceName} floor export cancellation did not settle.");
            activeExport!.GetAwaiter().GetResult();
            AssertExportTerminalAtFloor(
                floor,
                status(),
                UiStrings.WithoutMnemonic(UiStrings.StatusExportCancelled));
            activeExport = null;

            BeginFloorExport(
                floor,
                exporter,
                beginExport,
                work => activeExport = work,
                status,
                destinationName,
                surfaceName,
                expectedAttempt: 2);
            exporter.Complete();
            PumpUntil(
                () => activeExport!.IsCompleted,
                $"The {surfaceName} successful floor export did not settle.");
            activeExport!.GetAwaiter().GetResult();
            AssertExportTerminalAtFloor(
                floor,
                status(),
                UiStrings.FormatWithoutMnemonic(UiStrings.StatusExported, destinationName));
            activeExport = null;

            BeginFloorExport(
                floor,
                exporter,
                beginExport,
                work => activeExport = work,
                status,
                destinationName,
                surfaceName,
                expectedAttempt: 3);
            exporter.Refuse();
            PumpUntil(
                () => activeExport!.IsCompleted,
                $"The {surfaceName} floor export refusal did not settle.");
            activeExport!.GetAwaiter().GetResult();
            AssertExportRefusalAtFloor(floor, status());
            activeExport = null;
        }
        finally
        {
            SettleFloorExportAfterFailure(exporter, activeExport);
        }

        Assert.False(exporter.HasActiveAttempt);
    }

    private static void BeginFloorExport(
        FloorHost floor,
        DeferredFloorExport exporter,
        Func<Task> beginExport,
        Action<Task> trackExport,
        Func<string> status,
        string destinationName,
        string surfaceName,
        int expectedAttempt)
    {
        var exportWork = beginExport();
        trackExport(exportWork);
        PumpUntil(
            () => exporter.StartedCount == expectedAttempt,
            $"The {surfaceName} floor export attempt {expectedAttempt} did not start.");
        Assert.False(exportWork.IsCompleted);
        AssertExportBusyAtFloor(floor, status(), destinationName);
    }

    private static void SettleFloorExportAfterFailure(
        DeferredFloorExport exporter,
        Task? exportWork)
    {
        if (exportWork is null)
        {
            return;
        }

        if (!exportWork.IsCompleted)
        {
            _ = exporter.TrySettleActive();
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            while (!exportWork.IsCompleted && elapsed.Elapsed < TimeSpan.FromSeconds(5))
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(10);
            }
        }

        if (exportWork.IsCompleted)
        {
            try
            {
                exportWork.GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // Cleanup must preserve the assertion that triggered it. The
                // task is settled and observed; its normal-path semantics are
                // asserted before this failure-only boundary is entered.
            }
        }
        else
        {
            _ = exportWork.ContinueWith(
                static completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private static void AssertDeferredExportFailureIsIsolated()
    {
        var exporter = new DeferredFloorExport();
        Task? exportWork = null;
        var failure = Record.Exception(() =>
        {
            try
            {
                exportWork = exporter.RunAsync(CancellationToken.None);
                Assert.Fail("Synthetic assertion before a deferred export settled.");
            }
            finally
            {
                SettleFloorExportAfterFailure(exporter, exportWork);
            }
        });

        var assertion = Assert.IsType<Xunit.Sdk.FailException>(failure);
        Assert.Contains("Synthetic assertion", assertion.Message, StringComparison.Ordinal);
        Assert.NotNull(exportWork);
        Assert.True(exportWork.IsCompleted);
        exportWork.GetAwaiter().GetResult();
        Assert.False(exporter.HasActiveAttempt);
    }

    private static void AssertExportTerminalAtFloor(
        FloorHost floor,
        string status,
        string expectedStatus)
    {
        Assert.Equal(expectedStatus, status);
        Assert.False(ButtonByCaption(floor, UiStrings.CancelExport).Enabled);
        Assert.True(ButtonByCaption(floor, UiStrings.ExportEllipsis).Enabled);
        AssertStatusAccessibilityText(floor, status);
        FlushLayout(floor.ClientCanvas);
        AssertFloor(floor);
    }

    private static void AssertExportRefusalAtFloor(FloorHost floor, string status)
    {
        AssertExportTerminalAtFloor(
            floor,
            status,
            UiStrings.FormatWithoutMnemonic(UiStrings.StatusRefused, FloorExportRefusal));
    }

    private static void ExerciseEveryNodeEditorVariantAtFloor(float scale, Rectangle workingArea)
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
            var originalOuterSize = editor.Size;
            using var host = PrepareAtFloor(editor, scale, maximize: false, workingArea);
            Assert.Equal(originalOuterSize, host.OuterBounds.Size);
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

    private static ApprovedArtifact? ApproveSyntheticFloorArtifact(ReviewSession session)
    {
        session.SetRequiredIssuesAcknowledged(acknowledged: true);
        return session.CanApprove
            ? session.Approve("Synthetic floor teacher", DateTimeOffset.UnixEpoch)
            : null;
    }

    private static Task RunFloorExportWork(
        Func<Task> work,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return work();
    }

    private static ContentScaleMeasurement MeasureHermeticContentScale(float scale)
    {
        using var form = new Form { Size = new Size(400, 300) };
        using var authoredFont = new Font(
            form.Font.FontFamily,
            12,
            FontStyle.Bold,
            GraphicsUnit.Point);
        var inherited = new Label
        {
            AutoSize = true,
            AccessibleName = "Synthetic inherited-font metric",
            Location = new Point(20, 20),
            Text = "Hermetic inherited font metric",
        };
        var authored = new Label
        {
            AutoSize = true,
            AccessibleName = "Synthetic authored-font metric",
            Font = authoredFont,
            Location = new Point(20, 80),
            Text = "Hermetic authored font metric",
        };
        form.Controls.AddRange([inherited, authored]);

        Assert.False(HasLocallyAuthoredFont(inherited));
        Assert.True(HasLocallyAuthoredFont(authored));
        var inheritedFontSize = inherited.Font.Size;
        var authoredFontSize = authored.Font.Size;
        var inheritedTextSize = MeasureSingleLine(inherited.Text, inherited.Font);
        var authoredTextSize = MeasureSingleLine(authored.Text, authored.Font);
        var expectedClientSize = form.ClientSize;

        using var floor = PrepareAtFloor(form, scale, maximize: false);
        Assert.Equal(expectedClientSize, floor.ClientCanvas.ClientSize);
        Assert.Same(floor.ClientCanvas.Font, inherited.Font);
        Assert.NotSame(floor.ClientCanvas.Font, authored.Font);

        return new ContentScaleMeasurement(
            floor.ClientCanvas.ClientSize,
            inheritedFontSize,
            inherited.Font.Size,
            inheritedTextSize,
            MeasureSingleLine(inherited.Text, inherited.Font),
            authoredFontSize,
            authored.Font.Size,
            authoredTextSize,
            MeasureSingleLine(authored.Text, authored.Font));
    }

    private static Size MeasureSingleLine(string text, Font font)
        => TextRenderer.MeasureText(
            text,
            font,
            Size.Empty,
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

    private static void AssertApproximatelyDoubled(
        Size original,
        Size scaled,
        string fontKind)
    {
        Assert.InRange(
            scaled.Width / (double)original.Width,
            1.8,
            2.2);
        Assert.InRange(
            scaled.Height / (double)original.Height,
            1.8,
            2.2);
        Assert.True(
            scaled.Width > original.Width && scaled.Height > original.Height,
            $"The {fontKind} font's rendered text metrics did not materially increase: " +
            $"original={original},scaled={scaled}.");
    }

    private static Button ButtonByCaption(FloorHost floor, string caption)
    {
        var expected = WithoutMnemonics(caption);
        return Descendants(floor.ClientCanvas).OfType<Button>()
            .Single(button => string.Equals(
                WithoutMnemonics(button.Text),
                expected,
                StringComparison.Ordinal));
    }

    private static Label StatusLabel(FloorHost floor, string name)
        => Descendants(floor.ClientCanvas).OfType<Label>()
            .Single(label => string.Equals(label.Name, name, StringComparison.Ordinal));

    private static void AssertStatusAccessibilityText(FloorHost floor, string status)
    {
        var statusLabel = Descendants(floor.ClientCanvas).OfType<Label>()
            .Single(label => string.Equals(label.Text, status, StringComparison.Ordinal));
        Assert.Equal(status, statusLabel.AccessibilityObject.Name);
    }

    private static void PumpUntil(Func<bool> condition, string failureMessage)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(10);
        }

        Assert.True(condition(), failureMessage);
    }

    private static FloorHost PrepareAtFloor(
        Form form,
        float scale,
        bool maximize = true,
        Rectangle? workingArea = null)
    {
        Assert.False(
            form.IsHandleCreated,
            $"{form.GetType().Name} created a native window before the hermetic floor layout.");
        Assert.True(float.IsFinite(scale) && scale > 0, $"Invalid floor scale: {scale}.");

        var assertedWorkingArea = workingArea ?? FloorWorkingArea;
        var requested = maximize
            ? assertedWorkingArea.Size
            : new Size(
                Math.Min(form.Width, assertedWorkingArea.Width),
                Math.Min(form.Height, assertedWorkingArea.Height));

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
        var contentScaleFonts = new List<Font>();
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
                ApplyHermeticContentScale(host, scale, contentScaleFonts);
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
            return new FloorHost(
                form,
                host,
                assertedWorkingArea,
                new Rectangle(assertedWorkingArea.Location, requested),
                contentScaleFonts);
        }
        catch
        {
            host.Dispose();
            foreach (var font in contentScaleFonts)
            {
                font.Dispose();
            }

            throw;
        }
    }

    private static void ApplyHermeticContentScale(
        FloorClientCanvas host,
        float scale,
        List<Font> ownedFonts)
    {
        // Control.Scale changes geometry but deliberately leaves Font alone.
        // Capture the locally authored font boundaries before changing the
        // ambient root font. Each other control then receives its scaled font
        // through exactly one inheritance edge, while an authored override is
        // replaced once at its own boundary.
        var authoredFonts = Descendants(host)
            .Where(HasLocallyAuthoredFont)
            .Select(control => (Control: control, control.Font))
            .ToArray();
        var scaledHostFont = ScaleFont(host.Font, scale);
        ownedFonts.Add(scaledHostFont);
        host.Font = scaledHostFont;

        foreach (var authored in authoredFonts)
        {
            var scaledFont = ScaleFont(authored.Font, scale);
            ownedFonts.Add(scaledFont);
            authored.Control.Font = scaledFont;
        }
    }

    private static bool HasLocallyAuthoredFont(Control control)
        => TypeDescriptor.GetProperties(control)[nameof(Control.Font)]
            ?.ShouldSerializeValue(control) == true;

    private static Font ScaleFont(Font font, float scale)
        => new(
            font.FontFamily,
            font.Size * scale,
            font.Style,
            font.Unit,
            font.GdiCharSet,
            font.GdiVerticalFont);

    private static void AssertFloor(FloorHost floor)
    {
        var form = floor.Surface;
        Assert.True(
            floor.WorkingArea.Contains(floor.OuterBounds),
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

            if (control is ButtonBase button)
            {
                AssertButtonCaptionFits(form, button);
            }

            if (control is ComboBox combo)
            {
                AssertComboItemTextIsReadable(form, combo);
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

        foreach (var viewport in Descendants(floor.ClientCanvas)
                     .Where(control => control.Visible && IsEssentialVisualViewport(control)))
        {
            AssertEssentialVisualViewport(form, viewport, floor.ClientCanvas);
        }
    }

    private static bool IsReachabilitySurface(Control control)
        => control.TabStop
            || control is ButtonBase or ComboBox or ListBox or TextBoxBase or NumericUpDown
            || control.AccessibilityObject.Role == AccessibleRole.StatusBar;

    private static bool IsNoninteractiveChrome(Control control)
        => control is Label or GroupBox or TabControl or DataGridView;

    private static bool IsEssentialVisualViewport(Control control)
        => control is PictureBox or WebBrowser;

    private static void AssertEssentialVisualViewport<TControl>(
        FloorHost floor,
        string accessibleName)
        where TControl : Control
    {
        var expectedName = UiStrings.WithoutMnemonic(accessibleName);
        var viewport = Descendants(floor.ClientCanvas).OfType<TControl>()
            .Single(control => string.Equals(
                control.AccessibilityObject.Name,
                expectedName,
                StringComparison.Ordinal));
        Assert.True(
            viewport.Visible,
            $"{floor.Surface.GetType().Name}: essential visual viewport '{expectedName}' is not visible.");
        AssertEssentialVisualViewport(floor.Surface, viewport, floor.ClientCanvas);
    }

    private static void AssertEssentialVisualViewport(
        Form form,
        Control viewport,
        Control root)
    {
        var name = viewport.AccessibilityObject.Name;
        Assert.False(
            string.IsNullOrWhiteSpace(name),
            $"{form.GetType().Name} contains an unnamed essential {viewport.GetType().Name} viewport.");
        Assert.True(
            viewport.ClientSize.Width > 0 && viewport.ClientSize.Height > 0,
            $"{form.GetType().Name}: essential visual viewport '{name}' " +
            $"({viewport.GetType().Name}) has no nonzero client area. {BoundsTrace(viewport)}");
        Assert.True(
            IsFullyVisibleOrScrollable(viewport, root),
            $"{form.GetType().Name}: essential visual viewport '{name}' " +
            $"({viewport.GetType().Name}) is clipped inside a non-scrollable viewport at the " +
            $"1366 x 768 floor. {BoundsTrace(viewport)}");
    }

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

    private static void AssertButtonCaptionFits(Form form, ButtonBase button)
    {
        if (button.Text.Length == 0 || button.AutoEllipsis)
        {
            return;
        }

        var caption = button.UseMnemonic
            ? WithoutMnemonics(button.Text)
            : button.Text;
        var furniture = button switch
        {
            CheckBox { Appearance: Appearance.Normal } or
            RadioButton { Appearance: Appearance.Normal }
                => new Size(SystemInformation.MenuCheckSize.Width + 3, 0),
            _ => new Size(
                (SystemInformation.Border3DSize.Width * 2) + 4,
                SystemInformation.Border3DSize.Height * 2),
        };
        var available = new Size(
            Math.Max(
                1,
                button.ClientSize.Width - button.Padding.Horizontal - furniture.Width),
            Math.Max(
                0,
                button.ClientSize.Height - button.Padding.Vertical - furniture.Height));
        var required = TextRenderer.MeasureText(
            caption,
            button.Font,
            new Size(available.Width, int.MaxValue),
            TextFormatFlags.WordBreak |
            TextFormatFlags.TextBoxControl |
            TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix);
        Assert.True(
            required.Width <= available.Width && required.Height <= available.Height,
            $"{form.GetType().Name}: interactive caption '{caption}' requires " +
            $"{required.Width}x{required.Height}px when constrained to its visible text width, " +
            $"but its {button.GetType().Name} offers {available.Width}x{available.Height}px " +
            $"after authored padding and {furniture.Width}x{furniture.Height}px of " +
            $"interactive furniture; autoSize={button.AutoSize},dock={button.Dock}," +
            $"minimum={button.MinimumSize}. {BoundsTrace(button)}");
    }

    private static void AssertComboItemTextIsReadable(Form form, ComboBox combo)
    {
        var selectedWidth = Math.Max(
            0,
            combo.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
        var configuredDropDownWidth = combo.DropDownWidth > 0
            ? combo.DropDownWidth
            : combo.Width;
        var dropDownWidth = Math.Min(configuredDropDownWidth, FloorWorkingArea.Width);
        var dropDownItemWidth = Math.Max(
            0,
            dropDownWidth - SystemInformation.VerticalScrollBarWidth - 4);
        var availableWidth = Math.Max(selectedWidth, dropDownItemWidth);

        foreach (var item in combo.Items.Cast<object>())
        {
            var text = item.ToString() ?? "";
            if (text.Length == 0)
            {
                continue;
            }

            var required = TextRenderer.MeasureText(
                text,
                combo.Font,
                Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            Assert.True(
                required.Width <= availableWidth,
                $"{form.GetType().Name}: combo item '{text}' requires {required.Width}px, but its " +
                $"selected-value and dropdown reading paths offer at most {availableWidth}px.");
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
            $"when wrapped to {available.Width}px, but its visible header offers {available.Height}px; " +
            $"gridClient={grid.ClientRectangle},header={header}. {BoundsTrace(grid)}");
    }

    private static string WithoutMnemonics(string text)
    {
        var value = new StringBuilder(text.Length);
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
            var initialViewport = owner.RectangleToScreen(owner.ClientRectangle);
            var initialTarget = owner.RectangleToClient(
                target.RectangleToScreen(target.ClientRectangle));
            var logicalTarget = new Rectangle(
                initialTarget.Left - original.X,
                initialTarget.Top - original.Y,
                initialTarget.Width,
                initialTarget.Height);
            if (logicalTarget.Width <= 0 || logicalTarget.Height <= 0)
            {
                return false;
            }

            var oversizedHorizontally = logicalTarget.Width > initialViewport.Width;
            var oversizedVertically = logicalTarget.Height > initialViewport.Height;

            if (!oversizedHorizontally && !oversizedVertically)
            {
                owner.ScrollControlIntoView(target);
                owner.PerformLayout();
                var viewport = owner.RectangleToScreen(owner.ClientRectangle);
                var exposed = target.RectangleToScreen(target.ClientRectangle);
                return exposed.Width > 0
                    && exposed.Height > 0
                    && AxisIsFullyExposed(exposed.Left, exposed.Width, viewport.Left, viewport.Width)
                    && AxisIsFullyExposed(exposed.Top, exposed.Height, viewport.Top, viewport.Height);
            }

            // An oversized target cannot fit at one scroll position. Prove the
            // leading and trailing endpoints separately. AutoScroll clamps a
            // requested position to its real range, so a negative leading edge
            // or a trailing edge beyond the display extent remains exposed as
            // unreachable instead of passing on partial overlap.
            owner.AutoScrollPosition = new Point(
                Math.Max(0, logicalTarget.Left),
                Math.Max(0, logicalTarget.Top));
            owner.PerformLayout();
            var leadingViewport = owner.RectangleToScreen(owner.ClientRectangle);
            var leadingTarget = target.RectangleToScreen(target.ClientRectangle);
            var leadingExposed = AxisLeadingEndpointIsExposed(
                    leadingTarget.Left,
                    leadingTarget.Width,
                    leadingViewport.Left,
                    leadingViewport.Width)
                && AxisLeadingEndpointIsExposed(
                    leadingTarget.Top,
                    leadingTarget.Height,
                    leadingViewport.Top,
                    leadingViewport.Height);

            owner.AutoScrollPosition = new Point(
                Math.Max(0, logicalTarget.Right - initialViewport.Width),
                Math.Max(0, logicalTarget.Bottom - initialViewport.Height));
            owner.PerformLayout();
            var trailingViewport = owner.RectangleToScreen(owner.ClientRectangle);
            var trailingTarget = target.RectangleToScreen(target.ClientRectangle);
            var trailingExposed = AxisTrailingEndpointIsExposed(
                    trailingTarget.Left,
                    trailingTarget.Width,
                    trailingViewport.Left,
                    trailingViewport.Width)
                && AxisTrailingEndpointIsExposed(
                    trailingTarget.Top,
                    trailingTarget.Height,
                    trailingViewport.Top,
                    trailingViewport.Height);
            return leadingExposed && trailingExposed;
        }
        finally
        {
            // Reachability inspection must not leave one candidate scrolled
            // into view at the expense of the candidates asserted after it.
            owner.AutoScrollPosition = new Point(-original.X, -original.Y);
            owner.PerformLayout();
        }
    }

    private static bool AxisIsFullyExposed(
        int targetStart,
        int targetLength,
        int viewportStart,
        int viewportLength)
    {
        var targetEnd = (long)targetStart + targetLength;
        var viewportEnd = (long)viewportStart + viewportLength;
        return targetStart >= viewportStart && targetEnd <= viewportEnd;
    }

    private static bool AxisLeadingEndpointIsExposed(
        int targetStart,
        int targetLength,
        int viewportStart,
        int viewportLength)
        => targetLength <= viewportLength
            ? AxisIsFullyExposed(targetStart, targetLength, viewportStart, viewportLength)
            : targetStart >= viewportStart && targetStart < (long)viewportStart + viewportLength;

    private static bool AxisTrailingEndpointIsExposed(
        int targetStart,
        int targetLength,
        int viewportStart,
        int viewportLength)
    {
        if (targetLength <= viewportLength)
        {
            return AxisIsFullyExposed(targetStart, targetLength, viewportStart, viewportLength);
        }

        var targetEnd = (long)targetStart + targetLength;
        var viewportEnd = (long)viewportStart + viewportLength;
        return targetEnd > viewportStart && targetEnd <= viewportEnd;
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
            trace.Add(parent is ScrollableControl scrollable
                ? $"{parent.GetType().Name}={viewport},autoScroll={scrollable.AutoScroll}," +
                    $"display={scrollable.DisplayRectangle},min={scrollable.AutoScrollMinSize}," +
                    $"position={scrollable.AutoScrollPosition}"
                : $"{parent.GetType().Name}={viewport},autoScroll=False");
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

    private sealed class FloorHost(
        Form surface,
        Panel clientCanvas,
        Rectangle workingArea,
        Rectangle outerBounds,
        IReadOnlyList<Font> contentScaleFonts) : IDisposable
    {
        public Form Surface { get; } = surface;

        public Panel ClientCanvas { get; } = clientCanvas;

        public Rectangle WorkingArea { get; } = workingArea;

        public Rectangle OuterBounds { get; } = outerBounds;

        public void Dispose()
        {
            ClientCanvas.Dispose();
            foreach (var font in contentScaleFonts)
            {
                font.Dispose();
            }
        }
    }

    private sealed record ContentScaleMeasurement(
        Size ClientSize,
        float InheritedFontSize,
        float ScaledInheritedFontSize,
        Size InheritedTextSize,
        Size ScaledInheritedTextSize,
        float AuthoredFontSize,
        float ScaledAuthoredFontSize,
        Size AuthoredTextSize,
        Size ScaledAuthoredTextSize);

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

    private sealed class RefusingFloorNormalizer : IDocumentNormalizer
    {
        public Task<SourceEnvelope> NormalizeAsync(
            SourceEnvelope source,
            NormalizationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromException<SourceEnvelope>(new IOException(FloorNormalizationRefusal));
        }
    }

    private sealed class DeferredFloorExport
    {
        private readonly Lock _sync = new();
        private TaskCompletionSource<bool>? _completion;
        private int _startedCount;

        public int StartedCount => Volatile.Read(ref _startedCount);

        public bool HasActiveAttempt
        {
            get
            {
                lock (_sync)
                {
                    return _completion is { Task.IsCompleted: false };
                }
            }
        }

        public Task<bool> RunAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TaskCompletionSource<bool> completion;
            lock (_sync)
            {
                if (_completion is { Task.IsCompleted: false })
                {
                    throw new InvalidOperationException("A synthetic floor export attempt is already active.");
                }

                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _completion = completion;
                Interlocked.Increment(ref _startedCount);
            }

            return AwaitCompletion(completion, cancellationToken);
        }

        public void Complete() => CurrentCompletion().TrySetResult(true);

        public void Refuse()
            => CurrentCompletion().TrySetException(new IOException(FloorExportRefusal));

        public bool TrySettleActive()
        {
            lock (_sync)
            {
                return _completion?.TrySetResult(true) == true;
            }
        }

        private static async Task<bool> AwaitCompletion(
            TaskCompletionSource<bool> completion,
            CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
            return await completion.Task.ConfigureAwait(false);
        }

        private TaskCompletionSource<bool> CurrentCompletion()
        {
            lock (_sync)
            {
                return _completion
                    ?? throw new InvalidOperationException("No synthetic floor export attempt is active.");
            }
        }
    }

    private sealed class ReviewedFloorCatalogFile : IDisposable
    {
        private ReviewedFloorCatalogFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public string Sha256
            => Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path)));

        public static ReviewedFloorCatalogFile FromJson(string json)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"honest-ink-synthetic-reviewed-floor-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new ReviewedFloorCatalogFile(path);
        }

        public void Dispose() => File.Delete(Path);
    }

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
