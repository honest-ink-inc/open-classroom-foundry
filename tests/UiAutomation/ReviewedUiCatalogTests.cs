// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Foundry.App.WinForms;
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

    private static HashSet<string> ApprovedHashes(params CatalogFile[] files)
        => files.Select(file => file.Sha256)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void ConfigureApprovedForTest(
        CatalogFile[] approvedFiles,
        string[] args)
        => UiLocale.ConfigureForTest(args, ApprovedHashes(approvedFiles));

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
