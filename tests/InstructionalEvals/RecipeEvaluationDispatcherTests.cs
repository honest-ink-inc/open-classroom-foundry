// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Modules.BuiltIn;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.InstructionalEvals;

public sealed class RecipeEvaluationDispatcherTests
{
    public static TheoryData<string, string, string, string> ReplacementCases()
    {
        var cases = new TheoryData<string, string, string, string>();
        foreach (var corpus in RecipeEvaluationDispatcher.All.Where(route => route.Key.RecipeVersion == "0.2.0"))
        {
            foreach (var item in corpus.Cases)
            {
                cases.Add(corpus.Key.RecipeId, corpus.Key.RecipeVersion, corpus.Key.EvaluationVersion, item.Id);
            }
        }

        return cases;
    }

    [Fact]
    public void Ten_exact_routes_bind_retained_corpora_and_distinct_replacement_review_cases()
    {
        var routes = RecipeEvaluationDispatcher.All;
        Assert.Equal(10, routes.Count);
        Assert.Equal(routes.Count, routes.Select(route => route.Key).Distinct().Count());
        Assert.Equal(
            [
                new RecipeEvaluationKey("lesson-loom", "0.1.0", "0.1"),
                new RecipeEvaluationKey("board-to-brief", "0.1.0", "0.1"),
                new RecipeEvaluationKey("source-lens", "0.1.0", "0.1"),
                new RecipeEvaluationKey("lesson-loom", "0.2.0", "0.2"),
                new RecipeEvaluationKey("board-to-brief", "0.2.0", "0.2"),
                new RecipeEvaluationKey("source-lens", "0.2.0", "0.2"),
                new RecipeEvaluationKey("press.charts", "0.2.0", "0.2"),
                new RecipeEvaluationKey("press.learner-held", "0.2.0", "0.2"),
                new RecipeEvaluationKey("press.calibration", "0.2.0", "0.2"),
                new RecipeEvaluationKey("press.flashcards", "0.2.0", "0.2"),
            ],
            routes.Select(route => route.Key));
        AssertHistorical("lesson-loom", nameof(LessonLoomFixtureTests),
            LessonLoomFixtureTests.Fixtures.Select(fixture => fixture.Id));
        AssertHistorical("board-to-brief", nameof(BoardToBriefFixtureTests),
            BoardToBriefFixtureTests.Fixtures.Select(fixture => fixture.Id));
        AssertHistorical("source-lens", nameof(SourceLensFixtureTests),
            SourceLensFixtureTests.Fixtures.Select(fixture => fixture.Id));

        foreach (var corpus in routes.Where(route => route.Key.RecipeVersion == "0.2.0"
            && !route.Key.RecipeId.StartsWith("press.", StringComparison.Ordinal)))
        {
            Assert.Equal(corpus.Key.RecipeId == "source-lens" ? 8 : 7, corpus.Cases.Count);
            Assert.StartsWith(nameof(ReplacementModuleReviewCorpus) + ".", corpus.Source, StringComparison.Ordinal);
            Assert.Contains(corpus.Cases, item => item.Id == "review-unchanged");
            Assert.Contains(corpus.Cases, item => item.Id == "review-edit-after-approval-needs-fresh-acknowledgement");
            Assert.Contains(corpus.Cases, item => item.Id.StartsWith("refusal-", StringComparison.Ordinal));
            Assert.Same(corpus, RecipeEvaluationDispatcher.Resolve(corpus.Key));
            Assert.Empty(corpus.Cases.Select(item => item.Id).Intersect(
                RecipeEvaluationDispatcher.Resolve(new(corpus.Key.RecipeId, "0.1.0", "0.1"))
                    .Cases.Select(item => item.Id), StringComparer.Ordinal));
        }

        foreach (var corpus in routes.Where(route => route.Key.RecipeId.StartsWith("press.", StringComparison.Ordinal)))
        {
            Assert.Equal(corpus.Key.RecipeId switch
            {
                "press.charts" => 4,
                "press.learner-held" => 6,
                "press.calibration" => 5,
                "press.flashcards" => 4,
                _ => throw new InvalidOperationException("Unexpected press evaluation tuple."),
            }, corpus.Cases.Count);
            Assert.StartsWith(nameof(ReplacementPressReviewCorpus) + ".", corpus.Source, StringComparison.Ordinal);
            Assert.Same(corpus, RecipeEvaluationDispatcher.Resolve(corpus.Key));
            Assert.Throws<ArgumentException>(() => RecipeEvaluationDispatcher.Resolve(new(corpus.Key.RecipeId, "0.1.0", "0.1")));
        }
    }

    [Theory]
    [MemberData(nameof(ReplacementCases))]
    public Task Exact_replacement_tuple_executes_its_declared_review_control(
        string recipeId,
        string recipeVersion,
        string evaluationVersion,
        string caseId)
        => RecipeEvaluationDispatcher.RunAsync(new(recipeId, recipeVersion, evaluationVersion), caseId);

