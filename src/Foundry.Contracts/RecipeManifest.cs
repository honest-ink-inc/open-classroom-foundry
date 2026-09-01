// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Contracts;

/// <summary>
/// The declarative recipe identity of implementation plan §6.6. Data only —
/// no code, no scripts, no network calls are representable here.
/// </summary>
public sealed record RecipeManifest(
    string Id,
    string Version,
    string License,
    string MinimumEngineVersion,
    string InstructionalPurpose,
    IReadOnlyList<string> ProhibitedPurposes,
    IReadOnlyList<string> AllowedInputKinds,
    DataLane MaximumLane,
    IReadOnlyList<string> RequiredProviderCapabilities,
    string OutputSchemaId,
    IReadOnlyList<string> ValidatorIds,
    string EditorId,
    string RendererId,
    IReadOnlyList<RenderTarget> SupportedExports,
    IReadOnlyList<string> Warnings,
    string EvaluationSuiteVersion)
{
    /// <summary>
    /// Allowlisted local preprocessing identities required before this recipe
    /// executes. An empty list is an explicit declaration that none are used.
    /// </summary>
    public IReadOnlyList<string> LocalPreprocessingIds { get; init; } = [];

    /// <summary>
    /// Recipe-owned localization resource identities. Global application
    /// chrome catalogs are not recipe resources and do not belong here.
    /// </summary>
    public IReadOnlyList<string> LocalizationResourceIds { get; init; } = [];

    /// <summary>
    /// Explicit project-specific migration identities admitted for this recipe.
    /// First-admission recipes correctly declare an empty list.
    /// </summary>
    public IReadOnlyList<string> MigrationIds { get; init; } = [];
}
