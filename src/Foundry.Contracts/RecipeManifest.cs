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
    string EvaluationSuiteVersion);
