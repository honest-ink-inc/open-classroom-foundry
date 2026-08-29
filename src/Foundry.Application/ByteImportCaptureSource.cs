// SPDX-License-Identifier: GPL-3.0-or-later
using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Application;

/// <summary>
/// The import path of the capture pipeline: the shell reads a teacher-chosen file
/// and hands bytes here — the filename and path are already gone. The envelope
/// starts in the Amber lane (unknown defaults to Amber); the teacher confirms a
/// lane later, and detection may only escalate.
/// </summary>
public sealed class ByteImportCaptureSource(ISessionByteStore store) : ICaptureSource
{
    public const string Kind = "file-import";

    private static readonly HashSet<string> SupportedMimeTypes =
        ["image/png", "image/jpeg", "image/bmp"];

    public Task<SourceEnvelope> CaptureAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Content.IsEmpty)
        {
            throw new ArgumentException("Import received no content.", nameof(request));
        }

        if (!SupportedMimeTypes.Contains(request.MimeType))
        {
            throw new ArgumentException($"Unsupported import type '{request.MimeType}'. PDF and document import arrive with Board to Brief.", nameof(request));
        }

        var reference = store.Put(request.Content);

        var envelope = new SourceEnvelope(
            SourceKind: Kind,
            MimeType: request.MimeType,
            PageCount: 1,
            Lane: LanePolicy.DefaultForUnknown,
            MetadataStripped: false,
            TeacherStatedRights: string.Empty,
            Bytes: reference);

        return Task.FromResult(envelope);
    }
}
