using System;
using System.IO;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Represents common interface for the wrapper of the unmanaged memory.
    /// </summary>
    public interface IUnmanagedMemory : IDisposable
    {
        /// <summary>
        /// Gets size of referenced unmanaged memory, in bytes.
        /// </summary>
        long Size { get; }

        /// <summary>
        /// Sets all bits of allocated memory to zero.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        void Clear();

        /// <summary>
        /// Gets a pointer to the allocated unmanaged memory.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        Pointer<byte> Pointer { get; }

        /// <summary>
        /// Gets a span of bytes from the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        Span<byte> Bytes { get; }

        /// <summary>
        /// Represents unmanaged memory as stream.
        /// </summary>
        /// <returns>The stream of unmanaged memory.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        Stream AsStream();
    }
}