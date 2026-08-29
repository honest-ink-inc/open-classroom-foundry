using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Application;

/// <summary>
/// Council finding R2-8, binding: a symbol submission is a capture, and it walks
/// the same privacy preflight — metadata stripped by re-encode, teacher crop and
/// redaction burns applied — before its bytes may reach the shelf. The raw and
/// intermediate bytes are released from the session on the way out.
/// </summary>
public static class SymbolPreflight
{
    public static async Task<SymbolSubmission> PrepareAsync(
        ReadOnlyMemory<byte> rawImage,
        string mimeType,
        AssetId id,
        string intendedMeaning,
        string altText,
        string teacherStatedRights,
        ISessionByteStore store,
        IDocumentNormalizer normalizer,
        NormalizationRequest normalization,
        CancellationToken cancellationToken,
        string? ambiguityNotes = null,
        string? license = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(normalization);

        var rawReference = store.Put(rawImage);
        var envelope = new SourceEnvelope(
            "symbol-submission", mimeType, 1, LanePolicy.DefaultForUnknown,
            MetadataStripped: false, teacherStatedRights, rawReference);

        var normalized = await normalizer.NormalizeAsync(envelope, normalization, cancellationToken).ConfigureAwait(false);

        if (!store.TryGet(normalized.Bytes, out var content))
        {
            throw new InvalidOperationException("Normalization produced no bytes.");
        }

        var preflighted = content.ToArray();
        store.Release(rawReference);
        store.Release(normalized.Bytes);

        return new SymbolSubmission(
            id, intendedMeaning, altText, preflighted, normalized.MimeType,
            teacherStatedRights, ambiguityNotes, license);
    }
}
