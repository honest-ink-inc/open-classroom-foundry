using Foundry.Domain;

namespace Foundry.Contracts;

/// <summary>
/// The complete provenance record of implementation plan §9 and the Symbol Commons
/// kernel (§10.11): every shipped asset carries its source, creator, license,
/// integrity hash, intended meaning, and known ambiguity. Unknown rights block
/// distribution — a document referencing an asset absent from the catalog cannot
/// be saved or exported.
/// </summary>
public sealed record AssetProvenance(
    AssetId Id,
    string ConceptId,
    string Version,
    string FileName,
    string MimeType,
    string Source,
    string Creator,
    string License,
    string Sha256,
    string IntendedMeaning,
    string AltText,
    bool Redistributable,
    string? AmbiguityNotes = null,
    string? RequiredAttribution = null,
    string? Modifications = null);
