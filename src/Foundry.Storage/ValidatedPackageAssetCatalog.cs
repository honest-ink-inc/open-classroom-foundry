// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Storage;

/// <summary>
/// Immutable asset bytes recovered from one package only after the hostile
/// package validator has checked topology, provenance, bounds, and SHA-256.
/// Reopened projects carry this catalog so rendering and a deliberate re-save
/// never depend on a mutable installed pack having the same IDs.
/// </summary>
internal sealed class ValidatedPackageAssetCatalog : IAssetCatalog
{
    private readonly Dictionary<AssetId, (AssetProvenance Provenance, byte[] Content)> _assets;

    internal ValidatedPackageAssetCatalog(
        IEnumerable<(AssetProvenance Provenance, byte[] Content)> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _assets = assets.ToDictionary(
            asset => asset.Provenance.Id,
            asset => (asset.Provenance, asset.Content.ToArray()));
    }

    public IReadOnlyList<AssetProvenance> All
        => [.. _assets.Values.Select(asset => asset.Provenance)];

    public AssetProvenance? Find(AssetId id)
        => _assets.TryGetValue(id, out var asset) ? asset.Provenance : null;

    public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
    {
        if (_assets.TryGetValue(id, out var asset))
        {
            content = asset.Content;
            mimeType = asset.Provenance.MimeType;
            return true;
        }

        content = default;
        mimeType = string.Empty;
        return false;
    }
}
