using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Globalization.CultureInfo;
using static System.Runtime.CompilerServices.Unsafe;
using NumberStyles = System.Globalization.NumberStyles;

namespace DotNext
{
    using Buffers;
    using Runtime;

    /// <summary>
    /// Provides extension methods for type <see cref="Span{T}"/> and <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    public static class Span
    {
#if NETSTANDARD2_1
        [StructLayout(LayoutKind.Auto)]
        private readonly struct ValueComparer<T> : ISupplier<T, T, int>
        {
            private readonly IComparer<T> comparer;

            internal ValueComparer(IComparer<T> comparer) => this.comparer = comparer;

            int ISupplier<T, T, int>.Invoke(T arg1, T arg2) => comparer.Compare(arg1, arg2);
        }
#endif

        private static readonly char[] HexTable = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

        /// <summary>
        /// Computes bitwise hash code for the memory identified by the given span.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span whose content to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>32-bit hash code of the span content.</returns>
        public static int BitwiseHashCode<T>(this Span<T> span, bool salted = true)
            where T : unmanaged
            => BitwiseHashCode((ReadOnlySpan<T>)span, salted);

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
            return Intrinsics.GetHashCode32(ref As<T, byte>(ref MemoryMarshal.GetReference(span)), span.Length * sizeof(T), salted);
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
        /// <returns>32-bit hash code of the array content.</returns>
        [CLSCompliant(false)]
        public static unsafe int BitwiseHashCode<T>(this Span<T> span, int hash, delegate*<int, int, int> hashFunction, bool salted = true)
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
        /// <returns>64-bit hash code of the array content.</returns>
        [CLSCompliant(false)]
        public static unsafe long BitwiseHashCode64<T>(this Span<T> span, long hash, delegate*<long, long, long> hashFunction, bool salted = true)
            where T : unmanaged
            => BitwiseHashCode64((ReadOnlySpan<T>)span, hash, hashFunction, salted);

        private static unsafe int BitwiseHashCode<T, THashFunction>(ReadOnlySpan<T> span, int hash, THashFunction hashFunction, bool salted)
            where T : unmanaged
            where THashFunction : struct, ISupplier<int, int, int>
        {
            if (span.IsEmpty)
                return salted ? hashFunction.Invoke(hash, RandomExtensions.BitwiseHashSalt) : hash;

            return Intrinsics.GetHashCode32(ref As<T, byte>(ref MemoryMarshal.GetReference(span)), span.Length * sizeof(T), hash, hashFunction, salted);
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
            => BitwiseHashCode<T, DelegatingSupplier<int, int, int>>(span, hash, hashFunction, salted);

        /// <summary>
        /// Computes bitwise hash code for the memory identified by the given span using custom hash function.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span whose content to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Custom hashing algorithm.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>32-bit hash code of the array content.</returns>
        [CLSCompliant(false)]
        public static unsafe int BitwiseHashCode<T>(this ReadOnlySpan<T> span, int hash, delegate*<int, int, int> hashFunction, bool salted = true)
            where T : unmanaged
            => BitwiseHashCode<T, Supplier<int, int, int>>(span, hash, hashFunction, salted);

        private static unsafe long BitwiseHashCode64<T, THashFunction>(ReadOnlySpan<T> span, long hash, THashFunction hashFunction, bool salted)
            where T : unmanaged
            where THashFunction : struct, ISupplier<long, long, long>
        {
            if (span.IsEmpty)
                return salted ? hashFunction.Invoke(hash, RandomExtensions.BitwiseHashSalt) : hash;

            return Intrinsics.GetHashCode64(ref As<T, byte>(ref MemoryMarshal.GetReference(span)), span.Length * sizeof(T), hash, hashFunction, salted);
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
        public static long BitwiseHashCode64<T>(this ReadOnlySpan<T> span, long hash, Func<long, long, long> hashFunction, bool salted = true)
            where T : unmanaged
            => BitwiseHashCode64<T, DelegatingSupplier<long, long, long>>(span, hash, hashFunction, salted);

        /// <summary>
        /// Computes bitwise hash code for the memory identified by the given span using custom hash function.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span whose content to be hashed.</param>
        /// <param name="hash">Initial value of the hash.</param>
        /// <param name="hashFunction">Custom hashing algorithm.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>64-bit hash code of the array content.</returns>
        [CLSCompliant(false)]
        public static unsafe long BitwiseHashCode64<T>(this ReadOnlySpan<T> span, long hash, delegate*<long, long, long> hashFunction, bool salted = true)
            where T : unmanaged
            => BitwiseHashCode64<T, Supplier<long, long, long>>(span, hash, hashFunction, salted);

        /// <summary>
        /// Computes bitwise hash code for the memory identified by the given span.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="span">The span whose content to be hashed.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>64-bit hash code of the span content.</returns>
        public static long BitwiseHashCode64<T>(this Span<T> span, bool salted = true)
            where T : unmanaged
            => BitwiseHashCode64((ReadOnlySpan<T>)span, salted);

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
            return Intrinsics.GetHashCode64(ref As<T, byte>(ref MemoryMarshal.GetReference(span)), span.Length * sizeof(T), salted);
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
            => MemoryMarshal.AsBytes(first).SequenceEqual(MemoryMarshal.AsBytes(second));

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
            => MemoryMarshal.AsBytes(first).SequenceEqual(MemoryMarshal.AsBytes(second));

        /// <summary>
        /// Compares content of the two memory blocks identified by the given spans.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="first">The first memory span to compare.</param>
        /// <param name="second">The second array to compare.</param>
        /// <returns>Comparison result.</returns>
        public static int BitwiseCompare<T>(this Span<T> first, Span<T> second)
            where T : unmanaged
            => MemoryMarshal.AsBytes(first).SequenceCompareTo(MemoryMarshal.AsBytes(second));

        /// <summary>
        /// Compares content of the two memory blocks identified by the given spans.
        /// </summary>
        /// <typeparam name="T">The type of elements in the span.</typeparam>
        /// <param name="first">The first memory span to compare.</param>
        /// <param name="second">The second array to compare.</param>
        /// <returns>Comparison result.</returns>
        public static int BitwiseCompare<T>(this ReadOnlySpan<T> first, ReadOnlySpan<T> second)
            where T : unmanaged
            => MemoryMarshal.AsBytes(first).SequenceCompareTo(MemoryMarshal.AsBytes(second));

#if NETSTANDARD2_1
        private static void QuickSort<T, TComparer>(Span<T> span, int startIndex, int endIndex, TComparer comparison)
            where TComparer : struct, ISupplier<T, T, int>
        {
            while (startIndex < endIndex)
            {
                var partitionIndex = Partition(span, startIndex, endIndex, ref comparison);
                QuickSort(span, startIndex, partitionIndex - 1, comparison);
                startIndex = partitionIndex + 1;
            }

            static int Partition(Span<T> span, int startIndex, int endIndex, ref TComparer comparison)
            {
                var pivot = span[endIndex];
                var i = startIndex - 1;
                for (var j = startIndex; j < endIndex; j++)
                {
                    ref var jptr = ref span[j];
                    if (comparison.Invoke(jptr, pivot) > 0)
                        continue;
                    i += 1;
                    Intrinsics.Swap(ref span[i], ref jptr);
                }

                i += 1;
                Intrinsics.Swap(ref span[endIndex], ref span[i]);
                return i;
            }
        }
#endif

        /// <summary>
        /// Sorts the elements.
        /// </summary>
        /// <param name="span">The contiguous region of arbitrary memory to sort.</param>
        /// <param name="comparison">The comparer used for sorting.</param>
        /// <typeparam name="T">The type of the elements.</typeparam>
#if !NETSTANDARD2_1
        [Obsolete("Use MemoryExtensions.Sort() extension method instead")]
#endif
        public static void Sort<T>(this Span<T> span, IComparer<T>? comparison = null)
        {
#if NETSTANDARD2_1
            QuickSort(span, 0, span.Length - 1, new ValueComparer<T>(comparison ?? Comparer<T>.Default));
#else
            MemoryExtensions.Sort(span, comparison ?? Comparer<T>.Default);
#endif
        }

        /// <summary>
        /// Sorts the elements.
        /// </summary>
        /// <param name="span">The contiguous region of arbitrary memory to sort.</param>
        /// <param name="comparison">The comparer used for sorting.</param>
        /// <typeparam name="T">The type of the elements.</typeparam>
#if !NETSTANDARD2_1
        [Obsolete("Use MemoryExtensions.Sort() extension method instead")]
#endif
        public static void Sort<T>(this Span<T> span, Comparison<T?> comparison)
#if NETSTANDARD2_1
            => QuickSort(span, 0, span.Length - 1, new DelegatingComparer<T>(comparison));
#else
            => MemoryExtensions.Sort(span, comparison);
#endif

        /// <summary>
        /// Sorts the elements.
        /// </summary>
        /// <param name="span">The contiguous region of arbitrary memory to sort.</param>
        /// <param name="comparison">The comparer used for sorting.</param>
        /// <typeparam name="T">The type of the elements.</typeparam>
        [CLSCompliant(false)]
        public static unsafe void Sort<T>(this Span<T> span, delegate*<T?, T?, int> comparison)
#if NETSTANDARD2_1
            => QuickSort<T, ComparerWrapper<T>>(span, 0, span.Length - 1, comparison);
#else
            => MemoryExtensions.Sort<T, ComparerWrapper<T>>(span, comparison);
#endif

        /// <summary>
        /// Trims the span to specified length if it exceeds it.
        /// If length is less that <paramref name="maxLength" /> then the original span returned.
        /// </summary>
        /// <typeparam name="T">The type of items in the span.</typeparam>
        /// <param name="span">A contiguous region of arbitrary memory.</param>
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>Trimmed span.</returns>
        public static Span<T> TrimLength<T>(this Span<T> span, int maxLength)
            => span.Length <= maxLength ? span : span.Slice(0, maxLength);

        /// <summary>
        /// Trims the span to specified length if it exceeds it.
        /// If length is less that <paramref name="maxLength" /> then the original span returned.
        /// </summary>
        /// <typeparam name="T">The type of items in the span.</typeparam>
        /// <param name="span">A contiguous region of arbitrary memory.</param>
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>Trimmed span.</returns>
        public static ReadOnlySpan<T> TrimLength<T>(this ReadOnlySpan<T> span, int maxLength)
            => span.Length <= maxLength ? span : span.Slice(0, maxLength);

        private static int IndexOf<T, TComparer>(ReadOnlySpan<T> span, T value, int startIndex, TComparer comparer)
            where TComparer : struct, ISupplier<T, T, bool>
        {
            if (span.IsEmpty)
                goto not_found;

            for (var i = startIndex; i < span.Length; i++)
            {
                if (comparer.Invoke(span[i], value))
                    return i;
            }

            not_found:
            return -1;
        }

        /// <summary>
        /// Returns the zero-based index of the first occurrence of the specified value in the <see cref="Span{T}"/>. The search starts at a specified position.
        /// </summary>
        /// <typeparam name="T">The of the elements in the span.</typeparam>
        /// <param name="span">The span to search.</param>
        /// <param name="value">The value to search for.</param>
        /// <param name="startIndex">The search starting position.</param>
        /// <param name="comparer">The comparer used to compare the expected value and the actual value from the span.</param>
        /// <returns>The zero-based index position of <paramref name="value"/> from the start of the given span if that value is found, or -1 if it is not.</returns>
        public static int IndexOf<T>(this ReadOnlySpan<T> span, T value, int startIndex, Func<T, T, bool> comparer)
            => IndexOf<T, DelegatingSupplier<T, T, bool>>(span, value, startIndex, comparer);

        /// <summary>
        /// Returns the zero-based index of the first occurrence of the specified value in the <see cref="Span{T}"/>. The search starts at a specified position.
        /// </summary>
        /// <typeparam name="T">The of the elements in the span.</typeparam>
        /// <param name="span">The span to search.</param>
        /// <param name="value">The value to search for.</param>
        /// <param name="startIndex">The search starting position.</param>
        /// <param name="comparer">The comparer used to compare the expected value and the actual value from the span.</param>
        /// <returns>The zero-based index position of <paramref name="value"/> from the start of the given span if that value is found, or -1 if it is not.</returns>
        [CLSCompliant(false)]
        public static unsafe int IndexOf<T>(this ReadOnlySpan<T> span, T value, int startIndex, delegate*<T, T, bool> comparer)
            => IndexOf<T, Supplier<T, T, bool>>(span, value, startIndex, comparer);

        /// <summary>
        /// Iterates over elements of the span.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <param name="span">The span to iterate.</param>
        /// <param name="action">The action to be applied for each element of the span.</param>
        public static void ForEach<T>(this Span<T> span, RefAction<T, int> action)
        {
            for (var i = 0; i < span.Length; i++)
                action(ref span[i], i);
        }

        /// <summary>
        /// Iterates over elements of the span.
        /// </summary>
        /// <typeparam name="T">The type of the elements.</typeparam>
        /// <typeparam name="TArg">The type of the argument to be passed to the action.</typeparam>
        /// <param name="span">The span to iterate.</param>
        /// <param name="action">The action to be applied for each element of the span.</param>
        /// <param name="arg">The argument to be passed to the action.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
        [CLSCompliant(false)]
        public static unsafe void ForEach<T, TArg>(this Span<T> span, delegate*<ref T, TArg, void> action, TArg arg)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            foreach (ref var item in span)
                action(ref item, arg);
        }

        /// <summary>
        /// Converts set of bytes into hexadecimal representation.
        /// </summary>
        /// <param name="bytes">The bytes to convert.</param>
        /// <param name="output">The buffer used to write hexadecimal representation of bytes.</param>
        /// <param name="lowercased"><see langword="true"/> to return lowercased hex string; <see langword="false"/> to return uppercased hex string.</param>
        /// <returns>The actual number of characters in <paramref name="output"/> written by the method.</returns>
        public static int ToHex(this ReadOnlySpan<byte> bytes, Span<char> output, bool lowercased = false)
        {
            if (bytes.IsEmpty || output.IsEmpty)
                return 0;
            var bytesCount = Math.Min(bytes.Length, output.Length / 2);
            ref byte firstByte = ref MemoryMarshal.GetReference(bytes);
            ref char charPtr = ref MemoryMarshal.GetReference(output);
#if NETSTANDARD2_1
            ref char hexTable = ref HexTable[lowercased ? 0 : 16];
#else
            ref char hexTable = ref MemoryMarshal.GetArrayDataReference(HexTable);
            if (!lowercased)
                hexTable = ref Unsafe.Add(ref hexTable, 16);
#endif
            for (var i = 0; i < bytesCount; i++, charPtr = ref Add(ref charPtr, 1))
            {
                var value = Add(ref firstByte, i);
                charPtr = Add(ref hexTable, value >> 4);
                charPtr = ref Add(ref charPtr, 1);
                charPtr = Add(ref hexTable, value & 0B1111);
            }

            return bytesCount * 2;
        }

        /// <summary>
        /// Converts set of bytes into hexadecimal representation.
        /// </summary>
        /// <param name="bytes">The bytes to convert.</param>
        /// <param name="lowercased"><see langword="true"/> to return lowercased hex string; <see langword="false"/> to return uppercased hex string.</param>
        /// <returns>The hexadecimal representation of bytes.</returns>
#if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        public static string ToHex(this ReadOnlySpan<byte> bytes, bool lowercased = false)
        {
            var count = bytes.Length * 2;
            if (count == 0)
                return string.Empty;
            using MemoryRental<char> buffer = count <= MemoryRental<char>.StackallocThreshold ? stackalloc char[count] : new MemoryRental<char>(count);
            count = ToHex(bytes, buffer.Span, lowercased);
            return new string(buffer.Span.Slice(0, count));
        }

        /// <summary>
        /// Decodes hexadecimal representation of bytes.
        /// </summary>
        /// <param name="chars">The hexadecimal representation of bytes.</param>
        /// <param name="output">The output buffer used to write decoded bytes.</param>
        /// <returns>The actual number of bytes in <paramref name="output"/> written by the method.</returns>
        public static int FromHex(this ReadOnlySpan<char> chars, Span<byte> output)
        {
            if (chars.IsEmpty || output.IsEmpty)
                return 0;
            var charCount = Math.Min(chars.Length, output.Length * 2);
            charCount -= charCount % 2;
            ref (char, char) pair = ref As<char, (char, char)>(ref MemoryMarshal.GetReference(chars));
            ref byte bytePtr = ref MemoryMarshal.GetReference(output);
            for (var i = 0; i < charCount; i += 2, bytePtr = ref Add(ref bytePtr, 1), pair = ref Add(ref pair, 1))
                bytePtr = byte.Parse(pair.AsSpan(), NumberStyles.AllowHexSpecifier, InvariantCulture);
            return charCount / 2;
        }

        /// <summary>
        /// Decodes hexadecimal representation of bytes.
        /// </summary>
        /// <param name="chars">The characters containing hexadecimal representation of bytes.</param>
        /// <returns>The decoded array of bytes.</returns>
#if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        public static byte[] FromHex(this ReadOnlySpan<char> chars)
        {
            var count = chars.Length / 2;
            if (count == 0)
                return Array.Empty<byte>();
            using MemoryRental<byte> buffer = count <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[count] : new MemoryRental<byte>(count);
            count = FromHex(chars, buffer.Span);
            return buffer.Span.Slice(0, count).ToArray();
        }

        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="value">The managed pointer.</param>
        /// <typeparam name="T">The type of the pointer.</typeparam>
        /// <returns>The span of contiguous memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> AsBytes<T>(ref T value)
            where T : unmanaged
            => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1));

        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="value">The managed pointer.</param>
        /// <typeparam name="T">The type of the pointer.</typeparam>
        /// <returns>The span of contiguous memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> AsReadOnlyBytes<T>(in T value)
            where T : unmanaged
            => AsBytes(ref AsRef(in value));

        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="pointer">The typed pointer.</param>
        /// <typeparam name="T">The type of the pointer.</typeparam>
        /// <returns>The span of contiguous memory.</returns>
        [CLSCompliant(false)]
        public static unsafe Span<byte> AsBytes<T>(T* pointer)
            where T : unmanaged
            => AsBytes(ref pointer[0]);

