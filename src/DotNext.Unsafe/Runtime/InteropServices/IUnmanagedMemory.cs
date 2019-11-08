using System;
using System.IO;
using System.Threading.Tasks;

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

        /// <summary>
        /// Copies bytes from the memory location to the stream asynchronously.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        /// <returns>The task instance representing asynchronous state of the copying process.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        ValueTask WriteToAsync(Stream destination) => Pointer.WriteToAsync(destination, Size);

        /// <summary>
        /// Copies bytes from the given stream to the memory location identified by this object asynchronously.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        ValueTask<long> ReadFromAsync(Stream source) => Pointer.ReadFromAsync(source, Size);

        /// <summary>
        /// Copies elements from the current memory location to the specified memory location.
        /// </summary>
        /// <param name="destination">The target memory location.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        void WriteTo(Pointer<byte> destination) => Pointer.WriteTo(destination, Size);

        /// <summary>
        /// Copies bytes from the source memory to the memory identified by this object.
        /// </summary>
        /// <param name="source">The pointer to the source unmanaged memory.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        void ReadFrom(Pointer<byte> source) => source.WriteTo(Pointer, Size);

        /// <summary>
        /// Computes bitwise equality between two blocks of memory.
        /// </summary>
        /// <param name="other">The block of memory to be compared.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        bool BitwiseEquals(IUnmanagedMemory other) => Size == other.Size && Pointer.BitwiseEquals(other.Pointer, Size);

        /// <summary>
        /// Bitwise comparison of the memory blocks.
        /// </summary>
        /// <param name="other">The block of memory to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory is released.</exception>
        int BitwiseCompare(IUnmanagedMemory other) => Size == other.Size ? Pointer.BitwiseCompare(other.Pointer, Size) : Size.CompareTo(other.Size);
    }
}