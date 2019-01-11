using System;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices
{
	/// <summary>
	/// Represents handle to unmanaged memory.
	/// </summary>
	/// <typeparam name="T">Type of pointer.</typeparam>
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