        /// <summary>
        /// Concatenates memory blocks.
        /// </summary>
        /// <param name="first">The first memory block.</param>
        /// <param name="second">The second memory block.</param>
        /// <param name="allocator">The memory allocator used to allocate buffer for the result.</param>
        /// <typeparam name="T">The type of the elements in the memory.</typeparam>
        /// <returns>The memory block containing elements from the specified two memory blocks.</returns>
        public static MemoryOwner<T> Concat<T>(this ReadOnlySpan<T> first, ReadOnlySpan<T> second, MemoryAllocator<T>? allocator = null)
        {
            if (first.IsEmpty && second.IsEmpty)
                return default;

            var length = checked(first.Length + second.Length);
            var result = allocator is null ?
                new MemoryOwner<T>(ArrayPool<T>.Shared, length) :
                allocator(length);

            var output = result.Memory.Span;
            first.CopyTo(output);
            second.CopyTo(output.Slice(first.Length));

            return result;
        }

        /// <summary>
        /// Concatenates memory blocks.
        /// </summary>
        /// <param name="first">The first memory block.</param>
        /// <param name="second">The second memory block.</param>
        /// <param name="third">The third memory block.</param>
        /// <param name="allocator">The memory allocator used to allocate buffer for the result.</param>
        /// <typeparam name="T">The type of the elements in the memory.</typeparam>
        /// <returns>The memory block containing elements from the specified two memory blocks.</returns>
        public static MemoryOwner<T> Concat<T>(this ReadOnlySpan<T> first, ReadOnlySpan<T> second, ReadOnlySpan<T> third, MemoryAllocator<T>? allocator = null)
        {
            if (first.IsEmpty && second.IsEmpty && third.IsEmpty)
                return default;

            var length = checked(first.Length + second.Length + third.Length);
            var result = allocator is null ?
                new MemoryOwner<T>(ArrayPool<T>.Shared, length) :
                allocator(length);

            var output = result.Memory.Span;
            first.CopyTo(output);
            second.CopyTo(output = output.Slice(first.Length));
            third.CopyTo(output.Slice(second.Length));

            return result;
        }

