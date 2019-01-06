using System;
using System.Runtime.InteropServices;

namespace Cheats.Runtime.InteropServices
{
    using static Threading.AtomicInteger;

    public abstract class UnmanagedMemoryHandle<T>: SafeHandle
        where T: unmanaged
    {
        internal unsafe UnmanagedMemoryHandle(IUnmanagedMemory<T> memory, bool ownsHandle)
            : base(IntPtr.Zero, ownsHandle)
        {
            handle = new IntPtr(memory.Address);
        }
    }
}