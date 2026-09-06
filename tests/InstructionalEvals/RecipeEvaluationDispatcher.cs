// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.DeterministicPress;

namespace Foundry.Tests.InstructionalEvals;

/// <summary>
/// Test-harness identity, not a project migration or admission record. No part
/// of the tuple is normalized, inferred from the engine, or treated as latest.
/// </summary>
internal readonly record struct RecipeEvaluationKey(string RecipeId, string RecipeVersion, string EvaluationVersion);

internal sealed record RecipeEvaluationCase(string Id, Func<Task> EvaluateAsync);

internal sealed class RecipeEvaluationCorpus
{
    internal RecipeEvaluationCorpus(
        RecipeEvaluationKey key,
        string source,
        IEnumerable<RecipeEvaluationCase> cases)
    {
        Key = key;
        Source = source;
        var snapshot = cases.ToArray();
        if (snapshot.Length == 0
            || snapshot.Any(item => string.IsNullOrWhiteSpace(item.Id) || item.EvaluateAsync is null)
            || snapshot.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("An evaluation corpus needs distinct named executable cases.", nameof(cases));
        }

        Cases = Array.AsReadOnly(snapshot);
    }

    internal RecipeEvaluationKey Key { get; }
    internal string Source { get; }
    internal IReadOnlyList<RecipeEvaluationCase> Cases { get; }

    internal Task RunAsync(int caseIndex)
    {
        if ((uint)caseIndex >= (uint)Cases.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(caseIndex), caseIndex, "No such case exists in this exact evaluation corpus.");
        }

        return Cases[caseIndex].EvaluateAsync();
    }

    internal Task RunAsync(string caseId)
        => (Cases.SingleOrDefault(item => string.Equals(item.Id, caseId, StringComparison.Ordinal))
            ?? throw new ArgumentException("No such case exists in this exact evaluation corpus.", nameof(caseId)))
            .EvaluateAsync();
}

/// <summary>
/// Executing evaluation dispatch for the seven authorized replacement families.
/// Original 36-case inputs and assertions stay with 0.1.0/eval 0.1; replacement
/// review controls are distinct executable corpora, not renamed old cases.
/// This scoped inventory does not claim to cover every Foundry recipe.
/// Press 0.1 routes are not fabricated here; their separate C1 comparison
/// harness remains distinct from these new press evaluation corpora.
/// </summary>
internal static class RecipeEvaluationDispatcher
{
    internal static IReadOnlyList<RecipeEvaluationCorpus> All { get; } = Array.AsReadOnly(
    [
        Historical("lesson-loom", nameof(LessonLoomFixtureTests),
            LessonLoomFixtureTests.Fixtures.Select(fixture => fixture.Id),
            LessonLoomFixtureTests.EvaluateHistoricalFixtureAsync),
        Historical("board-to-brief", nameof(BoardToBriefFixtureTests),
            BoardToBriefFixtureTests.Fixtures.Select(fixture => fixture.Id),
            BoardToBriefFixtureTests.EvaluateHistoricalFixtureAsync),
        Historical("source-lens", nameof(SourceLensFixtureTests),
            SourceLensFixtureTests.Fixtures.Select(fixture => fixture.Id),
            SourceLensFixtureTests.EvaluateHistoricalFixtureAsync),
        ReplacementModuleReviewCorpus.LessonLoom(),
        ReplacementModuleReviewCorpus.BoardToBrief(),
        ReplacementModuleReviewCorpus.SourceLens(),
        ReplacementPressReviewCorpus.Charts(),
        ReplacementPressReviewCorpus.LearnerHeld(),
        ReplacementPressReviewCorpus.Calibration(),
        ReplacementPressReviewCorpus.Flashcards(),
    ]);

    internal static RecipeEvaluationCorpus Resolve(RecipeEvaluationKey key)
    {
        var corpus = All.SingleOrDefault(candidate => candidate.Key == key)
            ?? throw new ArgumentException("No executable corpus is registered for this exact recipe/version/evaluation tuple.", nameof(key));
        var definitionId = key.RecipeId switch
        {
            "press.charts" => "bar-chart",
            "press.learner-held" => "goal-post",
            "press.calibration" => "calibration-proof",
            "press.flashcards" => "flashcards",
            _ => null,
        };
        var recipe = definitionId is null
            ? ModuleStudioCatalog.ByModeKey(key.RecipeId, key.RecipeVersion).Recipe
            : PressRoomCatalog.ById(definitionId, key.RecipeVersion).Recipe;
        RequireManifest(key, recipe);
        return corpus;
    }

    internal static void RequireManifest(RecipeEvaluationKey key, RecipeManifest recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (!string.Equals(recipe.Id, key.RecipeId, StringComparison.Ordinal)
            || !string.Equals(recipe.Version, key.RecipeVersion, StringComparison.Ordinal)
            || !string.Equals(recipe.EvaluationSuiteVersion, key.EvaluationVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("The selected manifest does not bind the exact requested evaluation tuple.", nameof(recipe));
        }
    }

    internal static Task RunAsync(RecipeEvaluationKey key, int caseIndex)
        => Resolve(key).RunAsync(caseIndex);

    internal static Task RunAsync(RecipeEvaluationKey key, string caseId)
        => Resolve(key).RunAsync(caseId);

    private static RecipeEvaluationCorpus Historical(
        string recipeId,
        string source,
        IEnumerable<string> caseIds,
        Func<int, Task> evaluate)
        => new(new(recipeId, "0.1.0", "0.1"), source,
            caseIds.Select((id, index) => new RecipeEvaluationCase(id, () => evaluate(index))));
}
