// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Rendering;

/// <summary>
/// Read-only package-verification boundary for deterministic learner snapshots.
/// It answers exact-correspondence questions but never returns rendered bytes.
/// Compatibility preparation preserves an admitted historical snapshot instead
/// of turning package validation into a draft-rendering capability.
/// </summary>
public static class PortableProjectSnapshot
{
    public static bool IsAdmittedRendererVersion(string writerEngineVersion)
        => string.Equals(writerEngineVersion, EngineIdentity.EngineVersion, StringComparison.Ordinal)
            || string.Equals(
                writerEngineVersion,
                LegacyPortableSnapshotRenderer.EngineVersion,
                StringComparison.Ordinal);

    public static bool MatchesExact(
        ArtifactDocument document,
        string writerEngineVersion,
        bool hasPersistedContext,
        RenderRequest request,
        ReadOnlySpan<byte> candidate)
    {
        ValidateArguments(document, writerEngineVersion, request);
        var legacy = string.Equals(
            writerEngineVersion,
            LegacyPortableSnapshotRenderer.EngineVersion,
            StringComparison.Ordinal);
        if (legacy
            && hasPersistedContext
                && (request.TextScalePercent != 100 || request.TargetLanguageFirst))
        {
            return false;
        }

        var expected = legacy
            ? LegacyPortableSnapshotRenderer.RenderV010Dev(document)
            : AccessibleHtmlRenderer.RenderPortableSnapshot(document, request);
        return candidate.SequenceEqual(expected);
    }

    private static void ValidateArguments(
        ArtifactDocument document,
        string writerEngineVersion,
        RenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(writerEngineVersion);
        ArgumentNullException.ThrowIfNull(request);
        if (!IsAdmittedRendererVersion(writerEngineVersion))
        {
            throw new NotSupportedException(
                "The portable snapshot names no admitted exact renderer version.");
        }

        if (request.Target != RenderTarget.AccessibleHtml
            || request.Audience != RenderAudience.Learner)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "A portable project snapshot is always learner-audience accessible HTML.");
        }
    }
}
