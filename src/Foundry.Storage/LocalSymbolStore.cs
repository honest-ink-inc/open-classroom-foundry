// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text.Json;
using Foundry.Contracts;
using Foundry.Domain;

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

    private static readonly HashSet<string> OpenLicenses = ["CC0-1.0", "CC-BY-4.0", "CC-BY-SA-4.0"];

    private static readonly Dictionary<string, string> ExtensionsByMime = new()
    {
        ["image/svg+xml"] = ".svg",
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
    };

    private readonly string _directory;
    private readonly string _manifestPath;
    private readonly Dictionary<AssetId, AssetProvenance> _assets;

    public LocalSymbolStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = directory;
        _manifestPath = Path.Combine(directory, ManifestFileName);

        _assets = File.Exists(_manifestPath)
            ? (JsonSerializer.Deserialize<List<AssetProvenance>>(File.ReadAllText(_manifestPath), StorageJson.Options) ?? [])
                .ToDictionary(a => a.Id)
            : [];
    }

    public IReadOnlyList<AssetProvenance> All => [.. _assets.Values];

    public AssetProvenance? Find(AssetId id) => _assets.GetValueOrDefault(id);

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

    public AssetProvenance Add(SymbolSubmission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.IntendedMeaning);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.AltText);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.TeacherStatedRights);

        if (submission.Content.IsEmpty)
        {
            throw new ArgumentException("A symbol needs content.", nameof(submission));
        }

        if (!ExtensionsByMime.TryGetValue(submission.MimeType, out var extension))
        {
            throw new ArgumentException($"Unsupported symbol type '{submission.MimeType}'.", nameof(submission));
        }

        if (_assets.ContainsKey(submission.Id))
        {
            throw new InvalidOperationException($"Symbol '{submission.Id.Value}' already exists; version, don't overwrite.");
        }

        var isOpen = submission.License is not null && OpenLicenses.Contains(submission.License);
        var fileName = Sanitize(submission.Id.Value) + extension;

        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(Path.Combine(_directory, fileName), submission.Content.ToArray());

        var provenance = new AssetProvenance(
            Id: submission.Id,
            ConceptId: $"teacher.{Sanitize(submission.Id.Value)}",
            Version: "1.0.0",
            FileName: fileName,
            MimeType: submission.MimeType,
            Source: "teacher-created",
            Creator: submission.TeacherStatedRights,
            License: isOpen ? submission.License! : "LicenseRef-teacher-local",
            Sha256: Convert.ToHexString(SHA256.HashData(submission.Content.Span)),
            IntendedMeaning: submission.IntendedMeaning,
            AltText: submission.AltText,
            Redistributable: isOpen,
            AmbiguityNotes: submission.AmbiguityNotes);

        _assets.Add(submission.Id, provenance);
        File.WriteAllText(_manifestPath, JsonSerializer.Serialize(_assets.Values.ToList(), StorageJson.Options));
        return provenance;
    }

    /// <summary>R2-2: the teacher's shelf deserves the same tamper check as the libre pack.</summary>
    public IReadOnlyList<ValidationIssue> VerifyIntegrity()
    {
        var issues = new List<ValidationIssue>();

        foreach (var provenance in _assets.Values)
        {
            var path = Path.Combine(_directory, provenance.FileName);
            if (!File.Exists(path))
            {
                issues.Add(ValidationIssue.Blocking("asset.missing-file", $"Symbol {provenance.Id.Value} has provenance but no file '{provenance.FileName}'."));
                continue;
            }

            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            if (!actual.Equals(provenance.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(ValidationIssue.Blocking("asset.hash-mismatch", $"Symbol {provenance.Id.Value} does not match its recorded SHA-256."));
            }
        }

        return issues;
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
