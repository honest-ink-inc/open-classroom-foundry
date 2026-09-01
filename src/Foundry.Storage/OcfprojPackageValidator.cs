// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Foundry.Contracts;
using Foundry.Domain;
using Foundry.Rendering;

namespace Foundry.Storage;

/// <summary>
/// One fail-closed reader for every .ocfproj trust boundary. It treats the ZIP
/// directory, JSON shapes, semantic document, snapshot, assets, and provenance
/// as one package contract; callers cannot validate a convenient subset and
/// accidentally describe it as the whole package.
/// </summary>
internal static class OcfprojPackageValidator
{
    internal const int MaxEntries = 512;
    internal const long MaxPackageBytes = 256L * 1024 * 1024;
    internal const long MaxTotalUncompressedBytes = 256L * 1024 * 1024;
    internal const long MaxJsonEntryBytes = 64L * 1024 * 1024;

    private const long MaxSmallJsonEntryBytes = 1024 * 1024;
    private const long MaxSnapshotBytes = 16L * 1024 * 1024;
    private const long MaxPreviewBytes = 16L * 1024 * 1024;
    private const int MaxEntryNameCharacters = 256;
    private const int MaxSegmentCharacters = 128;
    private const int MaxManifestAssetCount = 256;
    private const int MaxDocumentNodes = 4096;
    private const int MaxDocumentRenderUnits = 16384;
    private const int MaxImageReferences = 512;
    private const long MaxEmbeddedDerivativeCharacters = 32L * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions ValidationJson = new(StorageJson.Options)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly HashSet<string> ManifestProperties =
    [
        "schemaVersion",
        "projectId",
        "moduleId",
        "moduleVersion",
        "recipeId",
        "recipeVersion",
        "createdUtc",
        "modifiedUtc",
        "dataLane",
        "retentionMode",
        "sourceLocale",
        "outputLocale",
        "engineVersion",
        "artifactPath",
        "assetIds",
        "purpose",
    ];

    private static readonly HashSet<string> RequiredManifestProperties =
        [.. ManifestProperties.Where(property => !string.Equals(property, "purpose", StringComparison.Ordinal))];

    private static readonly HashSet<string> ArtifactProperties = ["nodes", "language"];

    private static readonly HashSet<string> RequiredArtifactProperties = ["nodes"];

    private static readonly HashSet<string> ValidationProperties =
    [
        "schemaVersion",
        "kind",
        "recipeId",
        "recipeVersion",
        "lane",
        "purpose",
        "artifactSha256",
        "untrustedNoticeCodes",
    ];

    private static readonly HashSet<string> RenderProfileProperties =
    [
        "schemaVersion",
        "artifactSha256",
        "audience",
        "textScalePercent",
        "targetLanguageFirst",
    ];

    private static readonly HashSet<string> ProvenanceProperties =
    [
        "id",
        "conceptId",
        "version",
        "fileName",
        "mimeType",
        "source",
        "creator",
        "license",
        "sha256",
        "intendedMeaning",
        "altText",
        "redistributable",
        "ambiguityNotes",
        "requiredAttribution",
        "modifications",
    ];

    private static readonly HashSet<string> RequiredProvenanceProperties =
    [
        "id",
        "conceptId",
        "version",
        "fileName",
        "mimeType",
        "source",
        "creator",
        "license",
        "sha256",
        "intendedMeaning",
        "altText",
        "redistributable",
    ];

