using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Globalization.CultureInfo;
using static System.Runtime.CompilerServices.Unsafe;
using Debug = System.Diagnostics.Debug;
using NumberStyles = System.Globalization.NumberStyles;

namespace DotNext
{
    using Runtime;
    using ByteBuffer = Buffers.MemoryRental<byte>;
    using CharBuffer = Buffers.MemoryRental<char>;

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

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct HexByte
        {
            private readonly char high, low;

            internal HexByte(byte value)
            {
                var str = value.ToString("X2", InvariantCulture);
                Debug.Assert(str.Length == 2);
                high = str[0];
                low = str[1];
            }

            private HexByte(char high, char low)
            {
                this.high = high;
                this.low = low;
            }

            private static char ToLowerFast(char ch) => ch >= 'A' && ch <= 'F' ? (char)('a' + (ch - 'A')) : ch;

            internal char GetHigh(bool uppercase) => uppercase ? high : ToLowerFast(high);

            internal char GetLow(bool uppercase) => uppercase ? low : ToLowerFast(low);

            public static implicit operator ReadOnlySpan<char>(in HexByte hex) => MemoryMarshal.CreateReadOnlySpan(ref AsRef(in hex.high), 2);
        }

        private static readonly ReadOnlyMemory<HexByte> HexLookupTable;

        static Span()
        {
            var lookup = new HexByte[byte.MaxValue + 1];
            for (var i = 0; i <= byte.MaxValue; i++)
                lookup[i] = new HexByte((byte)i);
            HexLookupTable = lookup;
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
            return Intrinsics.GetHashCode32(ref As<T, byte>(ref MemoryMarshal.GetReference(span)), span.Length * sizeof(T), hash, in hashFunction, salted);
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
            return Intrinsics.GetHashCode64(ref As<T, byte>(ref MemoryMarshal.GetReference(span)), span.Length * sizeof(T), hash, in hashFunction, salted);
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
                Intrinsics.Swap(ref span[i], ref jptr);
            }

            i += 1;
            Intrinsics.Swap(ref span[endIndex], ref span[i]);
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
        public static void Sort<T>(this Span<T> span, IComparer<T>? comparison = null)
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

        /// <summary>
        /// Converts set of bytes into hexadecimal representation.
        /// </summary>
        /// <param name="bytes">The bytes to convert.</param>
        /// <param name="output">The buffer used to write hexadecimal representation of bytes.</param>
        /// <param name="uppercase"><see langword="true"/> to return uppercased hex string; <see langword="false"/> to return lowercased hex string.</param>
        /// <returns>The actual number of characters in <paramref name="output"/> written by the method.</returns>
        public static int ToHex(this ReadOnlySpan<byte> bytes, Span<char> output, bool uppercase = true)
        {
            if (bytes.IsEmpty || output.IsEmpty)
                return 0;
            var bytesCount = Math.Min(bytes.Length, output.Length / 2);
            ref byte firstByte = ref MemoryMarshal.GetReference(bytes);
            ref char charPtr = ref MemoryMarshal.GetReference(output);
            ref HexByte firstHex = ref MemoryMarshal.GetReference(HexLookupTable.Span);
            for (var i = 0; i < bytesCount; i++, charPtr = ref Add(ref charPtr, 1))
            {
                var hexInfo = Add(ref firstHex, Add(ref firstByte, i));
                charPtr = hexInfo.GetHigh(uppercase);
                charPtr = ref Add(ref charPtr, 1);
                charPtr = hexInfo.GetLow(uppercase);
            }
            return bytesCount * 2;
        }

        /// <summary>
        /// Converts set of bytes into hexadecimal representation.
        /// </summary>
        /// <param name="bytes">The bytes to convert.</param>
        /// <param name="uppercase"><see langword="true"/> to return uppercased hex string; <see langword="false"/> to return lowercased hex string.</param>
        /// <returns>The hexadecimal representation of bytes.</returns>
        public static string ToHex(this ReadOnlySpan<byte> bytes, bool uppercase = true)
        {
            var count = bytes.Length * 2;
            if (count == 0)
                return string.Empty;
            using CharBuffer buffer = count <= CharBuffer.StackallocThreshold ? stackalloc char[count] : new CharBuffer(count);
            count = ToHex(bytes, buffer.Span, uppercase);
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
            ref HexByte pair = ref As<char, HexByte>(ref MemoryMarshal.GetReference(chars));
            ref byte bytePtr = ref MemoryMarshal.GetReference(output);
            for (var i = 0; i < charCount; i += 2, bytePtr = ref Add(ref bytePtr, 1), pair = ref Add(ref pair, 1))
                bytePtr = byte.Parse(pair, NumberStyles.AllowHexSpecifier, InvariantCulture);
            return charCount / 2;
        }

        /// <summary>
        /// Decodes hexadecimal representation of bytes.
        /// </summary>
        /// <param name="chars">The characters containing hexadecimal representation of bytes.</param>
        /// <returns>The decoded array of bytes.</returns>
        public static byte[] FromHex(this ReadOnlySpan<char> chars)
        {
            var count = chars.Length / 2;
            if (count == 0)
                return Array.Empty<byte>();
            using ByteBuffer buffer = count <= ByteBuffer.StackallocThreshold ? stackalloc byte[count] : new ByteBuffer(count);
            count = FromHex(chars, buffer.Span);
            return buffer.Span.Slice(0, count).ToArray();
        }

        /// <summary>
        /// Reads the value of blittable type
        /// from the block of memory and advances the original span.
        /// </summary>
        /// <param name="bytes">The block of memory.</param>
        /// <typeparam name="T">The blittable type.</typeparam>
        /// <returns>The deserialized value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bytes"/> is smaller than <typeparamref name="T"/>.</exception>
        public static unsafe T Read<T>(ref ReadOnlySpan<byte> bytes)
            where T : unmanaged
        {
            var result = MemoryMarshal.Read<T>(bytes);
            bytes = bytes.Slice(sizeof(T));
            return result;
        }

        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="value">The managed pointer.</param>
        /// <typeparam name="T">The type of the pointer.</typeparam>
        /// <returns>The span of contiguous memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<byte> AsBytes<T>(ref T value) where T : unmanaged => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1));

        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="value">The managed pointer.</param>
        /// <typeparam name="T">The type of the pointer.</typeparam>
        /// <returns>The span of contiguous memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<byte> AsReadOnlyBytes<T>(in T value) where T : unmanaged => AsBytes(ref AsRef(in value));

        /// <summary>
        /// Converts contiguous memory identified by the specified pointer
        /// into <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="pointer">The typed pointer.</param>
        /// <typeparam name="T">The type of the pointer.</typeparam>
        /// <returns>The span of contiguous memory.</returns>
        [CLSCompliant(false)]
        public static unsafe Span<byte> AsBytes<T>(T* pointer) where T : unmanaged => AsBytes(ref pointer[0]);
    }
}