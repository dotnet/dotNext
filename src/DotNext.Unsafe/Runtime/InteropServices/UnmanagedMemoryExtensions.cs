using System;
using System.Threading.Tasks;
using System.IO;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Represents extension methods common to all unmanaged memory structures.
    /// </summary>
    public unsafe static class UnmanagedMemoryExtensions
    {
        /// <summary>
        /// Creates a copy of unmanaged memory inside of managed heap.
        /// </summary>
        /// <returns>A copy of unmanaged memory in the form of byte array.</returns>
        public static byte[] ToByteArray<M>(this ref M memory) where M : struct, IUnmanagedMemory => memory.ToPointer<byte>().ToByteArray(memory.Size);

        /// <summary>
		/// Represents unmanaged memory as stream.
		/// </summary>
        /// <param name="memory">The unmanaged memory.</param>
		/// <returns>A stream to unmanaged memory.</returns>
        public static UnmanagedMemoryStream AsStream<M>(this ref M memory)
            where M : struct, IUnmanagedMemory
            => memory.ToPointer<byte>().AsStream(memory.Size);

        /// <summary>
        /// Copies bytes from the memory location to the stream.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source memory location.</param>
        /// <param name="destination">The destination stream.</param>
        public static void WriteTo<M>(this ref M source, Stream destination)
            where M : struct, IUnmanagedMemory
            => source.ToPointer<byte>().WriteTo(destination, source.Size);

        /// <summary>
        /// Copies bytes from the memory location to the stream asynchronously.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source memory location.</param>
        /// <param name="destination">The destination stream.</param>
        /// <returns>The task instance representing asynchronous state of the copying process.</returns>
        public static Task WriteToAsync<M>(this ref M source, Stream destination)
            where M : struct, IUnmanagedMemory
            => source.ToPointer<byte>().WriteToAsync(destination, source.Size);

        /// <summary>
        /// Copies bytes from the memory location to the managed array of bytes.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source memory location.</param>
        /// <param name="destination">The destination array.</param>
        /// <param name="offset">The position in the destination array from which copying begins.</param>
        /// <param name="count">The number of arrays elements to be copied.</param>
        /// <returns>The actual number of copied bytes.</returns>
        public static long WriteTo<M>(this ref M source, byte[] destination, long offset, long count)
            where M : struct, IUnmanagedMemory
            => source.ToPointer<byte>().WriteTo(destination, offset, count);

        /// <summary>
        /// Copies bytes from the memory location to the managed array of bytes.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source memory location.</param>
        /// <param name="destination">The destination array.</param>
        /// <returns>The actual number of copied bytes.</returns>
        public static long WriteTo<M>(this ref M source, byte[] destination)
            where M : struct, IUnmanagedMemory
            => source.WriteTo(destination, 0, destination.LongLength.Min(source.Size));

        /// <summary>
		/// Sets all bits of allocated memory to zero.
		/// </summary>
        /// <param name="memory">The unmanaged memory to be cleared.</param>
		/// <exception cref="NullPointerException">The memory is not allocated.</exception>
        public static void Clear<M>(this ref M memory)
            where M : struct, IUnmanagedMemory
            => memory.ToPointer<byte>().Clear(memory.Size);

        /// <summary>
        /// Copies bytes from the the managed array of bytes to the memory location.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source array.</param>
        /// <param name="destination">The destination memory location.</param>
        /// <param name="offset">The position in the source array from which copying begins.</param>
        /// <param name="count">The number of arrays elements to be copied.</param>
        /// <returns>The actual number of copied bytes.</returns>
        public static long WriteTo<M>(this byte[] source, M destination, long offset, long count)
            where M : IUnmanagedMemory
            => destination.ToPointer<byte>().ReadFrom(source, offset, count);

        /// <summary>
        /// Copies bytes from the the managed array of bytes to the given memory location.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source array.</param>
        /// <param name="destination">The destination memory location.</param>
        /// <returns>The actual number of copied bytes.</returns>
        public static long WriteTo<M>(this byte[] source, M destination)
            where M : IUnmanagedMemory
            => source.WriteTo(destination, 0, source.LongLength.Min(destination.Size));

        /// <summary>
        /// Copies bytes from the stream to the given memory location.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source stream.</param>
        /// <param name="destination">The destination memory location.</param>
        /// <returns>The actual number</returns>
        public static long WriteTo<M>(this Stream source, M destination)
            where M : IUnmanagedMemory
            => destination.ToPointer<byte>().ReadFrom(source, destination.Size);

        /// <summary>
        /// Copies bytes from the stream to the given memory location.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source stream.</param>
        /// <param name="destination">The destination memory location.</param>
        /// <returns>The actual number</returns>
        public static Task<long> WriteToAsync<M>(this Stream source, M destination)
            where M : IUnmanagedMemory
            => destination.ToPointer<byte>().ReadFromAsync(source, destination.Size);

        /// <summary>
        /// Computes bitwise equality between two blocks of memory.
        /// </summary>
        /// <typeparam name="M1">The first type of the unmanaged memory view.</typeparam>
        /// <typeparam name="M2">The second type of the unmanaged memory view.</typeparam>
        /// <param name="first">The first block of memory to be compared.</param>
        /// <param name="second">The second block of memory to be compared.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
        public static bool BitwiseEquals<M1, M2>(this ref M1 first, M2 second)
            where M1 : struct, IUnmanagedMemory
            where M2 : struct, IUnmanagedMemory
            => first.Size == second.Size && first.ToPointer<byte>().BitwiseEquals(second.ToPointer<byte>(), first.Size);

        /// <summary>
        /// Computes 32-bit hash code for the block of memory.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="memory">The memory block.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public static int BitwiseHashCode<M>(this ref M memory, bool salted = true)
            where M : struct, IUnmanagedMemory
            => memory.ToPointer<byte>().BitwiseHashCode(memory.Size, salted);

        /// <summary>
        /// Computes 64-bit hash code for the block of memory.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="memory">The memory block.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public static long BitwiseHashCode64<M>(this ref M memory, bool salted = true)
            where M : struct, IUnmanagedMemory
            => memory.ToPointer<byte>().BitwiseHashCode64(memory.Size, salted);

        /// <summary>
        /// Bitwise comparison of two memory blocks.
        /// </summary>
        /// <typeparam name="M1">The first type of the unmanaged memory view.</typeparam>
        /// <typeparam name="M2">The second type of the unmanaged memory view.</typeparam>
        /// <param name="first">The first block of memory to be compared.</param>
        /// <param name="second">The second block of memory to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        public static int BitwiseCompare<M1, M2>(this ref M1 first, M2 second)
            where M1 : struct, IUnmanagedMemory
            where M2 : IUnmanagedMemory
            => first.Size == second.Size ? first.ToPointer<byte>().BitwiseCompare(second.ToPointer<byte>(), first.Size) : first.Size.CompareTo(second.Size);

        /// <summary>
		/// Gets pointer to the memory block.
		/// </summary>
        /// <param name="memory">Referenced memory.</param>      
		/// <param name="offset">Zero-based byte offset.</param>
		/// <returns>Byte located at the specified offset in the memory.</returns>
		/// <exception cref="NullPointerException">This buffer is not allocated.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Invalid offset.</exception>    
        public static Pointer<byte> ToPointer<M>(this ref M memory, long offset) 
            where M : struct, IUnmanagedMemory
            => offset >= 0 && offset < memory.Size ?
                memory.ToPointer<byte>() + offset :
                throw new ArgumentOutOfRangeException(nameof(offset), offset, ExceptionMessages.InvalidOffsetValue(memory.Size));
    }
}