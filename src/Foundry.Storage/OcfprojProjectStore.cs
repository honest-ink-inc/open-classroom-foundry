// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;

namespace Foundry.Storage;

public sealed record LoadedProject(
    ProjectManifest Manifest,
    ArtifactDocument Document,
    ProjectValidationEnvelope? Validation,
    ProjectRenderProfile? RenderProfile,
    IAssetCatalog? Assets = null);

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
    public const long MaxEntryBytes = OcfprojPackageValidator.MaxJsonEntryBytes;

    /// <summary>Zip records DOS timestamps, which begin in 1980 and end in 2107; the save instant is clamped into that window.</summary>
    private static readonly DateTimeOffset ZipEpoch = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ZipLastInstant = new(2107, 12, 31, 23, 59, 58, TimeSpan.Zero);

    /// <summary>ASCII unit separator: it joins the id's fields without any of them being able to forge a boundary.</summary>
    private const char UnitSeparator = (char)0x1F;

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

        if ((request.Validation is null) != (request.RenderProfile is null))
        {
            throw new InvalidOperationException(
                "A saved validation context and render profile must be written together.");
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
            ProjectId: DeterministicProjectId(request),
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
            AssetIds: [.. assetIds.Select(a => a.Value)],
            Purpose: artifact.Revision.Purpose);

        if (request.Validation is not null && request.RenderProfile is not null)
        {
            ValidateSaveContext(artifact, request, request.Validation, request.RenderProfile);
        }

        // A portable project is shareable: retain the reviewed scale and
        // language order, but its durable snapshot is always learner-audience
        // and therefore carries neither teacher-only material nor approval PII.
        var snapshotRequest = OcfprojPackageValidator.SnapshotRenderRequest(request.RenderProfile);
        var snapshot = renderer is AccessibleHtmlRenderer
            ? await AccessibleHtmlRenderer.RenderPortableSnapshotAsync(
                artifact,
                snapshotRequest,
                cancellationToken).ConfigureAwait(false)
            : await renderer.RenderAsync(
                artifact,
                snapshotRequest,
                cancellationToken).ConfigureAwait(false);

        Directory.CreateDirectory(rootDirectory);
        var path = PathFor(request.DestinationHint);
        var stagingPath = UniqueStagingPath(path);
        try
        {
            await using (var stream = new FileStream(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough))
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    var stamp = ZipStamp(request.SavedAtUtc);

                    await WriteEntryAsync(archive, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest, StorageJson.Options), stamp, cancellationToken).ConfigureAwait(false);
                    await WriteEntryAsync(archive, "artifact.json", JsonSerializer.SerializeToUtf8Bytes(document, StorageJson.Options), stamp, cancellationToken).ConfigureAwait(false);
                    await WriteEntryAsync(archive, "snapshot.html", snapshot.Content.ToArray(), stamp, cancellationToken).ConfigureAwait(false);
                    if (request.Validation is not null && request.RenderProfile is not null)
                    {
                        await WriteEntryAsync(
                            archive,
                            "validation.json",
                            JsonSerializer.SerializeToUtf8Bytes(request.Validation, StorageJson.Options),
                            stamp,
                            cancellationToken).ConfigureAwait(false);
                        await WriteEntryAsync(
                            archive,
                            "render-profile.json",
                            JsonSerializer.SerializeToUtf8Bytes(request.RenderProfile, StorageJson.Options),
                            stamp,
                            cancellationToken).ConfigureAwait(false);
                    }

                    foreach (var (provenance, content) in resolved)
                    {
                        await WriteEntryAsync(archive, $"assets/{provenance.FileName}", content.ToArray(), stamp, cancellationToken).ConfigureAwait(false);
                        await WriteEntryAsync(
                            archive,
                            $"provenance/{provenance.Id.Value}.json",
                            JsonSerializer.SerializeToUtf8Bytes(provenance, StorageJson.Options),
                            stamp,
                            cancellationToken).ConfigureAwait(false);
                    }
                }

                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            await ValidateStagedPackageAsync(stagingPath, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            CommitStagedPackage(stagingPath, path);
        }
        finally
        {
            TryDeleteStage(stagingPath);
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
        => LoadProjectFileAsync(PathFor(destinationHint), cancellationToken);

    /// <summary>
    /// Reads a package from an exact path. This is the compatibility boundary
    /// used by managed-upgrade preparation: callers can validate a frozen input
    /// without copying it into the live project library first.
    /// </summary>
    public static async Task<LoadedProject> LoadProjectFileAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return await OcfprojPackageValidator.ValidateAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (OcfprojPackageException exception)
        {
            // Preserve the exact public exception contract while keeping the
            // validator's code available to the upgrade boundary internally.
            throw new InvalidOperationException(exception.Message, exception);
        }
    }

    /// <summary>
    /// Reads the manifest only after validating the complete package. Version
    /// routing inside an upgrade uses the held-stream validator directly so it
    /// cannot reopen a retargetable path.
    /// </summary>
    public static ProjectManifest ReadManifestFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return OcfprojPackageValidator.ValidateAsync(stream, CancellationToken.None).GetAwaiter().GetResult().Manifest;
        }
        catch (OcfprojPackageException exception)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
    }

    public string PathFor(string destinationHint) => Path.Combine(rootDirectory, Sanitize(destinationHint) + Extension);

    private static void ValidateSaveContext(
        ApprovedArtifact artifact,
        ProjectSaveRequest request,
        ProjectValidationEnvelope validation,
        ProjectRenderProfile renderProfile)
    {
        var digest = ArtifactDocumentFingerprint.Compute(artifact.Revision.Document);
        if (validation.SchemaVersion != ProjectValidationEnvelope.CurrentSchemaVersion
            || !string.Equals(validation.Kind, ProjectValidationEnvelope.ExactApprovedDocumentKind, StringComparison.Ordinal)
            || !string.Equals(validation.RecipeId, request.RecipeId, StringComparison.Ordinal)
            || !string.Equals(validation.RecipeVersion, request.RecipeVersion, StringComparison.Ordinal)
            || validation.Lane != artifact.Revision.Lane
            || validation.Purpose != artifact.Revision.Purpose
            || !string.Equals(validation.ArtifactSha256, digest, StringComparison.OrdinalIgnoreCase)
            || validation.UntrustedNoticeCodes is null
            || validation.UntrustedNoticeCodes.Count > 128
            || validation.UntrustedNoticeCodes.Any(code => !ProjectValidationEnvelope.IsStableNoticeCode(code))
            || validation.UntrustedNoticeCodes.Distinct(StringComparer.Ordinal).Count()
                != validation.UntrustedNoticeCodes.Count
            || renderProfile.SchemaVersion != ProjectRenderProfile.CurrentSchemaVersion
            || !string.Equals(renderProfile.ArtifactSha256, digest, StringComparison.OrdinalIgnoreCase)
            || !Enum.IsDefined(renderProfile.Audience)
            || !double.IsFinite(renderProfile.TextScalePercent)
            || renderProfile.TextScalePercent is < 100 or > 200)
        {
            throw new InvalidOperationException(
                "The saved validation context or render profile does not bind to this exact approved artifact.");
        }
    }

    private static string Sanitize(string hint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hint);

        var cleaned = new string([.. hint.Where(c => !Path.GetInvalidFileNameChars().Contains(c) && c != '.')]).Trim();
        return string.IsNullOrWhiteSpace(cleaned)
            ? throw new ArgumentException("The destination hint has no usable characters.", nameof(hint))
            : cleaned;
    }

    /// <summary>
    /// The project id is a function of the project, never of the clock: the same
    /// module, recipe, destination, and save instant reproduce the same id, so an
    /// identical save is an identical package. Nothing reads this field, which is
    /// precisely why a random value bought nothing and cost determinism. Name-based
    /// over SHA-256, stamped version 8 (RFC 9562 §5.8, custom).
    /// </summary>
    private static Guid DeterministicProjectId(ProjectSaveRequest request)
    {
        var name = string.Join(
            UnitSeparator,
            EngineIdentity.ProjectSchemaVersion,
            request.ModuleId,
            request.RecipeId,
            request.RecipeVersion,
            Sanitize(request.DestinationHint),
            request.SavedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(name))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>
    /// A request carrying no instant (the record's default) writes the zip epoch
    /// rather than throwing, and no instant reaches the archive from the clock.
    /// DOS stamps hold two-second granularity; the truncation is deterministic.
    /// </summary>
    private static DateTimeOffset ZipStamp(DateTimeOffset savedAt)
    {
        var utc = savedAt.ToUniversalTime();
        if (utc < ZipEpoch)
        {
            return ZipEpoch;
        }

        return utc > ZipLastInstant ? ZipLastInstant : utc;
    }

    private static async Task WriteEntryAsync(
        ZipArchive archive, string name, byte[] content, DateTimeOffset stamp, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = stamp;
        await using var entryStream = entry.Open();
        await entryStream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
    }

    private static string UniqueStagingPath(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("The project destination has no parent directory.");
        return Path.Combine(
            directory,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.stage");
    }

    private static async Task ValidateStagedPackageAsync(
        string stagingPath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            stagingPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        _ = await OcfprojPackageValidator.ValidateAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private static void CommitStagedPackage(string stagingPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            File.Replace(stagingPath, destinationPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            return;
        }

        File.Move(stagingPath, destinationPath);
    }

    private static void TryDeleteStage(string stagingPath)
    {
        try
        {
            File.Delete(stagingPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Never mask the save/validation failure. The unique sibling is the
            // only cleanup target; an existing valid destination is untouched.
        }
    }

}
