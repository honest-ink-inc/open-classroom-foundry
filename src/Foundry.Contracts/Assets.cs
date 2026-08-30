// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Domain;

namespace Foundry.Contracts;

/// <summary>
/// The complete provenance record of implementation plan §9 and the Symbol Commons
/// kernel (§10.11): every shipped asset carries its source, creator, license,
/// integrity hash, intended meaning, and known ambiguity. Unknown rights block
/// distribution — a document referencing an asset absent from the catalog cannot
/// be saved or exported.
/// </summary>
/// <summary>
/// Proof that a teacher symbol has completed the binding R2-8 privacy preflight.
/// Production callers can receive this capability from <c>SymbolPreflight</c>,
/// but cannot mint one, replace its normalized bytes, or rewrite its provenance.
/// </summary>
public sealed record SymbolSubmission
{
    private readonly byte[] _content;

    internal SymbolSubmission(
        AssetId Id,
        string IntendedMeaning,
        string AltText,
        ReadOnlyMemory<byte> Content,
        string MimeType,
        string TeacherStatedRights,
        string? AmbiguityNotes = null,
        string? License = null)
    {
        this.Id = Id;
        this.IntendedMeaning = IntendedMeaning;
        this.AltText = AltText;
        _content = Content.ToArray();
        this.MimeType = MimeType;
        this.TeacherStatedRights = TeacherStatedRights;
        this.AmbiguityNotes = AmbiguityNotes;
        this.License = License;
    }

    public AssetId Id { get; internal init; }

    public string IntendedMeaning { get; internal init; }

    public string AltText { get; internal init; }

    public int ContentLength => _content.Length;

    public string MimeType { get; internal init; }

    public string TeacherStatedRights { get; internal init; }

    public string? AmbiguityNotes { get; internal init; }

    public string? License { get; internal init; }

    /// <summary>
    /// Returns a defensive copy so a shelf or preview can consume the normalized
    /// bytes without gaining a mutable reference to the preflight capability.
    /// </summary>
    public byte[] CopyContent() => [.. _content];
}

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
