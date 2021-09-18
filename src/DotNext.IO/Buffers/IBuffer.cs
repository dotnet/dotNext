using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

internal interface IBuffer<T>
    where T : unmanaged
{
    int Length { get; }

    Span<T> Span { get; }
}

[StructLayout(LayoutKind.Auto)]
internal readonly unsafe struct UnsafeBuffer<T> : IBuffer<T>
    where T : unmanaged
{
    private readonly T* ptr;
    private readonly int length;

    internal UnsafeBuffer(T* ptr, int length)
    {
        this.ptr = ptr;
        this.length = length;
    }

    int IBuffer<T>.Length => length;

    Span<T> IBuffer<T>.Span => new(ptr, length);
}

[StructLayout(LayoutKind.Auto)]
internal struct ArrayBuffer<T> : IBuffer<T>, IDisposable
    where T : unmanaged
{
    private MemoryOwner<T> buffer;

    internal ArrayBuffer(int length)
        => buffer = new MemoryOwner<T>(ArrayPool<T>.Shared, length);

    readonly int IBuffer<T>.Length => buffer.Length;

    readonly Span<T> IBuffer<T>.Span => buffer.Memory.Span;

    public void Dispose() => buffer.Dispose();
}