        /// <summary>
        /// Copies the contents from the source span into a destination span.
        /// </summary>
        /// <param name="source">Source memory.</param>
        /// <param name="destination">Destination memory.</param>
        /// <param name="writtenCount">The number of copied elements.</param>
        /// <typeparam name="T">The type of the elements in the span.</typeparam>
#if !NETSTANDARD2_1
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static void CopyTo<T>(this ReadOnlySpan<T> source, Span<T> destination, out int writtenCount)
        {
            if (source.Length > destination.Length)
            {
                source = source.Slice(0, destination.Length);
                writtenCount = destination.Length;
            }
            else
            {
                writtenCount = source.Length;
            }

            source.CopyTo(destination);
        }

        /// <summary>
        /// Copies the contents from the source span into a destination span.
        /// </summary>
        /// <param name="source">Source memory.</param>
        /// <param name="destination">Destination memory.</param>
        /// <param name="writtenCount">The number of copied elements.</param>
        /// <typeparam name="T">The type of the elements in the span.</typeparam>
        public static void CopyTo<T>(this Span<T> source, Span<T> destination, out int writtenCount)
            => CopyTo((ReadOnlySpan<T>)source, destination, out writtenCount);

        private static Span<T> TupleToSpan<T, TTuple>(ref TTuple tuple)
            where TTuple : struct, ITuple
            => MemoryMarshal.CreateSpan(ref As<TTuple, T>(ref tuple), tuple.Length);

