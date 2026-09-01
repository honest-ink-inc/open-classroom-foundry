// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Automation;
using Foundry.App.WinForms;
using Foundry.Application;
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
    private const string ReviewedFixtureMarker = "⟬test-pinned-reviewed-catalog⟭";

    private static readonly Type[] DirectPseudoSurfaceTypes =
    [
        typeof(ReviewForm),
        typeof(CaptureForm),
        typeof(PressRoomForm),
        typeof(AllAboardForm),
        typeof(ModuleStudioForm),
        typeof(LoadedProjectPreflightForm),
        typeof(TileForm),
        typeof(BoardToBriefIntakeForm),
    ];
    private static readonly Type[] SpecializedPseudoSurfaceTypes = [typeof(NodeEditorForm)];

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
    public void Every_catalog_entry_stretches_by_at_least_forty_percent()
        => InPseudo(() =>
        {
            var shortfalls = UiCatalogInventory.NeutralStrings
                .Select(entry =>
                {
                    var pseudo = UiStrings.Localize(entry.Key, entry.Value);
                    return new
                    {
                        Id = entry.Key,
                        NeutralLength = entry.Value.Length,
                        PseudoLength = pseudo.Length,
                        RequiredLength = (int)Math.Ceiling(entry.Value.Length * 1.4),
                    };
                })
                .Where(entry => entry.PseudoLength < entry.RequiredLength)
                .OrderBy(entry => entry.Id, StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                shortfalls.Length == 0,
                $"{shortfalls.Length} pseudo-locale entries were shorter than 140 percent. "
                + string.Join(
                    "; ",
                    shortfalls.Take(20).Select(entry =>
                        $"{entry.Id}={entry.NeutralLength}->{entry.PseudoLength} (needs {entry.RequiredLength})")));
        });

    [Fact]
    public void Every_shipped_form_type_has_a_deliberate_pseudo_locale_scenario()
    {
        var shipped = typeof(PressRoomForm).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(Form).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
        var covered = DirectPseudoSurfaceTypes.Concat(SpecializedPseudoSurfaceTypes)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(shipped, covered);
    }

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
            ExerciseEveryConstructedSurface(AssertSurfaceChromeIsCatalogBacked)));

    [Theory]
    [InlineData("ltr", RightToLeft.No, false)]
    [InlineData("rtl", RightToLeft.Yes, true)]
    public void Synthetic_test_pinned_reviewed_catalog_projects_through_every_shipped_surface_without_granting_seat_review(
        string direction,
        RightToLeft expectedRightToLeft,
        bool expectedRightToLeftLayout)
    {
        using var catalog = CreateSyntheticReviewedFixtureCatalog(direction);
        try
        {
            Assert.Empty(UiCatalogDeployment.ApprovedCatalogSha256);
            UiLocale.ConfigureForTest(
                [UiLocale.CatalogSwitch, catalog.Path],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { catalog.Sha256 });
            Assert.Equal(UiLocaleMode.ReviewedCatalog, UiLocale.Mode);
            Assert.Contains("not protected-seat evidence", UiLocale.ActiveReview?.ReviewerName, StringComparison.Ordinal);

            Sta.Run(() => ExerciseEveryConstructedSurface(form =>
                AssertSurfaceChromeUsesReviewedFixture(
                    form,
                    expectedRightToLeft,
                    expectedRightToLeftLayout)));
        }
        finally
        {
            UiLocale.Set(UiLocaleMode.Neutral);
            Assert.Empty(UiCatalogDeployment.ApprovedCatalogSha256);
        }
    }

    private static void ExerciseEveryConstructedSurface(Action<Form> assertSurface)
    {
        using var review = UiaHarness.CreateReviewForm();
        using var capture = UiaHarness.CreateCaptureForm();
        using var pressRoom = new PressRoomForm(_ => null);
        using var allAboard = new AllAboardForm(new AppServices.NoAssetsCatalog(), _ => null);
        using var modules = new ModuleStudioForm(_ => null);
        using var preflight = new LoadedProjectPreflightForm(SyntheticLoadedProject());
        using var tile = new TileForm();
        using var boardFixture = CreateBoardIntakePseudoFixture();

        foreach (var form in new Form[]
        {
            review,
            capture,
            pressRoom,
            allAboard,
            modules,
            preflight,
            tile,
            boardFixture.Form,
        })
        {
            assertSurface(form);
        }

        ExerciseEveryReviewTab(review, assertSurface);
        ExerciseEveryPress(pressRoom, assertSurface);
        ExerciseEveryAllAboardMode(allAboard, assertSurface);
        ExerciseEveryModuleMode(modules, assertSurface);
        ExerciseEveryNodeEditorVariant(assertSurface);
    }

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

    private static void ExerciseEveryReviewTab(ReviewForm form, Action<Form> assertSurface)
    {
        var tabs = ReviewSurfaceContractTests.Flatten(form).OfType<TabControl>().Single();
        Assert.Equal(3, tabs.TabPages.Count);
        for (var index = 0; index < tabs.TabPages.Count; index++)
        {
            tabs.SelectedIndex = index;
            assertSurface(form);
        }
    }

    private static void ExerciseEveryPress(PressRoomForm form, Action<Form> assertSurface)
    {
        var presses = ReviewSurfaceContractTests.Flatten(form).OfType<ListBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.PressList);
        for (var index = 0; index < presses.Items.Count; index++)
        {
            presses.SelectedIndex = index;
            assertSurface(form);
        }
    }

    private static void ExerciseEveryAllAboardMode(AllAboardForm form, Action<Form> assertSurface)
    {
        var modes = ReviewSurfaceContractTests.Flatten(form).OfType<ComboBox>()
            .Single(control => control.AccessibilityObject.Name == UiStrings.OutputMode);
        for (var index = 0; index < modes.Items.Count; index++)
        {
            modes.SelectedIndex = index;
            assertSurface(form);
        }
    }

    private static void ExerciseEveryModuleMode(ModuleStudioForm form, Action<Form> assertSurface)
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
                assertSurface(form);
            }
        }
    }

    private static void ExerciseEveryNodeEditorVariant(Action<Form> assertSurface)
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
            assertSurface(editor);
            if (node is not VectorGraphic)
            {
                continue;
            }

            var primitives = ReviewSurfaceContractTests.Flatten(editor).OfType<ListBox>()
                .Single(control => control.AccessibilityObject.Name == UiStrings.EditorVectorPrimitives);
            for (var index = 0; index < primitives.Items.Count; index++)
            {
                primitives.SelectedIndex = index;
                assertSurface(editor);
            }
        }
    }

    private static void AssertSurfaceChromeUsesReviewedFixture(
        Form form,
        RightToLeft expectedRightToLeft,
        bool expectedRightToLeftLayout)
    {
        if (!form.Visible)
        {
            form.Show();
        }

        System.Windows.Forms.Application.DoEvents();
        Assert.Equal(expectedRightToLeft, form.RightToLeft);
        Assert.Equal(expectedRightToLeftLayout, form.RightToLeftLayout);

        foreach (var control in ReviewSurfaceContractTests.Flatten(form)
            .Where(control => control.TabStop && control.CanSelect))
        {
            AssertReviewedFixtureMarker(
                control.AccessibilityObject.Name,
                $"{form.GetType().Name} focusable {control.GetType().Name} accessible name");
        }

        AssertReviewedFixtureMarker(form.Text, $"{form.GetType().Name} title");
        foreach (var control in ReviewSurfaceContractTests.Flatten(form))
        {
            if (control is Label or GroupBox or ButtonBase or TabPage
                && !string.IsNullOrWhiteSpace(control.Text))
            {
                AssertReviewedFixtureMarker(
                    control.Text,
                    $"{form.GetType().Name} visible {control.GetType().Name} chrome");
            }

            if (control is DataGridView grid)
            {
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    if (!string.IsNullOrWhiteSpace(column.HeaderText))
                    {
                        AssertReviewedFixtureMarker(
                            column.HeaderText,
                            $"{form.GetType().Name} grid header");
                    }
                }
            }
        }

        foreach (var combo in ReviewSurfaceContractTests.Flatten(form).OfType<ComboBox>())
        {
            foreach (var item in combo.Items.Cast<object>())
            {
                AssertReviewedFixtureMarker(item.ToString(), $"{form.GetType().Name} combo choice");
            }
        }

        foreach (var grid in ReviewSurfaceContractTests.Flatten(form).OfType<DataGridView>())
        {
            foreach (var column in grid.Columns.OfType<DataGridViewComboBoxColumn>())
            {
                foreach (var item in column.Items.Cast<object>())
                {
                    AssertReviewedFixtureMarker(item.ToString(), $"{form.GetType().Name} grid choice");
                }
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
                AssertReviewedFixtureMarker(item.ToString(), $"{form.GetType().Name} catalog list item");
            }
        }
    }

    private static void AssertReviewedFixtureMarker(string? text, string context)
    {
        Assert.False(string.IsNullOrWhiteSpace(text), $"{context} was blank.");
        Assert.Contains(ReviewedFixtureMarker, text, StringComparison.Ordinal);
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

        foreach (var grid in ReviewSurfaceContractTests.Flatten(form).OfType<DataGridView>())
        {
            foreach (var column in grid.Columns.OfType<DataGridViewComboBoxColumn>())
            {
                foreach (var item in column.Items.Cast<object>())
                {
                    var text = item.ToString();
                    Assert.False(string.IsNullOrWhiteSpace(text));
                    Assert.Contains("⟦", text, StringComparison.Ordinal);
                }
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

    private static BoardIntakePseudoFixture CreateBoardIntakePseudoFixture()
    {
        var store = new InMemorySessionByteStore();
        var session = new CaptureSession(
            new ByteImportCaptureSource(store),
            new MetadataOnlyNormalizer(),
            store);
        session.CaptureAsync(
                new CaptureRequest(ByteImportCaptureSource.Kind, "image/png", TinyPng()),
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
            new PseudoOcrService(),
            DistrictPolicy.Offline,
            captureRunner: _ => DialogResult.OK,
            noticePresenter: (_, _, _) => { });
        return new BoardIntakePseudoFixture(form, session);
    }

    private static SyntheticReviewedCatalogFile CreateSyntheticReviewedFixtureCatalog(string direction)
    {
        var root = JsonNode.Parse(UiCatalogInventory.CreateTemplateJson())!.AsObject();
        root["languageTag"] = "en-US";
        root["direction"] = direction;
        var review = root["review"]!.AsObject();
        review["status"] = UiCatalogInventory.ReviewedStatus;
        review["reviewerName"] = "Synthetic automated fixture — not protected-seat evidence";
        review["reviewerRole"] = UiCatalogInventory.RequiredReviewerRole;
        review["reviewedAtUtc"] = "2026-08-31T12:00:00Z";
        var provenance = root["provenance"]!.AsObject();
        provenance["catalogId"] = "synthetic-full-surface-projection";
        provenance["creator"] = "Automated test fixture";
        provenance["source"] = "Generated in memory for this test only";
        provenance["license"] = "GPL-3.0-or-later test fixture";
        provenance["modificationHistory"] = new JsonArray("Appended a diagnostic marker to neutral fixture text");

        var strings = root["strings"]!.AsObject();
        foreach (var pair in UiCatalogInventory.NeutralStrings)
        {
            strings[pair.Key] = $"{pair.Value} {ReviewedFixtureMarker}";
        }

        return SyntheticReviewedCatalogFile.FromJson(
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static byte[] TinyPng()
    {
        using var bitmap = new Bitmap(4, 4);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
        }

        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return output.ToArray();
    }

    private sealed record BoardIntakePseudoFixture(
        BoardToBriefIntakeForm Form,
        CaptureSession Session) : IDisposable
    {
        public void Dispose() => Form.Dispose();
    }

    private sealed class MetadataOnlyNormalizer : IDocumentNormalizer
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

    private sealed class PseudoOcrService : IOcrService
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

    private sealed class SyntheticReviewedCatalogFile : IDisposable
    {
        private SyntheticReviewedCatalogFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public string Sha256
            => Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path)));

        public static SyntheticReviewedCatalogFile FromJson(string json)
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"honest-ink-synthetic-reviewed-projection-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new SyntheticReviewedCatalogFile(path);
        }

        public void Dispose() => File.Delete(Path);
    }

    [HeadedFact]
    public void The_pseudo_locale_switch_reaches_the_real_chrome_end_to_end()
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "Foundry.App.WinForms.exe");
        using var process = Process.Start(new ProcessStartInfo(exe, $"{UiaHarness.Switch} review {UiLocale.PseudoSwitch}"))!;
        Exception? bodyFailure = null;
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
        catch (Exception failure)
        {
            bodyFailure = failure;
        }

        Exception? cleanupFailure = null;
        try
        {
            HeadedProcessLifetime.TerminateAndWait(process);
        }
        catch (Exception failure)
        {
            cleanupFailure = failure;
        }

        if (bodyFailure is not null && cleanupFailure is not null)
        {
            throw new AggregateException(
                "The headed pseudo-locale assertion and its child-process cleanup both failed.",
                bodyFailure,
                cleanupFailure);
        }

        if (bodyFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(bodyFailure).Throw();
        }

        if (cleanupFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        }
    }
}
