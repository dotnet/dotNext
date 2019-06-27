using System;
using System.Collections.Generic;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext
{
    using Runtime.InteropServices;

    /// <summary>
    /// Provides extension methods for type <see cref="Span{T}"/> and <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    public static class Span
    {
        /// <summary>
        /// Computes bitwise hash code for the memory identified by the given span.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span whose content to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>32-bit hash code of the span content.</returns>
        public static int BitwiseHashCode<T>(this Span<T> span, bool salted = true) where T : unmanaged => BitwiseHashCode((ReadOnlySpan<T>)span, salted);

        /// <summary>
        /// Computes bitwise hash code for the memory identified by the given span.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span whose content to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>32-bit hash code of the span content.</returns>
        public static unsafe int BitwiseHashCode<T>(this ReadOnlySpan<T> span, bool salted = true)
            where T : unmanaged
        {
            if (span.IsEmpty)
                return 0;
            fixed (T* ptr = span)
                return Memory.GetHashCode32(ptr, span.Length * ValueType<T>.Size, salted);
        }

        /// <summary>
        /// Computes bitwise hash code for the memory identified by the given span using custom hash function.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span whose content to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Custom hashing algorithm.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>32-bit hash code of the array content.</returns>
        public static int BitwiseHashCode<T>(this Span<T> span, int hash, Func<int, int, int> hashFunction, bool salted = true)
            where T : unmanaged
            => BitwiseHashCode((ReadOnlySpan<T>)span, hash, hashFunction, salted);

        /// <summary>
        /// Computes bitwise hash code for the memory identified by the given span using custom hash function.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span whose content to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Custom hashing algorithm.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>64-bit hash code of the array content.</returns>
        public static long BitwiseHashCode64<T>(this Span<T> span, long hash, Func<long, long, long> hashFunction, bool salted = true)
            where T : unmanaged
            => BitwiseHashCode64((ReadOnlySpan<T>)span, hash, hashFunction, salted);

        /// <summary>
        /// Computes bitwise hash code for the memory identified by the given span using custom hash function.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span whose content to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Custom hashing algorithm.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>32-bit hash code of the array content.</returns>
        public static unsafe int BitwiseHashCode<T>(this ReadOnlySpan<T> span, int hash, Func<int, int, int> hashFunction, bool salted = true)
            where T : unmanaged
        {
            if (span.IsEmpty)
                return hash;
            fixed (T* ptr = span)
                return Memory.GetHashCode32(ptr, span.Length * ValueType<T>.Size, hash, hashFunction, salted);
        }

        /// <summary>
        /// Computes bitwise hash code for the memory identified by the given span using custom hash function.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span whose content to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Custom hashing algorithm.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>64-bit hash code of the array content.</returns>
        public static unsafe long BitwiseHashCode64<T>(this ReadOnlySpan<T> span, long hash, Func<long, long, long> hashFunction, bool salted = true)
            where T : unmanaged
        {
            if (span.IsEmpty)
                return hash;
            fixed (T* ptr = span)
                return Memory.GetHashCode64(ptr, span.Length * ValueType<T>.Size, hash, hashFunction, salted);
        }

        /// <summary>
        /// Computes bitwise hash code for the memory identified by the given span.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span whose content to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>64-bit hash code of the span content.</returns>
        public static long BitwiseHashCode64<T>(this Span<T> span, bool salted = true) where T : unmanaged => BitwiseHashCode64((ReadOnlySpan<T>)span, salted);

        /// <summary>
        /// Computes bitwise hash code for the memory identified by the given span.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span whose content to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>64-bit hash code of the span content.</returns>
        public static unsafe long BitwiseHashCode64<T>(this ReadOnlySpan<T> span, bool salted = true)
            where T : unmanaged
        {
            if (span.IsEmpty)
                return 0;
            fixed (T* ptr = span)
                return Memory.GetHashCode64(ptr, span.Length * ValueType<T>.Size, salted);
        }

        /// <summary>
		/// Determines whether two memory blocks identified by the given spans contain the same set of elements.
		/// </summary>
		/// <remarks>
		/// This method performs bitwise equality between each pair of elements.
		/// </remarks>
		/// <typeparam name="T">The type of elements in the span.</typeparam>
		/// <param name="first">The first memory span to compare.</param>
		/// <param name="second">The second memory span to compare.</param>
		/// <returns><see langword="true"/>, if both memory blocks are equal; otherwise, <see langword="false"/>.</returns>
		public static bool BitwiseEquals<T>(this Span<T> first, Span<T> second)
            where T : unmanaged
            => AsBytes(first).SequenceEqual(AsBytes(second));

        /// <summary>
        /// Determines whether two memory blocks identified by the given spans contain the same set of elements.
        /// </summary>
        /// <remarks>
        /// This method performs bitwise equality between each pair of elements.
        /// </remarks>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="first">The first memory span to compare.</param>
        /// <param name="second">The second memory span to compare.</param>
        /// <returns><see langword="true"/>, if both memory blocks are equal; otherwise, <see langword="false"/>.</returns>
        public static bool BitwiseEquals<T>(this ReadOnlySpan<T> first, ReadOnlySpan<T> second)
            where T : unmanaged
            => AsBytes(first).SequenceEqual(AsBytes(second));

        /// <summary>
        /// Compares content of the two memory blocks identified by the given spans.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="first">The first memory span to compare.</param>
        /// <param name="second">The second array to compare.</param>
        /// <returns>Comparison result.</returns>
        public static int BitwiseCompare<T>(this Span<T> first, Span<T> second)
            where T : unmanaged
            => AsBytes(first).SequenceCompareTo(AsBytes(second));

        /// <summary>
        /// Compares content of the two memory blocks identified by the given spans.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="first">The first memory span to compare.</param>
        /// <param name="second">The second array to compare.</param>
        /// <returns>Comparison result.</returns>
        public static int BitwiseCompare<T>(this ReadOnlySpan<T> first, ReadOnlySpan<T> second)
            where T : unmanaged
            => AsBytes(first).SequenceCompareTo(AsBytes(second));
        
        private static int Partition<T>(Span<T> span, int startIndex, int endIndex, IComparer<T> comparison)
        {
            var pivot = span[endIndex];
            var i = startIndex - 1;
            for (var j = startIndex; j < endIndex; j++)
            {
                ref var jptr = ref span[j];
                if (comparison.Compare(jptr, pivot) > 0) continue;
                i += 1;
                Memory.Swap(ref span[i], ref jptr);
            }

            i += 1;
            Memory.Swap(ref span[endIndex], ref span[i]);
            return i;
        }

        private static void QuickSort<T>(Span<T> span, int startIndex, int endIndex, IComparer<T> comparison)
        {
            while (startIndex < endIndex)
            {
                var partitionIndex = Partition(span, startIndex, endIndex, comparison);
                QuickSort(span, startIndex, partitionIndex - 1, comparison);
                startIndex = partitionIndex + 1;
            }
        }

        /// <summary>
        /// Sorts the elements.
        /// </summary>
        /// <param name="span">The contiguous region of arbitrary memory to sort.</param>
        /// <param name="comparison">The comparer used for sorting.</param>
        /// <typeparam name="T">The type of the elements.</typeparam>
        public static void Sort<T>(this Span<T> span, IComparer<T> comparison = null)
            => QuickSort(span, 0, span.Length - 1, comparison ?? Comparer<T>.Default);
    }
}