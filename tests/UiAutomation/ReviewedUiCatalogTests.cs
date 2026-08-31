// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using System.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Foundry.App.WinForms;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.UiAutomation;

public class ReviewedUiCatalogTests
{
    [Fact]
    public void Inventory_covers_every_static_property_and_every_dynamic_press_and_module_label()
    {
        var inventory = UiCatalogInventory.NeutralStrings;
        var staticProperties = typeof(UiStrings).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0)
            .ToList();

        Assert.All(staticProperties, property =>
            Assert.True(inventory.ContainsKey(UiCatalogIds.Chrome(property.Name)), property.Name));

        foreach (var press in PressRoomCatalog.All)
        {
            Assert.Equal(press.Title, inventory[UiCatalogIds.PressTitle(press.Id)]);
            foreach (var parameter in press.Parameters)
            {
                Assert.Equal(parameter.Label, inventory[UiCatalogIds.PressParameter(press.Id, parameter.Key)]);
                if (parameter is ChoiceParameter choice)
                {
                    for (var index = 0; index < choice.Options.Count; index++)
                    {
                        Assert.Equal(choice.Options[index], inventory[UiCatalogIds.PressChoice(press.Id, parameter.Key, choice.Options[index])]);
                    }
                }
            }
        }

        foreach (var display in ModuleDisplays())
        {
            Assert.Equal(display.Fallback, inventory[display.LocalizationId]);
        }

        Assert.Equal(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [UiCatalogIds.AllAboardFirstCard] = "First",
                [UiCatalogIds.AllAboardThenCard] = "Then",
                [UiCatalogIds.AllAboardNowCard] = "Now",
                [UiCatalogIds.AllAboardNextCard] = "Next",
                [UiCatalogIds.AllAboardDoneCard] = "Done",
            },
            inventory.Where(pair => pair.Key.StartsWith("all-aboard.card.", StringComparison.Ordinal))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

        Assert.DoesNotContain(inventory, pair => string.Equals(pair.Value, ProductIdentity.PublicName, StringComparison.Ordinal));
        foreach (var identity in ModulePublicIdentity.All)
        {
            Assert.Contains(inventory.Values, value =>
                value.Contains(identity.DisplayName, StringComparison.Ordinal));
        }

        foreach (var retiredName in new[]
        {
            "All Aboard",
            "Lesson Loom",
            "Talk Moves Studio",
            "Exit Lens",
            "Source Lens",
            "Family Bridge",
            "TaskDock",
        })
        {
            Assert.DoesNotContain(inventory.Values, value =>
                value.Contains(retiredName, StringComparison.Ordinal));
        }

        Assert.Equal(inventory.Count, inventory.Keys.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(inventory.Keys.OrderBy(id => id, StringComparer.Ordinal), inventory.Keys);
    }

    [Fact]
    public void Neutral_review_packet_is_deterministic_complete_and_cannot_activate_as_a_draft()
    {
        var first = UiCatalogInventory.CreateTemplateJson();
        var second = UiCatalogInventory.CreateTemplateJson();
        Assert.Equal(first, second);
        Assert.Equal(SHA256Hex(first), SHA256Hex(second));

        var root = JsonNode.Parse(first)!.AsObject();
        Assert.Equal(UiCatalogInventory.SchemaVersion, (int)root["schemaVersion"]!);
        Assert.Equal(UiCatalogInventory.DraftStatus, (string)root["review"]!["status"]!);
        Assert.Equal(UiCatalogInventory.SourceDigestSha256, (string)root["review"]!["sourceDigestSha256"]!);
        Assert.Equal(UiCatalogInventory.NeutralStrings.Count, root["neutralStrings"]!.AsObject().Count);
        Assert.Equal(UiCatalogInventory.NeutralStrings.Count, root["strings"]!.AsObject().Count);

        using var file = CatalogFile.FromJson(first);
        var refusal = Assert.Throws<InvalidDataException>(() => UiCatalogLoader.LoadReviewed(file.Path));
        Assert.Contains("draft", refusal.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UiLocaleMode.Neutral, UiLocale.Mode);
    }

