using System.Buffers;
using System.IO.MemoryMappedFiles;

namespace DotNext.IO.MemoryMappedFiles;

using Runtime.InteropServices;

internal sealed unsafe class MappedMemory : MemoryManager<byte>, IMappedMemory
{
    private readonly MemoryMappedViewAccessor accessor;
    private readonly byte* ptr;

    internal MappedMemory(MemoryMappedViewAccessor accessor)
    {
        if (accessor.Capacity > int.MaxValue)
            throw new ArgumentException(ExceptionMessages.SegmentVeryLarge, nameof(accessor));

        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        this.accessor = accessor;
    }

    int IUnmanagedMemory<byte>.Length => (int)accessor.Capacity;

    nuint IUnmanagedMemory.Size => (nuint)accessor.Capacity;

    public Pointer<byte> Pointer => new(ptr + accessor.PointerOffset);

    Span<byte> IUnmanagedMemory.Bytes => GetSpan();

    Span<byte> IUnmanagedMemory<byte>.Span => GetSpan();

    public Stream AsStream() => Pointer.AsStream(accessor.Capacity, accessor.GetFileAccess());

    public void Flush() => accessor.Flush();

    public override Span<byte> GetSpan() => Pointer.ToSpan((int)accessor.Capacity);

    public override Memory<byte> Memory => CreateMemory((int)accessor.Capacity);

    public override MemoryHandle Pin(int elementIndex) => Pointer.Pin(elementIndex);

    public override void Unpin()
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            accessor.ReleasePointerAndDispose();
        }
    }
}