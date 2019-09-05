using System;
using System.IO.MemoryMappedFiles;

namespace DotNext.IO.MemoryMappedFiles
{
    using Runtime.InteropServices;

    public unsafe readonly struct UnsafeMemoryMappedViewAccessor : IDisposable
    {
        private readonly MemoryMappedViewAccessor accessor;
        private readonly byte* ptr;

        internal UnsafeMemoryMappedViewAccessor(MemoryMappedViewAccessor accessor)
        {
            MemoryMappedFile f;
            this.accessor = accessor;
            ptr = default;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
        }

        public Pointer<byte> Pointer => new Pointer<byte>(ptr + accessor.PointerOffset);

        public long Length => accessor.Capacity;

        public Span<byte> Bytes => new Span<byte>(ptr, checked((int)accessor.Capacity));

        public void Dispose()
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            accessor.Dispose();
        }
    }
}
