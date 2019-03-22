using System;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.CompilerServices;

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
        public static byte[] ToByteArray<M>(this ref M memory)
            where M : struct, IUnmanagedMemory
        {
            if (memory.Address == IntPtr.Zero)
                return Array.Empty<byte>();
            var result = new byte[memory.Size];
            fixed (byte* destination = result)
                Memory.Copy(memory.Address.ToPointer(), destination, result.LongLength);
            return result;
        }

        /// <summary>
		/// Represents unmanaged memory as stream.
		/// </summary>
        /// <param name="memory">The unmanaged memory.</param>
		/// <returns>A stream to unmanaged memory.</returns>
        public static UnmanagedMemoryStream AsStream<M>(this ref M memory)
            where M : struct, IUnmanagedMemory
            => memory.Address == IntPtr.Zero ? throw new NullPointerException() : new UnmanagedMemoryStream((byte*)memory.Address, memory.Size);

        /// <summary>
        /// Copies bytes from the memory location to the stream.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source memory location.</param>
        /// <param name="destination">The destination stream.</param>
        public static void WriteTo<M>(this ref M source, Stream destination)
            where M : struct, IUnmanagedMemory
        {
            if (source.Address == IntPtr.Zero)
                throw new NullPointerException();
            else
                Memory.WriteToSteam(source.Address, source.Size, destination);
        }

        /// <summary>
        /// Copies bytes from the memory location to the stream asynchronously.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source memory location.</param>
        /// <param name="destination">The destination stream.</param>
        /// <returns>The task instance representing asynchronous state of the copying process.</returns>
        public static Task WriteToAsync<M>(this ref M source, Stream destination)
            where M : struct, IUnmanagedMemory
            => source.Address == IntPtr.Zero ? throw new NullPointerException() : Memory.WriteToSteamAsync(source.Address, source.Size, destination);

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
        {
            if (source.Address == IntPtr.Zero)
                throw new NullPointerException();
            else if (count < 0L)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (offset < 0L)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if (destination.LongLength == 0L)
                return 0L;
            count = count.Min(destination.LongLength - offset);
            fixed (byte* dest = &destination[offset])
                Memory.Copy(source.Address.ToPointer(), dest, count);
            return count;
        }

        /// <summary>
        /// Copies bytes from the memory location to the managed array of bytes.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source memory location.</param>
        /// <param name="destination">The destination array.</param>
        /// <returns>The actual number of copied bytes.</returns>
        public static long WriteTo<M>(this ref M source, byte[] destination)
            where M : struct, IUnmanagedMemory
            => source.WriteTo(destination, 0L, destination.LongLength);

        /// <summary>
		/// Sets all bits of allocated memory to zero.
		/// </summary>
        /// <param name="memory">The unmanaged memory to be cleared.</param>
		/// <exception cref="NullPointerException">The memory is not allocated.</exception>
        public static void Clear<M>(this ref M memory)
            where M : struct, IUnmanagedMemory
        {
            if (memory.Address == IntPtr.Zero)
                throw new NullPointerException();
            else
                Memory.ZeroMem(memory.Address, memory.Size);
        }

        /// <summary>
        /// Copies bytes from the the managed array of bytes to the memory location.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source array.</param>
        /// <param name="destination">The destination memory location.</param>
        /// <param name="offset">The position in the source array from which copying begins.</param>
        /// <param name="count">The number of arrays elements to be copied.</param>
        /// <returns>The actual number of copied bytes.</returns>
        public static long ReadFrom<M>(this ref M destination, byte[] source, long offset, long count)
            where M : struct, IUnmanagedMemory
        {
            if (destination.Address == IntPtr.Zero)
                throw new NullPointerException();
            else if (count < 0L)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (offset < 0L)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if (source.LongLength == 0L)
                return 0L;
            count = count.Min(source.LongLength - offset);
            fixed (byte* src = &source[offset])
                Memory.Copy(src, destination.Address.ToPointer(), count);
            return count;
        }

        /// <summary>
        /// Copies bytes from the the managed array of bytes to the given memory location.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source array.</param>
        /// <param name="destination">The destination memory location.</param>
        /// <returns>The actual number of copied bytes.</returns>
        public static long ReadFrom<M>(this ref M destination, byte[] source)
            where M : struct, IUnmanagedMemory
            => destination.ReadFrom(source, 0L, source.LongLength);

        /// <summary>
        /// Copies bytes from the stream to the given memory location.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source stream.</param>
        /// <param name="destination">The destination memory location.</param>
        /// <returns>The actual number</returns>
        public static long ReadFrom<M>(this ref M destination, Stream source)
            where M : struct, IUnmanagedMemory
            => destination.Address == IntPtr.Zero ? throw new NullPointerException() : Memory.ReadFromStream(source, destination.Address, destination.Size);

        /// <summary>
        /// Copies bytes from the stream to the given memory location.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source stream.</param>
        /// <param name="destination">The destination memory location.</param>
        /// <returns>The actual number</returns>
        public static Task<long> ReadFromAsync<M>(this ref M destination, Stream source)
            where M : struct, IUnmanagedMemory
            => destination.Address == IntPtr.Zero ? throw new NullPointerException() : Memory.ReadFromStreamAsync(source, destination.Address, destination.Size);

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
        {
            if (first.Address == second.Address)
                return true;
            else if (first.Address == IntPtr.Zero || second.Address == IntPtr.Zero || first.Size != second.Size)
                return false;
            else
                return Memory.Equals(first.Address, second.Address, first.Size);
        }

        /// <summary>
        /// Computes 32-bit hash code for the block of memory.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="memory">The memory block.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public static int BitwiseHashCode<M>(this ref M memory, bool salted = true)
            where M : struct, IUnmanagedMemory
            => memory.Address == IntPtr.Zero ? 0 : Memory.GetHashCode(memory.Address, memory.Size, salted);

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
            where M2 : struct, IUnmanagedMemory
        {
            if (first.Address == IntPtr.Zero || second.Address == IntPtr.Zero)
                throw new NullPointerException();
            else if (first.Size == second.Size)
                return Memory.Compare(first.Address, second.Address, first.Size);
            else
                return first.Size.CompareTo(second.Size);
        }
    }
}