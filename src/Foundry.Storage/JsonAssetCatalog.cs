// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Storage;

internal static class StorageJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };
}

/// <summary>
/// A directory-backed asset catalog: a manifest.json of <see cref="AssetProvenance"/>
/// records beside the asset files they describe. <see cref="VerifyIntegrity"/>
/// recomputes every SHA-256 — a file that drifts from its recorded provenance is a
/// blocking issue, which is how "CI fails if a shipped file lacks provenance"
/// (plan §9) becomes a test rather than a promise.
/// </summary>
public sealed class JsonAssetCatalog : IAssetCatalog
{
    public const string ManifestFileName = "manifest.json";

    private readonly string _directory;
    private readonly Dictionary<AssetId, AssetProvenance> _assets;

    public JsonAssetCatalog(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;

        var manifestPath = Path.Combine(directory, ManifestFileName);
        var records = JsonSerializer.Deserialize<List<AssetProvenance>>(File.ReadAllText(manifestPath), StorageJson.Options)
            ?? throw new InvalidOperationException($"The asset manifest at {manifestPath} is empty.");

        _assets = records.ToDictionary(r => r.Id);
    }

    public IReadOnlyList<AssetProvenance> All => [.. _assets.Values];

    public AssetProvenance? Find(AssetId id)
        => _assets.TryGetValue(id, out var provenance) ? provenance : null;

    public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
    {
        if (_assets.TryGetValue(id, out var provenance))
        {
            var path = Path.Combine(_directory, provenance.FileName);
            if (File.Exists(path))
            {
                content = File.ReadAllBytes(path);
                mimeType = provenance.MimeType;
                return true;
            }
        }

        content = default;
        mimeType = string.Empty;
        return false;
    }

    public IReadOnlyList<ValidationIssue> VerifyIntegrity()
    {
        var issues = new List<ValidationIssue>();

        foreach (var provenance in _assets.Values)
        {
            var path = Path.Combine(_directory, provenance.FileName);
            if (!File.Exists(path))
            {
                issues.Add(ValidationIssue.Blocking("asset.missing-file", $"Asset {provenance.Id.Value} has provenance but no file '{provenance.FileName}'."));
                continue;
            }

            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            if (!actual.Equals(provenance.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(ValidationIssue.Blocking("asset.hash-mismatch", $"Asset {provenance.Id.Value} does not match its recorded SHA-256."));
            }

            if (string.IsNullOrWhiteSpace(provenance.License))
            {
                issues.Add(ValidationIssue.Blocking("asset.unknown-rights", $"Asset {provenance.Id.Value} has no license; unknown rights block distribution."));
            }

            if (string.IsNullOrWhiteSpace(provenance.AltText))
            {
                issues.Add(ValidationIssue.Blocking("asset.alt-text", $"Asset {provenance.Id.Value} has no alternative text."));
            }
        }

        return issues;
    }
}
