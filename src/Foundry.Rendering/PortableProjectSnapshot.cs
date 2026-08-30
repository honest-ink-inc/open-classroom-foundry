// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Rendering;

/// <summary>
/// Narrow package-verification boundary for deterministic learner snapshots.
/// It never renders arbitrary draft content for a caller. The only byte-returning
/// operation first requires an already-held snapshot to match the same document
/// through an admitted exact renderer, then rewrites that verified derivative
/// through the current core for managed compatibility preparation.
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
        var expected = !hasPersistedContext
            && string.Equals(
                writerEngineVersion,
                LegacyPortableSnapshotRenderer.EngineVersion,
                StringComparison.Ordinal)
            ? LegacyPortableSnapshotRenderer.RenderV010Dev(document)
            : AccessibleHtmlRenderer.RenderPortableSnapshot(document, request);
        return candidate.SequenceEqual(expected);
    }

    public static byte[] RewriteVerifiedForCurrent(
        ArtifactDocument document,
        string writerEngineVersion,
        bool hasPersistedContext,
        RenderRequest request,
        ReadOnlySpan<byte> existingSnapshot)
    {
        if (!MatchesExact(
            document,
            writerEngineVersion,
            hasPersistedContext,
            request,
            existingSnapshot))
        {
            throw new InvalidOperationException(
                "A portable snapshot can be rewritten only after exact semantic correspondence is established.");
        }

        return AccessibleHtmlRenderer.RenderPortableSnapshot(document, request);
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
