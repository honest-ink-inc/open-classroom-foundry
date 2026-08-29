// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Contracts;

/// <summary>The manifest of an .ocfproj package (plan §6.5, ADR-003).</summary>
public sealed record ProjectManifest(
    string SchemaVersion,
    Guid ProjectId,
    string ModuleId,
    string ModuleVersion,
    string RecipeId,
    string RecipeVersion,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ModifiedUtc,
    DataLane DataLane,
    string RetentionMode,
    string? SourceLocale,
    string? OutputLocale,
    string EngineVersion,
    string ArtifactPath,
    IReadOnlyList<string> AssetIds);
