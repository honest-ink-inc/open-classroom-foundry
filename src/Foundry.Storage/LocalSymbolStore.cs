// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text.Json;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;

namespace Foundry.Storage;

/// <summary>
/// The teacher's own symbol shelf: teachers add symbols to meet the individual
/// needs of those they work with, with full provenance recorded at submission.
/// Teacher-added symbols are local by default — Redistributable only when the
/// teacher explicitly declares a known open license — so a personal photograph
/// can serve one classroom without ever drifting into a public pack (Symbol
/// Commons invariant: local assets cannot enter open export).
/// </summary>
public sealed class LocalSymbolStore : IAssetCatalog
{
    public const string ManifestFileName = "teacher-symbols.json";

    private static readonly Dictionary<string, string> ExtensionsByMime = new()
    {
        ["image/svg+xml"] = ".svg",
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
    };

    private readonly string _directory;
    private readonly string _manifestPath;
    private readonly string _writeLockPath;
    private readonly Dictionary<AssetId, AssetProvenance> _assets;
    private string? _manifestFingerprint;

    public LocalSymbolStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        _manifestPath = Path.Combine(_directory, ManifestFileName);
        _writeLockPath = Path.Combine(_directory, $".{ManifestFileName}.lock");

        if (Directory.Exists(_manifestPath))
        {
            throw new InvalidDataException("The teacher-symbol manifest path is not a file.");
        }

        byte[]? manifestBytes = null;
        List<AssetProvenance> records;
        if (File.Exists(_manifestPath))
        {
            (records, manifestBytes) = AssetManifestReader.Read(_manifestPath, "teacher-symbol manifest");
        }
        else
        {
            records = [];
        }
        _manifestFingerprint = manifestBytes is null
            ? null
            : Convert.ToHexString(SHA256.HashData(manifestBytes));
        if (manifestBytes is not null)
        {
            CryptographicOperations.ZeroMemory(manifestBytes);
        }

        _assets = [];
        foreach (var provenance in records)
        {
            if (provenance is null || string.IsNullOrWhiteSpace(provenance.Id.Value))
            {
                throw new InvalidDataException("The teacher-symbol manifest contains an invalid asset identity.");
            }

            if (!_assets.TryAdd(provenance.Id, provenance))
            {
                throw new InvalidDataException("The teacher-symbol manifest contains a duplicate asset identity.");
            }
        }

