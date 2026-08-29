using Foundry.Contracts;
using Foundry.Domain;

namespace Foundry.Application;

/// <summary>
/// In-memory, session-scoped byte store. Release and purge zero the managed
/// buffers before dropping them — best effort at this layer; the OS residue
/// surface (pagefile, crash dumps) is the documented forensic boundary.
/// </summary>
public sealed class InMemorySessionByteStore : ISessionByteStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<SessionByteReference, byte[]> _content = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _content.Count;
            }
        }
    }

    public SessionByteReference Put(ReadOnlyMemory<byte> content)
    {
        if (content.IsEmpty)
        {
            throw new ArgumentException("Refusing to store empty content.", nameof(content));
        }

        var copy = content.ToArray();
        var reference = SessionByteReference.NewReference();

        lock (_gate)
        {
            _content.Add(reference, copy);
        }

        return reference;
    }

    public bool TryGet(SessionByteReference reference, out ReadOnlyMemory<byte> content)
    {
        lock (_gate)
        {
            if (_content.TryGetValue(reference, out var bytes))
            {
                content = bytes;
                return true;
            }
        }

        content = default;
        return false;
    }

    public void Release(SessionByteReference reference)
    {
        lock (_gate)
        {
            if (_content.Remove(reference, out var bytes))
            {
                Array.Clear(bytes);
            }
        }
    }

    public void PurgeAll()
    {
        lock (_gate)
        {
            foreach (var bytes in _content.Values)
            {
                Array.Clear(bytes);
            }

            _content.Clear();
        }
    }
}
