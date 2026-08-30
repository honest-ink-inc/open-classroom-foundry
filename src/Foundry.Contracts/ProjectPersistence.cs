// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Foundry.Domain;

namespace Foundry.Contracts;

/// <summary>
/// Versioned, data-only compatibility assertions for a saved project's
/// validation boundary. A package is mutable and does not authenticate any of
/// these fields. The digest freezes the exact semantic document for accidental
/// corruption and compatibility checks; purpose and notice codes gain no
/// provenance merely by appearing beside that digest. Notice text is therefore
/// never persisted or rehydrated as an engine finding.
/// </summary>
public sealed record ProjectValidationEnvelope(
    int SchemaVersion,
    string Kind,
    string RecipeId,
    string RecipeVersion,
    DataLane Lane,
    ArtifactPurpose Purpose,
    string ArtifactSha256,
    IReadOnlyList<string> UntrustedNoticeCodes)
{
    public const int CurrentSchemaVersion = 1;

    public const string ExactApprovedDocumentKind = "exact-approved-document-sha256";

    public static ProjectValidationEnvelope Exact(
        ApprovedArtifact artifact,
        string recipeId,
        string recipeVersion)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeVersion);

        var issues = artifact.ValidationIssues.Distinct().ToArray();
        if (issues.Any(issue => issue.Severity == ValidationSeverity.Blocking))
        {
            throw new InvalidOperationException(
                "A validation envelope cannot be created while blocking issues remain.");
        }

        var noticeCodes = issues
            .Select(issue => issue.Code)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (noticeCodes.Any(code => !IsStableNoticeCode(code)))
        {
            throw new InvalidOperationException(
                "A validation envelope accepts only bounded stable notice codes, never serialized notice text.");
        }

        return new ProjectValidationEnvelope(
            CurrentSchemaVersion,
            ExactApprovedDocumentKind,
            recipeId,
            recipeVersion,
            artifact.Revision.Lane,
            artifact.Revision.Purpose,
            ArtifactDocumentFingerprint.Compute(artifact.Revision.Document),
            noticeCodes);
    }

    public static bool IsStableNoticeCode(string? code)
        => !string.IsNullOrWhiteSpace(code)
            && code.Length <= 128
            && code.All(character => char.IsAsciiLetterOrDigit(character)
                || character is '.' or '-' or '_');
}

/// <summary>
/// The output choices that were on screen when the exact revision was
/// approved. The document digest prevents an unrelated profile from being
/// attached accidentally. The profile is not a seat assertion or a signature.
/// </summary>
public sealed record ProjectRenderProfile(
    int SchemaVersion,
    string ArtifactSha256,
    RenderAudience Audience,
    double TextScalePercent,
    bool TargetLanguageFirst)
{
    public const int CurrentSchemaVersion = 1;

    public static ProjectRenderProfile For(
        ApprovedArtifact artifact,
        RenderAudience audience = RenderAudience.Learner,
        double textScalePercent = 100,
        bool targetLanguageFirst = false)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (!Enum.IsDefined(audience))
        {
            throw new ArgumentOutOfRangeException(nameof(audience));
        }

        if (!double.IsFinite(textScalePercent)
            || textScalePercent < 100
            || textScalePercent > 200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(textScalePercent),
                "Saved text scale must be between 100 and 200 percent.");
        }

        return new ProjectRenderProfile(
            CurrentSchemaVersion,
            ArtifactDocumentFingerprint.Compute(artifact.Revision.Document),
            audience,
            textScalePercent,
            targetLanguageFirst);
    }

    public RenderRequest Request(RenderTarget target)
        => new(target, Audience, TextScalePercent, TargetLanguageFirst);
}

/// <summary>
/// One canonical semantic-document fingerprint shared by persistence and the
/// rehydrated validator. It hashes typed JSON, never rendered HTML.
/// </summary>
public static class ArtifactDocumentFingerprint
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Compute(ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Convert.ToHexStringLower(
            SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(document, Options)));
    }
}
