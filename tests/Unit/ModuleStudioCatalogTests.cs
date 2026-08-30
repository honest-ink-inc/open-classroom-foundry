// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;
using Foundry.Modules.BuiltIn;

namespace Foundry.Tests.Unit;

public class ModuleStudioCatalogTests
{
    [Fact]
    public void Catalog_has_the_exact_ten_doors_and_eleven_unique_modes()
    {
        Assert.Equal(
            [
                "board-to-brief",
                "access-remix",
                "directions-duet",
                "scaffold-smith",
                "talk-moves",
                "lesson-loom",
                "exit-lens",
                "rubric-relay",
                "source-lens",
                "family-bridge",
            ],
            ModuleStudioCatalog.All.Select(door => door.Id));

        var modes = ModuleStudioCatalog.All.SelectMany(door => door.Modes).ToList();
        Assert.Equal(11, modes.Count);
        Assert.Equal(modes.Count, modes.Select(mode => mode.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ModuleStudioCatalog.All.Count,
            ModuleStudioCatalog.All.Select(door => door.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_mode_binds_the_real_recipe_and_its_declared_lane()
    {
        var expectedRecipes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["board-to-brief"] = "board-to-brief",
            ["access-remix"] = "access-remix",
            ["directions-duet"] = "directions-duet",
            ["scaffold-smith.packet"] = "scaffold-smith.packet",
            ["scaffold-smith.task-entry"] = "scaffold-smith.task-entry",
            ["talk-moves-studio"] = "talk-moves-studio",
            ["lesson-loom"] = "lesson-loom",
            ["exit-lens"] = "exit-lens",
            ["rubric-relay"] = "rubric-relay",
            ["source-lens"] = "source-lens",
            ["family-bridge"] = "family-bridge",
        };

        foreach (var mode in ModuleStudioCatalog.All.SelectMany(door => door.Modes))
        {
            Assert.Equal(expectedRecipes[mode.Key], mode.Recipe.Id);
            Assert.Equal(mode.Recipe.MaximumLane, mode.Lane);
        }
    }

    [Theory]
    [InlineData("directions-duet")]
    [InlineData("family-bridge")]
    public void Multilingual_modes_expose_only_explicitly_unapproved_working_glossaries(string modeKey)
    {
        var mode = ModuleStudioCatalog.ByModeKey(modeKey);

        Assert.DoesNotContain(mode.Fields, field => field.Key == "reviewed-by");

        var glossaryVersion = Assert.Single(mode.Fields, field => field.Key == "glossary-version");
        Assert.Contains("Working glossary", glossaryVersion.Display.Fallback, StringComparison.Ordinal);
        Assert.Contains("not approved", glossaryVersion.Display.Fallback, StringComparison.Ordinal);

        var glossary = Assert.Single(mode.Fields, field => field.Key == "glossary");
        Assert.Contains("Working glossary", glossary.Display.Fallback, StringComparison.Ordinal);
        Assert.Contains("not approved", glossary.Display.Fallback, StringComparison.Ordinal);
        Assert.Contains(glossary.Columns, column =>
            column.Key == "target"
            && column.Display.Fallback.Contains("Working", StringComparison.Ordinal)
            && column.Display.Fallback.Contains("not approved", StringComparison.Ordinal));

        var values = ModuleStudioCatalog.Defaults(mode);
        values["reviewed-by"] = "Untrusted typed reviewer claim";
        var outcome = RequiredBuild(mode)(new ModuleInputValues(values));
        Assert.Contains(outcome.Document.Nodes.OfType<TeacherOnlyNotice>(), notice =>
            notice.Text.Contains("not approved by this application", StringComparison.Ordinal)
            && notice.Text.Contains("NOT yet language-reviewed", StringComparison.Ordinal));
        Assert.DoesNotContain(outcome.Document.Nodes.OfType<TeacherOnlyNotice>(), notice =>
            notice.Text.Contains("Untrusted typed reviewer claim", StringComparison.Ordinal));
    }

    [Fact]
    public void Display_text_has_stable_localization_ids_and_neutral_fallbacks()
    {
        foreach (var door in ModuleStudioCatalog.All)
        {
            AssertDisplay(door.Display);
            foreach (var mode in door.Modes)
            {
                AssertDisplay(mode.Display);
                foreach (var field in mode.Fields)
                {
                    AssertDisplay(field.Display);
                    foreach (var choice in field.Choices.Concat(field.Columns.SelectMany(column => column.Choices)))
                    {
                        Assert.False(string.IsNullOrWhiteSpace(choice.Value));
                        AssertDisplay(choice.Display);
                    }

                    foreach (var column in field.Columns)
                    {
                        AssertDisplay(column.Display);
                    }
                }
            }
        }
    }

    [Fact]
    public void Each_localization_id_has_one_unambiguous_neutral_meaning()
    {
        var displays = new List<ModuleDisplayText>();
        foreach (var door in ModuleStudioCatalog.All)
        {
            displays.Add(door.Display);
            foreach (var mode in door.Modes)
            {
                displays.Add(mode.Display);
                if (mode.UnavailableReason is not null)
                {
                    displays.Add(mode.UnavailableReason);
                }

                foreach (var field in mode.Fields)
                {
                    displays.Add(field.Display);
                    displays.AddRange(field.Choices.Select(choice => choice.Display));
                    foreach (var column in field.Columns)
                    {
                        displays.Add(column.Display);
                        displays.AddRange(column.Choices.Select(choice => choice.Display));
                    }
                }
            }
        }

        foreach (var group in displays.GroupBy(display => display.LocalizationId, StringComparer.Ordinal))
        {
            Assert.Single(group.Select(display => display.Fallback).Distinct(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void Every_synthetic_default_builds_without_blockers_and_revalidates_cleanly()
    {
        var syntheticModes = ModuleStudioCatalog.All
            .SelectMany(door => door.Modes)
            .Where(mode => mode.DefaultsAreSynthetic)
            .ToList();

        Assert.Equal(8, syntheticModes.Count);
        foreach (var mode in syntheticModes)
        {
            var build = mode.Build ?? throw new Xunit.Sdk.XunitException($"Synthetic mode '{mode.Key}' has no builder.");
            var outcome = build(new ModuleInputValues(ModuleStudioCatalog.Defaults(mode)));

            Assert.Equal(mode.Recipe, outcome.Recipe);
            Assert.Equal(DataLane.Green, outcome.Lane);
            Assert.Equal(ArtifactPurpose.Unknown, outcome.Purpose);
            Assert.False(DocumentValidator.HasBlockingIssues(outcome.Issues),
                $"Default for '{mode.Key}' blocked: {string.Join("; ", outcome.Issues.Select(issue => $"{issue.Code}: {issue.Message}"))}");
            Assert.False(DocumentValidator.HasBlockingIssues(outcome.Validator.Validate(outcome.Document)),
                $"Default for '{mode.Key}' failed its review validator.");
        }
    }

    [Fact]
    public void Access_operation_uses_stable_values_separate_from_display_text()
    {
        var mode = ModuleStudioCatalog.ByModeKey("access-remix");
        var operation = Assert.Single(mode.Fields, field => field.Key == "operation");
        Assert.Equal(ModuleFieldKind.Choice, operation.Kind);
        Assert.Equal(
            [ModuleStudioCatalog.AccessOperationChunk, ModuleStudioCatalog.AccessOperationOneStepPerPanel],
            operation.Choices.Select(choice => choice.Value));
        Assert.All(operation.Choices, choice => Assert.NotEqual(choice.Value, choice.Display.Fallback));

        var chunkSize = Assert.Single(mode.Fields, field => field.Key == "chunk-size");
        Assert.Equal(new ModuleFieldCondition("operation", ModuleStudioCatalog.AccessOperationChunk), chunkSize.Condition);
    }

    [Fact]
    public void Access_is_visible_but_has_no_catalog_build_or_submitted_authority_path()
    {
        var mode = ModuleStudioCatalog.ByModeKey("access-remix");

        Assert.Equal(DataLane.Green, mode.Lane);
        Assert.Equal(ModuleDefaultKind.Unavailable, mode.DefaultKind);
        Assert.False(mode.IsBuildAvailable);
        Assert.Null(mode.Build);
        Assert.Equal(
            ModuleStudioCatalog.AccessPurposeAuthorityRequiredId,
            mode.UnavailableReason?.LocalizationId);
        Assert.Contains("Typed content cannot grant it", mode.UnavailableReason?.Fallback, StringComparison.Ordinal);
        Assert.DoesNotContain(mode.Fields, field =>
            field.Key.Contains("authoriz", StringComparison.OrdinalIgnoreCase)
            || field.Key.Contains("purpose", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Module_keyboard_language_tags_are_refused_with_the_exact_field_named()
    {
        var mode = ModuleStudioCatalog.ByModeKey("directions-duet");
        var values = ModuleStudioCatalog.Defaults(mode);
        values["source-locale"] = "e n";

        var error = Assert.Throws<ArgumentException>(() =>
            RequiredBuild(mode)(new ModuleInputValues(values)));

        Assert.Equal("sourceLocale", error.ParamName);
        Assert.Contains("structurally valid language tag", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Amber_modes_are_visible_but_have_no_keyboard_authorization_path()
    {
        var amberModes = ModuleStudioCatalog.All.SelectMany(door => door.Modes)
            .Where(mode => mode.Lane == DataLane.Amber)
            .ToList();

        Assert.Equal(["exit-lens", "rubric-relay"], amberModes.Select(mode => mode.Key));
        foreach (var mode in amberModes)
        {
            Assert.False(mode.IsBuildAvailable);
            Assert.Null(mode.Build);
            Assert.Equal(ModuleDefaultKind.Unavailable, mode.DefaultKind);
            Assert.Equal(ModuleStudioCatalog.DistrictAuthorizationRequiredId, mode.UnavailableReason?.LocalizationId);
            Assert.DoesNotContain(mode.Fields, field => field.Key.Contains("authoriz", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Input_values_parse_invariantly_and_reject_malformed_records_loudly()
    {
        var values = new ModuleInputValues(new Dictionary<string, object?>
        {
            ["whole"] = "12",
            ["toggle"] = "true",
            ["choice"] = "stable-value",
            ["rows"] = "left | right\nnext | row",
        });

        Assert.Equal(12, values.Integer("whole"));
        Assert.True(values.Toggle("toggle"));
        Assert.Equal("stable-value", values.Choice("choice", ["stable-value"]));
        Assert.Equal(2, values.Records("rows", 2).Count);

        Assert.Throws<ArgumentException>(() => new ModuleInputValues(
            new Dictionary<string, object?> { ["whole"] = "1,2" }).Integer("whole"));
        Assert.Throws<ArgumentException>(() => new ModuleInputValues(
            new Dictionary<string, object?> { ["toggle"] = "yes" }).Toggle("toggle"));
        Assert.Throws<ArgumentException>(() => new ModuleInputValues(
            new Dictionary<string, object?> { ["rows"] = "one|two|three" }).Records("rows", 2));
        Assert.Throws<ArgumentException>(() => values.Choice("choice", ["different-value"]));
        Assert.Throws<ArgumentException>(() => values.Text("missing"));
    }

    [Fact]
    public void Review_validator_catches_locked_glossary_parity_and_structure_breaks()
    {
        var board = BuildDefaults("board-to-brief");
        var boardNodes = board.Document.Nodes.Where(node => node is not Paragraph { Text: "Monday" }).ToList();
        Assert.Contains(board.Validator.Validate(new ArtifactDocument(boardNodes, board.Document.Language)),
            issue => issue.Code == "locked.missing" && issue.Severity == ValidationSeverity.Blocking);

        var duet = BuildDefaults("directions-duet");
        var duetNodes = duet.Document.Nodes.Select(node => node is BilingualPair pair && pair.SourceText.Contains("folder", StringComparison.OrdinalIgnoreCase)
            ? new BilingualPair(pair.SourceText, "Abra 3.", pair.SourceLocale, pair.TargetLocale)
            : node).ToList();
        Assert.Contains(duet.Validator.Validate(new ArtifactDocument(duetNodes, duet.Document.Language)),
            issue => issue.Code == "duet.glossary" && issue.Severity == ValidationSeverity.Blocking);

        var scaffold = BuildDefaults("scaffold-smith.packet");
        var withoutNotices = new ArtifactDocument(
            [.. scaffold.Document.Nodes.Where(node => node is not TeacherOnlyNotice)],
            scaffold.Document.Language);
        Assert.Contains(scaffold.Validator.Validate(withoutNotices),
            issue => issue.Code == "scaffold.structure" && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void Initial_warnings_survive_review_edits()
    {
        var mode = ModuleStudioCatalog.ByModeKey("source-lens");
        var values = ModuleStudioCatalog.Defaults(mode);
        values["rights"] = Modules.BuiltIn.SourceLens.SourceLensBuilder.Unknown;
        var outcome = RequiredBuild(mode)(new ModuleInputValues(values));
        Assert.Contains(outcome.Issues, issue => issue.Code == "lens.rights-unknown" && issue.Severity == ValidationSeverity.Warning);

        var edited = new ArtifactDocument(
            [.. outcome.Document.Nodes, new Paragraph("Teacher-added inquiry note.")],
            outcome.Document.Language);
        Assert.Contains(outcome.Validator.Validate(edited),
            issue => issue.Code == "lens.rights-unknown" && issue.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void Initial_builder_blockers_survive_review_validation_until_inputs_are_rebuilt()
    {
        var mode = ModuleStudioCatalog.ByModeKey("source-lens");
        var values = ModuleStudioCatalog.Defaults(mode);
        values["transcript-verified"] = "false";

        var outcome = RequiredBuild(mode)(new ModuleInputValues(values));
        var blocker = Assert.Single(outcome.Issues, issue =>
            issue.Code == "lens.transcript" && issue.Severity == ValidationSeverity.Blocking);

        Assert.Contains(outcome.Validator.Validate(outcome.Document), issue => issue == blocker);
    }

    private static void AssertDisplay(ModuleDisplayText display)
    {
        Assert.StartsWith("modules.", display.LocalizationId, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(display.Fallback));
    }

    private static ModuleBuildOutcome BuildDefaults(string modeKey)
    {
        var mode = ModuleStudioCatalog.ByModeKey(modeKey);
        return RequiredBuild(mode)(new ModuleInputValues(ModuleStudioCatalog.Defaults(mode)));
    }

    private static Func<ModuleInputValues, ModuleBuildOutcome> RequiredBuild(ModuleModeDefinition mode)
        => mode.Build ?? throw new Xunit.Sdk.XunitException($"Mode '{mode.Key}' has no builder.");

}
