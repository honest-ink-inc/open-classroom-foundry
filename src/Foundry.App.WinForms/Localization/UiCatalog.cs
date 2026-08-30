// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.ObjectModel;
using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Foundry.Modules.BuiltIn;
using Foundry.Modules.DeterministicPress;

namespace Foundry.App.WinForms;

public enum UiTextDirection
{
    LeftToRight,
    RightToLeft,
}

/// <summary>Mechanically validated review assertion; deployment provenance authenticates the human, not JSON.</summary>
public sealed record UiCatalogReviewMetadata(
    string ReviewerName,
    string ReviewerRole,
    DateTimeOffset ReviewedAtUtc,
    string SourceDigestSha256);

public sealed record UiCatalogProvenance(
    string CatalogId,
    string Creator,
    string Source,
    string License,
    IReadOnlyList<string> ModificationHistory);

/// <summary>Stable ids for dynamic chrome whose neutral fallback lives in a module catalog.</summary>
public static class UiCatalogIds
{
    public const string AllAboardFirstCard = "all-aboard.card.first";
    public const string AllAboardThenCard = "all-aboard.card.then";
    public const string AllAboardNowCard = "all-aboard.card.now";
    public const string AllAboardNextCard = "all-aboard.card.next";
    public const string AllAboardDoneCard = "all-aboard.card.done";

    public static string Chrome(string memberName) => $"chrome.{memberName}";

    public static string PressTitle(string pressId) => $"presses.{pressId}.title";

    public static string PressParameter(string pressId, string parameterKey)
        => $"presses.{pressId}.parameter.{parameterKey}";

    /// <summary>The opaque suffix binds to the unchanged submitted value; visible fallback text is separate.</summary>
    public static string PressChoice(string pressId, string parameterKey, string submittedValue)
    {
        var valueId = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(submittedValue)))[..16];
        return $"presses.{pressId}.parameter.{parameterKey}.choice.{valueId}";
    }

}

/// <summary>
/// The complete neutral chrome inventory and the deterministic packet handed to
/// the multilingual seat. It contains no translated content and grants no review.
/// </summary>
public static class UiCatalogInventory
{
    public const int SchemaVersion = 1;
    public const string ReviewedStatus = "reviewed";
    public const string DraftStatus = "draft";
    public const string RequiredReviewerRole = "multilingual-educator-or-family-liaison";

    private static readonly Lazy<ReadOnlyDictionary<string, string>> Inventory = new(BuildInventory);

    public static IReadOnlyDictionary<string, string> NeutralStrings => Inventory.Value;

    public static string SourceDigestSha256
        => Convert.ToHexStringLower(SHA256.HashData(CanonicalNeutralBytes(NeutralStrings)));

