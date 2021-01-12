using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    using Runtime.InteropServices;

    internal class UnmanagedMemoryManager<T> : MemoryManager<T>
        where T : unmanaged
    {
        private protected IntPtr address;
        private readonly bool owner;

        //TODO: This ctor is reserved for future use by copying methods of Pointer<T> data type to convert raw pointer into Memory<T> type
        internal UnmanagedMemoryManager(IntPtr address, int length)
        {
            this.address = address;
            Length = length;
        }

        private protected UnmanagedMemoryManager(int length, bool zeroMem)
        {
            var size = UnmanagedMemoryHandle.SizeOf<T>(length);
            address = Marshal.AllocHGlobal(new IntPtr(size));
            GC.AddMemoryPressure(size);
            Length = length;
            if (zeroMem)
                Runtime.InteropServices.Memory.ClearBits(address, size);
            owner = true;
        }

        public long Size => UnmanagedMemoryHandle.SizeOf<T>(Length);

        public int Length { get; }

        public unsafe sealed override Span<T> GetSpan() => new Span<T>(address.ToPointer(), Length);

        public unsafe sealed override MemoryHandle Pin(int elementIndex = 0)
        {
            if (address == default)
                throw new ObjectDisposedException(GetType().Name);
            return new MemoryHandle(address.ToPointer<T>() + elementIndex);
        }

        public sealed override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (address != default && owner)
            {
                Marshal.FreeHGlobal(address);
                GC.RemoveMemoryPressure(Size);
            }
            address = default;
        }
    }
}
