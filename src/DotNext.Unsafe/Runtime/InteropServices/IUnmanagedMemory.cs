using System;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Describes a block of unmanaged memory.
    /// </summary>
    public interface IUnmanagedMemory : IDisposable, ICloneable
    {
        /// <summary>
        /// Number of bytes in the allocated memory.
        /// </summary>
        long Size { get; }

        /// <summary>
        /// The address of the unmanaged memory.
        /// </summary>
        IntPtr Address { get; }

        /// <summary>
        /// Obtains typed pointer to the unmanaged memory.
        /// </summary>
        /// <typeparam name="T">The type of the pointer.</typeparam>
        /// <returns>The typed pointer.</returns>
        Pointer<T> ToPointer<T>() where T : unmanaged;
    }

    internal interface IUnmanagedMemory<T> : IUnmanagedMemory
        where T : unmanaged
    {
        Pointer<T> Pointer { get; }

        Span<T> Span { get; }
    }
}