    public static string CreateTemplateJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteString("languageTag", "und");
            writer.WriteString("direction", "ltr");
            writer.WriteStartObject("review");
            writer.WriteString("status", DraftStatus);
            writer.WriteString("reviewerName", "");
            writer.WriteString("reviewerRole", RequiredReviewerRole);
            writer.WriteString("reviewedAtUtc", "");
            writer.WriteString("sourceDigestSha256", SourceDigestSha256);
            writer.WriteEndObject();
            writer.WriteStartObject("provenance");
            writer.WriteString("catalogId", "");
            writer.WriteString("creator", "");
            writer.WriteString("source", "");
            writer.WriteString("license", "");
            writer.WriteStartArray("modificationHistory");
            writer.WriteEndArray();
            writer.WriteEndObject();
            WriteStrings(writer, "neutralStrings", NeutralStrings);
            WriteStrings(writer, "strings", NeutralStrings);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    public static void WriteTemplate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllText(path, CreateTemplateJson(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static ReadOnlyDictionary<string, string> BuildInventory()
    {
        var entries = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in UiStrings.NeutralChrome)
        {
            Add(entries, pair.Key, pair.Value);
        }

        foreach (var press in PressRoomCatalog.All)
        {
            Add(entries, UiCatalogIds.PressTitle(press.Id), press.Title);
            foreach (var parameter in press.Parameters)
            {
                Add(entries, UiCatalogIds.PressParameter(press.Id, parameter.Key), parameter.Label);
                if (parameter is ChoiceParameter choice)
                {
                    for (var index = 0; index < choice.Options.Count; index++)
                    {
                        Add(entries, UiCatalogIds.PressChoice(press.Id, parameter.Key, choice.Options[index]), choice.Options[index]);
                    }
                }
            }
        }

        foreach (var door in ModuleStudioCatalog.All)
        {
            Add(entries, door.Display);
            foreach (var mode in door.Modes)
            {
                Add(entries, mode.Display);
                if (mode.UnavailableReason is not null)
                {
                    Add(entries, mode.UnavailableReason);
                }

                foreach (var field in mode.Fields)
                {
                    Add(entries, field.Display);
                    foreach (var choice in field.Choices)
                    {
                        Add(entries, choice.Display);
                    }

                    foreach (var column in field.Columns)
                    {
                        Add(entries, column.Display);
                        foreach (var choice in column.Choices)
                        {
                            Add(entries, choice.Display);
                        }
                    }
                }
            }
        }

        Add(entries, UiCatalogIds.AllAboardFirstCard, "First");
        Add(entries, UiCatalogIds.AllAboardThenCard, "Then");
        Add(entries, UiCatalogIds.AllAboardNowCard, "Now");
        Add(entries, UiCatalogIds.AllAboardNextCard, "Next");
        Add(entries, UiCatalogIds.AllAboardDoneCard, "Done");

        return new ReadOnlyDictionary<string, string>(entries);
    }

    private static void Add(SortedDictionary<string, string> entries, ModuleDisplayText display)
        => Add(entries, display.LocalizationId, display.Fallback);

    private static void Add(SortedDictionary<string, string> entries, string id, string fallback)
    {
        if (entries.TryGetValue(id, out var existing))
        {
            if (!string.Equals(existing, fallback, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(id);
            }

            return;
        }

        entries.Add(id, fallback);
    }

    private static byte[] CanonicalNeutralBytes(IReadOnlyDictionary<string, string> entries)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            writer.WriteStartObject();
            foreach (var pair in entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteString(pair.Key, pair.Value);
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteStrings(Utf8JsonWriter writer, string propertyName, IReadOnlyDictionary<string, string> strings)
    {
        writer.WriteStartObject(propertyName);
        foreach (var pair in strings.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteString(pair.Key, pair.Value);
        }

        writer.WriteEndObject();
    }
}

internal sealed class ReviewedUiCatalog(
    string languageTag,
    UiTextDirection direction,
    UiCatalogReviewMetadata review,
    UiCatalogProvenance provenance,
    IReadOnlyDictionary<string, string> strings)
{
    public string LanguageTag { get; } = languageTag;

    public UiTextDirection Direction { get; } = direction;

    public UiCatalogReviewMetadata Review { get; } = review;

    public UiCatalogProvenance Provenance { get; } = provenance;

    public string Translate(string id, string neutralFallback)
        => strings.TryGetValue(id, out var translation) ? translation : neutralFallback;
}

/// <summary>
/// Production activation is an exact-byte deployment decision, not a JSON
/// field or command-line decision. A catalog can join this allowlist only in a
/// reviewed source change after the multilingual seat supplies the completed
/// artifact and its provenance. The current build deliberately approves none.
/// </summary>
internal static class UiCatalogDeployment
{
    private static readonly FrozenSet<string> BuildApprovedCatalogSha256 =
        Array.Empty<string>().ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    internal static IReadOnlySet<string> ApprovedCatalogSha256
        => BuildApprovedCatalogSha256;

    internal static ReviewedUiCatalog LoadApproved(
        string path,
        IReadOnlySet<string> approvedCatalogSha256)
        => UiCatalogLoader.LoadApproved(path, approvedCatalogSha256);
}

internal static class UiCatalogLoader
{
    private const int MaxCatalogBytes = 8 * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly string[] RootProperties =
        ["schemaVersion", "languageTag", "direction", "review", "provenance", "neutralStrings", "strings"];

    private static readonly string[] ReviewProperties =
        ["status", "reviewerName", "reviewerRole", "reviewedAtUtc", "sourceDigestSha256"];

    private static readonly string[] ProvenanceProperties =
        ["catalogId", "creator", "source", "license", "modificationHistory"];

    public static ReviewedUiCatalog LoadReviewed(string path)
    {
        var bytes = ReadExactBytes(path);
        return ParseBytes(bytes, path);
    }

    internal static ReviewedUiCatalog LoadApproved(
        string path,
        IReadOnlySet<string> approvedCatalogSha256)
    {
        ArgumentNullException.ThrowIfNull(approvedCatalogSha256);
        var bytes = ReadExactBytes(path);
        var exactSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!approvedCatalogSha256.Contains(exactSha256))
        {
            throw new InvalidDataException(UiStrings.CatalogNotApprovedForBuild);
        }

        return ParseBytes(bytes, path);
    }

    private static byte[] ReadExactBytes(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);
            if (stream.Length is <= 0 or > MaxCatalogBytes)
            {
                throw new InvalidDataException(UiStrings.CatalogFileSizeInvalid);
            }

            var bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception failure) when (failure is IOException
                                             or UnauthorizedAccessException
                                             or ArgumentException
                                             or NotSupportedException)
        {
            throw Refusal(UiStrings.CatalogUnreadable, path, failure.GetType().Name);
        }
    }

