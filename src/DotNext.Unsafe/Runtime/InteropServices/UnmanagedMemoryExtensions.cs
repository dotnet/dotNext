using System;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.InteropServices
{
    public unsafe static class UnmanagedMemoryExtensions
    {

        /// <summary>
        /// Creates a copy of unmanaged memory inside of managed heap.
        /// </summary>
        /// <returns>A copy of unmanaged memory in the form of byte array.</returns>
        public static byte[] ToByteArray<M>(this ref M memory)
            where M: struct, IUnmanagedMemory
        {
            if(memory.Address == IntPtr.Zero)
                return Array.Empty<byte>();
            var result = new byte[memory.Size];
			fixed (byte* destination = result)
				Memory.Copy(memory.Address.ToPointer(), destination, result.LongLength);
			return result;
        }

        public static UnmanagedMemoryStream AsStream<M>(this ref M memory)
            where M: struct, IUnmanagedMemory
            => memory.Address == IntPtr.Zero ? throw new NullPointerException() : new UnmanagedMemoryStream((byte*)memory.Address, memory.Size);

        public static void WriteTo<M>(this ref M memory, Stream destination)
            where M: struct, IUnmanagedMemory
        {
            if (memory.Address == IntPtr.Zero)
				throw new NullPointerException();
            else
                Memory.WriteToSteam(memory.Address, memory.Size, destination);
        }

        public static Task WriteToAsync<M>(this ref M memory, Stream destination)
            where M: struct, IUnmanagedMemory
            => memory.Address == IntPtr.Zero ? throw new NullPointerException() : Memory.WriteToSteamAsync(memory.Address, memory.Size, destination);
        
        public static long WriteTo<M>(this ref M memory, byte[] destination, long offset, long count)
            where M: struct, IUnmanagedMemory
        {
            if(memory.Address == IntPtr.Zero)
                throw new NullPointerException();
            else if (count < 0L)
				throw new ArgumentOutOfRangeException(nameof(count));
            else if(offset < 0L)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if (destination.LongLength == 0L)
				return 0L;
            count = count.Min(destination.LongLength - offset);
            fixed(byte* dest = &destination[offset])
                Memory.Copy(memory.Address.ToPointer(), dest, count);
            return count;
        }

        public static void Clear<M>(this ref M memory)
            where M: struct, IUnmanagedMemory
        {
            if(memory.Address == IntPtr.Zero)
                throw new NullPointerException();
            for(int offset = 0, count = 0; memory.Size - offset > 0; offset += count)
            {
                count = (int)((memory.Size - offset).UpperBounded(int.MaxValue));
                Unsafe.InitBlockUnaligned(IntPtr.Add(memory.Address, offset).ToPointer(), 0, (uint)count);
            }
        }

        public static long ReadFrom<M>(this ref M memory, byte[] source, long offset, long count)
            where M: struct, IUnmanagedMemory
        {
            if(memory.Address == IntPtr.Zero)
                throw new NullPointerException();
            else if(count < 0L)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if(offset < 0L)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if(source.LongLength == 0L)
                return 0L;
            count = count.Min(source.LongLength - offset);
            fixed(byte* src = &source[offset])
                Memory.Copy(src, memory.Address.ToPointer(), count);
            return count;
        }
    }
}