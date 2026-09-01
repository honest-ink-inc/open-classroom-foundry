// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.BuiltIn.TalkMoves;

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
    public void Adopted_public_names_are_unique_and_leave_every_legacy_key_intact()
    {
        var expected = new Dictionary<string, (string Name, string Subtitle, string FileStem)>(StringComparer.Ordinal)
        {
            ["all-aboard"] = ("SequenceSlate", "Visual Support Studio", "sequenceslate"),
            ["lesson-loom"] = ("StrandPlan", "Lesson Design Studio", "strandplan"),
            ["talk-moves"] = ("Forumwright", "Discussion Design", "forumwright"),
            ["exit-lens"] = ("ReteachSignal", "Formative Evidence", "reteachsignal"),
            ["source-lens"] = ("Inquirywright", "Source & Inquiry", "inquirywright"),
            ["family-bridge"] = ("KinDispatch", "Bilingual & Family Press", "kindispatch"),
        };

        Assert.Equal(expected.Keys, ModulePublicIdentity.All.Select(identity => identity.LegacyId));
        Assert.Equal(ModulePublicIdentity.All.Count,
            ModulePublicIdentity.All.Select(identity => identity.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ModulePublicIdentity.All.Count,
            ModulePublicIdentity.All.Select(identity => identity.FileStem).Distinct(StringComparer.Ordinal).Count());

        foreach (var identity in ModulePublicIdentity.All)
        {
            Assert.Equal(expected[identity.LegacyId], (identity.Name, identity.Subtitle, identity.FileStem));
            Assert.Equal(identity, ModulePublicIdentity.FindByLegacyId(identity.LegacyId));

            if (!string.Equals(identity.LegacyId, ModulePublicIdentity.VisualSupport.LegacyId, StringComparison.Ordinal))
            {
                var door = ModuleStudioCatalog.ById(identity.LegacyId);
                Assert.Equal(identity.DisplayName, door.Display.Fallback);
                Assert.Equal(identity.FileStem, door.PublicFileStem);
            }
        }
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

    [Theory]
    [InlineData("directions-duet")]
    [InlineData("family-bridge")]
    public void Locked_fact_inventory_review_is_explicit_and_reviewed_empty_remains_valid(string modeKey)
    {
        var mode = ModuleStudioCatalog.ByModeKey(modeKey);
        var confirmation = Assert.Single(mode.Fields, field => field.Key == ModuleStudioCatalog.LockedFactsReviewedKey);
        Assert.Equal(ModuleFieldKind.Toggle, confirmation.Kind);
        Assert.Equal("false", confirmation.DefaultValue);
        Assert.Contains("Source content", confirmation.Display.Fallback, StringComparison.Ordinal);
        Assert.Contains("exact values", confirmation.Display.Fallback, StringComparison.Ordinal);
        Assert.Contains("not language", confirmation.Display.Fallback, StringComparison.Ordinal);
        Assert.Contains("specialist review", confirmation.Display.Fallback, StringComparison.Ordinal);

        var pendingValues = ModuleStudioCatalog.Defaults(mode);
        pendingValues["locked-fields"] = "";
        var pending = RequiredBuild(mode)(new ModuleInputValues(pendingValues));
        Assert.Contains(pending.Issues, issue =>
            issue.Code == "locked.inventory-review-required"
            && issue.Severity == ValidationSeverity.Blocking);
        Assert.Contains(pending.Validator.Validate(pending.Document), issue =>
            issue.Code == "locked.inventory-review-required"
            && issue.Severity == ValidationSeverity.Blocking);

        pendingValues[ModuleStudioCatalog.LockedFactsReviewedKey] = "true";
        var reviewedEmpty = RequiredBuild(mode)(new ModuleInputValues(pendingValues));
        Assert.DoesNotContain(reviewedEmpty.Issues, issue => issue.Code == "locked.inventory-review-required");
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
            var outcome = build(new ModuleInputValues(ReadyDefaults(mode)));

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
    public void Forumwright_catalog_input_cannot_count_duplicates_or_the_automatic_pass_option_as_modes()
    {
        var mode = ModuleStudioCatalog.ByModeKey("talk-moves-studio");
        var values = ModuleStudioCatalog.Defaults(mode);
        values["participation-modes"] = "Write\n write \nWRITE";

        var duplicateOutcome = RequiredBuild(mode)(new ModuleInputValues(values));
        Assert.Contains(duplicateOutcome.Issues, issue =>
            issue.Code == "talk.modes"
            && issue.Severity == ValidationSeverity.Blocking);

        values["participation-modes"] = $"Speak\nWrite\nPoint\n{TalkMovesBuilder.PassOption}";
        var reservedOutcome = RequiredBuild(mode)(new ModuleInputValues(values));

        Assert.Contains(reservedOutcome.Issues, issue =>
            issue.Code == "talk.modes"
            && issue.Severity == ValidationSeverity.Blocking);
        var modes = reservedOutcome.Document.Nodes.OfType<UnorderedList>()
            .Single(list => list.Items.Contains(TalkMovesBuilder.PassOption));
        Assert.Equal(
            1,
            modes.Items.Count(item => item == TalkMovesBuilder.PassOption));
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
    public void Directions_review_validator_refuses_a_locked_value_moved_to_another_aligned_row()
    {
        var duet = BuildDefaults("directions-duet");
        var editedNodes = duet.Document.Nodes.Select(node => node switch
        {
            BilingualPair pair when pair.SourceText.StartsWith("Open folder", StringComparison.Ordinal) =>
                new BilingualPair(pair.SourceText, "Abra la carpeta.", pair.SourceLocale, pair.TargetLocale),
            BilingualPair pair when pair.SourceText.StartsWith("Read page", StringComparison.Ordinal) =>
                new BilingualPair("Read the page.", pair.TargetText, pair.SourceLocale, pair.TargetLocale),
            _ => node,
        }).ToList();

        var issues = duet.Validator.Validate(new ArtifactDocument(editedNodes, duet.Document.Language));

        Assert.Contains(issues, issue =>
            issue.Code == "duet.locked"
            && issue.Severity == ValidationSeverity.Blocking
            && issue.Message.Contains("aligned item 1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Directions_review_validator_refuses_a_number_lock_embedded_in_a_changed_number()
    {
        var duet = BuildDefaults("directions-duet");
        var editedNodes = duet.Document.Nodes.Select(node => node is BilingualPair pair
            ? new BilingualPair(
                pair.SourceText,
                pair.TargetText.Replace("3", "13", StringComparison.Ordinal),
                pair.SourceLocale,
                pair.TargetLocale)
            : node).ToList();

        var issues = duet.Validator.Validate(new ArtifactDocument(editedNodes, duet.Document.Language));

        Assert.Contains(issues, issue =>
            issue.Code == "duet.locked"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void Directions_review_validator_refuses_a_decimal_lock_extended_after_GateB()
    {
        var mode = ModuleStudioCatalog.ByModeKey("directions-duet");
        var values = ReadyDefaults(mode);
        values["steps"] = "Pay $4.5.|Pague $4.5.";
        values["locked-fields"] = "number|$4.5";
        var duet = RequiredBuild(mode)(new ModuleInputValues(values));
        Assert.False(DocumentValidator.HasBlockingIssues(duet.Issues));

        var editedNodes = duet.Document.Nodes.Select(node => node is BilingualPair pair
            ? new BilingualPair(
                pair.SourceText,
                pair.TargetText.Replace("$4.5", "$4.50", StringComparison.Ordinal),
                pair.SourceLocale,
                pair.TargetLocale)
            : node).ToList();

        Assert.Contains(duet.Validator.Validate(new ArtifactDocument(editedNodes, duet.Document.Language)), issue =>
            issue.Code == "duet.locked"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void Family_review_validator_allows_a_preserved_fact_repeated_in_another_target_paragraph()
    {
        var family = BuildDefaults("family-bridge");
        var secondPairSeen = false;
        var editedNodes = family.Document.Nodes.Select(node =>
        {
            if (node is not BilingualPair pair)
            {
                return node;
            }

            if (!secondPairSeen && pair.SourceText.StartsWith("Forms", StringComparison.Ordinal))
            {
                secondPairSeen = true;
                return node;
            }

            return new BilingualPair(
                pair.SourceText,
                pair.TargetText + " Recordatorio June 10.",
                pair.SourceLocale,
                pair.TargetLocale);
        }).ToList();

        var issues = family.Validator.Validate(new ArtifactDocument(editedNodes, family.Document.Language));

        Assert.DoesNotContain(issues, issue => issue.Code == "bridge.locked");
    }

    [Theory]
    [InlineData("directions-duet")]
    [InlineData("family-bridge")]
    public void GateB_source_edits_stale_the_inventory_while_target_only_edits_remain_eligible(string modeKey)
    {
        var outcome = BuildDefaults(modeKey);
        var pairIndex = outcome.Document.Nodes.ToList().FindIndex(node => node is BilingualPair);
        Assert.True(pairIndex >= 0);
        var pair = Assert.IsType<BilingualPair>(outcome.Document.Nodes[pairIndex]);

        var sourceEditedNodes = outcome.Document.Nodes.ToList();
        sourceEditedNodes[pairIndex] = new BilingualPair(
            pair.SourceText + " New exact value 7.",
            pair.TargetText,
            pair.SourceLocale,
            pair.TargetLocale);
        var sourceIssues = outcome.Validator.Validate(
            new ArtifactDocument(sourceEditedNodes, outcome.Document.Language));

        Assert.Contains(sourceIssues, issue =>
            issue.Code == "locked.inventory-review-stale"
            && issue.Severity == ValidationSeverity.Blocking);

        var targetEditedNodes = outcome.Document.Nodes.ToList();
        targetEditedNodes[pairIndex] = new BilingualPair(
            pair.SourceText,
            pair.TargetText + " Nota adicional.",
            pair.SourceLocale,
            pair.TargetLocale);
        var targetIssues = outcome.Validator.Validate(
            new ArtifactDocument(targetEditedNodes, outcome.Document.Language));

        Assert.DoesNotContain(targetIssues, issue => issue.Code == "locked.inventory-review-stale");
        Assert.False(DocumentValidator.HasBlockingIssues(targetIssues));
    }

    [Theory]
    [InlineData("directions-duet", "duet.locale")]
    [InlineData("family-bridge", "bridge.locale")]
    public void GateB_target_locale_metadata_is_pinned_without_treating_target_text_as_source(
        string modeKey,
        string expectedIssueCode)
    {
        var outcome = BuildDefaults(modeKey);
        var nodes = outcome.Document.Nodes.ToList();
        var pairIndex = nodes.FindIndex(node => node is BilingualPair);
        var pair = Assert.IsType<BilingualPair>(nodes[pairIndex]);
        nodes[pairIndex] = new BilingualPair(
            pair.SourceText,
            pair.TargetText,
            pair.SourceLocale,
            "fr");

        var issues = outcome.Validator.Validate(new ArtifactDocument(nodes, outcome.Document.Language));

        Assert.Contains(issues, issue =>
            issue.Code == expectedIssueCode
            && issue.Severity == ValidationSeverity.Blocking);
        Assert.DoesNotContain(issues, issue => issue.Code == "locked.inventory-review-stale");
    }

    [Theory]
    [InlineData("directions-duet")]
    [InlineData("family-bridge")]
    public void GateB_root_source_language_changes_stale_the_confirmed_inventory(string modeKey)
    {
        var outcome = BuildDefaults(modeKey);
        var issues = outcome.Validator.Validate(
            new ArtifactDocument(outcome.Document.Nodes, "ar"));

        Assert.Contains(issues, issue =>
            issue.Code == "locked.inventory-review-stale"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Theory]
    [InlineData("directions-duet")]
    [InlineData("family-bridge")]
    public void Reordering_semantic_source_rows_stales_the_confirmed_inventory(string modeKey)
    {
        var outcome = BuildDefaults(modeKey);
        var nodes = outcome.Document.Nodes.ToList();
        var pairIndexes = nodes
            .Select((node, index) => (node, index))
            .Where(item => item.node is BilingualPair)
            .Take(2)
            .Select(item => item.index)
            .ToArray();
        Assert.Equal(2, pairIndexes.Length);
        (nodes[pairIndexes[0]], nodes[pairIndexes[1]]) = (nodes[pairIndexes[1]], nodes[pairIndexes[0]]);

        var issues = outcome.Validator.Validate(new ArtifactDocument(nodes, outcome.Document.Language));

        Assert.Contains(issues, issue =>
            issue.Code == "locked.inventory-review-stale"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void Directions_source_text_moved_between_semantic_roles_stales_the_confirmed_inventory()
    {
        var outcome = BuildDefaults("directions-duet");
        var nodes = outcome.Document.Nodes.ToList();
        var headingIndex = nodes.FindIndex(node => node is Heading { Level: 1 });
        var pairIndex = nodes.FindIndex(node => node is BilingualPair);
        var heading = Assert.IsType<Heading>(nodes[headingIndex]);
        var pair = Assert.IsType<BilingualPair>(nodes[pairIndex]);

        nodes[headingIndex] = new Heading(heading.Level, pair.SourceText);
        nodes[pairIndex] = new BilingualPair(
            heading.Text,
            pair.TargetText,
            pair.SourceLocale,
            pair.TargetLocale);

        var issues = outcome.Validator.Validate(new ArtifactDocument(nodes, outcome.Document.Language));

        Assert.Contains(issues, issue =>
            issue.Code == "locked.inventory-review-stale"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void Family_source_text_moved_between_action_and_deadline_roles_stales_the_confirmed_inventory()
    {
        var outcome = BuildDefaults("family-bridge");
        var nodes = outcome.Document.Nodes.ToList();
        var actionHeadingIndex = nodes.FindIndex(node => node is Heading { Text: "What we ask" });
        var deadlineHeadingIndex = nodes.FindIndex(node => node is Heading { Text: "By when" });
        var actionPairIndex = actionHeadingIndex + 1;
        var deadlinePairIndex = deadlineHeadingIndex + 1;
        var action = Assert.IsType<BilingualPair>(nodes[actionPairIndex]);
        var deadline = Assert.IsType<BilingualPair>(nodes[deadlinePairIndex]);

        nodes[actionPairIndex] = new BilingualPair(
            deadline.SourceText,
            action.TargetText,
            action.SourceLocale,
            action.TargetLocale);
        nodes[deadlinePairIndex] = new BilingualPair(
            action.SourceText,
            deadline.TargetText,
            deadline.SourceLocale,
            deadline.TargetLocale);

        var issues = outcome.Validator.Validate(new ArtifactDocument(nodes, outcome.Document.Language));

        Assert.Contains(issues, issue =>
            issue.Code == "locked.inventory-review-stale"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void Family_target_deadline_cannot_borrow_its_lock_from_a_body_paragraph()
    {
        var outcome = BuildDefaults("family-bridge");
        var nodes = outcome.Document.Nodes.ToList();
        var deadlineHeadingIndex = nodes.FindIndex(node => node is Heading { Text: "By when" });
        var deadlinePairIndex = deadlineHeadingIndex + 1;
        var deadline = Assert.IsType<BilingualPair>(nodes[deadlinePairIndex]);
        nodes[deadlinePairIndex] = new BilingualPair(
            deadline.SourceText,
            "June 11",
            deadline.SourceLocale,
            deadline.TargetLocale);

        var issues = outcome.Validator.Validate(new ArtifactDocument(nodes, outcome.Document.Language));

        Assert.DoesNotContain(issues, issue => issue.Code == "locked.inventory-review-stale");
        Assert.Contains(issues, issue =>
            issue.Code == "bridge.locked"
            && issue.Severity == ValidationSeverity.Blocking
            && issue.Message.Contains("deadline", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Family_gateB_readability_remains_scoped_to_message_paragraphs()
    {
        var mode = ModuleStudioCatalog.ByModeKey("family-bridge");
        var values = ReadyDefaults(mode);
        values["requested-action"] = string.Join(" ", Enumerable.Repeat("synthetic", 70)) + ".";
        values["target-requested-action"] = "Synthetic target action.";
        var outcome = RequiredBuild(mode)(new ModuleInputValues(values));

        Assert.DoesNotContain(outcome.Issues, issue => issue.Code == "bridge.readability");
        Assert.DoesNotContain(
            outcome.Validator.Validate(outcome.Document),
            issue => issue.Code == "bridge.readability");
    }

    [Fact]
    public void Family_catalog_refuses_populated_target_paragraphs_without_a_target_language()
    {
        var mode = ModuleStudioCatalog.ByModeKey("family-bridge");
        var values = ReadyDefaults(mode);
        values["target-locale"] = "";

        var outcome = RequiredBuild(mode)(new ModuleInputValues(values));

        Assert.Contains(outcome.Issues, issue =>
            issue.Code == "bridge.target-without-locale"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void Family_gateB_does_not_treat_generated_card_titles_as_teacher_source_facts()
    {
        var mode = ModuleStudioCatalog.ByModeKey("family-bridge");
        var values = ReadyDefaults(mode);
        values["target-locale"] = "";
        values["paragraphs"] = "Source message.|";
        values["locked-fields"] = "proper-name|What we ask";
        var outcome = RequiredBuild(mode)(new ModuleInputValues(values));

        Assert.Contains(outcome.Issues, issue =>
            issue.Code == "locked.missing"
            && issue.Severity == ValidationSeverity.Blocking);
        Assert.Contains(outcome.Validator.Validate(outcome.Document), issue =>
            issue.Code == "locked.missing"
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Theory]
    [InlineData("directions-duet")]
    [InlineData("family-bridge")]
    public void Layout_only_page_breaks_do_not_stale_source_inventory(string modeKey)
    {
        var outcome = BuildDefaults(modeKey);
        var nodes = outcome.Document.Nodes.ToList();
        var headingIndex = nodes.FindIndex(node => node is Heading);
        nodes.Insert(headingIndex + 1, new PageBreak());

        var issues = outcome.Validator.Validate(new ArtifactDocument(nodes, outcome.Document.Language));

        Assert.DoesNotContain(issues, issue => issue.Code == "locked.inventory-review-stale");
    }

    [Theory]
    [InlineData("directions-duet", "duet.structure")]
    [InlineData("family-bridge", "bridge.structure")]
    public void Semantic_heading_level_changes_stale_inventory_and_block_structure(
        string modeKey,
        string expectedStructureCode)
    {
        var outcome = BuildDefaults(modeKey);
        var nodes = outcome.Document.Nodes.ToList();
        var headingIndex = nodes.FindIndex(node => node is Heading { Level: 1 });
        var heading = Assert.IsType<Heading>(nodes[headingIndex]);
        nodes[headingIndex] = new Heading(2, heading.Text);

        var issues = outcome.Validator.Validate(new ArtifactDocument(nodes, outcome.Document.Language));

        Assert.Contains(issues, issue =>
            issue.Code == "locked.inventory-review-stale"
            && issue.Severity == ValidationSeverity.Blocking);
        Assert.Contains(issues, issue =>
            issue.Code == expectedStructureCode
            && issue.Severity == ValidationSeverity.Blocking);
    }

    [Fact]
    public void Every_buildable_mode_turns_each_manifest_warning_into_a_fresh_required_confirmation()
    {
        foreach (var mode in ModuleStudioCatalog.All.SelectMany(door => door.Modes).Where(mode => mode.IsBuildAvailable))
        {
            var first = RequiredBuild(mode)(new ModuleInputValues(ReadyDefaults(mode)));
            var second = RequiredBuild(mode)(new ModuleInputValues(ReadyDefaults(mode)));

            for (var index = 0; index < mode.Recipe.Warnings.Count; index++)
            {
                var code = $"recipe.warning.{index + 1}";
                var firstIssue = Assert.Single(first.Issues, issue => issue.Code == code);
                var secondIssue = Assert.Single(second.Issues, issue => issue.Code == code);
                Assert.Equal(mode.Recipe.Warnings[index], firstIssue.Message);
                Assert.Equal(ValidationSeverity.Warning, firstIssue.Severity);
                Assert.True(firstIssue.RequiresAcknowledgement);
                Assert.NotSame(firstIssue, secondIssue);
                Assert.Contains(first.Validator.Validate(first.Document), issue => issue == firstIssue);
            }

            Assert.Equal(
                mode.Recipe.Warnings.Count,
                first.Issues.Count(issue => issue.Code.StartsWith("recipe.warning.", StringComparison.Ordinal)));
        }
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
        return RequiredBuild(mode)(new ModuleInputValues(ReadyDefaults(mode)));
    }

    private static Dictionary<string, object?> ReadyDefaults(ModuleModeDefinition mode)
    {
        var values = ModuleStudioCatalog.Defaults(mode);
        if (values.ContainsKey(ModuleStudioCatalog.LockedFactsReviewedKey))
        {
            values[ModuleStudioCatalog.LockedFactsReviewedKey] = "true";
        }

        return values;
    }

    private static Func<ModuleInputValues, ModuleBuildOutcome> RequiredBuild(ModuleModeDefinition mode)
        => mode.Build ?? throw new Xunit.Sdk.XunitException($"Mode '{mode.Key}' has no builder.");

}