    private static ReviewedUiCatalog ParseBytes(byte[] bytes, string path)
    {
        string json;
        try
        {
            json = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException failure)
        {
            throw Refusal(UiStrings.CatalogUnreadable, path, failure.GetType().Name);
        }

        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16,
            });
            return Parse(document.RootElement);
        }
        catch (JsonException failure)
        {
            throw Refusal(UiStrings.CatalogInvalidJson, failure.LineNumber, failure.BytePositionInLine);
        }
    }

    private static ReviewedUiCatalog Parse(JsonElement root)
    {
        var rootValues = ExactObject(root, "root", RootProperties);
        if (rootValues["schemaVersion"].ValueKind != JsonValueKind.Number
            || !rootValues["schemaVersion"].TryGetInt32(out var schemaVersion)
            || schemaVersion != UiCatalogInventory.SchemaVersion)
        {
            throw Refusal(UiStrings.CatalogUnsupportedSchema, rootValues["schemaVersion"].ToString());
        }

        var reviewValues = ExactObject(rootValues["review"], "review", ReviewProperties);
        var status = RequiredString(reviewValues, "status");
        if (status == UiCatalogInventory.DraftStatus)
        {
            throw Refusal(UiStrings.CatalogDraftRefused);
        }

        if (status != UiCatalogInventory.ReviewedStatus)
        {
            throw Refusal(UiStrings.CatalogReviewStatusInvalid, status);
        }

        var languageTag = RequiredString(rootValues, "languageTag");
        ValidateLanguageTag(languageTag);
        var direction = RequiredString(rootValues, "direction") switch
        {
            "ltr" => UiTextDirection.LeftToRight,
            "rtl" => UiTextDirection.RightToLeft,
            var value => throw Refusal(UiStrings.CatalogDirectionInvalid, value),
        };

        var reviewerName = RequiredString(reviewValues, "reviewerName");
        if (!IsTrimmedVisible(reviewerName))
        {
            throw Refusal(UiStrings.CatalogReviewerMissing);
        }

        var reviewerRole = RequiredString(reviewValues, "reviewerRole");
        if (!string.Equals(reviewerRole, UiCatalogInventory.RequiredReviewerRole, StringComparison.Ordinal))
        {
            throw Refusal(UiStrings.CatalogReviewerRoleInvalid, reviewerRole);
        }

        var reviewedAtText = RequiredString(reviewValues, "reviewedAtUtc");
        if (!DateTimeOffset.TryParseExact(
                reviewedAtText,
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var reviewedAtUtc))
        {
            throw Refusal(UiStrings.CatalogReviewDateInvalid, reviewedAtText);
        }

        var digest = RequiredString(reviewValues, "sourceDigestSha256");
        if (!string.Equals(digest, UiCatalogInventory.SourceDigestSha256, StringComparison.Ordinal))
        {
            throw Refusal(UiStrings.CatalogDigestMismatch, digest, UiCatalogInventory.SourceDigestSha256);
        }

        var provenanceValues = ExactObject(rootValues["provenance"], "provenance", ProvenanceProperties);
        var catalogId = RequiredTrimmed(provenanceValues, "catalogId");
        var creator = RequiredTrimmed(provenanceValues, "creator");
        var source = RequiredTrimmed(provenanceValues, "source");
        var license = RequiredTrimmed(provenanceValues, "license");
        var modificationHistory = StringArray(provenanceValues["modificationHistory"], "provenance.modificationHistory");
        if (modificationHistory.Count == 0)
        {
            throw Refusal(UiStrings.CatalogProvenanceInvalid, "modificationHistory");
        }

        var expected = UiCatalogInventory.NeutralStrings;
        var neutral = StringObject(rootValues["neutralStrings"], "neutralStrings");
        ValidateExactKeys(neutral, expected, UiStrings.CatalogNeutralMissing, UiStrings.CatalogNeutralUnknown);
        foreach (var pair in expected)
        {
            if (!string.Equals(neutral[pair.Key], pair.Value, StringComparison.Ordinal))
            {
                throw Refusal(UiStrings.CatalogNeutralChanged, pair.Key);
            }
        }

        var translations = StringObject(rootValues["strings"], "strings");
        ValidateExactKeys(translations, expected, UiStrings.CatalogStringMissing, UiStrings.CatalogStringUnknown);
        foreach (var pair in expected)
        {
            var translation = translations[pair.Key];
            if (!IsTrimmedVisible(translation))
            {
                throw Refusal(UiStrings.CatalogStringBlank, pair.Key);
            }

            if (!FormatTokens(pair.Value, pair.Key).SequenceEqual(FormatTokens(translation, pair.Key), StringComparer.Ordinal))
            {
                throw Refusal(UiStrings.CatalogPlaceholderMismatch, pair.Key);
            }

            if (MnemonicCount(pair.Value) != MnemonicCount(translation))
            {
                throw Refusal(UiStrings.CatalogMnemonicMismatch, pair.Key);
            }
        }

        var review = new UiCatalogReviewMetadata(reviewerName, reviewerRole, reviewedAtUtc, digest);
        var provenance = new UiCatalogProvenance(catalogId, creator, source, license, modificationHistory);
        return new ReviewedUiCatalog(languageTag, direction, review, provenance, new ReadOnlyDictionary<string, string>(translations));
    }

    private static Dictionary<string, JsonElement> ExactObject(JsonElement element, string context, IReadOnlyList<string> expectedProperties)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Refusal(UiStrings.CatalogObjectRequired, context);
        }

        var allowed = expectedProperties.ToHashSet(StringComparer.Ordinal);
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw Refusal(UiStrings.CatalogPropertyUnknown, context, property.Name);
            }

            if (!values.TryAdd(property.Name, property.Value))
            {
                throw Refusal(UiStrings.CatalogPropertyDuplicate, context, property.Name);
            }
        }

        foreach (var property in expectedProperties)
        {
            if (!values.ContainsKey(property))
            {
                throw Refusal(UiStrings.CatalogPropertyMissing, context, property);
            }
        }

        return values;
    }

    private static Dictionary<string, string> StringObject(JsonElement element, string context)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Refusal(UiStrings.CatalogObjectRequired, context);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!values.TryAdd(property.Name, property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()!
                    : throw Refusal(UiStrings.CatalogStringRequired, context, property.Name)))
            {
                throw Refusal(UiStrings.CatalogPropertyDuplicate, context, property.Name);
            }
        }

        return values;
    }

    private static string RequiredString(Dictionary<string, JsonElement> values, string property)
        => values[property].ValueKind == JsonValueKind.String
            ? values[property].GetString()!
            : throw Refusal(UiStrings.CatalogStringRequired, "property", property);

    private static string RequiredTrimmed(Dictionary<string, JsonElement> values, string property)
    {
        var value = RequiredString(values, property);
        if (!IsTrimmedVisible(value))
        {
            throw Refusal(UiStrings.CatalogProvenanceInvalid, property);
        }

        return value;
    }

    private static List<string> StringArray(JsonElement element, string context)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw Refusal(UiStrings.CatalogArrayRequired, context);
        }

        var values = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !IsTrimmedVisible(item.GetString()!))
            {
                throw Refusal(UiStrings.CatalogProvenanceInvalid, context);
            }

            values.Add(item.GetString()!);
        }

        return values;
    }

    private static void ValidateExactKeys(
        IReadOnlyDictionary<string, string> actual,
        IReadOnlyDictionary<string, string> expected,
        string missingTemplate,
        string unknownTemplate)
    {
        foreach (var id in expected.Keys)
        {
            if (!actual.ContainsKey(id))
            {
                throw Refusal(missingTemplate, id);
            }
        }

        foreach (var id in actual.Keys)
        {
            if (!expected.ContainsKey(id))
            {
                throw Refusal(unknownTemplate, id);
            }
        }
    }

    private static void ValidateLanguageTag(string languageTag)
    {
        if (string.Equals(languageTag, "und", StringComparison.Ordinal)
            || string.Equals(languageTag, "ẋẋ", StringComparison.Ordinal)
            || languageTag.StartsWith("qps-", StringComparison.OrdinalIgnoreCase))
        {
            throw Refusal(UiStrings.CatalogLanguageTagInvalid, languageTag);
        }

        try
        {
            var canonical = CultureInfo.GetCultureInfo(languageTag).Name;
            if (canonical.Length == 0 || !string.Equals(canonical, languageTag, StringComparison.Ordinal))
            {
                throw Refusal(UiStrings.CatalogLanguageTagInvalid, languageTag);
            }
        }
        catch (CultureNotFoundException)
        {
            throw Refusal(UiStrings.CatalogLanguageTagInvalid, languageTag);
        }
    }

    private static List<string> FormatTokens(string text, string localizationId)
    {
        try
        {
            _ = CompositeFormat.Parse(text);
        }
        catch (FormatException)
        {
            throw Refusal(UiStrings.CatalogFormatInvalid, localizationId);
        }

        var tokens = new List<string>();
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '{')
            {
                if (index + 1 < text.Length && text[index + 1] == '{')
                {
                    index++;
                    continue;
                }

                var close = text.IndexOf('}', index + 1);
                if (close < 0)
                {
                    throw Refusal(UiStrings.CatalogFormatInvalid, localizationId);
                }

                var token = text[(index + 1)..close];
                var separator = token.IndexOfAny([',', ':']);
                var indexText = separator < 0 ? token : token[..separator];
                if (!int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                {
                    throw Refusal(UiStrings.CatalogFormatInvalid, localizationId);
                }

                tokens.Add(token);
                index = close;
            }
            else if (text[index] == '}')
            {
                if (index + 1 < text.Length && text[index + 1] == '}')
                {
                    index++;
                }
                else
                {
                    throw Refusal(UiStrings.CatalogFormatInvalid, localizationId);
                }
            }
        }

        tokens.Sort(StringComparer.Ordinal);
        return tokens;
    }

    private static int MnemonicCount(string text)
    {
        var count = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '&' || index + 1 >= text.Length)
            {
                continue;
            }

            if (text[index + 1] == '&')
            {
                index++;
            }
            else if (!char.IsWhiteSpace(text[index + 1]))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsTrimmedVisible(string value)
        => !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && !value.Any(char.IsControl);

    private static InvalidDataException Refusal(string template, params object?[] arguments)
        => new(UiStrings.Format(template, arguments));
}
