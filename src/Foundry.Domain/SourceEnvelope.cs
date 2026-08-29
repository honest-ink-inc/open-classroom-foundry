namespace Foundry.Domain;

/// <summary>
/// Opaque session-scoped handle to captured bytes. Modules receive this token,
/// never a filesystem path (implementation plan §6.5).
/// </summary>
public readonly record struct SessionByteReference(Guid Token)
{
    public static SessionByteReference NewReference() => new(Guid.NewGuid());
}

/// <summary>
/// Everything the engine knows about one piece of source material. Original
/// filenames and paths are discarded unless explicitly required.
/// </summary>
public sealed record SourceEnvelope(
    string SourceKind,
    string MimeType,
    int PageCount,
    DataLane Lane,
    bool MetadataStripped,
    string TeacherStatedRights,
    SessionByteReference Bytes);