    [Fact]
    public void Complete_review_assertion_still_needs_an_exact_build_pin_before_it_can_activate()
    {
        using var file = ReviewedCatalog(root =>
        {
            var strings = root["strings"]!.AsObject();
            strings[UiCatalogIds.Chrome(nameof(UiStrings.PressList))] = "[reviewed chrome]";
            strings[UiCatalogIds.PressTitle("calibration-proof")] = "[reviewed press title]";
            strings[ModuleStudioCatalog.ById("board-to-brief").Display.LocalizationId] = "[reviewed module door]";
        }, direction: "rtl");

        try
        {
            Assert.Empty(UiCatalogDeployment.ApprovedCatalogSha256);
            var unpinned = Assert.Throws<InvalidDataException>(() =>
                UiLocale.Configure([UiLocale.CatalogSwitch, file.Path]));
            Assert.Contains("not approved", unpinned.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("cannot grant", unpinned.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(file.Path, unpinned.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(UiLocaleMode.Neutral, UiLocale.Mode);

            ConfigureApprovedForTest([file], [UiLocale.CatalogSwitch, file.Path]);

            Assert.Equal(UiLocaleMode.ReviewedCatalog, UiLocale.Mode);
            Assert.Equal("en-US", UiLocale.LanguageTag);
            Assert.Equal(UiTextDirection.RightToLeft, UiLocale.TextDirection);
            Assert.Equal("Synthetic catalog reviewer", UiLocale.ActiveReview?.ReviewerName);
            Assert.Equal("synthetic-test-catalog", UiLocale.ActiveCatalogProvenance?.CatalogId);
            Assert.Equal("[reviewed chrome]", UiStrings.PressList);
            Assert.Equal("[reviewed press title]", UiStrings.Localize(
                UiCatalogIds.PressTitle("calibration-proof"),
                PressRoomCatalog.ById("calibration-proof").Title));
            Assert.Equal("[reviewed module door]", UiStrings.Localize(
                ModuleStudioCatalog.ById("board-to-brief").Display.LocalizationId,
                ModuleStudioCatalog.ById("board-to-brief").Display.Fallback));
            Assert.Equal("exact neutral fallback", UiStrings.Localize("future.unknown.id", "exact neutral fallback"));
            Assert.StartsWith(ProductIdentity.PublicName, UiStrings.MainWindowTitle, StringComparison.Ordinal);

            Sta.Run(() =>
            {
                using var form = new Form();
                UiLocale.ApplyChrome(form);
                Assert.Equal(RightToLeft.Yes, form.RightToLeft);
                Assert.True(form.RightToLeftLayout);
            });
        }
        finally
        {
            UiLocale.Set(UiLocaleMode.Neutral);
        }
    }

    [Fact]
    public void Cli_catalog_path_precedes_the_environment_path()
    {
        using var environmentCatalog = ReviewedCatalog(root =>
            root["strings"]![UiCatalogIds.Chrome(nameof(UiStrings.PressList))] = "[environment catalog]");
        using var cliCatalog = ReviewedCatalog(root =>
            root["strings"]![UiCatalogIds.Chrome(nameof(UiStrings.PressList))] = "[command line catalog]");
        var previous = Environment.GetEnvironmentVariable(UiLocale.CatalogEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(UiLocale.CatalogEnvironmentVariable, environmentCatalog.Path);
            var approved = ApprovedHashes(environmentCatalog, cliCatalog);
            UiLocale.ConfigureForTest([UiLocale.CatalogSwitch, cliCatalog.Path], approved);
            Assert.Equal("[command line catalog]", UiStrings.PressList);

            UiLocale.ConfigureForTest([], approved);
            Assert.Equal("[environment catalog]", UiStrings.PressList);
        }
        finally
        {
            Environment.SetEnvironmentVariable(UiLocale.CatalogEnvironmentVariable, previous);
            UiLocale.Set(UiLocaleMode.Neutral);
        }
    }

    [Fact]
    public void Catalog_refuses_missing_unknown_blank_and_changed_neutral_entries()
    {
        var id = UiCatalogIds.Chrome(nameof(UiStrings.PressList));
        AssertCatalogRefused(root => root["strings"]!.AsObject().Remove(id), id);
        AssertCatalogRefused(root => root["strings"]!["unknown.string.id"] = "value", "unknown.string.id");
        AssertCatalogRefused(root => root["strings"]![id] = " ", id);
        AssertCatalogRefused(root => root["neutralStrings"]![id] = "changed", id);
    }

    [Fact]
    public void Catalog_refuses_placeholder_and_mnemonic_contract_changes()
    {
        var placeholderId = UiCatalogIds.Chrome(nameof(UiStrings.StatusSaved));
        var mnemonicId = UiCatalogIds.Chrome(nameof(UiStrings.ApplyEdit));
        AssertCatalogRefused(root => root["strings"]![placeholderId] = "Saved without its value.", placeholderId);
        AssertCatalogRefused(root => root["strings"]![placeholderId] = "Saved {0,not-an-alignment}.", placeholderId);
        AssertCatalogRefused(root => root["strings"]![placeholderId] = "Saved {0}.\n", placeholderId);
        AssertCatalogRefused(root => root["strings"]![mnemonicId] = "Apply edit", mnemonicId);
        AssertCatalogRefused(root => root["strings"]![mnemonicId] = "Apply edit&", mnemonicId);
        AssertCatalogRefused(root => root["strings"]![mnemonicId] = "Apply & edit", mnemonicId);
        AssertCatalogRefused(root => root["strings"]![mnemonicId] = "Apply &…edit", mnemonicId);
        AssertCatalogRefused(root => root["strings"]![mnemonicId] = "Apply &𐐀edit", mnemonicId);
    }

    [Fact]
    public void Access_key_contexts_cover_every_mnemonic_bearing_static_chrome_id()
    {
        var expected = UiCatalogInventory.NeutralStrings
            .Where(pair => pair.Key.StartsWith("chrome.", StringComparison.Ordinal)
                && MnemonicKeys(pair.Value).Count > 0)
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var declared = UiCatalogAccessKeyContexts.All
            .SelectMany(context => context.LocalizationIds)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, declared);
        Assert.All(UiCatalogAccessKeyContexts.All, context =>
        {
            Assert.Equal(
                context.LocalizationIds.Count,
                context.LocalizationIds.Distinct(StringComparer.Ordinal).Count());
            Assert.All(context.LocalizationIds, localizationId =>
                Assert.Single(MnemonicKeys(UiCatalogInventory.NeutralStrings[localizationId])));
        });
    }

    [Theory]
    [InlineData(
        nameof(UiStrings.ReviewAndApprove),
        "Review and a&pprove…",
        nameof(UiStrings.PrintButton),
        "press-room")]
    [InlineData(
        nameof(UiStrings.ReviewElementsTab),
        "&Elements and issues",
        nameof(UiStrings.EditElement),
        "review")]
    [InlineData(
        nameof(UiStrings.AddItem),
        "A&dd item",
        nameof(UiStrings.DiscardReplacement),
        "node-editor-sequence")]
    public void Catalog_refuses_duplicate_access_keys_within_one_simultaneously_visible_context(
        string changedMember,
        string changedTranslation,
        string collidingMember,
        string contextName)
    {
        var changedId = UiCatalogIds.Chrome(changedMember);
        var collidingId = UiCatalogIds.Chrome(collidingMember);
        using var file = ReviewedCatalog(root => root["strings"]![changedId] = changedTranslation);

        var refusal = Assert.Throws<InvalidDataException>(() => UiCatalogLoader.LoadReviewed(file.Path));

        Assert.Contains(contextName, refusal.Message, StringComparison.Ordinal);
        Assert.Contains(changedId, refusal.Message, StringComparison.Ordinal);
        Assert.Contains(collidingId, refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_mirrors_WinForms_current_culture_for_dotted_I_access_key_collisions()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var culture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            var changedId = UiCatalogIds.Chrome(nameof(UiStrings.EditElement));
            var collidingId = UiCatalogIds.Chrome(nameof(UiStrings.ReviewElementsTab));
            const string ChangedTranslation = "Edit element with &İ…";
            using var file = ReviewedCatalog(root =>
                root["strings"]![changedId] = ChangedTranslation);

            var nativeCollision = Control.IsMnemonic('İ', UiStrings.ReviewElementsTab)
                || Control.IsMnemonic('i', ChangedTranslation);
            var outcome = Record.Exception(() => UiCatalogLoader.LoadReviewed(file.Path));

            if (nativeCollision)
            {
                var refusal = Assert.IsType<InvalidDataException>(outcome);
                Assert.Contains("review", refusal.Message, StringComparison.Ordinal);
                Assert.Contains(changedId, refusal.Message, StringComparison.Ordinal);
                Assert.Contains(collidingId, refusal.Message, StringComparison.Ordinal);
            }
            else
            {
                // Globalization-invariant runtimes intentionally give
                // Control.IsMnemonic invariant semantics; the loader must
                // mirror that same result rather than hard-code en-US data.
                Assert.Null(outcome);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Fact]
    public void Catalog_allows_access_key_reuse_across_separate_forms_and_mutually_exclusive_editor_variants()
    {
        var tileId = UiCatalogIds.Chrome(nameof(UiStrings.TileMake));
        var importId = UiCatalogIds.Chrome(nameof(UiStrings.ImportImage));
        var sequenceId = UiCatalogIds.Chrome(nameof(UiStrings.AddItem));
        var tableId = UiCatalogIds.Chrome(nameof(UiStrings.AddTableRow));
        using var file = ReviewedCatalog(root =>
        {
            root["strings"]![tileId] = "Make tiles with &Z";
            root["strings"]![importId] = "Import image with &Z…";
            root["strings"]![sequenceId] = "Add item with &Q";
            root["strings"]![tableId] = "Add row with &Q";
        });

        var catalog = UiCatalogLoader.LoadReviewed(file.Path);

        Assert.Equal("Make tiles with &Z", catalog.Translate(tileId, UiStrings.TileMake));
        Assert.Equal("Import image with &Z…", catalog.Translate(importId, UiStrings.ImportImage));
        Assert.Equal("Add item with &Q", catalog.Translate(sequenceId, UiStrings.AddItem));
        Assert.Equal("Add row with &Q", catalog.Translate(tableId, UiStrings.AddTableRow));
    }

    [Fact]
    public void Catalog_accepts_a_unicode_mnemonic_and_an_escaped_ampersand()
    {
        var id = UiCatalogIds.Chrome(nameof(UiStrings.ApplyEdit));
        using var file = ReviewedCatalog(root =>
            root["strings"]![id] = "&تطبيق && تأكيد");

        var catalog = UiCatalogLoader.LoadReviewed(file.Path);

        Assert.Equal("&تطبيق && تأكيد", catalog.Translate(id, UiStrings.ApplyEdit));
    }

    [Fact]
    public void Escaped_ampersands_survive_as_literals_in_review_accessible_names_and_window_titles()
    {
        using var file = ReviewedCatalog(root =>
        {
            root["strings"]![UiCatalogIds.Chrome(nameof(UiStrings.ReviewElementsTab))] = "A&&B &C";
            root["strings"]![UiCatalogIds.Chrome(nameof(UiStrings.TileForWall))] = "A&&B &C…";
            root["strings"]![UiCatalogIds.Chrome(nameof(UiStrings.ReviewWindowTitle))] = "reviewing an A&&B draft";
            root["strings"]![UiCatalogIds.Chrome(nameof(UiStrings.UnapprovedPreviewProfile))] =
                "A&&B profile: {0}; {1}; {2}; {3}";
        });

        try
        {
            ConfigureApprovedForTest([file], [UiLocale.CatalogSwitch, file.Path]);
            Sta.Run(() =>
            {
                using var review = new ReviewForm(ReviewSurfaceContractTests.SessionOver(
                    new Heading(1, "Synthetic review")));
                var elements = review.Controls.OfType<TabControl>().Single().TabPages[0];
                using var tile = new TileForm();

                Assert.Equal("A&&B &C", elements.Text);
                Assert.Equal("A&B C", elements.AccessibilityObject.Name);
                Assert.Contains("A&B draft", review.Text, StringComparison.Ordinal);
                Assert.DoesNotContain("A&&B", review.Text, StringComparison.Ordinal);
                var profile = ReviewSurfaceContractTests.Flatten(review)
                    .OfType<Label>()
                    .Single(label => label.Text.StartsWith("A&B profile:", StringComparison.Ordinal));
                Assert.DoesNotContain("A&&B", profile.Text, StringComparison.Ordinal);
                Assert.Equal(profile.Text, profile.AccessibilityObject.Name);
                Assert.Equal("A&B C", tile.Text);
            });
        }
        finally
        {
            UiLocale.Set(UiLocaleMode.Neutral);
        }
    }

    [Fact]
    public void Dynamic_press_and_all_aboard_labels_keep_literal_ampersands_without_disabling_static_access_keys()
    {
        const string NumberLabel = "Radius &size";
        const string ToggleLabel = "Numbers &labels";
        const string CardName = "First &follow";
        const string ExpectedReady = "Ready & waiting";
        const string ExpectedCardLabel = "Card & text: First &follow";
        var press = PressRoomCatalog.ById("clock-face");
        using var file = ReviewedCatalog(root =>
        {
            root["strings"]![UiCatalogIds.PressParameter(press.Id, "radius")] = NumberLabel;
            root["strings"]![UiCatalogIds.PressParameter(press.Id, "numerals")] = ToggleLabel;
            root["strings"]![UiCatalogIds.AllAboardFirstCard] = CardName;
            root["strings"]![UiCatalogIds.Chrome(nameof(UiStrings.StatusReady))] = "Ready && waiting";
            root["strings"]![UiCatalogIds.Chrome(nameof(UiStrings.CardTextLabel))] = "Card && text: {0}";
        });

        try
        {
            ConfigureApprovedForTest([file], [UiLocale.CatalogSwitch, file.Path]);
            Sta.Run(() =>
            {
                using var pressRoom = new PressRoomForm(_ => null);
                var pressList = ReviewSurfaceContractTests.Flatten(pressRoom)
                    .OfType<ListBox>()
                    .Single(list => string.Equals(list.AccessibleName, UiStrings.PressList, StringComparison.Ordinal));
                pressList.SelectedIndex = PressRoomCatalog.All.ToList().IndexOf(press);

                var numberLabel = ReviewSurfaceContractTests.Flatten(pressRoom)
                    .OfType<Label>()
                    .Single(label => string.Equals(label.Text, NumberLabel, StringComparison.Ordinal));
                var toggle = ReviewSurfaceContractTests.Flatten(pressRoom)
                    .OfType<CheckBox>()
                    .Single(check => string.Equals(check.Text, ToggleLabel, StringComparison.Ordinal));
                Assert.False(numberLabel.UseMnemonic);
                Assert.False(toggle.UseMnemonic);
                Assert.Equal(ToggleLabel, toggle.AccessibilityObject.Name);

                var pressReview = ReviewSurfaceContractTests.Flatten(pressRoom)
                    .OfType<Button>()
                    .Single(button => string.Equals(button.Text, UiStrings.ReviewAndApprove, StringComparison.Ordinal));
                Assert.True(pressReview.UseMnemonic);
                Assert.True(Control.IsMnemonic('r', pressReview.Text));
                var pressStatus = ReviewSurfaceContractTests.Flatten(pressRoom)
                    .OfType<Label>()
                    .Single(label => label.Dock == DockStyle.Bottom);
                Assert.False(pressStatus.UseMnemonic);
                Assert.Equal(ExpectedReady, pressStatus.Text);
                Assert.Equal(pressStatus.Text, pressStatus.AccessibilityObject.Name);

                using var allAboard = new AllAboardForm(new LiteralAmpersandAssetCatalog(), _ => null);
                var mode = ReviewSurfaceContractTests.Flatten(allAboard)
                    .OfType<ComboBox>()
                    .Single(combo => string.Equals(combo.AccessibleName, UiStrings.OutputMode, StringComparison.Ordinal));
                mode.SelectedIndex = 1;
                var cardLabel = ReviewSurfaceContractTests.Flatten(allAboard)
                    .OfType<Label>()
                    .Single(label => string.Equals(
                        label.Text,
                        ExpectedCardLabel,
                        StringComparison.Ordinal));
                Assert.False(cardLabel.UseMnemonic);

                mode.SelectedIndex = 3;
                var agencyMeaning = ReviewSurfaceContractTests.Flatten(allAboard)
                    .OfType<CheckBox>()
                    .Single(check => string.Equals(
                        check.Text,
                        LiteralAmpersandAssetCatalog.Meaning,
                        StringComparison.Ordinal));
                Assert.False(agencyMeaning.UseMnemonic);
                Assert.Equal(LiteralAmpersandAssetCatalog.Meaning, agencyMeaning.AccessibilityObject.Name);
                var allAboardStatus = ReviewSurfaceContractTests.Flatten(allAboard)
                    .OfType<Label>()
                    .Single(label => label.Dock == DockStyle.Bottom);
                Assert.False(allAboardStatus.UseMnemonic);
                Assert.Equal(ExpectedReady, allAboardStatus.Text);
                Assert.Equal(allAboardStatus.Text, allAboardStatus.AccessibilityObject.Name);
            });
        }
        finally
        {
            UiLocale.Set(UiLocaleMode.Neutral);
        }
    }

    [Fact]
    public void Dynamic_module_labels_escape_group_prefixes_and_disable_mnemonics_on_text_controls()
    {
        const string ToggleLabel = "Transcript &verified";
        const string NoticeLabel = "Layout &text only";
        const string UnavailableReason = "Authority &review required";
        var sourceDoor = ModuleStudioCatalog.ById("source-lens");
        var sourceToggle = Assert.Single(sourceDoor.Modes)
            .Fields.Single(field => field.Key == "transcript-verified");
        var accessDoor = ModuleStudioCatalog.ById("access-remix");
        var accessNotice = Assert.Single(accessDoor.Modes)
            .Fields.Single(field => field.Key == "layout-only");
        var accessReason = Assert.IsType<ModuleDisplayText>(Assert.Single(accessDoor.Modes).UnavailableReason);
        using var file = ReviewedCatalog(root =>
        {
            root["strings"]![sourceToggle.Display.LocalizationId] = ToggleLabel;
            root["strings"]![accessNotice.Display.LocalizationId] = NoticeLabel;
            root["strings"]![accessReason.LocalizationId] = UnavailableReason;
            root["strings"]![UiCatalogIds.Chrome(nameof(UiStrings.StatusModuleUnavailable))] =
                "Unavailable && held: {0}";
            root["strings"]![UiCatalogIds.Chrome(nameof(UiStrings.PendingReplacementMustBeAppliedOrDiscarded))] =
                "Apply && hold this replacement.";
            root["strings"]![UiCatalogIds.Chrome(nameof(UiStrings.NodeEditorInvalidNumber))] =
                "Invalid && value for {0}.";
            root["strings"]![UiCatalogIds.Chrome(nameof(UiStrings.EditorVectorWidthMm))] =
                "Vector && width";
        });

        try
        {
            ConfigureApprovedForTest([file], [UiLocale.CatalogSwitch, file.Path]);
            Sta.Run(() =>
            {
                using var form = new ModuleStudioForm(_ => null);
                var doors = ReviewSurfaceContractTests.Flatten(form)
                    .OfType<ListBox>()
                    .Single(list => string.Equals(list.AccessibleName, UiStrings.ModuleDoors, StringComparison.Ordinal));

                doors.SelectedIndex = ModuleStudioCatalog.All.ToList().IndexOf(sourceDoor);
                var toggle = ReviewSurfaceContractTests.Flatten(form)
                    .OfType<CheckBox>()
                    .Single(check => string.Equals(check.Text, ToggleLabel, StringComparison.Ordinal));
                var toggleGroup = ReviewSurfaceContractTests.Flatten(form)
                    .OfType<GroupBox>()
                    .Single(group => string.Equals(group.AccessibleName, ToggleLabel, StringComparison.Ordinal));
                Assert.False(toggle.UseMnemonic);
                Assert.Equal(ToggleLabel, toggle.AccessibilityObject.Name);
                Assert.Equal("Transcript &&verified", toggleGroup.Text);
                Assert.False(Control.IsMnemonic('v', toggleGroup.Text));

                doors.SelectedIndex = ModuleStudioCatalog.All.ToList().IndexOf(accessDoor);
                var notice = ReviewSurfaceContractTests.Flatten(form)
                    .OfType<Label>()
                    .Single(label => string.Equals(label.Text, NoticeLabel, StringComparison.Ordinal));
                var noticeGroup = ReviewSurfaceContractTests.Flatten(form)
                    .OfType<GroupBox>()
                    .Single(group => string.Equals(group.AccessibleName, NoticeLabel, StringComparison.Ordinal));
                Assert.False(notice.UseMnemonic);
                Assert.Equal("Layout &&text only", noticeGroup.Text);
                Assert.False(Control.IsMnemonic('t', noticeGroup.Text));

                var status = ReviewSurfaceContractTests.Flatten(form)
                    .OfType<Label>()
                    .Single(label => string.Equals(
                        label.AccessibleName,
                        "Unavailable & held: Authority &review required",
                        StringComparison.Ordinal));
                Assert.False(status.UseMnemonic);
                Assert.Contains("&review", status.Text, StringComparison.Ordinal);
                Assert.DoesNotContain("&&", status.AccessibleName, StringComparison.Ordinal);
                Assert.Equal(status.Text, status.AccessibleName);

                using var editor = new NodeEditorForm(new VectorGraphic(
                    10,
                    10,
                    [new LineSeg(0, 0, 1, 1)],
                    "Synthetic & vector"));
                editor.Show();
                var editorStatus = ReviewSurfaceContractTests.Flatten(editor)
                    .OfType<Label>()
                    .Single(label => label.AccessibilityObject.Role == AccessibleRole.StatusBar);
                Assert.True(string.IsNullOrEmpty(editorStatus.Text));
                Assert.True(string.IsNullOrEmpty(editorStatus.AccessibilityObject.Name));

                var width = ReviewSurfaceContractTests.Flatten(editor)
                    .OfType<TextBox>()
                    .Single(text => string.Equals(
                        text.AccessibleName,
                        "Vector & width",
                        StringComparison.Ordinal));
                width.Text = "not-a-number";
                Assert.Equal("Apply & hold this replacement.", editorStatus.Text);
                Assert.Equal(editorStatus.Text, editorStatus.AccessibilityObject.Name);

                var apply = ReviewSurfaceContractTests.Flatten(editor)
                    .OfType<Button>()
                    .Single(button => string.Equals(
                        button.AccessibilityObject.Name,
                        UiStrings.WithoutMnemonic(UiStrings.ApplyReplacement),
                        StringComparison.Ordinal));
                apply.PerformClick();
                Assert.Equal("Invalid & value for Vector & width.", editorStatus.Text);
                Assert.Equal(editorStatus.Text, editorStatus.AccessibilityObject.Name);
            });
        }
        finally
        {
            UiLocale.Set(UiLocaleMode.Neutral);
        }
    }

    [Fact]
    public void Catalog_refuses_noncanonical_language_bad_direction_stale_digest_and_unknown_schema_property()
    {
        AssertCatalogRefused(root => root["languageTag"] = "EN-us", "EN-us");
        AssertCatalogRefused(root => root["direction"] = "auto", "auto");
        AssertCatalogRefused(root => root["review"]!["sourceDigestSha256"] = new string('0', 64), "0000");
        AssertCatalogRefused(root => root["surprise"] = true, "surprise");
    }

    [Fact]
    public void Catalog_refuses_incomplete_review_assertions()
    {
        AssertCatalogRefused(root => root["review"]!["reviewerName"] = "", "reviewer");
        AssertCatalogRefused(root => root["review"]!["reviewerRole"] = "developer", "developer");
        AssertCatalogRefused(root => root["review"]!["reviewedAtUtc"] = "2026-08-30", "2026-08-30");
    }

    [Fact]
    public void Catalog_refuses_missing_translation_provenance()
    {
        AssertCatalogRefused(root => root["provenance"]!["creator"] = "", "creator");
        AssertCatalogRefused(root => root["provenance"]!["modificationHistory"] = new JsonArray(), "modificationHistory");
    }

    [Fact]
    public void Catalog_refuses_duplicate_properties_comments_and_trailing_commas_as_non_strict_json()
    {
        var template = UiCatalogInventory.CreateTemplateJson();
        var duplicate = template.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"schemaVersion\": 1,",
            StringComparison.Ordinal);
        AssertRawCatalogRefused(duplicate, "repeats");

        var commented = template.Insert(template.IndexOf('{') + 1, "\n  // comment");
        AssertRawCatalogRefused(commented, "strict JSON");

        var trailing = template.Insert(template.LastIndexOf('}'), ",");
        AssertRawCatalogRefused(trailing, "strict JSON");
    }

    [Theory]
    [InlineData("\\uD800")]
    [InlineData("\\uDC00")]
    public void Catalog_refuses_lone_utf16_surrogates_as_controlled_invalid_data(string escapedSurrogate)
    {
        using var reviewed = ReviewedCatalog();
        var template = File.ReadAllText(reviewed.Path);
        var stringsStart = template.IndexOf("\"strings\": {", StringComparison.Ordinal);
        var propertyMarker = $"\"{UiCatalogIds.Chrome(nameof(UiStrings.PressList))}\": ";
        var valueStart = template.IndexOf(propertyMarker, stringsStart, StringComparison.Ordinal)
            + propertyMarker.Length;
        var valueEnd = template.IndexOf(',', valueStart);
        Assert.True(stringsStart >= 0 && valueStart >= propertyMarker.Length && valueEnd > valueStart);
        var hostile = template[..valueStart] + $"\"{escapedSurrogate}\"" + template[valueEnd..];

        AssertRawCatalogRefused(hostile, "malformed Unicode");
    }

    [Fact]
    public void Translated_press_choice_label_never_changes_its_submitted_value()
    {
        var definition = PressRoomCatalog.ById("coordinate-grid");
        var quadrants = Assert.IsType<ChoiceParameter>(definition.Parameters.Single(parameter => parameter.Key == "quadrants"));
        var optionIndex = quadrants.Options.ToList().IndexOf(quadrants.Default);
        using var file = ReviewedCatalog(root =>
            root["strings"]![UiCatalogIds.PressChoice(definition.Id, quadrants.Key, quadrants.Options[optionIndex])] = "[localized choice label]");

        try
        {
            ConfigureApprovedForTest([file], [UiLocale.CatalogSwitch, file.Path]);
            Sta.Run(() =>
            {
                using var form = new PressRoomForm(_ => null);
                var pressList = Assert.Single(ReviewSurfaceContractTests.Flatten(form).OfType<ListBox>());
                pressList.SelectedIndex = PressRoomCatalog.All.ToList().IndexOf(definition);

                var translatedChoice = ReviewSurfaceContractTests.Flatten(form)
                    .OfType<ComboBox>()
                    .Single(combo => combo.Items.Cast<object>().Any(item =>
                        string.Equals(item.ToString(), "[localized choice label]", StringComparison.Ordinal)));
                Assert.Equal("[localized choice label]", translatedChoice.SelectedItem?.ToString());

                var readers = Assert.IsType<Dictionary<string, Func<string>>>(typeof(PressRoomForm)
                    .GetField("_valueReaders", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(form));
                Assert.Equal(quadrants.Default, readers[quadrants.Key]());
            });
        }
        finally
        {
            UiLocale.Set(UiLocaleMode.Neutral);
        }
    }

    [Fact]
    public void Translated_module_choice_label_never_changes_its_submitted_value()
    {
        var door = ModuleStudioCatalog.ById("access-remix");
        var mode = Assert.Single(door.Modes);
        var operation = mode.Fields.Single(field => field.Key == "operation");
        var chunk = operation.Choices.Single(choice => choice.Value == ModuleStudioCatalog.AccessOperationChunk);
        using var file = ReviewedCatalog(root =>
            root["strings"]![chunk.Display.LocalizationId] = "[localized module choice label]");

        try
        {
            ConfigureApprovedForTest([file], [UiLocale.CatalogSwitch, file.Path]);
            Sta.Run(() =>
            {
                using var form = new ModuleStudioForm(_ => null);
                var doorList = Assert.Single(ReviewSurfaceContractTests.Flatten(form)
                    .OfType<ListBox>()
, list => string.Equals(list.AccessibleName, UiStrings.ModuleDoors, StringComparison.Ordinal));
                doorList.SelectedIndex = ModuleStudioCatalog.All.ToList().IndexOf(door);

                var translatedChoice = ReviewSurfaceContractTests.Flatten(form)
                    .OfType<ComboBox>()
                    .Single(combo => combo.Items.Cast<object>().Any(item =>
                        string.Equals(item.ToString(), "[localized module choice label]", StringComparison.Ordinal)));
                Assert.Equal("[localized module choice label]", translatedChoice.SelectedItem?.ToString());

                var readers = Assert.IsType<Dictionary<string, Func<object?>>>(typeof(ModuleStudioForm)
                    .GetField("_valueReaders", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(form));
                Assert.Equal(ModuleStudioCatalog.AccessOperationChunk, readers[operation.Key]());
            });
        }
        finally
        {
            UiLocale.Set(UiLocaleMode.Neutral);
        }
    }

    [Fact]
    public void Pseudo_locale_still_transforms_stable_id_lookups_and_refuses_a_catalog_selector_collision()
    {
        try
        {
            UiLocale.Configure([UiLocale.PseudoSwitch]);
            Assert.StartsWith("⟦", UiStrings.Localize(UiCatalogIds.AllAboardFirstCard, "First"), StringComparison.Ordinal);

            using var file = ReviewedCatalog();
            var refusal = Assert.Throws<InvalidDataException>(() =>
                UiLocale.Configure([UiLocale.PseudoSwitch, UiLocale.CatalogSwitch, file.Path]));
            Assert.Contains("not both", refusal.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            UiLocale.Set(UiLocaleMode.Neutral);
        }
    }

    [Fact]
    public void Template_export_refuses_cli_or_environment_locale_selector_collisions()
    {
        var output = Path.Combine(Path.GetTempPath(), $"honest-ink-unused-template-{Guid.NewGuid():N}.json");
        var previousPseudo = Environment.GetEnvironmentVariable(UiLocale.PseudoEnvironmentVariable);
        var previousCatalog = Environment.GetEnvironmentVariable(UiLocale.CatalogEnvironmentVariable);
        try
        {
            Assert.Throws<InvalidDataException>(() => UiLocale.TryExportTemplate(
                [UiLocale.ExportTemplateSwitch, output, UiLocale.PseudoSwitch]));

            Environment.SetEnvironmentVariable(UiLocale.PseudoEnvironmentVariable, "1");
            Assert.Throws<InvalidDataException>(() => UiLocale.TryExportTemplate([UiLocale.ExportTemplateSwitch, output]));

            Environment.SetEnvironmentVariable(UiLocale.PseudoEnvironmentVariable, null);
            Environment.SetEnvironmentVariable(UiLocale.CatalogEnvironmentVariable, "seat-supplied-catalog.json");
            Assert.Throws<InvalidDataException>(() => UiLocale.TryExportTemplate([UiLocale.ExportTemplateSwitch, output]));
            Assert.False(File.Exists(output));
        }
        finally
        {
            Environment.SetEnvironmentVariable(UiLocale.PseudoEnvironmentVariable, previousPseudo);
            Environment.SetEnvironmentVariable(UiLocale.CatalogEnvironmentVariable, previousCatalog);
            UiLocale.Set(UiLocaleMode.Neutral);
        }
    }

    private static IEnumerable<ModuleDisplayText> ModuleDisplays()
    {
        foreach (var door in ModuleStudioCatalog.All)
        {
            yield return door.Display;
            foreach (var mode in door.Modes)
            {
                yield return mode.Display;
                if (mode.UnavailableReason is not null)
                {
                    yield return mode.UnavailableReason;
                }

                foreach (var field in mode.Fields)
                {
                    yield return field.Display;
                    foreach (var choice in field.Choices)
                    {
                        yield return choice.Display;
                    }

                    foreach (var column in field.Columns)
                    {
                        yield return column.Display;
                        foreach (var choice in column.Choices)
                        {
                            yield return choice.Display;
                        }
                    }
                }
            }
        }
    }

    private static CatalogFile ReviewedCatalog(Action<JsonObject>? change = null, string direction = "ltr")
    {
        var root = JsonNode.Parse(UiCatalogInventory.CreateTemplateJson())!.AsObject();
        root["languageTag"] = "en-US";
        root["direction"] = direction;
        var review = root["review"]!.AsObject();
        review["status"] = UiCatalogInventory.ReviewedStatus;
        review["reviewerName"] = "Synthetic catalog reviewer";
        review["reviewerRole"] = UiCatalogInventory.RequiredReviewerRole;
        review["reviewedAtUtc"] = "2026-08-30T12:00:00Z";
        var provenance = root["provenance"]!.AsObject();
        provenance["catalogId"] = "synthetic-test-catalog";
        provenance["creator"] = "Synthetic test fixture";
        provenance["source"] = "Generated only for automated tests";
        provenance["license"] = "GPL-3.0-or-later test fixture";
        provenance["modificationHistory"] = new JsonArray("Created for this test run");
        change?.Invoke(root);
        return CatalogFile.FromJson(root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void AssertCatalogRefused(Action<JsonObject> change, string expectedMessagePart)
    {
        using var file = ReviewedCatalog(change);
        try
        {
            var refusal = Assert.Throws<InvalidDataException>(() => UiCatalogLoader.LoadReviewed(file.Path));
            Assert.Contains(expectedMessagePart, refusal.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(UiLocaleMode.Neutral, UiLocale.Mode);
        }
        finally
        {
            UiLocale.Set(UiLocaleMode.Neutral);
        }
    }

    private static void AssertRawCatalogRefused(string json, string expectedMessagePart)
    {
        using var file = CatalogFile.FromJson(json);
        try
        {
            var refusal = Assert.Throws<InvalidDataException>(() => UiCatalogLoader.LoadReviewed(file.Path));
            Assert.Contains(expectedMessagePart, refusal.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            UiLocale.Set(UiLocaleMode.Neutral);
        }
    }

    private static string SHA256Hex(string text)
        => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static List<Rune> MnemonicKeys(string text)
    {
        var keys = new List<Rune>();
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '&')
            {
                continue;
            }

            if (index + 1 < text.Length && text[index + 1] == '&')
            {
                index++;
                continue;
            }

            keys.Add(Rune.GetRuneAt(text, index + 1));
        }

        return keys;
    }

    private static HashSet<string> ApprovedHashes(params CatalogFile[] files)
        => files.Select(file => file.Sha256)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void ConfigureApprovedForTest(
        CatalogFile[] approvedFiles,
        string[] args)
        => UiLocale.ConfigureForTest(args, ApprovedHashes(approvedFiles));

    private sealed class LiteralAmpersandAssetCatalog : IAssetCatalog
    {
        internal const string Meaning = "Stop &wait";

        private static readonly AssetProvenance Asset = new(
            new AssetId("synthetic-literal-ampersand"),
            "synthetic-literal-ampersand",
            "1.0.0",
            "synthetic.svg",
            "image/svg+xml",
            "Synthetic test fixture",
            "Automated test",
            "CC0-1.0",
            new string('0', 64),
            Meaning,
            "Stop and wait",
            Redistributable: true);

        public IReadOnlyList<AssetProvenance> All { get; } = [Asset];

        public AssetProvenance? Find(AssetId id)
            => id == Asset.Id ? Asset : null;

        public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
        {
            content = default;
            mimeType = "";
            return false;
        }
    }

    private sealed class CatalogFile : IDisposable
    {
        private CatalogFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public string Sha256
            => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path)));

        public static CatalogFile FromJson(string json)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"honest-ink-ui-catalog-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new CatalogFile(path);
        }

        public void Dispose() => File.Delete(Path);
    }
}
