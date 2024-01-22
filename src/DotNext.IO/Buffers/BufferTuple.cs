using System.Buffers;
using System.Collections;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

internal sealed class BufferTuple : MemoryManager<byte>, IReadOnlyList<ReadOnlyMemory<byte>>
{
    private readonly WeakReference second = new(target: null, trackResurrection: false);
    private int offset, length;
    internal ReadOnlyMemory<byte> First;

    internal ReadOnlyMemory<byte> Second
    {
        get => Memory;
        set
        {
            if (MemoryMarshal.TryGetArray(value, out var segment))
            {
                offset = segment.Offset;
                length = segment.Count;
                second.Target = segment.Array;
            }
            else if (MemoryMarshal.TryGetMemoryManager(value, out MemoryManager<byte>? manager, out offset, out length))
            {
                second.Target = manager;
            }
        }
    }

    int IReadOnlyCollection<ReadOnlyMemory<byte>>.Count => 2;

    public override Span<byte> GetSpan() => second.Target switch
    {
        byte[] array => new(array, offset, length),
        MemoryManager<byte> manager => manager.GetSpan().Slice(offset, length),
        _ => [],
    };

    ReadOnlyMemory<byte> IReadOnlyList<ReadOnlyMemory<byte>>.this[int index] => index switch
    {
        0 => First,
        1 => Second,
        _ => ReadOnlyMemory<byte>.Empty,
    };

    public IEnumerator<ReadOnlyMemory<byte>> GetEnumerator()
    {
        yield return First;
        yield return Second;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected override bool TryGetArray(out ArraySegment<byte> segment)
    {
        switch (second.Target)
        {
            case byte[] array:
                segment = new(array, offset, length);
                return true;
            case MemoryManager<byte> manager:
                return MemoryMarshal.TryGetArray(manager.Memory.Slice(offset, length), out segment);
            default:
                segment = default;
                return false;
        }
    }

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        elementIndex += offset;
        return second.Target switch
        {
            byte[] array => Pin(array, elementIndex),
            MemoryManager<byte> manager => manager.Pin(elementIndex),
            _ => default,
        };

        static unsafe MemoryHandle Pin(byte[] array, nint elementIndex)
        {
            var gch = GCHandle.Alloc(array, GCHandleType.Pinned);
            var elementPtr = gch.AddrOfPinnedObject() + elementIndex;
            return new(elementPtr.ToPointer(), gch);
        }
    }

    public override void Unpin()
        => (second.Target as MemoryManager<byte>)?.Unpin();

    internal void Clear()
    {
        First = default;
        second.Target = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Clear();
        }
    }
}