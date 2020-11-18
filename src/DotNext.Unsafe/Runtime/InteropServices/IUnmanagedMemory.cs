using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Represents common interface for the wrapper of the unmanaged memory.
    /// </summary>
    public interface IUnmanagedMemory : IDisposable, IConvertible<Stream>
    {
        /// <summary>
        /// Gets size of referenced unmanaged memory, in bytes.
        /// </summary>
        long Size { get; }

        /// <summary>
        /// Sets all bits of allocated memory to zero.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
        void Clear() => Pointer.Clear(Size);

        /// <summary>
        /// Gets a pointer to the allocated unmanaged memory.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
        Pointer<byte> Pointer { get; }

        /// <summary>
        /// Gets a span of bytes from the current instance.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
        Span<byte> Bytes { get; }

        /// <summary>
        /// Represents unmanaged memory as stream.
        /// </summary>
        /// <returns>The stream of unmanaged memory.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
        Stream AsStream();

        /// <inheritdoc/>
        Stream IConvertible<Stream>.Convert() => AsStream();

        /// <summary>
        /// Copies bytes from the memory location to the stream.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
        void WriteTo(Stream destination) => Pointer.WriteTo(destination, Size);

        /// <summary>
        /// Copies bytes from the memory location to the stream asynchronously.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task instance representing asynchronous state of the copying process.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteToAsync(Stream destination, CancellationToken token = default) => Pointer.WriteToAsync(destination, Size, token);

        /// <summary>
        /// Copies bytes from the given stream to the memory location identified by this object asynchronously.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
        long ReadFrom(Stream source) => Pointer.ReadFrom(source, Size);

        /// <summary>
        /// Copies bytes from the given stream to the memory location identified by this object asynchronously.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask<long> ReadFromAsync(Stream source, CancellationToken token = default) => Pointer.ReadFromAsync(source, Size, token);

        /// <summary>
        /// Copies elements from the current memory location to the specified memory location.
        /// </summary>
        /// <param name="destination">The target memory location.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
        void WriteTo(Pointer<byte> destination) => Pointer.WriteTo(destination, Size);

        /// <summary>
        /// Copies bytes from the source memory to the memory identified by this object.
        /// </summary>
        /// <param name="source">The pointer to the source unmanaged memory.</param>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
        void ReadFrom(Pointer<byte> source) => source.WriteTo(Pointer, Size);

        /// <summary>
        /// Computes bitwise equality between two blocks of memory.
        /// </summary>
        /// <param name="other">The block of memory to be compared.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
        bool BitwiseEquals(IUnmanagedMemory other) => Size == other.Size && Pointer.BitwiseEquals(other.Pointer, Size);

        /// <summary>
        /// Bitwise comparison of the memory blocks.
        /// </summary>
        /// <param name="other">The block of memory to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        /// <exception cref="ObjectDisposedException">The underlying unmanaged memory has been released.</exception>
        int BitwiseCompare(IUnmanagedMemory other) => Size == other.Size ? Pointer.BitwiseCompare(other.Pointer, Size) : Size.CompareTo(other.Size);
    }

    /// <summary>
    /// Represents unmanaged memory owner.
    /// </summary>
    /// <typeparam name="T">The type of elements in the unmanaged memory.</typeparam>
    public interface IUnmanagedMemory<T> : IUnmanagedMemory, IMemoryOwner<T>, IConvertible<Memory<T>>
        where T : unmanaged
    {
        /// <inheritdoc/>
        Memory<T> IConvertible<Memory<T>>.Convert() => Memory;
    }
}