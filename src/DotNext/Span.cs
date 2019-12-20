using System;
using System.Collections.Generic;
using static System.Runtime.CompilerServices.Unsafe;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext
{
    using Runtime.InteropServices;

    /// <summary>
    /// Provides extension methods for type <see cref="Span{T}"/> and <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    public static class Span
    {
        private readonly struct ValueComparer<T> : ISupplier<T, T, int>
        {
            private readonly IComparer<T> comparer;

            internal ValueComparer(IComparer<T> comparer) => this.comparer = comparer;

            int ISupplier<T, T, int>.Invoke(T arg1, T arg2) => comparer.Compare(arg1, arg2);
        }

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
                return salted ? RandomExtensions.BitwiseHashSalt : 0;
            fixed (T* ptr = span)
                return Memory.GetHashCode32(ptr, (long)span.Length * sizeof(T), salted);
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
        public static int BitwiseHashCode<T>(this Span<T> span, int hash, in ValueFunc<int, int, int> hashFunction, bool salted = true)
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
        public static long BitwiseHashCode64<T>(this Span<T> span, long hash, in ValueFunc<long, long, long> hashFunction, bool salted = true)
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
        public static unsafe int BitwiseHashCode<T>(this ReadOnlySpan<T> span, int hash, in ValueFunc<int, int, int> hashFunction, bool salted = true)
            where T : unmanaged
        {
            if (span.IsEmpty)
                return salted ? hashFunction.Invoke(hash, RandomExtensions.BitwiseHashSalt) : hash;
            fixed (T* ptr = span)
                return Memory.GetHashCode32(ptr, (long)span.Length * sizeof(T), hash, hashFunction, salted);
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
        public static int BitwiseHashCode<T>(this ReadOnlySpan<T> span, int hash, Func<int, int, int> hashFunction, bool salted = true)
            where T : unmanaged
            => BitwiseHashCode(span, hash, new ValueFunc<int, int, int>(hashFunction, true), salted);

        /// <summary>
        /// Computes bitwise hash code for the memory identified by the given span using custom hash function.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span whose content to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Custom hashing algorithm.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>64-bit hash code of the array content.</returns>
        public static unsafe long BitwiseHashCode64<T>(this ReadOnlySpan<T> span, long hash, in ValueFunc<long, long, long> hashFunction, bool salted = true)
            where T : unmanaged
        {
            if (span.IsEmpty)
                return salted ? hashFunction.Invoke(hash, RandomExtensions.BitwiseHashSalt) : hash;
            fixed (T* ptr = span)
                return Memory.GetHashCode64(ptr, (long)span.Length * sizeof(T), hash, hashFunction, salted);
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
            => BitwiseHashCode64(span, hash, new ValueFunc<long, long, long>(hashFunction, true), salted);

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
                return salted ? RandomExtensions.BitwiseHashSalt : 0L;
            fixed (T* ptr = span)
                return Memory.GetHashCode64(ptr, (long)span.Length * sizeof(T), salted);
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

        private static int Partition<T, C>(Span<T> span, int startIndex, int endIndex, ref C comparison)
            where C : struct, ISupplier<T, T, int>
        {
            var pivot = span[endIndex];
            var i = startIndex - 1;
            for (var j = startIndex; j < endIndex; j++)
            {
                ref var jptr = ref span[j];
                if (comparison.Invoke(jptr, pivot) > 0) continue;
                i += 1;
                Memory.Swap(ref span[i], ref jptr);
            }

            i += 1;
            Memory.Swap(ref span[endIndex], ref span[i]);
            return i;
        }

        private static void QuickSort<T, C>(Span<T> span, int startIndex, int endIndex, ref C comparison)
            where C : struct, ISupplier<T, T, int>
        {
            while (startIndex < endIndex)
            {
                var partitionIndex = Partition(span, startIndex, endIndex, ref comparison);
                QuickSort(span, startIndex, partitionIndex - 1, ref comparison);
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
        {
            var cmp = new ValueComparer<T>(comparison ?? Comparer<T>.Default);
            QuickSort(span, 0, span.Length - 1, ref cmp);
        }

        /// <summary>
        /// Sorts the elements.
        /// </summary>
        /// <param name="span">The contiguous region of arbitrary memory to sort.</param>
        /// <param name="comparison">The comparer used for sorting.</param>
        /// <typeparam name="T">The type of the elements.</typeparam>
        public static void Sort<T>(this Span<T> span, in ValueFunc<T, T, int> comparison)
            => QuickSort(span, 0, span.Length - 1, ref AsRef(comparison));

        /// <summary>
        /// Sorts the elements.
        /// </summary>
        /// <param name="span">The contiguous region of arbitrary memory to sort.</param>
        /// <param name="comparison">The comparer used for sorting.</param>
        /// <typeparam name="T">The type of the elements.</typeparam>
        public static void Sort<T>(this Span<T> span, Comparison<T> comparison)
            => Sort(span, comparison.AsValueFunc(true));

        /// <summary>
        /// Trims the span to specified length if it exceeds it.
        /// If length is less that <paramref name="maxLength" /> then the original span returned.
        /// </summary>
        /// <param name="span">A contiguous region of arbitrary memory.</param>
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>Trimmed span.</returns>
        public static Span<T> TrimLength<T>(this Span<T> span, int maxLength)
            => span.Length <= maxLength ? span : span.Slice(0, maxLength);

        /// <summary>
        /// Trims the span to specified length if it exceeds it.
        /// If length is less that <paramref name="maxLength" /> then the original span returned.
        /// </summary>
        /// <param name="span">A contiguous region of arbitrary memory.</param>
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>Trimmed span.</returns>
        public static ReadOnlySpan<T> TrimLength<T>(this ReadOnlySpan<T> span, int maxLength)
            => span.Length <= maxLength ? span : span.Slice(0, maxLength);

        /// <summary>
        /// Returns the zero-based index of the first occurrence of the specified value in the <see cref="Span{T}"/>. The search starts at a specified position. 
        /// </summary>
        /// <typeparam name="T">The of the elements in the span.</typeparam>
        /// <param name="span"></param>
        /// <param name="value"></param>
        /// <param name="startIndex">The search starting position.</param>
        /// <param name="comparer">The comparer used to compare the expected value and the actual value from the span.</param>
        /// <returns>The zero-based index position of <paramref name="value"/> from the start of the given span if that value is found, or -1 if it is not.</returns>
        public static int IndexOf<T>(this ReadOnlySpan<T> span, T value, int startIndex, in ValueFunc<T, T, bool> comparer)
        {
            if (span.IsEmpty)
                return -1;
            ref var reference = ref AsRef(in span[0]);
            for (var i = startIndex; i < span.Length; i++)
                if (comparer.Invoke(reference, value))
                    return i;
                else
                    reference = ref Add(ref reference, 1);
            return -1;
        }

        /// <summary>
        /// Returns the zero-based index of the first occurrence of the specified value in the <see cref="Span{T}"/>. The search starts at a specified position. 
        /// </summary>
        /// <typeparam name="T">The of the elements in the span.</typeparam>
        /// <param name="span"></param>
        /// <param name="value"></param>
        /// <param name="startIndex">The search starting position.</param>
        /// <param name="comparer">The comparer used to compare the expected value and the actual value from the span.</param>
        /// <returns>The zero-based index position of <paramref name="value"/> from the start of the given span if that value is found, or -1 if it is not.</returns>
        public static int IndexOf<T>(this ReadOnlySpan<T> span, T value, int startIndex, Func<T, T, bool> comparer) => IndexOf(span, value, startIndex, new ValueFunc<T, T, bool>(comparer, true));

        /// <summary>
        /// Iterates over elements of the span.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="span">The span to iterate.</param>
        /// <param name="action">The action to be applied for each element of the span.</param>
        public static void ForEach<T>(this Span<T> span, in ValueRefAction<T, int> action)
        {
            if (span.IsEmpty)
                return;
            ref var reference = ref span[0];
            for (var i = 0; i < span.Length; i++)
            {
                action.Invoke(ref reference, i);
                reference = ref Add(ref reference, 1);
            }
        }

        /// <summary>
        /// Iterates over elements of the span.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="span">The span to iterate.</param>
        /// <param name="action">The action to be applied for each element of the span.</param>
        public static void ForEach<T>(this Span<T> span, RefAction<T, int> action) => ForEach(span, new ValueRefAction<T, int>(action, true));
    }
}