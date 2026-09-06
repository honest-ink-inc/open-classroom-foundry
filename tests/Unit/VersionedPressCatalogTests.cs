// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.Unit;

// These are compiled-route controls, not a substitute for the separately
// captured C1 assembly/output comparison or the final schema disposition.
public sealed class VersionedPressCatalogTests
{
    private const string HistoricalVersion = "0.1.0";
    private const string CandidateVersion = "0.2.0";

    [Fact]
    public void Historical_manifests_keep_their_exact_C1_fingerprints_and_engine_floor()
    {
        Assert.All(DeterministicPressRecipes.All, recipe =>
        {
            Assert.Equal(HistoricalVersion, recipe.Version);
            Assert.Equal("0.1", recipe.EvaluationSuiteVersion);
            Assert.Equal("0.7.0-alpha", recipe.MinimumEngineVersion);
        });
        Assert.Equal("E5937D4D7B8494FF081A1E7657EBFABF5049952BE9DD6AF166FEB3D1A33B3518",
            RecipeContractFingerprint.ComputeSha256(DeterministicPressRecipes.Charts));
        Assert.Equal("363B309D85421A517A8530883A3AC63829EF3CBD9B6D1CE1450DBCA7966EA801",
            RecipeContractFingerprint.ComputeSha256(DeterministicPressRecipes.LearnerHeld));
        Assert.Equal("D4EF35243CC43AB847A3FF7443826299968501795D44F6BC30E1007D53F8773E",
            RecipeContractFingerprint.ComputeSha256(DeterministicPressRecipes.Calibration));
        Assert.Equal("18B4B7167D38FD941B65B1EC19D5E0C24F397974B9AC19AEC070ED4F54F9F5CF",
            RecipeContractFingerprint.ComputeSha256(DeterministicPressRecipes.Flashcards));
    }

    [Fact]
    public void Only_four_authorized_identities_have_six_explicit_candidate_producers()
    {
        Assert.Equal(["press.calibration", "press.charts", "press.flashcards", "press.learner-held"],
            DeterministicPressRecipes.ReplacementCandidates.Select(recipe => recipe.Id).Order(StringComparer.Ordinal));
        Assert.Equal(["bar-chart", "calibration-proof", "flashcards", "goal-post", "portfolio-passport", "strategy-shelf"],
            PressRoomCatalog.ReplacementCandidates.Select(definition => definition.Id).Order(StringComparer.Ordinal));
        Assert.All(PressRoomCatalog.All, definition =>
        {
            Assert.Equal(HistoricalVersion, definition.Recipe.Version);
            Assert.Same(definition, PressRoomCatalog.ById(definition.Id));
            Assert.Same(definition, PressRoomCatalog.ById(definition.Id, HistoricalVersion));
        });
        Assert.All(PressRoomCatalog.ReplacementCandidates, definition =>
        {
            Assert.Equal(CandidateVersion, definition.Recipe.Version);
            Assert.Equal("0.2", definition.Recipe.EvaluationSuiteVersion);
            Assert.Equal("0.8.0-alpha", definition.Recipe.MinimumEngineVersion);
            Assert.Same(definition, PressRoomCatalog.ById(definition.Id, CandidateVersion));
            var historical = PressRoomCatalog.ById(definition.Id);
            Assert.Equal(JsonSerializer.Serialize(historical.Recipe), JsonSerializer.Serialize(definition.Recipe with
            {
                Version = HistoricalVersion,
                EvaluationSuiteVersion = "0.1",
                MinimumEngineVersion = "0.7.0-alpha",
            }));
            Assert.Equal(PressRoomCatalog.Defaults(historical), PressRoomCatalog.Defaults(definition));
        });
        Assert.Equal(PressRoomCatalog.All.Count + 6, PressRoomCatalog.AllVersions.Count);
        Assert.Equal(DeterministicPressRecipes.All.Count + 4, DeterministicPressRecipes.AllVersions.Count);
        Assert.Equal(PressRoomCatalog.AllVersions.Count,
            PressRoomCatalog.AllVersions.Select(definition => (definition.Id, definition.Recipe.Version)).Distinct().Count());
        Assert.Single(PressRoomCatalog.AllVersions, definition => definition.Id == "booklet-guide");
        var learnerManifests = PressRoomCatalog.ReplacementCandidates
            .Where(definition => definition.Recipe.Id == "press.learner-held")
            .Select(definition => RecipeContractFingerprint.ComputeSha256(definition.Recipe));
        Assert.Single(learnerManifests.Distinct(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0.2")]
    [InlineData("0.2.0 ")]
    [InlineData(" 0.2.0")]
    [InlineData("V0.2.0")]
    [InlineData("0.3.0")]
    public void An_inexact_version_never_selects_a_latest_or_historical_fallback(string? version)
        => Assert.ThrowsAny<ArgumentException>(() => PressRoomCatalog.ById("bar-chart", version!));

    [Theory]
    [InlineData("BAR-CHART")]
    [InlineData("bar-chart ")]
    [InlineData(" bar-chart")]
    [InlineData("missing")]
    [InlineData("booklet-guide")]
    public void An_unknown_or_unreplaced_exact_execution_key_refuses(string id)
        => Assert.ThrowsAny<ArgumentException>(() => PressRoomCatalog.ById(id, CandidateVersion));

    [Theory]
    [InlineData("Up")]
    [InlineData("Across")]
    public void Zero_bar_preservation_and_candidate_omission_are_different_explicit_routes(string orientation)
    {
        var values = PressRoomCatalog.Defaults(PressRoomCatalog.ById("bar-chart"));
        values["data"] = "Synthetic zero | 0\nSynthetic one | 1";
        values["orientation"] = orientation;
        var old = Build("bar-chart", HistoricalVersion, values);
        var candidate = Build("bar-chart", CandidateVersion, values);
        var oldGraphic = Assert.IsType<VectorGraphic>(Assert.Single(old.Document.Nodes));
        var candidateGraphic = Assert.IsType<VectorGraphic>(Assert.Single(candidate.Document.Nodes));
        Assert.Equal(2, oldGraphic.Primitives.OfType<RectShape>().Count());
        Assert.Single(oldGraphic.Primitives.OfType<RectShape>(), rectangle => rectangle.WidthMm == 0 || rectangle.HeightMm == 0);
        Assert.Single(candidateGraphic.Primitives.OfType<RectShape>());
        Assert.Equal(oldGraphic.Primitives.OfType<TextLabel>(), candidateGraphic.Primitives.OfType<TextLabel>());
        Assert.True(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(old.Document)));
        Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(candidate.Document)));
    }

