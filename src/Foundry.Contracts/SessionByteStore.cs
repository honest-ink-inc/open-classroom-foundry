using Foundry.Domain;

namespace Foundry.Contracts;

/// <summary>
/// The session-scoped home of captured bytes (plan §6.4/§6.5). Content lives in
/// memory for the active session only; modules and documents hold opaque
/// references, never paths. Purge is best-effort at the managed layer — buffers
/// are zeroed before release — while pagefile and OS-copy residue remains the
/// documented forensic boundary, tested rather than denied.
/// </summary>
public interface ISessionByteStore
{
    int Count { get; }

    SessionByteReference Put(ReadOnlyMemory<byte> content);

    bool TryGet(SessionByteReference reference, out ReadOnlyMemory<byte> content);

    void Release(SessionByteReference reference);

    void PurgeAll();
}