        /// <summary>
        /// Obtains a span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static Span<T> AsSpan<T>(this ref ValueTuple tuple)
            => Span<T>.Empty;

        /// <summary>
        /// Obtains read-only span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in ValueTuple tuple)
            => ReadOnlySpan<T>.Empty;

        /// <summary>
        /// Obtains a span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static Span<T> AsSpan<T>(this ref ValueTuple<T> tuple)
            => TupleToSpan<T, ValueTuple<T>>(ref tuple);

        /// <summary>
        /// Obtains read-only span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in ValueTuple<T> tuple)
            => TupleToSpan<T, ValueTuple<T>>(ref AsRef(in tuple));

        /// <summary>
        /// Obtains a span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static Span<T> AsSpan<T>(this ref (T, T) tuple)
            => TupleToSpan<T, ValueTuple<T, T>>(ref tuple);

        /// <summary>
        /// Obtains read-only span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in (T, T) tuple)
            => TupleToSpan<T, ValueTuple<T, T>>(ref AsRef(in tuple));

        /// <summary>
        /// Obtains a span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static Span<T> AsSpan<T>(this ref (T, T, T) tuple)
            => TupleToSpan<T, (T, T, T)>(ref tuple);

        /// <summary>
        /// Obtains read-only span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in (T, T, T) tuple)
            => TupleToSpan<T, (T, T, T)>(ref AsRef(in tuple));

