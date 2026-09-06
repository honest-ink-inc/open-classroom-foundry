// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Modules.BuiltIn.BoardToBrief;
using Foundry.Modules.BuiltIn.LessonLoom;
using Foundry.Modules.BuiltIn.SourceLens;

namespace Foundry.Modules.BuiltIn;

/// <summary>
/// The three authorized built-in replacement candidates. These declarations
/// do not constitute final admission, schema ratification or a project migration.
/// Outgoing manifests remain independent of the current engine constant.
/// </summary>
public static class BuiltInReplacementRecipes
{
    public static RecipeManifest BoardToBrief { get; } = Replacement(BoardToBriefBuilder.Recipe);
    public static RecipeManifest LessonLoom { get; } = Replacement(LessonLoomBuilder.Recipe);
    public static RecipeManifest SourceLens { get; } = Replacement(SourceLensBuilder.Recipe);

    private static RecipeManifest Replacement(RecipeManifest outgoing) => outgoing with
    {
        Version = "0.2.0",
        MinimumEngineVersion = "0.8.0-alpha",
        EvaluationSuiteVersion = "0.2",
    };
}
