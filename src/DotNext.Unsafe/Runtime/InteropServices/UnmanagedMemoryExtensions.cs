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

        public static void WriteTo<M>(this ref M memory, Stream destination)
            where M : struct, IUnmanagedMemory
        {
            if (memory.Address == IntPtr.Zero)
                throw new NullPointerException();
            else
                Memory.WriteToSteam(memory.Address, memory.Size, destination);
        }

        public static Task WriteToAsync<M>(this ref M memory, Stream destination)
            where M : struct, IUnmanagedMemory
            => memory.Address == IntPtr.Zero ? throw new NullPointerException() : Memory.WriteToSteamAsync(memory.Address, memory.Size, destination);

        public static long WriteTo<M>(this ref M memory, byte[] destination, long offset, long count)
            where M : struct, IUnmanagedMemory
        {
            if (memory.Address == IntPtr.Zero)
                throw new NullPointerException();
            else if (count < 0L)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (offset < 0L)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if (destination.LongLength == 0L)
                return 0L;
            count = count.Min(destination.LongLength - offset);
            fixed (byte* dest = &destination[offset])
                Memory.Copy(memory.Address.ToPointer(), dest, count);
            return count;
        }

        public static long WriteTo<M>(this ref M memory, byte[] destination)
            where M : struct, IUnmanagedMemory
            => memory.WriteTo(destination, 0L, destination.LongLength);

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

        public static long ReadFrom<M>(this ref M memory, byte[] source, long offset, long count)
            where M : struct, IUnmanagedMemory
        {
            if (memory.Address == IntPtr.Zero)
                throw new NullPointerException();
            else if (count < 0L)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (offset < 0L)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if (source.LongLength == 0L)
                return 0L;
            count = count.Min(source.LongLength - offset);
            fixed (byte* src = &source[offset])
                Memory.Copy(src, memory.Address.ToPointer(), count);
            return count;
        }

        public static long ReadFrom<M>(this ref M memory, byte[] source)
            where M : struct, IUnmanagedMemory
            => memory.ReadFrom(source, 0L, source.LongLength);

        public static long ReadFrom<M>(this ref M memory, Stream source)
            where M : struct, IUnmanagedMemory
            => memory.Address == IntPtr.Zero ? throw new NullPointerException() : Memory.ReadFromStream(source, memory.Address, memory.Size);

        public static Task<long> ReadFromAsync<M>(this ref M memory, Stream source)
            where M : struct, IUnmanagedMemory
            => memory.Address == IntPtr.Zero ? throw new NullPointerException() : Memory.ReadFromStreamAsync(source, memory.Address, memory.Size);

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

        public static int BitwiseHashCode<M>(this ref M memory, bool salted = true)
            where M : struct, IUnmanagedMemory
            => memory.Address == IntPtr.Zero ? 0 : Memory.GetHashCode(memory.Address, memory.Size, salted);

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