// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;

namespace Foundry.Storage;

internal static class StorageJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // System.Text.Json otherwise follows Environment.NewLine, which makes
        // durable storage bytes differ between the Windows and Linux writers.
        NewLine = "\r\n",
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

    private static readonly Dictionary<string, string> ExtensionsByMime = new(StringComparer.Ordinal)
    {
        ["image/svg+xml"] = ".svg",
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
    };

    private readonly string _directory;
    private readonly Dictionary<AssetId, AssetProvenance> _assets;

    public JsonAssetCatalog(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));

        var manifestPath = Path.Combine(_directory, ManifestFileName);
        var (records, manifestBytes) = AssetManifestReader.Read(manifestPath, "asset manifest");
        try
        {
            ManifestSha256 = Convert.ToHexString(SHA256.HashData(manifestBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(manifestBytes);
        }

        _assets = [];
        foreach (var provenance in records)
        {
            if (provenance is null || string.IsNullOrWhiteSpace(provenance.Id.Value))
            {
                throw new InvalidDataException("The asset manifest contains an invalid asset identity.");
            }

            if (!_assets.TryAdd(provenance.Id, provenance))
            {
                throw new InvalidDataException("The asset manifest contains a duplicate asset identity.");
            }
        }
    }

    public IReadOnlyList<AssetProvenance> All => [.. _assets.Values];

    /// <summary>
    /// SHA-256 of the exact bounded manifest bytes parsed by this instance. This
    /// is an identity value, not a rights, AAC/SLP, accessibility, or source
    /// review assertion.
    /// </summary>
    public string ManifestSha256 { get; }

    public AssetProvenance? Find(AssetId id)
        => _assets.TryGetValue(id, out var provenance) ? provenance : null;

    public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
    {
        if (_assets.TryGetValue(id, out var provenance)
            && TryReadVerifiedContent(provenance, out var bytes))
        {
            content = bytes;
            mimeType = provenance.MimeType;
            return true;
        }

        content = default;
        mimeType = string.Empty;
        return false;
    }

    public IReadOnlyList<ValidationIssue> VerifyIntegrity()
    {
        var issues = new List<ValidationIssue>();
        var fileNames = new HashSet<string>(AssetFileSafety.FileNameComparer)
        {
            ManifestFileName,
        };

        foreach (var provenance in _assets.Values.OrderBy(asset => asset.Id.Value, StringComparer.Ordinal))
        {
            var id = provenance.Id.Value;
            if (!AssetRightsPolicy.HasCompleteRequiredMetadata(provenance))
            {
                issues.Add(ValidationIssue.Blocking(
                    "asset.incomplete-provenance",
                    $"Asset {id} has incomplete required provenance."));
            }

            if (string.IsNullOrWhiteSpace(provenance.License))
            {
                issues.Add(ValidationIssue.Blocking("asset.unknown-rights", $"Asset {id} has no license; unknown rights block distribution."));
            }
            else if (!AssetRightsPolicy.CanEnterOpenCatalog(provenance))
            {
                issues.Add(ValidationIssue.Blocking(
                    "asset.redistribution-rights",
                    $"Asset {id} does not carry a known-open license with redistribution enabled."));
            }

            if (string.IsNullOrWhiteSpace(provenance.AltText))
            {
                issues.Add(ValidationIssue.Blocking("asset.alt-text", $"Asset {id} has no alternative text."));
            }

            if (!AssetRightsPolicy.HasSafeOptionalMetadata(provenance))
            {
                issues.Add(ValidationIssue.Blocking(
                    "asset.invalid-optional-provenance",
                    $"Asset {id} has oversized or control-bearing optional provenance."));
            }

            if (!AssetFileSafety.TryResolveLeaf(_directory, provenance.FileName, out var path))
            {
                issues.Add(ValidationIssue.Blocking("asset.invalid-file-name", $"Asset {id} has an unsafe asset filename."));
                continue;
            }

            if (!fileNames.Add(provenance.FileName))
            {
                issues.Add(ValidationIssue.Blocking("asset.file-name-collision", $"Asset {id} collides with another catalog filename."));
            }

            if (string.IsNullOrWhiteSpace(provenance.MimeType)
                || !ExtensionsByMime.TryGetValue(provenance.MimeType, out var expectedExtension)
                || !Path.GetExtension(provenance.FileName).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(ValidationIssue.Blocking("asset.media-type", $"Asset {id} has an unsupported or mismatched media type."));
            }

            if (!AssetFileSafety.IsSha256(provenance.Sha256))
            {
                issues.Add(ValidationIssue.Blocking("asset.invalid-hash", $"Asset {id} has no valid SHA-256 provenance."));
                continue;
            }

            if (!File.Exists(path))
            {
                issues.Add(ValidationIssue.Blocking("asset.missing-file", $"Asset {id} has provenance but no file '{provenance.FileName}'."));
                continue;
            }

            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                {
                    issues.Add(ValidationIssue.Blocking("asset.reparse-file", $"Asset {id} resolves through a reparse point."));
                    continue;
                }

                var bytes = AssetFileSafety.ReadBoundedRegularFile(path);
                if (!AssetFileSafety.MatchesSha256(bytes, provenance.Sha256))
                {
                    issues.Add(ValidationIssue.Blocking("asset.hash-mismatch", $"Asset {id} does not match its recorded SHA-256."));
                }
                else if (!AccessibleHtmlRenderer.IsSupportedSelfContainedImage(bytes, provenance.MimeType))
                {
                    issues.Add(ValidationIssue.Blocking("asset.unsafe-content", $"Asset {id} is not a supported, self-contained image."));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(ValidationIssue.Blocking("asset.unreadable-file", $"Asset {id} could not be read for integrity validation."));
            }
            catch (InvalidDataException)
            {
                issues.Add(ValidationIssue.Blocking("asset.size-limit", $"Asset {id} is empty, oversized, or changed while read."));
            }

        }

        return issues;
    }

    /// <summary>
    /// Closes the on-disk root used for the application's current shipped
    /// symbol deployment. Generic symbol packs may carry separate sidecars such
    /// as ATTRIBUTIONS.md and therefore deliberately do not use this check.
    /// Passing this check proves only that no unlisted entry rides beside this
    /// manifest; it grants no protected review or candidate-source admission.
    /// </summary>
    public IReadOnlyList<ValidationIssue> VerifyClosedDeploymentRoot()
    {
        var issues = new List<ValidationIssue>();
        var expectedNames = new HashSet<string>(AssetFileSafety.FileNameComparer)
        {
            ManifestFileName,
        };

        foreach (var provenance in _assets.Values)
        {
            if (AssetFileSafety.TryResolveLeaf(_directory, provenance.FileName, out _))
            {
                expectedNames.Add(provenance.FileName);
            }
        }

        try
        {
            var seenNames = new HashSet<string>(AssetFileSafety.FileNameComparer);
            foreach (var entry in Directory.EnumerateFileSystemEntries(
                _directory,
                "*",
                SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(entry);
                if (!seenNames.Add(name) || !expectedNames.Contains(name))
                {
                    issues.Add(ValidationIssue.Blocking(
                        "asset.unexpected-deployment-entry",
                        "The shipped symbol catalog contains an entry outside its closed deployment inventory."));
                    continue;
                }

                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    issues.Add(ValidationIssue.Blocking(
                        "asset.invalid-deployment-entry-type",
                        "The shipped symbol catalog contains a directory where its closed deployment inventory requires files."));
                }
                else if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    issues.Add(ValidationIssue.Blocking(
                        "asset.reparse-deployment-entry",
                        "The shipped symbol catalog contains a reparse entry in its closed deployment inventory."));
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(ValidationIssue.Blocking(
                "asset.unreadable-deployment-inventory",
                "The shipped symbol catalog inventory could not be read safely."));
        }

        return issues;
    }

    private bool TryReadVerifiedContent(AssetProvenance provenance, out byte[] content)
    {
        content = [];
        if (!AssetRightsPolicy.HasCompleteRequiredMetadata(provenance)
            || !AssetRightsPolicy.HasSafeOptionalMetadata(provenance)
            || !AssetRightsPolicy.CanEnterOpenCatalog(provenance)
            || !AssetFileSafety.TryResolveLeaf(_directory, provenance.FileName, out var path)
            || !AssetFileSafety.IsSha256(provenance.Sha256)
            || string.IsNullOrWhiteSpace(provenance.MimeType)
            || !ExtensionsByMime.TryGetValue(provenance.MimeType, out var expectedExtension)
            || !Path.GetExtension(provenance.FileName).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            var bytes = AssetFileSafety.ReadBoundedRegularFile(path);
            if (!AssetFileSafety.MatchesSha256(bytes, provenance.Sha256)
                || !AccessibleHtmlRenderer.IsSupportedSelfContainedImage(bytes, provenance.MimeType))
            {
                return false;
            }

            content = bytes;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }
}
