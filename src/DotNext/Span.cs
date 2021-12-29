using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext;

using Buffers;
using Runtime;

/// <summary>
/// Provides extension methods for type <see cref="Span{T}"/> and <see cref="ReadOnlySpan{T}"/>.
/// </summary>
public static class Span
{
    private const byte NimbleMaxValue = 0B1111;

    private static readonly char[] NimbleToCharLookupTable = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

    private static ReadOnlySpan<byte> CharToNimbleLookupTable => new byte[]
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 15
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 31
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 47
            0x0,  0x1,  0x2,  0x3,  0x4,  0x5,  0x6,  0x7,  0x8,  0x9,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 63
            0xFF, 0xA,  0xB,  0xC,  0xD,  0xE,  0xF,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 79
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 95
            0xFF, 0xa,  0xb,  0xc,  0xd,  0xe,  0xf, // 102
        };

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

        return Intrinsics.GetHashCode32(ref As<T, byte>(ref MemoryMarshal.GetReference(span)), checked((nint)span.Length * sizeof(T)), salted);
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
    /// <typeparam name="THashFunction">The type of the hash algorithm.</typeparam>
    /// <param name="span">The span whose content to be hashed.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>32-bit hash code of the array content.</returns>
    [CLSCompliant(false)]
    public static int BitwiseHashCode<T, THashFunction>(this Span<T> span, bool salted = true)
        where T : unmanaged
        where THashFunction : struct, IConsumer<int>, ISupplier<int>
        => BitwiseHashCode<T, THashFunction>((ReadOnlySpan<T>)span, salted);

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
    /// <typeparam name="THashFunction">The type of the hash algorithm.</typeparam>
    /// <param name="span">The span whose content to be hashed.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>64-bit hash code of the array content.</returns>
    [CLSCompliant(false)]
    public static long BitwiseHashCode64<T, THashFunction>(this Span<T> span, bool salted = true)
        where T : unmanaged
        where THashFunction : struct, IConsumer<long>, ISupplier<long>
        => BitwiseHashCode64<T, THashFunction>((ReadOnlySpan<T>)span, salted);

    private static unsafe void BitwiseHashCode<T, THashFunction>(ReadOnlySpan<T> span, ref THashFunction hashFunction, bool salted)
        where T : unmanaged
        where THashFunction : struct, IConsumer<int>
    {
        if (!span.IsEmpty)
            Intrinsics.GetHashCode32(ref hashFunction, ref As<T, byte>(ref MemoryMarshal.GetReference(span)), checked((nint)span.Length * sizeof(T)));

        if (salted)
            hashFunction.Invoke(RandomExtensions.BitwiseHashSalt);
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
    {
        var fn = new Accumulator<int, int>(hashFunction, hash);
        BitwiseHashCode(span, ref fn, salted);
        return fn.Invoke();
    }

    /// <summary>
    /// Computes bitwise hash code for the memory identified by the given span using custom hash function.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="THashFunction">The type of the hash algorithm.</typeparam>
    /// <param name="span">The span whose content to be hashed.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>32-bit hash code of the array content.</returns>
    [CLSCompliant(false)]
    public static int BitwiseHashCode<T, THashFunction>(this ReadOnlySpan<T> span, bool salted = true)
        where T : unmanaged
        where THashFunction : struct, IConsumer<int>, ISupplier<int>
    {
        var hash = new THashFunction();
        BitwiseHashCode(span, ref hash, salted);
        return hash.Invoke();
    }

    private static unsafe void BitwiseHashCode64<T, THashFunction>(ReadOnlySpan<T> span, ref THashFunction hashFunction, bool salted)
        where T : unmanaged
        where THashFunction : struct, IConsumer<long>
    {
        if (!span.IsEmpty)
            Intrinsics.GetHashCode64(ref hashFunction, ref As<T, byte>(ref MemoryMarshal.GetReference(span)), checked((nint)span.Length * sizeof(T)));

        if (salted)
            hashFunction.Invoke(RandomExtensions.BitwiseHashSalt);
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
    {
        var fn = new Accumulator<long, long>(hashFunction, hash);
        BitwiseHashCode64(span, ref fn, salted);
        return fn.Invoke();
    }

    /// <summary>
    /// Computes bitwise hash code for the memory identified by the given span using custom hash function.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <typeparam name="THashFunction">The type of the hash algorithm.</typeparam>
    /// <param name="span">The span whose content to be hashed.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>64-bit hash code of the array content.</returns>
    [CLSCompliant(false)]
    public static long BitwiseHashCode64<T, THashFunction>(this ReadOnlySpan<T> span, bool salted = true)
        where T : unmanaged
        where THashFunction : struct, IConsumer<long>, ISupplier<long>
    {
        var hash = new THashFunction();
        BitwiseHashCode64(span, ref hash, salted);
        return hash.Invoke();
    }

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

        return Intrinsics.GetHashCode64(ref As<T, byte>(ref MemoryMarshal.GetReference(span)), checked((nint)span.Length * sizeof(T)), salted);
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

    /// <summary>
    /// Sorts the elements.
    /// </summary>
    /// <param name="span">The contiguous region of arbitrary memory to sort.</param>
    /// <param name="comparison">The comparer used for sorting.</param>
    /// <typeparam name="T">The type of the elements.</typeparam>
    [CLSCompliant(false)]
    public static unsafe void Sort<T>(this Span<T> span, delegate*<T?, T?, int> comparison)
        => MemoryExtensions.Sort<T, ComparerWrapper<T>>(span, comparison);

    /// <summary>
    /// Trims the span to specified length if it exceeds it.
    /// If length is less that <paramref name="maxLength" /> then the original span returned.
    /// </summary>
    /// <typeparam name="T">The type of items in the span.</typeparam>
    /// <param name="span">A contiguous region of arbitrary memory.</param>
    /// <param name="maxLength">Maximum length.</param>
    /// <returns>Trimmed span.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is less than zero.</exception>
    public static Span<T> TrimLength<T>(this Span<T> span, int maxLength)
    {
        switch (maxLength)
        {
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(maxLength));
            case 0:
                span = default;
                break;
            default:
                if (span.Length > maxLength)
                    span = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), maxLength);
                break;
        }

        return span;
    }

    /// <summary>
    /// Trims the span to specified length if it exceeds it.
    /// If length is less that <paramref name="maxLength" /> then the original span returned.
    /// </summary>
    /// <typeparam name="T">The type of items in the span.</typeparam>
    /// <param name="span">A contiguous region of arbitrary memory.</param>
    /// <param name="maxLength">Maximum length.</param>
    /// <returns>Trimmed span.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is less than zero.</exception>
    public static ReadOnlySpan<T> TrimLength<T>(this ReadOnlySpan<T> span, int maxLength)
        => TrimLength(MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), span.Length), maxLength);

    private static int IndexOf<T, TComparer>(ReadOnlySpan<T> span, T value, int startIndex, TComparer comparer)
        where TComparer : struct, ISupplier<T, T, bool>
    {
        while ((uint)startIndex < (uint)span.Length)
        {
            if (comparer.Invoke(span[startIndex], value))
                return startIndex;

            startIndex++;
        }

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

    internal static void ForEach<T>(ReadOnlySpan<T> span, Action<T> action)
    {
        foreach (var item in span)
            action(item);
    }

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
        ref char hexTable = ref MemoryMarshal.GetArrayDataReference(NimbleToCharLookupTable);
        if (!lowercased)
            hexTable = ref Unsafe.Add(ref hexTable, 16);
        for (var i = 0; i < bytesCount; i++, charPtr = ref Add(ref charPtr, 1))
        {
            var value = Add(ref firstByte, i);
            charPtr = Add(ref hexTable, value >> 4);
            charPtr = ref Add(ref charPtr, 1);
            charPtr = Add(ref hexTable, value & NimbleMaxValue);
        }

        return bytesCount * 2;
    }

    /// <summary>
    /// Converts set of bytes into hexadecimal representation.
    /// </summary>
    /// <param name="bytes">The bytes to convert.</param>
    /// <param name="lowercased"><see langword="true"/> to return lowercased hex string; <see langword="false"/> to return uppercased hex string.</param>
    /// <returns>The hexadecimal representation of bytes.</returns>
    [SkipLocalsInit]
    public static string ToHex(this ReadOnlySpan<byte> bytes, bool lowercased = false)
    {
        var count = bytes.Length * 2;
        if (count == 0)
            return string.Empty;

        using MemoryRental<char> buffer = (uint)count <= (uint)MemoryRental<char>.StackallocThreshold ? stackalloc char[count] : new MemoryRental<char>(count);
        count = ToHex(bytes, buffer.Span, lowercased);
        return new string(buffer.Span.Slice(0, count));
    }

    /// <summary>
    /// Decodes hexadecimal representation of bytes.
    /// </summary>
    /// <param name="chars">The hexadecimal representation of bytes.</param>
    /// <param name="output">The output buffer used to write decoded bytes.</param>
    /// <returns>The actual number of bytes in <paramref name="output"/> written by the method.</returns>
    /// <exception cref="FormatException"><paramref name="chars"/> contain invalid hexadecimal symbol.</exception>
    public static int FromHex(this ReadOnlySpan<char> chars, Span<byte> output)
    {
        if (chars.IsEmpty || output.IsEmpty)
            return 0;
        var charCount = Math.Min(chars.Length, output.Length * 2);
        charCount -= charCount % 2;
        ref byte bytePtr = ref MemoryMarshal.GetReference(output);
        for (var i = 0; i < charCount; i += 2, bytePtr = ref Add(ref bytePtr, 1))
        {
            var high = Unsafe.Add(ref MemoryMarshal.GetReference(chars), i);
            var low = Unsafe.Add(ref MemoryMarshal.GetReference(chars), i + 1);

            bytePtr = (byte)(ToNimble(low) | (ToNimble(high) << 4));
        }

        return charCount / 2;

        static byte ToNimble(int ch)
        {
            var table = CharToNimbleLookupTable;

            byte result;
            return (uint)ch < (uint)table.Length && ((result = table[ch]) <= NimbleMaxValue)
                ? result
                : throw new FormatException(ExceptionMessages.InvalidHexInput((char)ch));
        }
    }

    /// <summary>
    /// Decodes hexadecimal representation of bytes.
    /// </summary>
    /// <param name="chars">The characters containing hexadecimal representation of bytes.</param>
    /// <returns>The decoded array of bytes.</returns>
    /// <exception cref="FormatException"><paramref name="chars"/> contain invalid hexadecimal symbol.</exception>
    [SkipLocalsInit]
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
    public static unsafe Span<byte> AsBytes<T>(ref T value)
        where T : unmanaged
        => MemoryMarshal.CreateSpan(ref As<T, byte>(ref value), sizeof(T));

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
        MemoryOwner<T> result;
        var length = first.Length + second.Length;

        if (length == 0)
        {
            result = default;
        }
        else
        {
            result = allocator is null ?
                new(ArrayPool<T>.Shared, length) :
                allocator(length);

            var output = result.Span;
            first.CopyTo(output);
            second.CopyTo(output.Slice(first.Length));
        }

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

        var output = result.Span;
        first.CopyTo(output);
        second.CopyTo(output = output.Slice(first.Length));
        third.CopyTo(output.Slice(second.Length));

        return result;
    }

    /// <summary>
    /// Creates buffered copy of the memory block.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the memory.</typeparam>
    /// <param name="span">The span of elements to be copied to the buffer.</param>
    /// <param name="allocator">Optional buffer allocator.</param>
    /// <returns>The copy of the elements from <paramref name="span"/>.</returns>
    public static MemoryOwner<T> Copy<T>(this ReadOnlySpan<T> span, MemoryAllocator<T>? allocator = null)
    {
        if (span.IsEmpty)
            return default;

        var result = allocator is null ?
            new MemoryOwner<T>(ArrayPool<T>.Shared, span.Length) :
            allocator(span.Length);

        span.CopyTo(result.Span);
        return result;
    }

    /// <summary>
    /// Copies the contents from the source span into a destination span.
    /// </summary>
    /// <param name="source">Source memory.</param>
    /// <param name="destination">Destination memory.</param>
    /// <param name="writtenCount">The number of copied elements.</param>
    /// <typeparam name="T">The type of the elements in the span.</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static void CopyTo<T>(this ReadOnlySpan<T> source, Span<T> destination, out int writtenCount)
    {
        if (source.Length > destination.Length)
        {
            source = MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetReference(source), writtenCount = destination.Length);
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

    /// <summary>
    /// Shuffles elements in the span.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the span.</typeparam>
    /// <param name="span">The span of elements to shuffle.</param>
    /// <param name="random">The source of random values.</param>
    public static void Shuffle<T>(this Span<T> span, Random random)
    {
        for (var i = span.Length - 1; i > 0; i--)
        {
            var randomIndex = random.Next(i + 1);
            Intrinsics.Swap(ref span[randomIndex], ref span[i]);
        }
    }

    /// <summary>
    /// Gets first element in the span.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <param name="span">The span of elements.</param>
    /// <returns>The first element in the span; or <see cref="Optional{T}.None"/> if span is empty.</returns>
    public static Optional<T> FirstOrEmpty<T>(this ReadOnlySpan<T> span)
        => span.Length > 0 ? span[0] : Optional<T>.None;

    /// <summary>
    /// Returns the first element in a span that satisfies a specified condition.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the span.</typeparam>
    /// <param name="span">The source span.</param>
    /// <param name="filter">A function to test each element for a condition.</param>
    /// <returns>The first element in the span that matches to the specified filter; or <see cref="Optional{T}.None"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> is <see langword="null"/>.</exception>
    public static Optional<T> FirstOrEmpty<T>(this ReadOnlySpan<T> span, Predicate<T> filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        for (var i = 0; i < span.Length; i++)
        {
            var item = span[i];
            if (filter(item))
                return item;
        }

        return Optional<T>.None;
    }

    /// <summary>
    /// Gets random element from the span.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <param name="span">The span of elements.</param>
    /// <param name="random">The source of random values.</param>
    /// <returns>Randomly selected element from the span; or <see cref="Optional{T}.None"/> if span is empty.</returns>
    public static Optional<T> PeekRandom<T>(this ReadOnlySpan<T> span, Random random) => span.Length switch
    {
        0 => Optional<T>.None,
        1 => MemoryMarshal.GetReference(span),
        int length => span[random.Next(length)], // cannot use MemoryMarshal here because Random.Next is virtual so bounds check required for security reasons
    };

    internal static bool ElementAt<T>(ReadOnlySpan<T> span, int index, [MaybeNullWhen(false)] out T element)
    {
        if ((uint)index < (uint)span.Length)
        {
            element = span[index];
            return true;
        }

        element = default;
        return false;
    }

    /// <summary>
    /// Initializes each element in the span.
    /// </summary>
    /// <remarks>
    /// This method has the same behavior as <see cref="Array.Initialize"/> and supports reference types.
    /// </remarks>
    /// <typeparam name="T">The type of the element.</typeparam>
    /// <param name="span">The span of elements.</param>
    public static void Initialize<T>(this Span<T> span)
        where T : new()
    {
        foreach (ref var item in span)
            item = new T();
    }
}