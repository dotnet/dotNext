using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
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

        Span<T> IBuffer<T>.Span => new Span<T>(ptr, length);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct ArrayBuffer<T> : IBuffer<T>, IDisposable
        where T : unmanaged
    {
        private readonly MemoryOwner<T> buffer;

        internal ArrayBuffer(int length)
            => buffer = new MemoryOwner<T>(ArrayPool<T>.Shared, length);

        int IBuffer<T>.Length => buffer.Length;

        Span<T> IBuffer<T>.Span => buffer.Memory.Span;

        public void Dispose() => buffer.Dispose();
    }
}