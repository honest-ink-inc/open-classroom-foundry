using System.Text;
using System.Text.Json;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Storage;

/// <summary>
/// The open-pack export path, and the export-side proof of the Symbol Commons
/// invariant: a non-redistributable asset — a teacher's local symbol above all —
/// cannot enter an open pack. Not filtered out silently: the export refuses
/// entirely, so the person exporting learns the boundary instead of shipping a
/// hole in it. Packaging is deterministic (sorted by id) and attribution-complete.
/// </summary>
public static class SymbolPackExporter
{
    public const string AttributionsFileName = "ATTRIBUTIONS.md";

    public static IReadOnlyList<AssetProvenance> ExportPack(IAssetCatalog source, IReadOnlyList<AssetId> ids, string directory)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (ids.Count == 0)
        {
            throw new ArgumentException("An empty pack is not a pack.", nameof(ids));
        }

        var records = new List<AssetProvenance>();
        foreach (var id in ids.DistinctBy(i => i.Value).OrderBy(i => i.Value, StringComparer.Ordinal))
        {
            var provenance = source.Find(id)
                ?? throw new InvalidOperationException($"'{id.Value}' has no provenance; unknown rights block distribution.");

            if (!provenance.Redistributable)
            {
                throw new InvalidOperationException(
                    $"'{id.Value}' is not redistributable ({provenance.License}); local assets cannot enter open export. Remove it from the pack.");
            }

            if (!source.TryGetContent(id, out var content, out _))
            {
                throw new InvalidOperationException($"'{id.Value}' has provenance but no retrievable content.");
            }

            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, provenance.FileName), content.ToArray());
            records.Add(provenance);
        }

        File.WriteAllText(
            Path.Combine(directory, JsonAssetCatalog.ManifestFileName),
            JsonSerializer.Serialize(records, StorageJson.Options));

        var attributions = new StringBuilder("# Attributions\n\n");
        foreach (var record in records)
        {
            attributions.Append("- ").Append(record.Id.Value).Append(": \"").Append(record.IntendedMeaning)
                .Append("\" — ").Append(record.Creator).Append(", ").Append(record.License);
            if (!string.IsNullOrWhiteSpace(record.RequiredAttribution))
            {
                attributions.Append(". ").Append(record.RequiredAttribution);
            }

            attributions.Append('\n');
        }

        File.WriteAllText(Path.Combine(directory, AttributionsFileName), attributions.ToString());
        return records;
    }
}