    [Fact]
    public void Historical_grid_step_retains_C1_overflow_without_running_a_pathological_sheet()
    {
        // GridStep itself terminates for this input. Historical Sheet inputs
        // whose grid loop can overflow belong in externally bounded captures,
        // never in this in-process test. This is not a historical safety fix.
        Assert.Equal(2, HistoricalChartPress.GridStep(int.MaxValue));
        Assert.Equal(500_000_000, ChartPress.GridStep(int.MaxValue));
    }

    [Theory]
    [InlineData("Up")]
    [InlineData("Across")]
    public void Candidate_chart_supports_maximum_Int32_through_exact_catalog_selection(string orientation)
    {
        var values = PressRoomCatalog.Defaults(PressRoomCatalog.ById("bar-chart", CandidateVersion));
        values["data"] = "Synthetic maximum | 2147483647\nSynthetic half | 1073741823";
        values["orientation"] = orientation;
        var candidate = Build("bar-chart", CandidateVersion, values);
        Assert.False(DocumentValidator.HasBlockingIssues(DocumentValidator.Validate(candidate.Document)));
        var graphic = Assert.IsType<VectorGraphic>(Assert.Single(candidate.Document.Nodes));
        Assert.Contains(graphic.Primitives.OfType<TextLabel>(), label => label.Text == "2500000000");
    }

    [Theory]
    [InlineData("Letter landscape")]
    [InlineData("A4 landscape")]
    public void Historical_goal_remains_one_page_while_candidate_paginates_the_same_ordered_six_prompts(string page)
    {
        var prompts = Enumerable.Range(1, 6).Select(index => $"Synthetic prompt {index}").ToArray();
        var values = PressRoomCatalog.Defaults(PressRoomCatalog.ById("goal-post"));
        values["prompts"] = string.Join('\n', prompts);
        values[PressRoomCatalog.PageKey] = page;
        var old = Build("goal-post", HistoricalVersion, values);
        var candidate = Build("goal-post", CandidateVersion, values);
        Assert.Single(old.Document.Nodes);
        Assert.Equal(2, candidate.Document.Nodes.Count);
        Assert.Equal(prompts, candidate.Document.Nodes.OfType<VectorGraphic>()
            .SelectMany(graphic => graphic.Primitives.OfType<TextLabel>())
            .Where(label => label.Text != values["pledge"]).Select(label => label.Text));
        Assert.Equal([5, 1], candidate.Document.Nodes.OfType<VectorGraphic>()
            .Select(graphic => graphic.Primitives.OfType<TextLabel>().Count(label => label.Text != values["pledge"])));
        Assert.All(candidate.Document.Nodes.OfType<VectorGraphic>(), graphic =>
            Assert.Single(graphic.Primitives.OfType<TextLabel>(), label => label.Text == values["pledge"]));
    }

