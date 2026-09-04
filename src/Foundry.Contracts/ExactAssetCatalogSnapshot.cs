// SPDX-License-Identifier: GPL-3.0-or-later
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Foundry.Domain;

namespace Foundry.Contracts;

/// <summary>
/// Owns the exact referenced asset bytes observed at one trust boundary. Review
/// uses the frozen catalog for Gate B and attaches its bindings to approval;
/// output and storage recapture caller-supplied catalogs and require equality
/// before any bytes leave the process.
/// </summary>
public sealed class ExactAssetCatalogSnapshot : IAssetCatalog
{
    private readonly IReadOnlyDictionary<AssetId, FrozenAsset> _assets;

    private ExactAssetCatalogSnapshot(
        IReadOnlyDictionary<AssetId, FrozenAsset> assets,
        IReadOnlyList<AssetProvenance> provenance,
        IReadOnlyList<ApprovedAssetBinding> bindings)
    {
        _assets = assets;
        All = provenance;
        Bindings = bindings;
    }

    public IReadOnlyList<AssetProvenance> All { get; }

    public IReadOnlyList<ApprovedAssetBinding> Bindings { get; }

    public static ExactAssetCatalogSnapshot CaptureForReview(
        ArtifactDocument document,
        IAssetCatalog? catalog)
        => Capture(document, catalog, "Gate B review");

    public static ExactAssetCatalogSnapshot CaptureForApprovedOutput(
        ApprovedArtifact artifact,
        IAssetCatalog? catalog)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var snapshot = Capture(artifact.Revision.Document, catalog, "approved output");
        if (!artifact.AssetBindings.SequenceEqual(snapshot.Bindings))
        {
            throw new InvalidOperationException(
                "Approved output refused: the supplied catalog does not contain the exact asset bytes, MIME types, and provenance reviewed at Gate B.");
        }

        return snapshot;
    }

    public AssetProvenance? Find(AssetId id)
        => _assets.TryGetValue(id, out var asset) ? asset.Provenance : null;

    public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
    {
        if (_assets.TryGetValue(id, out var asset))
        {
            content = asset.Content.ToArray();
            mimeType = asset.Provenance.MimeType;
            return true;
        }

        content = default;
        mimeType = string.Empty;
        return false;
    }

    private static ExactAssetCatalogSnapshot Capture(
        ArtifactDocument document,
        IAssetCatalog? catalog,
        string operation)
    {
        ArgumentNullException.ThrowIfNull(document);
        var ids = ReferencedAssetIds(document);
        if (ids.Length > 0 && catalog is null)
        {
            throw new InvalidOperationException(
                $"{operation} refused: every referenced image requires an exact local asset catalog; a placeholder is not review evidence.");
        }

        var assets = new Dictionary<AssetId, FrozenAsset>();
        var provenance = new List<AssetProvenance>(ids.Length);
        var bindings = new List<ApprovedAssetBinding>(ids.Length);
        foreach (var id in ids)
        {
            var record = catalog!.Find(id)
                ?? throw new InvalidOperationException(
                    $"{operation} refused: asset '{id.Value}' has no provenance.");
            if (record.Id != id)
            {
                throw new InvalidOperationException(
                    $"{operation} refused: asset '{id.Value}' returned provenance for a different identity.");
            }

            if (!catalog.TryGetContent(id, out var content, out var mimeType))
            {
                throw new InvalidOperationException(
                    $"{operation} refused: asset '{id.Value}' has no retrievable content.");
            }

            var ownedContent = content.ToArray();
            if (ownedContent.Length == 0
                || string.IsNullOrWhiteSpace(mimeType)
                || !string.Equals(mimeType, record.MimeType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{operation} refused: asset '{id.Value}' does not satisfy its MIME/content contract.");
            }

            var actualHash = Convert.ToHexString(SHA256.HashData(ownedContent));
            if (!actualHash.Equals(record.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{operation} refused: asset '{id.Value}' does not match its recorded SHA-256.");
            }

            assets.Add(id, new FrozenAsset(record, ownedContent));
            provenance.Add(record);
            bindings.Add(new ApprovedAssetBinding(
                id,
                actualHash,
                mimeType,
                AssetProvenanceFingerprint.Compute(record)));
        }

        return new ExactAssetCatalogSnapshot(
            assets,
            Array.AsReadOnly(provenance.ToArray()),
            Array.AsReadOnly(bindings.ToArray()));
    }

    private static AssetId[] ReferencedAssetIds(ArtifactDocument document)
        => [.. document.Nodes
            .SelectMany(node => node switch
            {
                ImageReference image => new[] { image.Asset },
                StepRow { Symbol: { } symbol } => [symbol.Asset],
                _ => [],
            })
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)];

    private sealed record FrozenAsset(AssetProvenance Provenance, byte[] Content);
}

/// <summary>
/// Versioned, length-framed digest of the complete schema-1 provenance record.
/// Field boundaries and nulls are explicit, so independent strings cannot
/// regroup into the same input and rights changes always change the digest.
/// </summary>
public static class AssetProvenanceFingerprint
{
    private const string ContractVersion = "asset-provenance-fingerprint-v1";
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static string Compute(AssetProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, ContractVersion);
        Append(hash, provenance.Id.Value);
        Append(hash, provenance.ConceptId);
        Append(hash, provenance.Version);
        Append(hash, provenance.FileName);
        Append(hash, provenance.MimeType);
        Append(hash, provenance.Source);
        Append(hash, provenance.Creator);
        Append(hash, provenance.License);
        Append(hash, provenance.Sha256);
        Append(hash, provenance.IntendedMeaning);
        Append(hash, provenance.AltText);
        Append(hash, provenance.Redistributable ? "true" : "false");
        Append(hash, provenance.AmbiguityNotes);
        Append(hash, provenance.RequiredAttribution);
        Append(hash, provenance.Modifications);
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void Append(IncrementalHash hash, string? value)
    {
        Span<byte> length = stackalloc byte[4];
        if (value is null)
        {
            BinaryPrimitives.WriteInt32BigEndian(length, -1);
            hash.AppendData(length);
            return;
        }

        var bytes = StrictUtf8.GetBytes(value);
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
