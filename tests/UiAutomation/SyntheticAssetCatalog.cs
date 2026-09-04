// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Tests.UiAutomation;

/// <summary>
/// Passive, synthetic image bytes for UI contracts that inspect arbitrary
/// image identities. The catalog exists so Gate B exercises exact bytes and
/// provenance instead of manufacturing approval evidence in the test.
/// </summary>
internal sealed class SyntheticAssetCatalog : IAssetCatalog
{
    private static readonly byte[] Svg = Encoding.UTF8.GetBytes(
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\" role=\"img\" aria-label=\"Synthetic test symbol\"><title>Synthetic test symbol</title><rect x=\"1\" y=\"1\" width=\"8\" height=\"8\" fill=\"none\" stroke=\"black\"/></svg>");
    private readonly IReadOnlyDictionary<AssetId, AssetProvenance> _assets;

    private SyntheticAssetCatalog(IReadOnlyDictionary<AssetId, AssetProvenance> assets)
    {
        _assets = assets;
        All = Array.AsReadOnly(assets.Values.OrderBy(asset => asset.Id.Value, StringComparer.Ordinal).ToArray());
    }

    public IReadOnlyList<AssetProvenance> All { get; }

    public static SyntheticAssetCatalog ForDocument(ArtifactDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sha256 = Convert.ToHexString(SHA256.HashData(Svg));
        var ids = document.Nodes
            .SelectMany(node => node switch
            {
                ImageReference image => new[] { image.Asset },
                StepRow { Symbol: { } symbol } => [symbol.Asset],
                _ => [],
            })
            .Distinct()
            .ToDictionary(
                id => id,
                id => new AssetProvenance(
                    id,
                    $"synthetic:{id.Value}",
                    "1.0.0",
                    "synthetic-test-symbol.svg",
                    "image/svg+xml",
                    "Generated in-memory UI test fixture",
                    "Foundry test suite",
                    "LicenseRef-test-only",
                    sha256,
                    "A synthetic bounded shape",
                    "Synthetic test symbol",
                    Redistributable: false));
        return new SyntheticAssetCatalog(ids);
    }

    public AssetProvenance? Find(AssetId id)
        => _assets.GetValueOrDefault(id);

    public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
    {
        if (_assets.ContainsKey(id))
        {
            content = Svg.ToArray();
            mimeType = "image/svg+xml";
            return true;
        }

        content = default;
        mimeType = string.Empty;
        return false;
    }
}