        /// <summary>
        /// Obtains a span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static Span<T> AsSpan<T>(this ref (T, T, T, T) tuple)
            => TupleToSpan<T, (T, T, T, T)>(ref tuple);

        /// <summary>
        /// Obtains read-only span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in (T, T, T, T) tuple)
            => TupleToSpan<T, (T, T, T, T)>(ref AsRef(in tuple));

        /// <summary>
        /// Obtains a span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static Span<T> AsSpan<T>(this ref (T, T, T, T, T) tuple)
            => TupleToSpan<T, (T, T, T, T, T)>(ref tuple);

        /// <summary>
        /// Obtains read-only span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in (T, T, T, T, T) tuple)
            => TupleToSpan<T, (T, T, T, T, T)>(ref AsRef(in tuple));

        /// <summary>
        /// Obtains a span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static Span<T> AsSpan<T>(this ref (T, T, T, T, T, T) tuple)
            => TupleToSpan<T, (T, T, T, T, T, T)>(ref tuple);

        /// <summary>
        /// Obtains read-only span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in (T, T, T, T, T, T) tuple)
            => TupleToSpan<T, (T, T, T, T, T, T)>(ref AsRef(in tuple));

        /// <summary>
        /// Obtains a span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static Span<T> AsSpan<T>(this ref (T, T, T, T, T, T, T) tuple)
            => TupleToSpan<T, (T, T, T, T, T, T, T)>(ref tuple);

        /// <summary>
        /// Obtains read-only span over tuple items.
        /// </summary>
        /// <param name="tuple">The tuple.</param>
        /// <typeparam name="T">The type of items in the tuple.</typeparam>
        /// <returns>The span over items in the tuple.</returns>
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in (T, T, T, T, T, T, T) tuple)
            => TupleToSpan<T, (T, T, T, T, T, T, T)>(ref AsRef(in tuple));
    }
}