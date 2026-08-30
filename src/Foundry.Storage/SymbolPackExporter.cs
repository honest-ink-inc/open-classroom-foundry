// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text;
using System.Text.Json;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;

namespace Foundry.Storage;

/// <summary>
/// The open-pack export path, and the export-side proof of the Symbol Commons
/// invariant: a non-redistributable asset — a teacher's local symbol above all —
/// cannot enter an open pack. Not filtered out silently: the export refuses
/// entirely, so the person exporting learns the boundary instead of shipping a
/// hole in it. Packaging is deterministic (sorted by id) and emits a bounded
/// attribution draft from the catalog. Export requires explicit attribution
/// and modification dispositions rather than interpreting missing values as
/// "none"; rights-seat completeness and license-specific obligations remain
/// outside this mechanical proof.
/// </summary>
public static class SymbolPackExporter
{
    public const string AttributionsFileName = "ATTRIBUTIONS.md";
    private const int MaxAttributionsBytes = 8 * 1024 * 1024;
    private const long MaxPackContentBytes = 256L * 1024 * 1024;

    private static readonly Dictionary<string, string> ExtensionsByMime = new(StringComparer.Ordinal)
    {
        ["image/svg+xml"] = ".svg",
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
    };

    public static IReadOnlyList<AssetProvenance> ExportPack(IAssetCatalog source, IReadOnlyList<AssetId> ids, string directory)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (ids.Count == 0)
        {
            throw new ArgumentException("An empty pack is not a pack.", nameof(ids));
        }

        if (ids.Count > AssetManifestReader.MaxRecordCount)
        {
            throw new ArgumentException(
                $"A pack cannot request more than {AssetManifestReader.MaxRecordCount} symbols.",
                nameof(ids));
        }

        var destination = GetNewDestination(directory);
        var records = ValidateProvenance(source, ids, destination);
        var preparedAssets = ValidateContent(source, records);
        var manifest = JsonSerializer.SerializeToUtf8Bytes(records, StorageJson.Options);
        if (manifest.Length > AssetManifestReader.MaxManifestBytes)
        {
            throw new InvalidOperationException(
                $"The symbol-pack manifest exceeds {AssetManifestReader.MaxManifestBytes} bytes.");
        }

        var attributions = BuildAttributions(records);
        if (Encoding.UTF8.GetByteCount(attributions) > MaxAttributionsBytes)
        {
            throw new InvalidOperationException("The symbol-pack attribution draft exceeds its bounded output limit.");
        }

        var parent = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(parent);

