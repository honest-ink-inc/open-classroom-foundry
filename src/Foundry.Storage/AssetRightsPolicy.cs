// SPDX-License-Identifier: GPL-3.0-or-later
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

    public static bool RequiredText(string? value, int maximumLength)
        => !string.IsNullOrWhiteSpace(value)
            && value.Length <= maximumLength
            && !value.Any(char.IsControl);

    public static bool OptionalText(string? value, int maximumLength)
        => value is null
            || (value.Length <= maximumLength && !value.Any(char.IsControl));
}
