// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text;
using Foundry.Contracts;

namespace Foundry.Storage;

/// <summary>
/// The single code-owned interpretation of the licenses that permit an asset to
/// travel in an open symbol pack. A manifest boolean is a claim, not authority:
/// it is valid only when it agrees with this policy.
/// </summary>
internal static class AssetRightsPolicy
{
    private static readonly HashSet<string> KnownOpenLicenses = new(StringComparer.Ordinal)
    {
        "CC0-1.0",
        "CC-BY-4.0",
        "CC-BY-SA-4.0",
    };

    public static bool IsKnownOpenLicense(string? license)
        => license is not null && KnownOpenLicenses.Contains(license);

    public static bool HasConsistentRedistributionRights(AssetProvenance provenance)
        => !string.IsNullOrWhiteSpace(provenance.License)
            && provenance.Redistributable == IsKnownOpenLicense(provenance.License);

    /// <summary>
    /// The stricter boundary for a catalog that ships or can be exported as an
    /// open pack. A private shelf or project may retain a consistently marked
    /// non-open asset; a redistributable catalog may not admit it.
    /// </summary>
    public static bool CanEnterOpenCatalog(AssetProvenance provenance)
        => provenance.Redistributable
            && IsKnownOpenLicense(provenance.License);

    public static bool HasCompleteRequiredMetadata(AssetProvenance provenance)
        => RequiredText(provenance.Id.Value, 128)
            && RequiredText(provenance.ConceptId, 128)
            && RequiredText(provenance.Version, 64)
            && RequiredText(provenance.FileName, 255)
            && RequiredText(provenance.MimeType, 64)
            && RequiredText(provenance.Source, 256)
            && RequiredText(provenance.Creator, 256)
            && RequiredText(provenance.License, 128)
            && RequiredText(provenance.Sha256, 64)
            && RequiredText(provenance.IntendedMeaning, 1024)
            && RequiredText(provenance.AltText, 2048);

    public static bool HasSafeOptionalMetadata(AssetProvenance provenance)
        => OptionalText(provenance.AmbiguityNotes, 2048)
            && OptionalText(provenance.RequiredAttribution, 2048)
            && OptionalText(provenance.Modifications, 2048);

    /// <summary>
    /// Open export cannot treat an absent string as meaning "none." The current
    /// schema already carries these two fields, so requiring an explicit
    /// disposition here hardens distribution without changing durable schema-1
    /// provenance. Whether a disposition is legally sufficient remains a
    /// rights-seat judgment.
    /// </summary>
    public static bool HasExplicitExportDispositions(AssetProvenance provenance)
        => HasSubstantiveText(provenance.RequiredAttribution, 2048)
            && HasSubstantiveText(provenance.Modifications, 2048);

    private static bool HasSubstantiveText(string? value, int maximumLength)
        => RequiredText(value, maximumLength);

    public static bool RequiredText(string? value, int maximumLength)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= maximumLength
            && HasSafeCharacters(value)
            // Joiners and script-significant format marks may occur inside a
            // valid multilingual value, but they cannot be the entire value.
            // Requiring a letter or digit also rejects other visually blank
            // punctuation-only provenance at the open-export boundary.
            && value.EnumerateRunes().Any(Rune.IsLetterOrDigit);

    public static bool OptionalText(string? value, int maximumLength)
        => value is null
            || (value.Length <= maximumLength && HasSafeCharacters(value));

    private static bool HasSafeCharacters(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsControl(character))
            {
                return false;
            }

            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    return false;
                }

                index++;
            }
            else if (char.IsLowSurrogate(character))
            {
                return false;
            }
        }

        return value.EnumerateRunes().All(rune =>
        {
            var category = Rune.GetUnicodeCategory(rune);
            return category is not (UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                // Preserve script-significant format marks (for example
                // Syriac U+070F and Arabic U+06DD) as well as ZWNJ/ZWJ. Reject
                // only format controls that can hide, reorder, or annotate the
                // one-line provenance record without visible content.
                && !IsUnsafeInvisibleOrDirectionalFormat(rune.Value)
                // Unicode classifies these invisible spacing fillers as letters,
                // so the substantive-text check alone cannot distinguish them.
                && rune.Value is not (0x115F or 0x1160 or 0x3164 or 0xFFA0);
        });
    }

    private static bool IsUnsafeInvisibleOrDirectionalFormat(int value)
        => value is 0x00AD // soft hyphen
            or 0x061C // Arabic letter mark
            or 0x180E // Mongolian vowel separator
            or 0x200B // zero-width space
            or 0x200E or 0x200F // directional marks
            or >= 0x202A and <= 0x202E // directional embedding/override
            or >= 0x2060 and <= 0x206F // word joiner and bidi controls
            or 0xFEFF // zero-width no-break space / BOM
            or >= 0xFFF9 and <= 0xFFFB // interlinear annotation controls
            or 0xE0001 // language tag
            or >= 0xE0020 and <= 0xE007F; // invisible tag characters
}
