using System;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Low-level methods for direct memory access.
    /// </summary>
    /// <remarks>
    /// Methods in this class doesn't perform
    /// any safety check. Incorrect usage of them may destabilize
    /// Common Language Runtime.
    /// </remarks>
    public unsafe static class Memory
    {
        private interface IHashFunction<T>
            where T : unmanaged, IConvertible, IComparable<T>
        {
            void AddData(T data);
            T Result { get; }
        }

        private struct FNV1a32 : IHashFunction<int>
        {
            internal const int Offset = unchecked((int)2166136261);
            private const int Prime = 16777619;

            private int hash;

            internal FNV1a32(int initialHash) => hash = initialHash;

            public int Result => hash;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void IHashFunction<int>.AddData(int data) => hash = (hash ^ data) * Prime;
        }

        private struct FNV1a64 : IHashFunction<long>
        {
            internal const long Offset = unchecked((long)14695981039346656037);
            private const long Prime = 1099511628211;

            private long hash;

            internal FNV1a64(long initialHash) => hash = initialHash;

            public long Result => hash;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void IHashFunction<long>.AddData(long data) => hash = (hash ^ data) * Prime;
        }

        private struct Int32HashFunction : IHashFunction<int>
        {
            private int hash;
            private readonly Func<int, int, int> function;

            internal Int32HashFunction(int hash, Func<int, int, int> function)
            {
                this.hash = hash;
                this.function = function;
            }

            public int Result => hash;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void IHashFunction<int>.AddData(int data) => hash = function(hash, data);
        }

        private struct Int64HashFunction : IHashFunction<long>
        {
            private long hash;
            private readonly Func<long, long, long> function;

            internal Int64HashFunction(long hash, Func<long, long, long> function)
            {
                this.hash = hash;
                this.function = function;
            }

            public long Result => hash;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void IHashFunction<long>.AddData(long data) => hash = function(hash, data);
        }

        /// <summary>
        /// Represents null pointer.
        /// </summary>
        [CLSCompliant(false)]
        public static readonly void* NullPtr = IntPtr.Zero.ToPointer();

        /// <summary>
        /// Converts the value of this instance to a pointer of the specified type.
        /// </summary>
        /// <param name="source">The value to be converted into pointer.</param>
        /// <typeparam name="T">The type of the pointer.</typeparam>
        /// <returns>The typed pointer.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T* ToPointer<T>(this IntPtr source) where T : unmanaged => (T*)source;

        /// <summary>
        /// Reads a value of type <typeparamref name="T"/> from the given location
        /// and adjust pointer according with size of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Unmanaged type to dereference.</typeparam>
        /// <param name="source">A pointer to block of memory.</param>
        /// <returns>Dereferenced value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(ref IntPtr source)
            where T : unmanaged
        {
            var result = Unsafe.Read<T>(source.ToPointer());
            source += Unsafe.SizeOf<T>();
            return result;
        }

        /// <summary>
        /// Reads a value of type <typeparamref name="T"/> from the given location
        /// without assuming architecture dependent alignment of the addresses;
        /// and adjust pointer according with size of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Unmanaged type to dereference.</typeparam>
        /// <param name="source">A pointer to block of memory.</param>
        /// <returns>Dereferenced value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadUnaligned<T>(ref IntPtr source)
            where T : unmanaged
        {
            var result = Unsafe.ReadUnaligned<T>(source.ToPointer());
            source += Unsafe.SizeOf<T>();
            return result;
        }

        /// <summary>
        /// Writes a value into the address using aligned access
        /// and adjust address according with size of
        /// type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Unmanaged type.</typeparam>
        /// <param name="destination">Destination address.</param>
        /// <param name="value">The value to write into the address.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Write<T>(ref IntPtr destination, T value)
            where T : unmanaged
        {
            Unsafe.Write(destination.ToPointer(), value);
            destination += Unsafe.SizeOf<T>();
        }

        /// <summary>
        /// Writes a value into the address using unaligned access
        /// and adjust address according with size of
        /// type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Unmanaged type.</typeparam>
        /// <param name="destination">Destination address.</param>
        /// <param name="value">The value to write into the address.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUnaligned<T>(ref IntPtr destination, T value)
            where T : unmanaged
        {
            Unsafe.WriteUnaligned(destination.ToPointer(), value);
            destination += Unsafe.SizeOf<T>();
        }

        /// <summary>
        /// Copies specified number of bytes from one address in memory to another.
        /// </summary>
        /// <param name="source">The address of the bytes to copy.</param>
        /// <param name="destination">The target address.</param>
        /// <param name="length">The number of bytes to copy from source address to destination.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static void Copy(void* source, void* destination, long length)
            => Buffer.MemoryCopy(source, destination, length, length);

        /// <summary>
        /// Copies specified number of bytes from one address in memory to another.
        /// </summary>
        /// <param name="source">The address of the bytes to copy.</param>
        /// <param name="destination">The target address.</param>
        /// <param name="length">The number of bytes to copy from source address to destination.</param>
		public static void Copy(IntPtr source, IntPtr destination, long length)
            => Copy(source.ToPointer(), destination.ToPointer(), length);

        private static void ComputeHashCode64<H>(IntPtr source, long length, ref H hashFunction, bool salted)
            where H : struct, IHashFunction<long>
        {
            switch (length)
            {
                case sizeof(byte):
                    hashFunction.AddData(Unsafe.Read<byte>(source.ToPointer()));
                    break;
                case sizeof(short):
                    hashFunction.AddData(Unsafe.ReadUnaligned<short>(source.ToPointer()));
                    break;
                case sizeof(int):
                    hashFunction.AddData(Unsafe.ReadUnaligned<int>(source.ToPointer()));
                    break;
                default:
                    while (length >= IntPtr.Size)
                    {
                        hashFunction.AddData(ReadUnaligned<IntPtr>(ref source).ToInt64());
                        length -= IntPtr.Size;
                    }
                    while (length > 0)
                    {
                        hashFunction.AddData(Read<byte>(ref source));
                        length -= sizeof(byte);
                    }
                    break;
            }
            if (salted)
                hashFunction.AddData(RandomExtensions.BitwiseHashSalt);
        }

        /// <summary>
        /// Computes 64-bit hash code for the block of memory, 64-bit version.
        /// </summary>
        /// <remarks>
        /// This method may give different value each time you run the program for
        /// the same data. To disable this behavior, pass false to <paramref name="salted"/>. 
        /// </remarks>
        /// <param name="source">A pointer to the block of memory.</param>
        /// <param name="length">Length of memory block to be hashed, in bytes.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Hashing function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Hash code of the memory block.</returns>
        public static long GetHashCode64(IntPtr source, long length, long hash, Func<long, long, long> hashFunction, bool salted = true)
        {
            var hashInfo = new Int64HashFunction(hash, hashFunction);
            ComputeHashCode64(source, length, ref hashInfo, salted);
            return hashInfo.Result;
        }

        /// <summary>
		/// Computes 64-bit hash code for the block of memory, 64-bit version.
		/// </summary>
		/// <remarks>
		/// This method may give different value each time you run the program for
		/// the same data. To disable this behavior, pass false to <paramref name="salted"/>. 
		/// </remarks>
		/// <param name="source">A pointer to the block of memory.</param>
		/// <param name="length">Length of memory block to be hashed, in bytes.</param>
		/// <param name="hash">Initial value of the hash.</param>
		/// <param name="hashFunction">Hashing function.</param>
		/// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
		/// <returns>Hash code of the memory block.</returns>
		[CLSCompliant(false)]
        public static long GetHashCode64(void* source, long length, long hash, Func<long, long, long> hashFunction, bool salted = true)
            => GetHashCode64(new IntPtr(source), length, hash, hashFunction, salted);

        /// <summary>
        /// Computes 64-bit hash code for the block of memory.
        /// </summary>
        /// <param name="source">A pointer to the block of memory.</param>
        /// <param name="length">Length of memory block to be hashed, in bytes.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <remarks>
        /// This method uses FNV-1a hash algorithm.
        /// </remarks>
        /// <returns>Content hash code.</returns>
        /// <seealso href="http://www.isthe.com/chongo/tech/comp/fnv/#FNV-1a">FNV-1a</seealso>
        public static long GetHashCode64(IntPtr source, long length, bool salted = true)
        {
            var hashInfo = new FNV1a64(FNV1a64.Offset);
            ComputeHashCode64(source, length, ref hashInfo, salted);
            return hashInfo.Result;
        }

        /// <summary>
        /// Computes 64-bit hash code for the block of memory.
        /// </summary>
        /// <param name="source">A pointer to the block of memory.</param>
        /// <param name="length">Length of memory block to be hashed, in bytes.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <remarks>
        /// This method uses FNV-1a hash algorithm.
        /// </remarks>
        /// <returns>Content hash code.</returns>
        /// <seealso href="http://www.isthe.com/chongo/tech/comp/fnv/#FNV-1a">FNV-1a</seealso>
        [CLSCompliant(false)]
        public static long GetHashCode64(void* source, long length, bool salted = true)
            => GetHashCode64(new IntPtr(source), length, salted);

        private static void ComputeHashCode32<H>(IntPtr source, long length, ref H hashFunction, bool salted)
            where H : struct, IHashFunction<int>
        {
            switch (length)
            {
                case sizeof(byte):
                    hashFunction.AddData(Unsafe.Read<byte>(source.ToPointer()));
                    break;
                case sizeof(short):
                    hashFunction.AddData(Unsafe.ReadUnaligned<short>(source.ToPointer()));
                    break;
                default:
                    while (length >= sizeof(int))
                    {
                        hashFunction.AddData(ReadUnaligned<int>(ref source));
                        length -= sizeof(int);
                    }
                    while (length > 0)
                    {
                        hashFunction.AddData(Read<byte>(ref source));
                        length -= sizeof(byte);
                    }
                    break;
            }
            if (salted)
                hashFunction.AddData(RandomExtensions.BitwiseHashSalt);
        }

        /// <summary>
        /// Computes 32-bit hash code for the block of memory.
        /// </summary>
        /// <remarks>
        /// This method may give different value each time you run the program for
        /// the same data. To disable this behavior, pass false to <paramref name="salted"/>. 
        /// </remarks>
        /// <param name="source">A pointer to the block of memory.</param>
        /// <param name="length">Length of memory block to be hashed, in bytes.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Hashing function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Hash code of the memory block.</returns>
        public static int GetHashCode32(IntPtr source, long length, int hash, Func<int, int, int> hashFunction, bool salted = true)
        {
            var hashInfo = new Int32HashFunction(hash, hashFunction);
            ComputeHashCode32(source, length, ref hashInfo, salted);
            return hashInfo.Result;
        }

        /// <summary>
        /// Computes 32-bit hash code for the block of memory.
        /// </summary>
        /// <remarks>
        /// This method may give different value each time you run the program for
        /// the same data. To disable this behavior, pass false to <paramref name="salted"/>. 
        /// </remarks>
        /// <param name="source">A pointer to the block of memory.</param>
        /// <param name="length">Length of memory block to be hashed, in bytes.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Hashing function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Hash code of the memory block.</returns>
        [CLSCompliant(false)]
        public static int GetHashCode32(void* source, long length, int hash, Func<int, int, int> hashFunction, bool salted = true)
            => GetHashCode32(new IntPtr(source), length, hash, hashFunction, salted);

        /// <summary>
        /// Computes 32-bit hash code for the block of memory.
        /// </summary>
        /// <param name="source">A pointer to the block of memory.</param>
        /// <param name="length">Length of memory block to be hashed, in bytes.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <remarks>
        /// This method uses FNV-1a hash algorithm.
        /// </remarks>
        /// <returns>Content hash code.</returns>
        /// <seealso href="http://www.isthe.com/chongo/tech/comp/fnv/#FNV-1a">FNV-1a</seealso>
        public static int GetHashCode32(IntPtr source, long length, bool salted = true)
        {
            var hashInfo = new FNV1a32(FNV1a32.Offset);
            ComputeHashCode32(source, length, ref hashInfo, salted);
            return hashInfo.Result;
        }

        /// <summary>
        /// Computes 32-bit hash code for the block of memory.
        /// </summary>
        /// <param name="source">A pointer to the block of memory.</param>
        /// <param name="length">Length of memory block to be hashed, in bytes.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <remarks>
        /// This method uses FNV-1a hash algorithm.
        /// </remarks>
        /// <returns>Content hash code.</returns>
        /// <seealso href="http://www.isthe.com/chongo/tech/comp/fnv/#FNV-1a">FNV-1a</seealso>
        [CLSCompliant(false)]
        public static int GetHashCode32(void* source, long length, bool salted = true)
            => GetHashCode32(new IntPtr(source), length, salted);

        /// <summary>
        /// Sets all bits of allocated memory to zero.
        /// </summary>
        /// <remarks>
        /// This method has the same behavior as <see cref="Unsafe.InitBlockUnaligned(ref byte,byte,uint)"/> but
        /// without restriction on <see cref="uint"/> data type for the length of the memory block.
        /// </remarks>
        /// <param name="ptr">The pointer to the memory to be cleared.</param>
        /// <param name="length">The length of the memory to be cleared, in bytes.</param>
        public static void ClearBits(IntPtr ptr, long length)
        {
            do
            {
                var count = (int)length.Min(int.MaxValue);
                Unsafe.InitBlockUnaligned(ptr.ToPointer(), 0, (uint)count);
                ptr += count;
                length -= count;
            } while (length > 0);
        }

        /// <summary>
        /// Sets all bits of allocated memory to zero.
        /// </summary>
        /// <param name="ptr">The pointer to the memory to be cleared.</param>
        /// <param name="length">The length of the memory to be cleared.</param>
        [CLSCompliant(false)]
        public static void ClearBits(void* ptr, long length) => ClearBits(new IntPtr(ptr), length);

        /// <summary>
		/// Computes equality between two blocks of memory.
		/// </summary>
		/// <param name="first">A pointer to the first memory block.</param>
		/// <param name="second">A pointer to the second memory block.</param>
		/// <param name="length">Length of first and second memory blocks, in bytes.</param>
		/// <returns><see langword="true"/>, if both memory blocks have the same data; otherwise, <see langword="false"/>.</returns>
		[CLSCompliant(false)]
        public static bool Equals(void* first, void* second, long length) => Equals(new IntPtr(first), new IntPtr(second), length);

        /// <summary>
        /// Computes equality between two blocks of memory.
        /// </summary>
        /// <param name="first">A pointer to the first memory block.</param>
        /// <param name="second">A pointer to the second memory block.</param>
        /// <param name="length">Length of first and second memory blocks, in bytes.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same data; otherwise, <see langword="false"/>.</returns>
        public static bool Equals(IntPtr first, IntPtr second, long length)
        {
            if (first == second)
                return true;
            switch (length)
            {
                case 0L:
                    return true;
                case sizeof(byte):
                    return Unsafe.Read<byte>(first.ToPointer()) == Unsafe.ReadUnaligned<byte>(second.ToPointer());
                case sizeof(ushort):
                    return Unsafe.ReadUnaligned<ushort>(first.ToPointer()) == Unsafe.ReadUnaligned<ushort>(second.ToPointer());
                case sizeof(uint):
                    return Unsafe.ReadUnaligned<uint>(first.ToPointer()) == Unsafe.ReadUnaligned<uint>(second.ToPointer());
                case sizeof(ulong):
                    return Unsafe.ReadUnaligned<ulong>(first.ToPointer()) == Unsafe.ReadUnaligned<ulong>(second.ToPointer());
                default:
                    do
                    {
                        var count = (int)length.Min(int.MaxValue);
                        if (new ReadOnlySpan<byte>(first.ToPointer(), count).SequenceEqual(new ReadOnlySpan<byte>(second.ToPointer(), count)))
                        {
                            first += count;
                            second += count;
                            length -= count;
                        }
                        else
                            return false;
                    } while (length > 0);
                    return true;
            }
        }

        internal static bool IsZero(void* source, long length) => IsZero(new IntPtr(source), length);

        internal static bool IsZero(IntPtr source, long length)
        {
            switch (length)
            {
                case 0L:
                    return true;
                case sizeof(byte):
                    return Unsafe.Read<byte>(source.ToPointer()) == 0;
                case sizeof(ushort):
                    return Unsafe.ReadUnaligned<ushort>(source.ToPointer()) == 0;
                case sizeof(uint):
                    return Unsafe.ReadUnaligned<uint>(source.ToPointer()) == 0;
                case sizeof(ulong):
                    return Unsafe.ReadUnaligned<ulong>(source.ToPointer()) == 0;
                default:
                    while (length >= IntPtr.Size)
                        if (ReadUnaligned<IntPtr>(ref source) == IntPtr.Zero)
                            length -= IntPtr.Size;
                        else
                            return false;
                    while (length > sizeof(byte))
                        if (Read<byte>(ref source) == 0)
                            length -= sizeof(byte);
                        else
                            return false;
                    return true;
            }
        }

        /// <summary>
        /// Bitwise comparison of two memory blocks.
        /// </summary>
        /// <param name="first">The pointer to the first memory block.</param>
        /// <param name="second">The pointer to the second memory block.</param>
        /// <param name="length">The length of the first and second memory blocks.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        public static int Compare(IntPtr first, IntPtr second, long length)
        {
            if (first == second)
                return 0;
            switch (length)
            {
                case 0L:
                    return 0;
                case sizeof(byte):
                    return Unsafe.Read<byte>(first.ToPointer()).CompareTo(Unsafe.Read<byte>(second.ToPointer()));
                case sizeof(ushort):
                    return Unsafe.ReadUnaligned<ushort>(first.ToPointer()).CompareTo(Unsafe.ReadUnaligned<ushort>(second.ToPointer()));
                case sizeof(uint):
                    return Unsafe.ReadUnaligned<uint>(first.ToPointer()).CompareTo(Unsafe.ReadUnaligned<uint>(second.ToPointer()));
                case sizeof(ulong):
                    return Unsafe.ReadUnaligned<ulong>(first.ToPointer()).CompareTo(Unsafe.ReadUnaligned<ulong>(second.ToPointer()));
                default:
                    int comparison;
                    do
                    {
                        var count = (int)length.Min(int.MaxValue);
                        comparison = new ReadOnlySpan<byte>(first.ToPointer(), count).SequenceCompareTo(new ReadOnlySpan<byte>(second.ToPointer(), count));
                        if (comparison == 0)
                        {
                            first += count;
                            second += count;
                            length -= count;
                        }
                        else
                            break;
                    } while (length > 0);
                    return comparison;
            }
        }

        /// <summary>
        /// Bitwise comparison of two memory blocks.
        /// </summary>
        /// <param name="first">The pointer to the first memory block.</param>
        /// <param name="second">The pointer to the second memory block.</param>
        /// <param name="length">The length of the first and second memory blocks.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        [CLSCompliant(false)]
        public static int Compare(void* first, void* second, long length) => Compare(new IntPtr(first), new IntPtr(second), length);

        /// <summary>
        /// Indicates that two managed pointers are equal.
        /// </summary>
        /// <typeparam name="T">Type of managed pointer.</typeparam>
        /// <param name="first">The first managed pointer.</param>
        /// <param name="second">The second managed pointer.</param>
        /// <returns><see langword="true"/>, if both managed pointers are equal; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public static bool AreSame<T>(in T first, in T second)
            => Unsafe.AreSame(ref Unsafe.AsRef(in first), ref Unsafe.AsRef(in second));

        /// <summary>
        /// Returns address of the managed pointer to type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Type of managed pointer.</typeparam>
        /// <param name="value">Managed pointer to convert into address.</param>
        /// <returns>The address for the managed pointer.</returns>
        /// <remarks>
        /// This method converts managed pointer into address,
        /// not the address of the object itself.
        /// </remarks>
        [CLSCompliant(false)]
        public static IntPtr AddressOf<T>(in T value)
            => new IntPtr(Unsafe.AsPointer(ref Unsafe.AsRef(in value)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int PointerHashCode(void* pointer) => new IntPtr(pointer).GetHashCode();

        /// <summary>
        /// Swaps two values.
        /// </summary>
        /// <param name="first">The first value to be replaced with <paramref name="second"/>.</param>
        /// <param name="second">The second value to be replaced with <paramref name="first"/>.</param>
        /// <typeparam name="T">The type of the value.</typeparam>
        public static void Swap<T>(ref T first, ref T second) => (first, second) = (second, first);

        /// <summary>
        /// Swaps two values.
        /// </summary>
        /// <param name="first">The first value to be replaced with <paramref name="second"/>.</param>
        /// <param name="second">The second value to be replaced with <paramref name="first"/>.</param>
        /// <typeparam name="T">The type of the value.</typeparam>
        [CLSCompliant(false)]
        public static void Swap<T>(T* first, T* second) where T : unmanaged => (*first, *second) = (*second, *first);
    }
}