    [Theory]
    [InlineData("bar-chart")]
    [InlineData("goal-post")]
    [InlineData("portfolio-passport")]
    [InlineData("strategy-shelf")]
    [InlineData("flashcards")]
    public void Unchanged_ordinary_defaults_match_across_explicit_routes(string id)
    {
        var values = PressRoomCatalog.Defaults(PressRoomCatalog.ById(id));
        var old = Build(id, HistoricalVersion, values);
        var candidate = Build(id, CandidateVersion, values);
        Assert.Equal(JsonSerializer.Serialize(old.Document), JsonSerializer.Serialize(candidate.Document));
        Assert.Equal(old.Issues, candidate.Issues);
    }

    public static TheoryData<string, string, int> UnchangedSiblingCases()
    {
        var data = new TheoryData<string, string, int>();
        foreach (var page in new[] { "Letter", "A4", "Letter landscape", "A4 landscape" })
        {
            foreach (var count in new[] { 4, 14 })
            {
                data.Add("portfolio-passport", page, count);
            }
            foreach (var count in new[] { 4, 8, 9, 16, 17, 24 })
            {
                data.Add("strategy-shelf", page, count);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(UnchangedSiblingCases))]
    public void Learner_held_siblings_keep_ordinary_boundary_documents_identical(string id, string page, int count)
    {
        var values = PressRoomCatalog.Defaults(PressRoomCatalog.ById(id));
        values[PressRoomCatalog.PageKey] = page;
        if (id == "portfolio-passport")
        {
            values["contents"] = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            values["selection"] = "Synthetic one\nSynthetic two\nSynthetic three\nSynthetic four";
            values["reflection"] = values["selection"];
        }
        else
        {
            values["strategies"] = string.Join('\n', Enumerable.Range(1, count).Select(index => $"Synthetic strategy {index}"));
        }

        Assert.Equal(JsonSerializer.Serialize(Build(id, HistoricalVersion, values).Document),
            JsonSerializer.Serialize(Build(id, CandidateVersion, values).Document));
    }

    [Theory]
    [InlineData(32.24, 1)]
    [InlineData(32.247, 1)]
    [InlineData(32.249, 2)]
    [InlineData(32.2499, 2)]
    [InlineData(32.25, 2)]
    [InlineData(32.26, 2)]
    public void Raw_goal_precision_controls_keep_the_historical_one_page_and_candidate_boundary(double margin, int candidatePages)
    {
        string[] prompts = ["Synthetic one", "Synthetic two", "Synthetic three", "Synthetic four", "Synthetic five", "Synthetic six"];
        Assert.Single(HistoricalLearnerHeldKit.GoalPost(prompts, "Synthetic pledge", PageSize.Letter, margin).Nodes);
        Assert.Equal(candidatePages, LearnerHeldKit.GoalPost(prompts, "Synthetic pledge", PageSize.Letter, margin).Nodes.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.MaxValue)]
    [InlineData(88)]
    public void Raw_historical_goal_construction_is_not_misreported_as_candidate_margin_validation(double margin)
    {
        string[] prompts = ["Synthetic one", "Synthetic two", "Synthetic three", "Synthetic four", "Synthetic five", "Synthetic six"];
        // Historical construction did not validate these margins. No approval
        // or renderer is invoked for these malformed historical documents.
        Assert.Single(HistoricalLearnerHeldKit.GoalPost(prompts, "Synthetic pledge", PageSize.A4Landscape, margin).Nodes);
        Assert.Throws<ArgumentException>(() => LearnerHeldKit.GoalPost(prompts, "Synthetic pledge", PageSize.A4Landscape, margin));
    }

    [Theory]
    [InlineData(40, 0)]
    [InlineData(41, 1)]
    [InlineData(90, 1)]
    public void Flashcard_warning_omission_is_historical_and_preservation_is_candidate_only(int answerLength, int warningCount)
    {
        var answer = new string('x', answerLength);
        var values = PressRoomCatalog.Defaults(PressRoomCatalog.ById("flashcards"));
        values["pairs"] = "Synthetic | " + answer;
        var raw = FlashcardFlywheel.Build([new FlashcardPair("Synthetic", answer)]);
        var old = Build("flashcards", HistoricalVersion, values);
        var candidate = Build("flashcards", CandidateVersion, values);
        Assert.Empty(old.Issues);
        Assert.Equal(warningCount, raw.Issues.Count);
        Assert.Equal(raw.Issues, candidate.Issues);
        Assert.Equal(JsonSerializer.Serialize(raw.Document), JsonSerializer.Serialize(old.Document));
        Assert.Equal(JsonSerializer.Serialize(old.Document), JsonSerializer.Serialize(candidate.Document));
        Assert.All(candidate.Issues, issue =>
        {
            Assert.Equal("flashcard.overflow", issue.Code);
            Assert.Equal(ValidationSeverity.Warning, issue.Severity);
            Assert.False(issue.RequiresAcknowledgement);
        });
    }

    [Fact]
    public void Selected_definitions_bind_distinct_low_ink_semantics_without_mutating_the_original()
    {
        var document = new ArtifactDocument([
            new VectorGraphic(100, 100,
                [new RectShape(10, 10, 22, 14, 0.4, Filled: true), new CircleShape(50, 50, 6, 0.5, Filled: true)],
                "Synthetic filled primitives"),
        ], "en");
        var original = JsonSerializer.Serialize(document);
        foreach (var definition in PressRoomCatalog.AllVersions)
        {
            var low = definition.ApplyLowInk(document);
            var graphic = Assert.IsType<VectorGraphic>(Assert.Single(low.Nodes));
            Assert.Equal(definition.Recipe.Version == CandidateVersion, Assert.IsType<RectShape>(graphic.Primitives[0]).Filled);
            Assert.False(Assert.IsType<CircleShape>(graphic.Primitives[1]).Filled);
            Assert.Equal("en", low.Language);
            Assert.Equal(original, JsonSerializer.Serialize(document));
        }
    }

    [Fact]
    public void Low_ink_binding_calls_the_selected_delegate_once_and_refuses_missing_results()
    {
        var document = new ArtifactDocument([new Paragraph("Synthetic transform input")]);
        var transformed = new ArtifactDocument([new Paragraph("Synthetic transform output")]);
        var calls = 0;
        var definition = new PressDefinition("synthetic", "Synthetic", DeterministicPressRecipes.Blankforms, [],
            _ => document, PressDefinition.NeutralEnglishLanguage, applyLowInk: input =>
            {
                calls++;
                Assert.Same(document, input);
                return transformed;
            });
        Assert.Same(transformed, definition.ApplyLowInk(document));
        Assert.Equal(1, calls);
        Assert.Throws<ArgumentNullException>(() => definition.ApplyLowInk(null!));
        Assert.Equal(1, calls);

        var missing = new PressDefinition("synthetic-missing", "Synthetic", DeterministicPressRecipes.Blankforms, [],
            _ => document, PressDefinition.NeutralEnglishLanguage, applyLowInk: _ => null!);
        var refusal = Assert.Throws<InvalidOperationException>(() => missing.ApplyLowInk(document));
        Assert.Equal("Press 'synthetic-missing' returned no low-ink document.", refusal.Message);
    }

    [Fact]
    public void Existing_six_argument_constructor_signatures_remain_available()
    {
        Assert.NotNull(typeof(PressDefinition).GetConstructor([
            typeof(string), typeof(string), typeof(RecipeManifest), typeof(IReadOnlyList<PressParameter>),
            typeof(Func<PressInputs, ArtifactDocument>), typeof(string),
        ]));
        Assert.NotNull(typeof(PressDefinition).GetConstructor([
            typeof(string), typeof(string), typeof(RecipeManifest), typeof(IReadOnlyList<PressParameter>),
            typeof(Func<PressInputs, PressBuildResult>), typeof(string),
        ]));
    }

    private static PressBuildResult Build(string id, string version, Dictionary<string, string> values)
        => PressRoomCatalog.ById(id, version).BuildForReview(new PressInputs(values));
}