        ThrowIfIntegrityInvalid();
    }

    public IReadOnlyList<AssetProvenance> All => [.. _assets.Values];

    public AssetProvenance? Find(AssetId id) => _assets.GetValueOrDefault(id);

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

    public AssetProvenance Add(SymbolSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.Id.Value);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.IntendedMeaning);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.AltText);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.MimeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.TeacherStatedRights);

        if (!AssetRightsPolicy.RequiredText(submission.Id.Value, 128)
            || !AssetRightsPolicy.RequiredText(submission.IntendedMeaning, 1024)
            || !AssetRightsPolicy.RequiredText(submission.AltText, 2048)
            || !AssetRightsPolicy.RequiredText(submission.MimeType, 64)
            || !AssetRightsPolicy.RequiredText(submission.TeacherStatedRights, 256)
            || !AssetRightsPolicy.OptionalText(submission.AmbiguityNotes, 2048)
            || !AssetRightsPolicy.OptionalText(submission.License, 128))
        {
            throw new ArgumentException(
                "Symbol provenance must be bounded text without control characters.",
                nameof(submission));
        }

        if (submission.ContentLength == 0)
        {
            throw new ArgumentException("A symbol needs content.", nameof(submission));
        }

        if (submission.ContentLength > AssetFileSafety.MaxAssetBytes)
        {
            throw new ArgumentException("A symbol exceeds the bounded asset-content limit.", nameof(submission));
        }

        if (!ExtensionsByMime.TryGetValue(submission.MimeType, out var extension))
        {
            throw new ArgumentException($"Unsupported symbol type '{submission.MimeType}'.", nameof(submission));
        }

        Directory.CreateDirectory(_directory);
        using var writeLock = AcquireWriteLock();
        EnsureManifestUnchanged();
        ThrowIfIntegrityInvalid();

        if (_assets.ContainsKey(submission.Id))
        {
            throw new InvalidOperationException($"Symbol '{submission.Id.Value}' already exists; version, don't overwrite.");
        }

        var isOpen = AssetRightsPolicy.IsKnownOpenLicense(submission.License);
        var sanitizedId = Sanitize(submission.Id.Value);
        if (string.IsNullOrWhiteSpace(sanitizedId))
        {
            throw new ArgumentException("A symbol identity must contain at least one portable filename character.", nameof(submission));
        }

        var fileName = sanitizedId + extension;
        if (!AssetFileSafety.TryResolveLeaf(_directory, fileName, out var assetPath))
        {
            throw new ArgumentException("The symbol identity does not produce a safe local filename.", nameof(submission));
        }

        if (_assets.Values.Any(existing => AssetFileSafety.FileNameComparer.Equals(existing.FileName, fileName))
            || File.Exists(assetPath)
            || Directory.Exists(assetPath))
        {
            throw new InvalidOperationException(
                $"Symbol '{submission.Id.Value}' collides with an existing symbol filename; version, don't overwrite.");
        }

        var submittedContent = submission.CopyContent();
        try
        {
            if (!AccessibleHtmlRenderer.IsSupportedSelfContainedImage(submittedContent, submission.MimeType))
            {
                throw new ArgumentException(
                    "A symbol must be a bounded, passive SVG or a supported raster image.",
                    nameof(submission));
            }

            var provenance = new AssetProvenance(
                Id: submission.Id,
                ConceptId: $"teacher.{sanitizedId}",
                Version: "1.0.0",
                FileName: fileName,
                MimeType: submission.MimeType,
                Source: "teacher-created",
                Creator: submission.TeacherStatedRights,
                License: isOpen ? submission.License! : "LicenseRef-teacher-local",
                Sha256: Convert.ToHexString(SHA256.HashData(submittedContent)),
                IntendedMeaning: submission.IntendedMeaning,
                AltText: submission.AltText,
                Redistributable: isOpen,
                AmbiguityNotes: submission.AmbiguityNotes);

            var stagingPath = Path.Combine(_directory, $".{fileName}.{Guid.NewGuid():N}.stage");
            var promoted = false;
            try
            {
                using (var stream = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(submittedContent);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(stagingPath, assetPath);
                promoted = true;
                _assets.Add(submission.Id, provenance);
                WriteManifestAtomically();
            }
            catch (Exception exception)
            {
                _assets.Remove(submission.Id);
                try
                {
                    if (promoted && File.Exists(assetPath))
                    {
                        File.Delete(assetPath);
                    }

                    if (File.Exists(stagingPath))
                    {
                        File.Delete(stagingPath);
                    }
                }
                catch (Exception cleanupException)
                {
                    throw new IOException(
                        "The symbol add failed and its staged asset residue could not be removed.",
                        new AggregateException(exception, cleanupException));
                }

                throw;
            }
            finally
            {
                if (File.Exists(stagingPath))
                {
                    File.Delete(stagingPath);
                }
            }

            return provenance;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(submittedContent);
        }
    }

    /// <summary>R2-2: the teacher's shelf deserves the same tamper check as the libre pack.</summary>
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
                    $"Symbol {id} has incomplete required provenance."));
            }

            if (string.IsNullOrWhiteSpace(provenance.License))
            {
                issues.Add(ValidationIssue.Blocking("asset.unknown-rights", $"Symbol {id} has no license; unknown rights block distribution."));
            }
            else if (!AssetRightsPolicy.HasConsistentRedistributionRights(provenance))
            {
                issues.Add(ValidationIssue.Blocking(
                    "asset.redistribution-rights",
                    $"Symbol {id} has inconsistent license and redistribution metadata."));
            }

            if (string.IsNullOrWhiteSpace(provenance.AltText))
            {
                issues.Add(ValidationIssue.Blocking("asset.alt-text", $"Symbol {id} has no alternative text."));
            }

            if (!AssetRightsPolicy.HasSafeOptionalMetadata(provenance))
            {
                issues.Add(ValidationIssue.Blocking(
                    "asset.invalid-optional-provenance",
                    $"Symbol {id} has oversized or control-bearing optional provenance."));
            }

            if (!AssetFileSafety.TryResolveLeaf(_directory, provenance.FileName, out var path))
            {
                issues.Add(ValidationIssue.Blocking("asset.invalid-file-name", $"Symbol {id} has an unsafe asset filename."));
                continue;
            }

            if (!fileNames.Add(provenance.FileName))
            {
                issues.Add(ValidationIssue.Blocking("asset.file-name-collision", $"Symbol {id} collides with another shelf filename."));
            }

            if (string.IsNullOrWhiteSpace(provenance.MimeType)
                || !ExtensionsByMime.TryGetValue(provenance.MimeType, out var expectedExtension)
                || !Path.GetExtension(provenance.FileName).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(ValidationIssue.Blocking("asset.media-type", $"Symbol {id} has an unsupported or mismatched media type."));
            }

            if (!AssetFileSafety.IsSha256(provenance.Sha256))
            {
                issues.Add(ValidationIssue.Blocking("asset.invalid-hash", $"Symbol {id} has no valid SHA-256 provenance."));
                continue;
            }

            if (!File.Exists(path))
            {
                issues.Add(ValidationIssue.Blocking("asset.missing-file", $"Symbol {provenance.Id.Value} has provenance but no file '{provenance.FileName}'."));
                continue;
            }

            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                {
                    issues.Add(ValidationIssue.Blocking("asset.reparse-file", $"Symbol {id} resolves through a reparse point."));
                    continue;
                }

                var bytes = AssetFileSafety.ReadBoundedRegularFile(path);
                try
                {
                    if (!AssetFileSafety.MatchesSha256(bytes, provenance.Sha256))
                    {
                        issues.Add(ValidationIssue.Blocking("asset.hash-mismatch", $"Symbol {id} does not match its recorded SHA-256."));
                    }
                    else if (!AccessibleHtmlRenderer.IsSupportedSelfContainedImage(bytes, provenance.MimeType))
                    {
                        issues.Add(ValidationIssue.Blocking("asset.unsafe-content", $"Symbol {id} is not a supported, self-contained image."));
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(bytes);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(ValidationIssue.Blocking("asset.unreadable-file", $"Symbol {id} could not be read for integrity validation."));
            }
            catch (InvalidDataException)
            {
                issues.Add(ValidationIssue.Blocking("asset.size-limit", $"Symbol {id} is empty, oversized, or changed while read."));
            }
        }

        return issues;
    }

    private bool TryReadVerifiedContent(AssetProvenance provenance, out byte[] content)
    {
        content = [];

        if (!AssetRightsPolicy.HasCompleteRequiredMetadata(provenance)
            || !AssetRightsPolicy.HasSafeOptionalMetadata(provenance)
            || !AssetRightsPolicy.HasConsistentRedistributionRights(provenance)
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

    private void ThrowIfIntegrityInvalid()
    {
        var issues = VerifyIntegrity();
        if (issues.Count == 0)
        {
            return;
        }

        throw new InvalidDataException(
            $"The teacher-symbol shelf failed closed ({string.Join(", ", issues.Select(issue => issue.Code).Distinct().Order())}).");
    }

    private void WriteManifestAtomically()
    {
        EnsureManifestUnchanged();
        if (_assets.Count > AssetManifestReader.MaxRecordCount)
        {
            throw new InvalidOperationException(
                $"The teacher-symbol shelf cannot exceed {AssetManifestReader.MaxRecordCount} records.");
        }

        var serialized = JsonSerializer.SerializeToUtf8Bytes(_assets.Values.ToList(), StorageJson.Options);
        if (serialized.Length > AssetManifestReader.MaxManifestBytes)
        {
            throw new InvalidOperationException(
                $"The teacher-symbol manifest cannot exceed {AssetManifestReader.MaxManifestBytes} bytes.");
        }

        var stagingPath = Path.Combine(_directory, $".{ManifestFileName}.{Guid.NewGuid():N}.stage");

        try
        {
            using (var stream = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(serialized);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_manifestPath))
            {
                File.Replace(stagingPath, _manifestPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(stagingPath, _manifestPath);
            }

            _manifestFingerprint = Convert.ToHexString(SHA256.HashData(serialized));
        }
        finally
        {
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
            }
        }
    }

    private FileStream AcquireWriteLock()
    {
        try
        {
            return new FileStream(_writeLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "The teacher-symbol shelf is already being changed; reopen it before adding a symbol.",
                exception);
        }
    }

    private void EnsureManifestUnchanged()
    {
        string? currentFingerprint;
        if (!File.Exists(_manifestPath))
        {
            currentFingerprint = null;
        }
        else
        {
            var (_, currentBytes) = AssetManifestReader.Read(_manifestPath, "teacher-symbol manifest");
            try
            {
                currentFingerprint = Convert.ToHexString(SHA256.HashData(currentBytes));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(currentBytes);
            }
        }

        if (!string.Equals(currentFingerprint, _manifestFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The teacher-symbol shelf changed after it was opened; reopen it before adding a symbol.");
        }
    }

    private static string Sanitize(string value)
        => new([.. value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '.' or '_')]);
}

/// <summary>One lookup across the libre pack and the teacher's shelf, first match wins.</summary>
public sealed class CompositeAssetCatalog(params IAssetCatalog[] catalogs) : IAssetCatalog
{
    public IReadOnlyList<AssetProvenance> All => [.. catalogs.SelectMany(c => c.All)];

    public AssetProvenance? Find(AssetId id)
        => catalogs.Select(c => c.Find(id)).FirstOrDefault(p => p is not null);

    public bool TryGetContent(AssetId id, out ReadOnlyMemory<byte> content, out string mimeType)
    {
        foreach (var catalog in catalogs)
        {
            if (catalog.TryGetContent(id, out content, out mimeType))
            {
                return true;
            }
        }

        content = default;
        mimeType = string.Empty;
        return false;
    }
}