    [Theory]
    [InlineData("lesson-loom", "0.1.0", "0.2")]
    [InlineData("lesson-loom", "0.2.0", "0.1")]
    [InlineData("board-to-brief", "0.1.0", "0.2")]
    [InlineData("board-to-brief", "0.2.0", "0.1")]
    [InlineData("source-lens", "0.1.0", "0.2")]
    [InlineData("source-lens", "0.2.0", "0.1")]
    [InlineData("source-lens", "0.3.0", "0.2")]
    [InlineData("source-lens", "0.2", "0.2")]
    [InlineData("source-lens", "latest", "0.2")]
    [InlineData("source-lens", " 0.2.0", "0.2")]
    [InlineData("source-lens", "0.2.0 ", "0.2")]
    [InlineData("source-lens", "0.2.0", "0.2 ")]
    [InlineData("source-lens", "0.2.0", "0.20")]
    [InlineData("SOURCE-LENS", "0.2.0", "0.2")]
    [InlineData("source-lens ", "0.2.0", "0.2")]
    [InlineData("family-bridge", "0.2.0", "0.2")]
    [InlineData("unknown", "0.2.0", "0.2")]
    [InlineData("press.charts", "0.1.0", "0.1")]
    [InlineData("press.learner-held", "0.1.0", "0.1")]
    [InlineData("press.calibration", "0.1.0", "0.1")]
    [InlineData("press.flashcards", "0.1.0", "0.1")]
    [InlineData("press.charts", "0.2.0", "0.1")]
    [InlineData("press.learner-held", "0.2.0", "0.1")]
    [InlineData("press.calibration", "0.2.0", "0.1")]
    [InlineData("press.flashcards", "0.2.0", "0.1")]
    [InlineData("press.charts", "0.3.0", "0.2")]
    [InlineData("bar-chart", "0.2.0", "0.2")]
    [InlineData(null, "0.2.0", "0.2")]
    [InlineData("source-lens", null, "0.2")]
    [InlineData("source-lens", "0.2.0", null)]
    public void Unknown_or_mismatched_tuples_have_no_fallback(
        string? recipeId,
        string? recipeVersion,
        string? evaluationVersion)
        => Assert.Throws<ArgumentException>(() => RecipeEvaluationDispatcher.Resolve(
            new(recipeId!, recipeVersion!, evaluationVersion!)));

    [Theory]
    [InlineData("lesson-loom")]
    [InlineData("board-to-brief")]
    [InlineData("source-lens")]
    [InlineData("press.charts")]
    [InlineData("press.learner-held")]
    [InlineData("press.calibration")]
    [InlineData("press.flashcards")]
    public void Declared_execution_manifest_must_match_all_three_tuple_fields(string recipeId)
    {
        var key = new RecipeEvaluationKey(recipeId, "0.2.0", "0.2");
        var manifest = recipeId.StartsWith("press.", StringComparison.Ordinal)
            ? DeterministicPressRecipes.AllVersions.Single(recipe => recipe.Id == recipeId && recipe.Version == "0.2.0")
            : ModuleStudioCatalog.ByModeKey(recipeId, "0.2.0").Recipe;
        RecipeEvaluationDispatcher.RequireManifest(key, manifest);
        Assert.Throws<ArgumentException>(() => RecipeEvaluationDispatcher.RequireManifest(key,
            manifest with { Id = "different-recipe" }));
        Assert.Throws<ArgumentException>(() => RecipeEvaluationDispatcher.RequireManifest(key,
            manifest with { Version = "0.1.0" }));
        Assert.Throws<ArgumentException>(() => RecipeEvaluationDispatcher.RequireManifest(key,
            manifest with { EvaluationSuiteVersion = "0.1" }));
    }

    [Theory]
    [InlineData("lesson-loom")]
    [InlineData("board-to-brief")]
    [InlineData("source-lens")]
    public async Task A_case_cannot_be_borrowed_from_another_version_or_an_unknown_position(string recipeId)
    {
        var historical = RecipeEvaluationDispatcher.Resolve(new(recipeId, "0.1.0", "0.1"));
        var replacement = RecipeEvaluationDispatcher.Resolve(new(recipeId, "0.2.0", "0.2"));
        await Assert.ThrowsAsync<ArgumentException>(() => replacement.RunAsync(historical.Cases[0].Id));
        await Assert.ThrowsAsync<ArgumentException>(() => historical.RunAsync(replacement.Cases[0].Id));
        await Assert.ThrowsAsync<ArgumentException>(() => replacement.RunAsync("REVIEW-UNCHANGED"));
        await Assert.ThrowsAsync<ArgumentException>(() => replacement.RunAsync("review-unchanged "));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => historical.RunAsync(-1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => historical.RunAsync(36));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => replacement.RunAsync(replacement.Cases.Count));
    }

    private static void AssertHistorical(string recipeId, string source, IEnumerable<string> expectedIds)
    {
        var corpus = RecipeEvaluationDispatcher.Resolve(new(recipeId, "0.1.0", "0.1"));
        Assert.Equal(source, corpus.Source);
        Assert.Equal(36, corpus.Cases.Count);
        Assert.Equal(expectedIds, corpus.Cases.Select(item => item.Id));
    }
}