        var stagingDirectory = Path.Combine(
            parent,
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.stage");
        var promoted = false;

        try
        {
            Directory.CreateDirectory(stagingDirectory);

            foreach (var prepared in preparedAssets)
            {
                if (!AssetFileSafety.TryResolveLeaf(stagingDirectory, prepared.Provenance.FileName, out var assetPath))
                {
                    throw new InvalidOperationException($"'{prepared.Provenance.Id.Value}' has an unsafe asset filename.");
                }

                File.WriteAllBytes(assetPath, prepared.Content);
            }

            File.WriteAllBytes(Path.Combine(stagingDirectory, JsonAssetCatalog.ManifestFileName), manifest);
            File.WriteAllText(Path.Combine(stagingDirectory, AttributionsFileName), attributions);

            var stagedIssues = new JsonAssetCatalog(stagingDirectory).VerifyIntegrity();
            if (stagedIssues.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The staged symbol pack failed integrity validation ({string.Join(", ", stagedIssues.Select(issue => issue.Code).Distinct().Order())}).");
            }

            if (File.Exists(destination) || Directory.Exists(destination))
            {
                throw new IOException("The symbol-pack destination appeared while the pack was being prepared.");
            }

            Directory.Move(stagingDirectory, destination);
            promoted = true;
            return records;
        }
        finally
        {
            if (!promoted && Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private static string GetNewDestination(string directory)
    {
        string destination;
        try
        {
            destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("The symbol-pack destination is not a valid path.", nameof(directory), exception);
        }

        if (Path.GetDirectoryName(destination) is null || Path.GetPathRoot(destination) == destination)
        {
            throw new ArgumentException("The symbol-pack destination must name a directory beneath a parent directory.", nameof(directory));
        }

        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw new IOException("The symbol-pack destination must not already exist.");
        }

        return destination;
    }

    private static List<AssetProvenance> ValidateProvenance(
        IAssetCatalog source,
        IReadOnlyList<AssetId> ids,
        string destination)
    {
        var records = new List<AssetProvenance>();
        var fileNames = new HashSet<string>(AssetFileSafety.FileNameComparer)
        {
            JsonAssetCatalog.ManifestFileName,
            AttributionsFileName,
        };

        foreach (var id in ids.DistinctBy(i => i.Value).OrderBy(i => i.Value, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(id.Value))
            {
                throw new InvalidOperationException("A symbol pack cannot contain an empty asset identity.");
            }

            var provenance = source.Find(id)
                ?? throw new InvalidOperationException($"'{id.Value}' has no provenance; unknown rights block distribution.");

            if (provenance.Id != id)
            {
                throw new InvalidOperationException($"'{id.Value}' resolved to different provenance; the pack was refused.");
            }

            if (string.IsNullOrWhiteSpace(provenance.License))
            {
                throw new InvalidOperationException($"'{id.Value}' has no license; unknown rights block distribution.");
            }

            if (!AssetRightsPolicy.HasConsistentRedistributionRights(provenance))
            {
                throw new InvalidOperationException(
                    $"'{id.Value}' has inconsistent license and redistribution metadata; the pack was refused.");
            }

            if (!provenance.Redistributable)
            {
                throw new InvalidOperationException(
                    $"'{id.Value}' is not redistributable ({provenance.License}); local assets cannot enter open export. Remove it from the pack.");
            }

            if (!AssetRightsPolicy.HasCompleteRequiredMetadata(provenance)
                || !AssetRightsPolicy.HasSafeOptionalMetadata(provenance))
            {
                throw new InvalidOperationException($"'{id.Value}' has incomplete required provenance.");
            }

            if (!AssetRightsPolicy.HasExplicitExportDispositions(provenance))
            {
                throw new InvalidOperationException(
                    $"'{id.Value}' has no explicit attribution and modification dispositions; open export was refused.");
            }

            if (!AssetFileSafety.TryResolveLeaf(destination, provenance.FileName, out _))
            {
                throw new InvalidOperationException($"'{id.Value}' has an unsafe asset filename.");
            }

            if (!fileNames.Add(provenance.FileName))
            {
                throw new InvalidOperationException($"'{id.Value}' has an asset filename that collides with another pack entry.");
            }

            if (string.IsNullOrWhiteSpace(provenance.MimeType)
                || !ExtensionsByMime.TryGetValue(provenance.MimeType, out var expectedExtension)
                || !Path.GetExtension(provenance.FileName).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"'{id.Value}' has an unsupported or mismatched media type.");
            }

            if (!AssetFileSafety.IsSha256(provenance.Sha256))
            {
                throw new InvalidOperationException($"'{id.Value}' has no valid SHA-256 provenance.");
            }

            records.Add(provenance);
        }

        return records;
    }

    private static List<PreparedAsset> ValidateContent(IAssetCatalog source, List<AssetProvenance> records)
    {
        var prepared = new List<PreparedAsset>(records.Count);
        long totalBytes = 0;

        foreach (var provenance in records)
        {
            if (!source.TryGetContent(provenance.Id, out var content, out var mimeType))
            {
                throw new InvalidOperationException($"'{provenance.Id.Value}' has provenance but no retrievable content.");
            }

            if (content.IsEmpty || content.Length > AssetFileSafety.MaxAssetBytes)
            {
                throw new InvalidOperationException($"'{provenance.Id.Value}' has empty or oversized content.");
            }

            var bytes = content.ToArray();

            if (!string.Equals(mimeType, provenance.MimeType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"'{provenance.Id.Value}' content does not match its recorded media type.");
            }

            if (!AssetFileSafety.MatchesSha256(bytes, provenance.Sha256))
            {
                throw new InvalidOperationException($"'{provenance.Id.Value}' content does not match its recorded SHA-256.");
            }

            if (!AccessibleHtmlRenderer.IsSupportedSelfContainedImage(bytes, mimeType))
            {
                throw new InvalidOperationException(
                    $"'{provenance.Id.Value}' is not a supported, self-contained image.");
            }

            totalBytes = checked(totalBytes + bytes.Length);
            if (totalBytes > MaxPackContentBytes)
            {
                throw new InvalidOperationException("The symbol pack exceeds its bounded content limit.");
            }

            prepared.Add(new PreparedAsset(provenance, bytes));
        }

        return prepared;
    }

    private static string BuildAttributions(List<AssetProvenance> records)
    {
        // Keep every author-controlled value in one fenced literal block.
        // Required metadata cannot contain a line break, so no value can place
        // a closing fence at the beginning of a new line. GFM performs neither
        // inline markup nor extended autolinking inside a fenced code block.
        var attributions = new StringBuilder("# Attributions\n\n```text\n");
        foreach (var record in records)
        {
            attributions.Append("- ").Append(record.Id.Value).Append(": \"")
                .Append(record.IntendedMeaning).Append("\" — ")
                .Append("Creator: ").Append(record.Creator)
                .Append(". Source: ").Append(record.Source)
                .Append(". License: ").Append(record.License)
                .Append(". Modifications: ").Append(record.Modifications)
                .Append(". Attribution: ").Append(record.RequiredAttribution);
            attributions.Append('\n');
        }

        attributions.Append("```\n");
        return attributions.ToString();
    }

    private sealed record PreparedAsset(AssetProvenance Provenance, byte[] Content);
}
