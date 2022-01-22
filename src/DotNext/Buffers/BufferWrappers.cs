using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

[StructLayout(LayoutKind.Auto)]
internal struct MemoryOwnerWrapper<T> : IBufferWriter<T>
{
    private readonly MemoryAllocator<T>? allocator;
    internal MemoryOwner<T> Buffer;

    internal MemoryOwnerWrapper(MemoryAllocator<T>? allocator)
    {
        this.allocator = allocator;
        Buffer = default;
    }

    private void Allocate(int length) => Buffer = allocator is null ? new(ArrayPool<T>.Shared, length) : allocator(length);

    Span<T> IBufferWriter<T>.GetSpan(int sizeHint)
    {
        Allocate(sizeHint);
        return Buffer.Span;
    }

    Memory<T> IBufferWriter<T>.GetMemory(int sizeHint)
    {
        Allocate(sizeHint);
        return Buffer.Memory;
    }

    void IBufferWriter<T>.Advance(int count)
    {
        if (count is 0)
        {
            Buffer.Dispose();
        }
        else
        {
            Buffer.Truncate(count);
        }
    }
}