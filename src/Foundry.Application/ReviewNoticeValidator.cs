// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Application;

/// <summary>
/// Keeps recipe and transformation notices inside Gate B instead of on a
/// disposable pre-review surface. Required notices must be acknowledged in
/// every fresh review, survive document edits, and persist in project envelopes.
/// </summary>
public sealed class ReviewNoticeValidator : IArtifactValidator
{
    private readonly IArtifactValidator _inner;
    private readonly IReadOnlyList<ValidationIssue> _notices;

    public ReviewNoticeValidator(
        IArtifactValidator inner,
        IReadOnlyList<ValidationIssue> notices)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        ArgumentNullException.ThrowIfNull(notices);
        if (notices.Any(issue => issue.Severity == ValidationSeverity.Blocking))
        {
            throw new ArgumentException(
                "Persistent review notices cannot introduce a hidden blocking issue.",
                nameof(notices));
        }

        _notices = [.. notices.Distinct()];
    }

    public IReadOnlyList<ValidationIssue> Validate(ArtifactDocument document)
        => [.. _inner.Validate(document).Concat(_notices).Distinct()];

    public static IReadOnlyList<ValidationIssue> RequiredRecipeWarnings(
        RecipeManifest recipe,
        IReadOnlyList<string>? transformationReport = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var issues = recipe.Warnings.Select((message, index) => ValidationIssue.Warning(
            $"recipe.warning.{index + 1}",
            message,
            requiresAcknowledgement: true));
        var transformations = (transformationReport ?? []).Select((message, index) => ValidationIssue.Warning(
            $"recipe.transformation.{index + 1}",
            message,
            requiresAcknowledgement: true));
        return [.. issues.Concat(transformations).Distinct()];
    }
}
