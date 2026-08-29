// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Storage;

public sealed record LoadedProject(ProjectManifest Manifest, ArtifactDocument Document);

/// <summary>
/// The real .ocfproj store (plan §6.5, ADR-003): a ZIP/JSON package holding
/// manifest.json, artifact.json, the referenced assets with their provenance
/// records, and — Green being the only lane this store accepts — an accessible
/// snapshot.html so the project stays human-legible with no Foundry installed.
/// An asset the catalog does not know is a save-stopping error: unknown rights
/// block distribution.
/// </summary>
public sealed class OcfprojProjectStore(string rootDirectory, IRenderer renderer, IAssetCatalog assetCatalog) : IProjectStore
{
    public const string Extension = ".ocfproj";

    /// <summary>R2-3: a generous ceiling for classroom artifacts, a wall for decompression bombs. The full hostile-package suite remains scheduled (plan §7).</summary>
    public const long MaxEntryBytes = 64L * 1024 * 1024;

    public async Task SaveGreenProjectAsync(ApprovedArtifact artifact, ProjectSaveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (artifact.Revision.Lane != DataLane.Green)
        {
            throw new InvalidOperationException(
                $"Only Green-lane products may be saved to the project library; this artifact is {artifact.Revision.Lane}.");
        }

        var document = artifact.Revision.Document;
        var assetIds = document.Nodes
            .SelectMany(node => node switch
            {
                ImageReference image => new[] { image.Asset },
                StepRow { Symbol: { } symbol } => [symbol.Asset],
                _ => [],
            })
            .Distinct()
            .ToList();

        var resolved = new List<(AssetProvenance Provenance, ReadOnlyMemory<byte> Content)>();
        foreach (var id in assetIds)
        {
            var provenance = assetCatalog.Find(id)
                ?? throw new InvalidOperationException($"Asset '{id.Value}' has no provenance in the catalog; unknown rights block distribution.");

            if (!assetCatalog.TryGetContent(id, out var content, out _))
            {
                throw new InvalidOperationException($"Asset '{id.Value}' has provenance but no retrievable content.");
            }

            resolved.Add((provenance, content));
        }

        var manifest = new ProjectManifest(
            SchemaVersion: EngineIdentity.ProjectSchemaVersion,
            ProjectId: Guid.NewGuid(),
            ModuleId: request.ModuleId,
            ModuleVersion: request.RecipeVersion,
            RecipeId: request.RecipeId,
            RecipeVersion: request.RecipeVersion,
            CreatedUtc: request.SavedAtUtc,
            ModifiedUtc: request.SavedAtUtc,
            DataLane: artifact.Revision.Lane,
            RetentionMode: "teacher-managed",
            SourceLocale: document.Language,
            OutputLocale: null,
            EngineVersion: EngineIdentity.EngineVersion,
            ArtifactPath: "artifact.json",
            AssetIds: [.. assetIds.Select(a => a.Value)]);

        var snapshot = await renderer.RenderAsync(
            artifact, new RenderRequest(RenderTarget.AccessibleHtml, RenderAudience.Learner), cancellationToken).ConfigureAwait(false);

        Directory.CreateDirectory(rootDirectory);
        var path = PathFor(request.DestinationHint);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        await WriteEntryAsync(archive, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest, StorageJson.Options), cancellationToken).ConfigureAwait(false);
        await WriteEntryAsync(archive, "artifact.json", JsonSerializer.SerializeToUtf8Bytes(document, StorageJson.Options), cancellationToken).ConfigureAwait(false);
        await WriteEntryAsync(archive, "snapshot.html", snapshot.Content.ToArray(), cancellationToken).ConfigureAwait(false);

        foreach (var (provenance, content) in resolved)
        {
            await WriteEntryAsync(archive, $"assets/{provenance.FileName}", content.ToArray(), cancellationToken).ConfigureAwait(false);
            await WriteEntryAsync(
                archive,
                $"provenance/{provenance.Id.Value}.json",
                JsonSerializer.SerializeToUtf8Bytes(provenance, StorageJson.Options),
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reversibility (constitution 11): a saved project reopens without the
    /// model, the network, or this session. The reader distrusts the package
    /// (R2-3 hostile-package depth): duplicate entry names are a smuggling
    /// vector (a scanner reads one, the app reads the other), and a manifest
    /// that disagrees with the engine's schema, the Green-only lane, or the
    /// package's own contents is tampering or corruption — refused loudly.
    /// </summary>
    public Task<LoadedProject> LoadProjectAsync(string destinationHint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var archive = ZipFile.OpenRead(PathFor(destinationHint));

        var collision = archive.Entries
            .GroupBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (collision is not null)
        {
            throw new InvalidOperationException(
                $"The package holds {collision.Count()} entries named '{collision.Key}'; colliding names are refused outright.");
        }

        var manifest = ReadEntry<ProjectManifest>(archive, "manifest.json");
        var document = ReadEntry<ArtifactDocument>(archive, "artifact.json");

        if (manifest.SchemaVersion != EngineIdentity.ProjectSchemaVersion)
        {
            throw new InvalidOperationException(
                $"The manifest declares schema version '{manifest.SchemaVersion}'; this engine reads version '{EngineIdentity.ProjectSchemaVersion}' and refuses what it does not understand.");
        }

        if (manifest.DataLane != DataLane.Green)
        {
            throw new InvalidOperationException(
                $"This is the Green project library and the manifest claims {manifest.DataLane}; a lane above Green never persists, so the package is tampered or misplaced.");
        }

        foreach (var assetId in manifest.AssetIds)
        {
            if (archive.GetEntry($"provenance/{assetId}.json") is null)
            {
                throw new InvalidOperationException(
                    $"The manifest declares asset '{assetId}' but the package carries no provenance record for it; manifest and contents disagree.");
            }
        }

        var issues = DocumentValidator.Validate(document);
        if (DocumentValidator.HasBlockingIssues(issues))
        {
            throw new InvalidOperationException(
                "The artifact entry fails structural validation; an approved artifact never does, so the package is tampered or corrupt.");
        }

        return Task.FromResult(new LoadedProject(manifest, document));
    }

    public string PathFor(string destinationHint) => Path.Combine(rootDirectory, Sanitize(destinationHint) + Extension);

    private static string Sanitize(string hint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hint);

        var cleaned = new string([.. hint.Where(c => !Path.GetInvalidFileNameChars().Contains(c) && c != '.')]).Trim();
        return string.IsNullOrWhiteSpace(cleaned)
            ? throw new ArgumentException("The destination hint has no usable characters.", nameof(hint))
            : cleaned;
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string name, byte[] content, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await entryStream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
    }

    private static T ReadEntry<T>(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name)
            ?? throw new InvalidOperationException($"The package has no '{name}' entry.");

        if (entry.Length > MaxEntryBytes)
        {
            throw new InvalidOperationException(
                $"Entry '{name}' declares {entry.Length} bytes, over the {MaxEntryBytes}-byte ceiling; refusing to read it.");
        }

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return JsonSerializer.Deserialize<T>(reader.ReadToEnd(), StorageJson.Options)
            ?? throw new InvalidOperationException($"The '{name}' entry is empty.");
    }
}
