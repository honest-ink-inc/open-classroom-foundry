// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Application;

/// <summary>
/// Product-owner-adopted rehearsal requirement R2-8: a symbol submission is a
/// capture, and it walks
/// the same privacy preflight — metadata stripped by re-encode, teacher crop and
/// redaction burns applied — before its bytes may reach the shelf. The raw and
/// intermediate bytes are released from the session on the way out.
/// </summary>
internal static class SymbolPreflight
{
    internal static async Task<SymbolSubmission> PrepareAsync(
        ReadOnlyMemory<byte> rawImage,
        string mimeType,
        AssetId id,
        string intendedMeaning,
        string altText,
        string teacherStatedRights,
        ISessionByteStore store,
        IDocumentNormalizer normalizer,
        NormalizationRequest normalization,
        DataLane teacherConfirmedLane,
        CancellationToken cancellationToken,
        string? ambiguityNotes = null,
        string? license = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(normalization);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (teacherConfirmedLane != DataLane.Green)
            {
                throw new InvalidOperationException(
                    "A teacher must confirm that a symbol is Green before it can reach the local shelf.");
            }

            var rawReference = store.Put(rawImage);
            var envelope = new SourceEnvelope(
                "symbol-submission", mimeType, 1, LanePolicy.DefaultForUnknown,
                MetadataStripped: false, teacherStatedRights, rawReference);

            var normalized = await normalizer.NormalizeAsync(envelope, normalization, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (!normalized.MetadataStripped
                || normalized.Bytes == rawReference
                || !string.Equals(normalized.MimeType, "image/png", StringComparison.Ordinal)
                || !string.Equals(normalized.SourceKind, envelope.SourceKind, StringComparison.Ordinal)
                || normalized.PageCount != envelope.PageCount
                || normalized.Lane != envelope.Lane
                || !string.Equals(
                    normalized.TeacherStatedRights,
                    envelope.TeacherStatedRights,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Symbol privacy preflight requires a fresh metadata-stripped PNG without envelope drift.");
            }

            if (!store.TryGet(normalized.Bytes, out var content))
            {
                throw new InvalidOperationException("Normalization produced no bytes.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var preflighted = content.ToArray();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                return new SymbolSubmission(
                    id, intendedMeaning, altText, preflighted, normalized.MimeType,
                    teacherStatedRights, ambiguityNotes, license);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(preflighted);
            }
        }
        finally
        {
            // The supplied store is the submission's session-owned transient
            // store. Purge it as one unit so a normalizer that allocated an
            // intermediate before failing cannot leave an undisclosed orphan.
            store.PurgeAll();
        }
    }
}