    private static readonly HashSet<string> SafeSnapshotTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "head", "meta", "title", "style", "body",
        "h1", "h2", "h3", "h4", "h5", "h6", "p", "ol", "ul", "li", "div",
        "footer", "section", "table", "thead", "tbody", "tr", "th", "td", "figure",
        "figcaption", "span", "cite", "aside", "svg", "line", "circle", "rect", "text",
    };

    private static readonly Dictionary<string, HashSet<string>> SafeSnapshotAttributes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["html"] = SnapshotAttributes("lang", "dir"),
            ["head"] = SnapshotAttributes(),
            ["meta"] = SnapshotAttributes("charset"),
            ["title"] = SnapshotAttributes(),
            ["style"] = SnapshotAttributes(),
            ["body"] = SnapshotAttributes(),
            ["h1"] = SnapshotAttributes(),
            ["h2"] = SnapshotAttributes(),
            ["h3"] = SnapshotAttributes(),
            ["h4"] = SnapshotAttributes(),
            ["h5"] = SnapshotAttributes(),
            ["h6"] = SnapshotAttributes(),
            ["p"] = SnapshotAttributes("class", "lang", "dir"),
            ["ol"] = SnapshotAttributes("class", "start"),
            ["ul"] = SnapshotAttributes("class"),
            ["li"] = SnapshotAttributes(),
            ["div"] = SnapshotAttributes("class", "aria-hidden"),
            ["footer"] = SnapshotAttributes("class"),
            ["section"] = SnapshotAttributes("class"),
            ["table"] = SnapshotAttributes(),
            ["thead"] = SnapshotAttributes(),
            ["tbody"] = SnapshotAttributes(),
            ["tr"] = SnapshotAttributes(),
            ["th"] = SnapshotAttributes("scope"),
            ["td"] = SnapshotAttributes(),
            ["figure"] = SnapshotAttributes("class", "data-asset-id", "role", "aria-label"),
            ["figcaption"] = SnapshotAttributes(),
            ["span"] = SnapshotAttributes("class"),
            ["cite"] = SnapshotAttributes(),
            ["aside"] = SnapshotAttributes("class"),
            ["svg"] = SnapshotAttributes("xmlns", "viewBox", "width", "height", "role", "aria-label"),
            ["line"] = SnapshotAttributes("x1", "y1", "x2", "y2", "stroke", "stroke-width", "stroke-linecap", "stroke-dasharray"),
            ["circle"] = SnapshotAttributes("cx", "cy", "r", "fill", "stroke", "stroke-width"),
            ["rect"] = SnapshotAttributes("x", "y", "width", "height", "fill", "stroke", "stroke-width"),
            ["text"] = SnapshotAttributes("x", "y", "font-size", "font-family", "text-anchor"),
        };

    internal static async Task<ProjectManifest> ReadRoutingManifestAsync(
        Stream package,
        CancellationToken cancellationToken)
    {
        var inspection = await InspectAsync(package, fullValidation: false, cancellationToken).ConfigureAwait(false);
        return inspection.Manifest;
    }

    internal static async Task<LoadedProject> ValidateAsync(
        Stream package,
        CancellationToken cancellationToken)
    {
        var inspection = await InspectAsync(package, fullValidation: true, cancellationToken).ConfigureAwait(false);
        return new LoadedProject(
            inspection.Manifest,
            inspection.Document ?? throw Invalid("package.artifact-missing", "The project package has no validated artifact."),
            inspection.Validation,
            inspection.RenderProfile,
            inspection.Assets);
    }

    internal static bool IsSafePackageSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MaxSegmentCharacters
            || value is "." or ".."
            || value[0] == '.'
            || !value.IsNormalized(NormalizationForm.FormC))
        {
            return false;
        }

        return value.All(character =>
            char.IsLetterOrDigit(character)
            || character is '-' or '_' or '.');
    }

    internal static void ValidateSnapshotBytes(ReadOnlySpan<byte> bytes)
    {
        string html;
        try
        {
            html = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw Invalid("package.snapshot-encoding", "The project snapshot is not valid UTF-8.");
        }

        if (html.Contains('\0'))
        {
            throw Invalid("package.snapshot-active-content", "The project snapshot contains active or remote content.");
        }

        var sawHtml = false;
        var sawBody = false;
        var sawCharset = false;
        var position = 0;
        while (position < html.Length)
        {
            var tagStart = html.IndexOf('<', position);
            if (tagStart < 0)
            {
                break;
            }

            if (StartsWithAt(html, tagStart, "<!--", StringComparison.Ordinal)
                || StartsWithAt(html, tagStart, "<?", StringComparison.Ordinal))
            {
                throw Invalid("package.snapshot-structure", "The project snapshot is not a bounded self-contained HTML document.");
            }

            var tagEnd = FindSnapshotTagEnd(html, tagStart);
            var token = html[(tagStart + 1)..tagEnd].Trim();
            if (token.StartsWith("!DOCTYPE", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(token, "!DOCTYPE html", StringComparison.OrdinalIgnoreCase))
                {
                    throw Invalid("package.snapshot-structure", "The project snapshot is not a bounded self-contained HTML document.");
                }

                position = tagEnd + 1;
                continue;
            }

            if (token.StartsWith('!'))
            {
                throw Invalid("package.snapshot-structure", "The project snapshot is not a bounded self-contained HTML document.");
            }

            if (token.StartsWith('/'))
            {
                var closingName = token[1..].Trim();
                if (!IsSnapshotName(closingName) || !SafeSnapshotTags.Contains(closingName))
                {
                    throw Invalid("package.snapshot-active-content", "The project snapshot contains active or remote content.");
                }

                position = tagEnd + 1;
                continue;
            }

            var parsed = ParseSnapshotStartTag(token);
            if (!SafeSnapshotTags.Contains(parsed.Name))
            {
                throw Invalid("package.snapshot-active-content", "The project snapshot contains active or remote content.");
            }

            ValidateSnapshotAttributes(parsed.Name, parsed.Attributes);
            sawHtml |= parsed.Name.Equals("html", StringComparison.OrdinalIgnoreCase);
            sawBody |= parsed.Name.Equals("body", StringComparison.OrdinalIgnoreCase);
            if (parsed.Name.Equals("meta", StringComparison.OrdinalIgnoreCase))
            {
                if (parsed.Attributes.Count != 1
                    || !parsed.Attributes.TryGetValue("charset", out var charset)
                    || !charset.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
                {
                    throw Invalid("package.snapshot-active-content", "The project snapshot contains active or remote content.");
                }

                sawCharset = true;
            }

            if (parsed.Name.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                var styleEnd = html.IndexOf("</style", tagEnd + 1, StringComparison.OrdinalIgnoreCase);
                if (styleEnd < 0)
                {
                    throw Invalid("package.snapshot-structure", "The project snapshot is not a bounded self-contained HTML document.");
                }

                ValidateSnapshotStyle(html[(tagEnd + 1)..styleEnd]);
            }

            position = tagEnd + 1;
        }

        if (!sawHtml || !sawBody || !sawCharset)
        {
            throw Invalid("package.snapshot-structure", "The project snapshot is not a bounded self-contained HTML document.");
        }
    }

    private static int FindSnapshotTagEnd(string html, int tagStart)
    {
        char quote = '\0';
        for (var index = tagStart + 1; index < html.Length; index++)
        {
            var character = html[index];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
            }
            else if (character == '>')
            {
                return index;
            }
            else if (character == '<')
            {
                break;
            }
        }

        throw Invalid("package.snapshot-structure", "The project snapshot is not a bounded self-contained HTML document.");
    }

    private static SnapshotStartTag ParseSnapshotStartTag(string token)
    {
        var selfClosing = token.EndsWith('/');
        var limit = selfClosing ? token.Length - 1 : token.Length;
        var position = 0;
        SkipSnapshotWhitespace(token, ref position, limit);
        var name = ReadSnapshotName(token, ref position, limit);
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (position < limit)
        {
            SkipSnapshotWhitespace(token, ref position, limit);
            if (position >= limit)
            {
                break;
            }

            var attributeName = ReadSnapshotName(token, ref position, limit);
            SkipSnapshotWhitespace(token, ref position, limit);
            if (position >= limit || token[position] != '=')
            {
                throw Invalid("package.snapshot-structure", "The project snapshot is not a bounded self-contained HTML document.");
            }

            position++;
            SkipSnapshotWhitespace(token, ref position, limit);
            if (position >= limit || token[position] is not ('\'' or '"'))
            {
                throw Invalid("package.snapshot-structure", "The project snapshot is not a bounded self-contained HTML document.");
            }

            var quote = token[position++];
            var valueStart = position;
            while (position < limit && token[position] != quote)
            {
                position++;
            }

            if (position >= limit || !attributes.TryAdd(attributeName, token[valueStart..position]))
            {
                throw Invalid("package.snapshot-structure", "The project snapshot is not a bounded self-contained HTML document.");
            }

            position++;
        }

        return new SnapshotStartTag(name, attributes);
    }

    private static void ValidateSnapshotAttributes(string tagName, IReadOnlyDictionary<string, string> attributes)
    {
        if (!SafeSnapshotAttributes.TryGetValue(tagName, out var admittedAttributes))
        {
            throw Invalid("package.snapshot-active-content", "The project snapshot contains active or remote content.");
        }

        foreach (var (attributeName, value) in attributes)
        {
            if (!admittedAttributes.Contains(attributeName)
                || value.Any(char.IsControl)
                || value.Contains("url(", StringComparison.OrdinalIgnoreCase))
            {
                throw Invalid("package.snapshot-active-content", "The project snapshot contains active or remote content.");
            }

            if (attributeName.Equals("xmlns", StringComparison.OrdinalIgnoreCase)
                && (!tagName.Equals("svg", StringComparison.OrdinalIgnoreCase)
                    || !value.Equals("http://www.w3.org/2000/svg", StringComparison.Ordinal)))
            {
                throw Invalid("package.snapshot-active-content", "The project snapshot contains active or remote content.");
            }
        }
    }

    private static HashSet<string> SnapshotAttributes(params string[] names)
        => new(names, StringComparer.OrdinalIgnoreCase);

    private static void ValidateSnapshotStyle(string style)
    {
        string[] forbiddenStyleFragments =
        [
            "/*", "\\", "url(", "@import", "expression(", "@font-face", "@namespace",
            "image-set(", "src:", "http:", "https:", "data:", "blob:", "file:",
            "javascript:", "vbscript:", "behavior:", "-moz-binding",
        ];
        if (forbiddenStyleFragments.Any(fragment => style.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            throw Invalid("package.snapshot-active-content", "The project snapshot contains active or remote content.");
        }
    }

    private static string ReadSnapshotName(string token, ref int position, int limit)
    {
        var start = position;
        while (position < limit && IsSnapshotNameCharacter(token[position]))
        {
            position++;
        }

        if (start == position)
        {
            throw Invalid("package.snapshot-structure", "The project snapshot is not a bounded self-contained HTML document.");
        }

        return token[start..position];
    }

    private static bool IsSnapshotName(string name)
        => name.Length > 0 && name.All(IsSnapshotNameCharacter);

    private static bool IsSnapshotNameCharacter(char character)
        => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or ':';

    private static void SkipSnapshotWhitespace(string token, ref int position, int limit)
    {
        while (position < limit && char.IsWhiteSpace(token[position]))
        {
            position++;
        }
    }

    private static bool StartsWithAt(string text, int startIndex, string value, StringComparison comparison)
        => text.AsSpan(startIndex).StartsWith(value, comparison);

    internal static IReadOnlyList<string> ReferencedAssetIds(ArtifactDocument document)
        => [.. ReferencedAssetOccurrences(document).Keys.Order(StringComparer.Ordinal)];

    private static Dictionary<string, int> ReferencedAssetOccurrences(ArtifactDocument document)
    {
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in document.Nodes)
        {
            var image = node switch
            {
                ImageReference direct => direct,
                StepRow { Symbol: { } symbol } => symbol,
                _ => null,
            };
            if (image is null)
            {
                continue;
            }

            occurrences.TryGetValue(image.Asset.Value, out var count);
            occurrences[image.Asset.Value] = count + 1;
        }

        return occurrences;
    }

    private static async Task<PackageInspection> InspectAsync(
        Stream package,
        bool fullValidation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);
        cancellationToken.ThrowIfCancellationRequested();

        if (!package.CanRead || !package.CanSeek)
        {
            throw Invalid("package.stream", "The project package requires a readable seekable stream.");
        }

        try
        {
            if (package.Length > MaxPackageBytes)
            {
                throw Invalid("package.size", "The project package exceeds the bounded package size.");
            }

            package.Position = 0;
            using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
            var entries = ValidateArchiveEnvelope(archive);

            var manifestBytes = await ReadEntryBytesAsync(
                RequireEntry(entries, "manifest.json"),
                MaxSmallJsonEntryBytes,
                cancellationToken).ConfigureAwait(false);
            using var manifestJson = ValidateJsonObject(
                manifestBytes,
                ManifestProperties,
                RequiredManifestProperties,
                "package.manifest-shape");
            var manifest = Deserialize<ProjectManifest>(manifestBytes, "package.manifest-json");
            ValidateManifest(manifest);

            if (!fullValidation)
            {
                return new PackageInspection(manifest, null, null, null, null);
            }

            if (!string.Equals(manifest.SchemaVersion, EngineIdentity.ProjectSchemaVersion, StringComparison.Ordinal))
            {
                throw Invalid("package.schema-version", "The project package schema version is unsupported.");
            }

            var artifactBytes = await ReadEntryBytesAsync(
                RequireEntry(entries, "artifact.json"),
                MaxJsonEntryBytes,
                cancellationToken).ConfigureAwait(false);
            using var artifactJson = ValidateJsonObject(
                artifactBytes,
                ArtifactProperties,
                RequiredArtifactProperties,
                "package.artifact-shape");
            EnsureNoDuplicateJsonProperties(artifactJson.RootElement, "package.artifact-duplicates");
            var document = Deserialize<ArtifactDocument>(artifactBytes, "package.artifact-json");
            ValidateDocument(manifest, document);

            var snapshotBytes = await ReadEntryBytesAsync(
                RequireEntry(entries, "snapshot.html"),
                MaxSnapshotBytes,
                cancellationToken).ConfigureAwait(false);
            ValidateSnapshotBytes(snapshotBytes);

            var expectedEntries = new HashSet<string>(StringComparer.Ordinal)
            {
                "manifest.json",
                "artifact.json",
                "snapshot.html",
            };
            ProjectValidationEnvelope? validation = null;
            ProjectRenderProfile? renderProfile = null;
            var hasValidation = entries.TryGetValue("validation.json", out var validationEntry);
            var hasRenderProfile = entries.TryGetValue("render-profile.json", out var renderProfileEntry);
            if (hasValidation != hasRenderProfile)
            {
                throw Invalid(
                    "package.context-pair",
                    "The project validation context and render profile must appear together.");
            }

            if (hasValidation && hasRenderProfile)
            {
                var validationBytes = await ReadEntryBytesAsync(
                    validationEntry!,
                    MaxSmallJsonEntryBytes,
                    cancellationToken).ConfigureAwait(false);
                using var validationJson = ValidateJsonObject(
                    validationBytes,
                    ValidationProperties,
                    ValidationProperties,
                    "package.validation-shape");
                EnsureNoDuplicateJsonProperties(validationJson.RootElement, "package.validation-duplicates");
                validation = Deserialize<ProjectValidationEnvelope>(validationBytes, "package.validation-json");

                var renderProfileBytes = await ReadEntryBytesAsync(
                    renderProfileEntry!,
                    MaxSmallJsonEntryBytes,
                    cancellationToken).ConfigureAwait(false);
                using var renderProfileJson = ValidateJsonObject(
                    renderProfileBytes,
                    RenderProfileProperties,
                    RenderProfileProperties,
                    "package.render-profile-shape");
                EnsureNoDuplicateJsonProperties(renderProfileJson.RootElement, "package.render-profile-duplicates");
                renderProfile = Deserialize<ProjectRenderProfile>(renderProfileBytes, "package.render-profile-json");
                ValidateProjectContext(manifest, document, validation, renderProfile);
                expectedEntries.Add("validation.json");
                expectedEntries.Add("render-profile.json");
            }

            ValidateSnapshotCorrespondence(
                manifest,
                document,
                renderProfile,
                snapshotBytes);

            var assetFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resolvedAssets = new List<(AssetProvenance Provenance, byte[] Content)>();
            var assetOccurrences = ReferencedAssetOccurrences(document);
            long embeddedDerivativeCharacters = 0;

            foreach (var assetId in manifest.AssetIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var provenanceEntryName = $"provenance/{assetId}.json";
                if (!entries.TryGetValue(provenanceEntryName, out var provenanceEntry))
                {
                    throw Invalid("package.provenance-missing", "The project asset manifest and provenance records disagree.");
                }

                var provenanceBytes = await ReadEntryBytesAsync(
                    provenanceEntry,
                    MaxSmallJsonEntryBytes,
                    cancellationToken).ConfigureAwait(false);
                using var provenanceJson = ValidateJsonObject(
                    provenanceBytes,
                    ProvenanceProperties,
                    RequiredProvenanceProperties,
                    "package.provenance-shape");
                EnsureNoDuplicateJsonProperties(provenanceJson.RootElement, "package.provenance-duplicates");
                var provenance = Deserialize<AssetProvenance>(provenanceBytes, "package.provenance-json");
                ValidateProvenance(assetId, provenance);

                if (!assetFileNames.Add(provenance.FileName))
                {
                    throw Invalid("package.asset-collision", "The project package has colliding asset file names.");
                }

                var assetEntryName = $"assets/{provenance.FileName}";
                var assetEntry = RequireEntry(entries, assetEntryName);
                var assetContent = await ReadEntryBytesAsync(
                    assetEntry,
                    EntryLimit(assetEntryName),
                    cancellationToken).ConfigureAwait(false);
                var actualHash = Convert.ToHexString(SHA256.HashData(assetContent));
                if (!actualHash.Equals(provenance.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw Invalid("package.asset-hash", "A project asset does not match its provenance hash.");
                }

                if (!AccessibleHtmlRenderer.IsSupportedSelfContainedImage(assetContent, provenance.MimeType, cancellationToken))
                {
                    throw Invalid("package.asset-content", "A project asset is not a supported, self-contained image.");
                }

                var base64Characters = ((long)assetContent.Length + 2) / 3 * 4;
                var perReferenceCharacters = checked(base64Characters + provenance.MimeType.Length + 13);
                embeddedDerivativeCharacters = checked(
                    embeddedDerivativeCharacters + (perReferenceCharacters * assetOccurrences[assetId]));
                if (embeddedDerivativeCharacters > MaxEmbeddedDerivativeCharacters)
                {
                    throw Invalid(
                        "package.asset-derivative-budget",
                        "The project image references exceed the bounded embedded-derivative budget.");
                }

                resolvedAssets.Add((provenance, assetContent));

                expectedEntries.Add(provenanceEntryName);
                expectedEntries.Add(assetEntryName);
            }

            foreach (var preview in entries.Values.Where(entry => entry.FullName.StartsWith("previews/", StringComparison.Ordinal)))
            {
                await ValidatePngPreviewAsync(preview, cancellationToken).ConfigureAwait(false);
                expectedEntries.Add(preview.FullName);
            }

            if (entries.Keys.Any(name => !expectedEntries.Contains(name)))
            {
                throw Invalid("package.entry-unknown", "The project package contains an entry outside the admitted topology.");
            }

            return new PackageInspection(
                manifest,
                document,
                validation,
                renderProfile,
                new ValidatedPackageAssetCatalog(resolvedAssets));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OcfprojPackageException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            IOException or
            InvalidDataException or
            JsonException or
            DecoderFallbackException or
            OverflowException or
            NotSupportedException or
            ArgumentException)
        {
            throw Invalid("package.malformed", "The project package is malformed.");
        }
        finally
        {
            if (package.CanSeek)
            {
                package.Position = 0;
            }
        }
    }

    private static Dictionary<string, ZipArchiveEntry> ValidateArchiveEnvelope(ZipArchive archive)
    {
        if (archive.Entries.Count is 0 or > MaxEntries)
        {
            throw Invalid("package.entry-count", "The project package has an invalid number of entries.");
        }

        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        var collisionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalCompressed = 0;
        long totalUncompressed = 0;

        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            if (!collisionNames.Add(name) || !entries.TryAdd(name, entry))
            {
                throw Invalid("package.entry-collision", "The project package has colliding entry names.");
            }

            if (!IsNormalizedEntryName(name) || !IsAdmittedEntryShape(name))
            {
                throw Invalid("package.entry-name", "The project package has an unsafe or unknown entry name.");
            }

            var entryLimit = EntryLimit(name);
            if (entry.Length < 0 || entry.CompressedLength < 0 || entry.Length > entryLimit)
            {
                throw Invalid("package.entry-size", "A project package entry exceeds its bounded size ceiling.");
            }

            totalCompressed = checked(totalCompressed + entry.CompressedLength);
            totalUncompressed = checked(totalUncompressed + entry.Length);
            if (totalCompressed > MaxPackageBytes || totalUncompressed > MaxTotalUncompressedBytes)
            {
                throw Invalid("package.total-size", "The project package exceeds its total size bounds.");
            }
        }

        return entries;
    }

    private static bool IsNormalizedEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name.Length > MaxEntryNameCharacters
            || name[0] == '/'
            || name[^1] == '/'
            || name.Contains('\\', StringComparison.Ordinal)
            || name.Contains(':', StringComparison.Ordinal)
            || name.Contains("//", StringComparison.Ordinal)
            || !name.IsNormalized(NormalizationForm.FormC))
        {
            return false;
        }

        return name.Split('/').All(IsSafePackageSegment);
    }

    private static bool IsAdmittedEntryShape(string name)
    {
        if (name is "manifest.json" or "artifact.json" or "snapshot.html" or "validation.json" or "render-profile.json")
        {
            return true;
        }

        if (name.StartsWith("assets/", StringComparison.Ordinal))
        {
            return name.Count(character => character == '/') == 1;
        }

        if (name.StartsWith("provenance/", StringComparison.Ordinal)
            && name.EndsWith(".json", StringComparison.Ordinal))
        {
            return name.Count(character => character == '/') == 1
                && IsSafePackageSegment(name["provenance/".Length..^".json".Length]);
        }

        return name.StartsWith("previews/", StringComparison.Ordinal)
            && name.EndsWith(".png", StringComparison.Ordinal)
            && name.Count(character => character == '/') == 1;
    }

    private static long EntryLimit(string name)
        => name switch
        {
            "manifest.json" => MaxSmallJsonEntryBytes,
            "artifact.json" => MaxJsonEntryBytes,
            "snapshot.html" => MaxSnapshotBytes,
            "validation.json" or "render-profile.json" => MaxSmallJsonEntryBytes,
            _ when name.StartsWith("provenance/", StringComparison.Ordinal) => MaxSmallJsonEntryBytes,
            _ when name.StartsWith("previews/", StringComparison.Ordinal) => MaxPreviewBytes,
            _ => MaxJsonEntryBytes,
        };

    private static ZipArchiveEntry RequireEntry(
        Dictionary<string, ZipArchiveEntry> entries,
        string name)
        => entries.TryGetValue(name, out var entry)
            ? entry
            : throw Invalid("package.entry-missing", "The project package is missing a required entry.");

    private static async Task<byte[]> ReadEntryBytesAsync(
        ZipArchiveEntry entry,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (entry.Length > maximumBytes || entry.Length > int.MaxValue)
        {
            throw Invalid("package.entry-size", "A project package entry exceeds its bounded size ceiling.");
        }

        var content = new byte[checked((int)entry.Length)];
        await using var input = entry.Open();
        var offset = 0;
        while (offset < content.Length)
        {
            var read = await input.ReadAsync(content.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw Invalid("package.entry-truncated", "A project package entry ended before its declared size.");
            }

            offset += read;
        }

        if (await input.ReadAsync(new byte[1], cancellationToken).ConfigureAwait(false) != 0)
        {
            throw Invalid("package.entry-overrun", "A project package entry exceeds its declared size.");
        }

        return content;
    }

    private static JsonDocument ValidateJsonObject(
        ReadOnlySpan<byte> bytes,
        HashSet<string> allowedProperties,
        HashSet<string> requiredProperties,
        string code)
    {
        var document = ParseJsonObject(bytes, code);

        var properties = document.RootElement.EnumerateObject().Select(property => property.Name).ToList();
        if (properties.Distinct(StringComparer.OrdinalIgnoreCase).Count() != properties.Count
            || properties.Any(property => !allowedProperties.Contains(property))
            || requiredProperties.Any(required => !properties.Contains(required, StringComparer.Ordinal)))
        {
            document.Dispose();
            throw Invalid(code, "A project package JSON entry has missing, duplicate, or unknown fields.");
        }

        return document;
    }

    private static JsonDocument ParseJsonObject(ReadOnlySpan<byte> bytes, string code)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64,
            });
        }
        catch (JsonException)
        {
            throw Invalid(code, "A project package JSON entry is malformed.");
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            throw Invalid(code, "A project package JSON entry has the wrong shape.");
        }

        return document;
    }

    private static void EnsureNoDuplicateJsonProperties(JsonElement element, string code)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = element.EnumerateObject().ToList();
                if (properties.Select(property => property.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != properties.Count)
                {
                    throw Invalid(code, "A project package JSON entry has duplicate fields.");
                }

                foreach (var property in properties)
                {
                    EnsureNoDuplicateJsonProperties(property.Value, code);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    EnsureNoDuplicateJsonProperties(item, code);
                }

                break;
        }
    }

    private static T Deserialize<T>(ReadOnlySpan<byte> bytes, string code)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(bytes, ValidationJson)
                ?? throw Invalid(code, "A project package JSON entry is empty.");
        }
        catch (JsonException)
        {
            throw Invalid(code, "A project package JSON entry does not satisfy its schema.");
        }
    }

    private static void ValidateManifest(ProjectManifest manifest)
    {
        if (!RequiredText(manifest.SchemaVersion, 32)
            || manifest.ProjectId == Guid.Empty
            || !RequiredText(manifest.ModuleId, 128)
            || !RequiredText(manifest.ModuleVersion, 64)
            || !RequiredText(manifest.RecipeId, 128)
            || !RequiredText(manifest.RecipeVersion, 64)
            || !string.Equals(manifest.RetentionMode, "teacher-managed", StringComparison.Ordinal)
            || !RequiredText(manifest.EngineVersion, 64)
            || !string.Equals(manifest.ArtifactPath, "artifact.json", StringComparison.Ordinal)
            || !Enum.IsDefined(manifest.Purpose)
            || manifest.ModifiedUtc < manifest.CreatedUtc
            || manifest.AssetIds is null
            || manifest.AssetIds.Count > MaxManifestAssetCount)
        {
            throw Invalid("package.manifest-values", "The project manifest has invalid required values.");
        }

        if (manifest.DataLane != DataLane.Green)
        {
            throw Invalid("package.lane", "The project package is outside the Green persistence lane.");
        }

        if ((manifest.SourceLocale is not null && !RequiredText(manifest.SourceLocale, 64))
            || (manifest.OutputLocale is not null && !RequiredText(manifest.OutputLocale, 64))
            || manifest.AssetIds.Any(assetId => !IsSafePackageSegment(assetId))
            || manifest.AssetIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.AssetIds.Count)
        {
            throw Invalid("package.manifest-values", "The project manifest has invalid required values.");
        }
    }

    private static void ValidateDocument(ProjectManifest manifest, ArtifactDocument document)
    {
        if (document.Nodes is null
            || !string.Equals(document.Language, manifest.SourceLocale, StringComparison.Ordinal))
        {
            throw Invalid("package.artifact-manifest", "The project artifact and manifest disagree or were tampered with.");
        }

        if (document.Nodes.Count > MaxDocumentNodes)
        {
            throw Invalid(
                "package.artifact-bounds",
                $"The project artifact exceeds the bounded {MaxDocumentNodes}-node limit.");
        }

        long renderUnits = document.Nodes.Count;
        var imageReferenceCount = 0;
        foreach (var node in document.Nodes)
        {
            renderUnits += node switch
            {
                OrderedSteps steps => steps.Steps.Count,
                UnorderedList list => list.Items.Count,
                TableNode table => (table.HeaderRow?.Count ?? 0)
                    + table.Rows.Count
                    + table.Rows.Sum(row => (long)row.Count),
                ChoiceSet choices => choices.Options.Count,
                VectorGraphic graphic => graphic.Primitives.Count,
                _ => 0,
            };
            if (renderUnits > MaxDocumentRenderUnits)
            {
                throw Invalid(
                    "package.artifact-bounds",
                    $"The project artifact exceeds the bounded {MaxDocumentRenderUnits}-unit limit.");
            }

            if (node is ImageReference or StepRow { Symbol: not null })
            {
                imageReferenceCount++;
                if (imageReferenceCount > MaxImageReferences)
                {
                    throw Invalid(
                        "package.artifact-bounds",
                        $"The project artifact exceeds the bounded {MaxImageReferences}-image-reference limit.");
                }
            }
        }

        IReadOnlyList<ValidationIssue> issues;
        try
        {
            issues = DocumentValidator.Validate(document);
        }
        catch (Exception exception) when (exception is ArgumentException or NullReferenceException)
        {
            throw Invalid("package.artifact-structure", "The project artifact fails structural validation.");
        }

        if (DocumentValidator.HasBlockingIssues(issues))
        {
            throw Invalid("package.artifact-structure", "The project artifact fails structural validation.");
        }

        var documentAssets = ReferencedAssetIds(document);
        var manifestAssets = manifest.AssetIds.Order(StringComparer.Ordinal).ToList();
        if (!documentAssets.SequenceEqual(manifestAssets, StringComparer.Ordinal))
        {
            throw Invalid("package.asset-manifest", "The project artifact and asset manifest disagree.");
        }
    }

    private static void ValidateProjectContext(
        ProjectManifest manifest,
        ArtifactDocument document,
        ProjectValidationEnvelope validation,
        ProjectRenderProfile renderProfile)
    {
        var digest = ArtifactDocumentFingerprint.Compute(document);
        if (validation.SchemaVersion != ProjectValidationEnvelope.CurrentSchemaVersion
            || !string.Equals(validation.Kind, ProjectValidationEnvelope.ExactApprovedDocumentKind, StringComparison.Ordinal)
            || !RequiredText(validation.RecipeId, 128)
            || !RequiredText(validation.RecipeVersion, 64)
            || !string.Equals(validation.RecipeId, manifest.RecipeId, StringComparison.Ordinal)
            || !string.Equals(validation.RecipeVersion, manifest.RecipeVersion, StringComparison.Ordinal)
            || validation.Lane != manifest.DataLane
            || !Enum.IsDefined(validation.Purpose)
            || validation.Purpose != manifest.Purpose
            || !IsSha256(validation.ArtifactSha256)
            || !string.Equals(validation.ArtifactSha256, digest, StringComparison.OrdinalIgnoreCase)
            || validation.UntrustedNoticeCodes is null
            || validation.UntrustedNoticeCodes.Count > 128
            || validation.UntrustedNoticeCodes.Any(code => !ProjectValidationEnvelope.IsStableNoticeCode(code))
            || validation.UntrustedNoticeCodes.Distinct(StringComparer.Ordinal).Count()
                != validation.UntrustedNoticeCodes.Count)
        {
            throw Invalid(
                "package.validation-values",
                "The project validation context does not bind to the admitted manifest and artifact.");
        }

        if (renderProfile.SchemaVersion != ProjectRenderProfile.CurrentSchemaVersion
            || !IsSha256(renderProfile.ArtifactSha256)
            || !string.Equals(renderProfile.ArtifactSha256, digest, StringComparison.OrdinalIgnoreCase)
            || !Enum.IsDefined(renderProfile.Audience)
            || !double.IsFinite(renderProfile.TextScalePercent)
            || renderProfile.TextScalePercent is < 100 or > 200)
        {
            throw Invalid(
                "package.render-profile-values",
                "The project render profile does not bind to the admitted artifact.");
        }
    }

    internal static RenderRequest SnapshotRenderRequest(ProjectRenderProfile? renderProfile)
        => new(
            RenderTarget.AccessibleHtml,
            RenderAudience.Learner,
            renderProfile?.TextScalePercent ?? 100,
            renderProfile?.TargetLanguageFirst ?? false);

    private static void ValidateSnapshotCorrespondence(
        ProjectManifest manifest,
        ArtifactDocument document,
        ProjectRenderProfile? renderProfile,
        ReadOnlySpan<byte> snapshotBytes)
    {
        if (!PortableProjectSnapshot.IsAdmittedRendererVersion(manifest.EngineVersion))
        {
            throw Invalid(
                "package.snapshot-renderer",
                "The project snapshot names no admitted exact renderer version.");
        }

        if (!PortableProjectSnapshot.MatchesExact(
            document,
            manifest.EngineVersion,
            hasPersistedContext: renderProfile is not null,
            SnapshotRenderRequest(renderProfile),
            snapshotBytes))
        {
            throw Invalid(
                "package.snapshot-artifact",
                "The project snapshot does not correspond exactly to its artifact and render profile.");
        }
    }

    private static void ValidateProvenance(string assetId, AssetProvenance provenance)
    {
        if (!string.Equals(provenance.Id.Value, assetId, StringComparison.Ordinal)
            || !IsSafePackageSegment(provenance.Id.Value)
            || !IsSafePackageSegment(provenance.FileName)
            || !AssetRightsPolicy.HasCompleteRequiredMetadata(provenance)
            || !AssetRightsPolicy.HasSafeOptionalMetadata(provenance)
            || !RequiredText(provenance.ConceptId, 128)
            || !RequiredText(provenance.Version, 64)
            || !RequiredText(provenance.MimeType, 128)
            || !RequiredText(provenance.Source, 256)
            || !RequiredText(provenance.Creator, 256)
            || !RequiredText(provenance.License, 128)
            || !RequiredText(provenance.IntendedMeaning, 1024)
            || !RequiredText(provenance.AltText, 2048)
            || !IsSha256(provenance.Sha256))
        {
            throw Invalid(
                "package.provenance-values",
                "A project provenance record has invalid required or optional values.");
        }

        // A private project may retain an explicitly non-redistributable asset
        // under a non-open license. Its boolean may not manufacture or conceal
        // redistribution authority, however.
        if (!AssetRightsPolicy.HasConsistentRedistributionRights(provenance))
        {
            throw Invalid(
                "package.provenance-rights",
                "A project provenance record has inconsistent license and redistribution metadata.");
        }
    }

    private static async Task ValidatePngPreviewAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        var content = await ReadEntryBytesAsync(entry, MaxPreviewBytes, cancellationToken).ConfigureAwait(false);
        if (!AccessibleHtmlRenderer.IsSupportedSelfContainedImage(content, "image/png", cancellationToken))
        {
            throw Invalid("package.preview", "A project preview is not a bounded PNG image.");
        }
    }

    private static bool RequiredText(string? value, int maximumLength)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= maximumLength
            && value.IsNormalized(NormalizationForm.FormC)
            && !value.Any(char.IsControl);

    private static bool IsSha256(string value)
        => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static OcfprojPackageException Invalid(string code, string message)
        => new(code, message);

    private sealed record PackageInspection(
        ProjectManifest Manifest,
        ArtifactDocument? Document,
        ProjectValidationEnvelope? Validation,
        ProjectRenderProfile? RenderProfile,
        IAssetCatalog? Assets);

    private sealed record SnapshotStartTag(
        string Name,
        IReadOnlyDictionary<string, string> Attributes);
}

internal sealed class OcfprojPackageException(string code, string message) : InvalidOperationException(message)
{
    internal string Code { get; } = code;
}
