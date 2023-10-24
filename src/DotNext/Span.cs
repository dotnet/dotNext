using System.Buffers;
using System.Diagnostics;
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
public static partial class Span
{
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

        return Intrinsics.GetHashCode32Unaligned(ref As<T, byte>(ref MemoryMarshal.GetReference(span)), checked((nuint)span.Length * (nuint)sizeof(T)), salted);
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

    private static unsafe void BitwiseHashCode<T, THashFunction>(ReadOnlySpan<T> span, scoped ref THashFunction hashFunction, bool salted)
        where T : unmanaged
        where THashFunction : struct, IConsumer<int>
    {
        if (!span.IsEmpty)
            Intrinsics.GetHashCode32Unaligned(ref hashFunction, ref As<T, byte>(ref MemoryMarshal.GetReference(span)), checked((nuint)span.Length * (nuint)sizeof(T)));

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

    private static unsafe void BitwiseHashCode64<T, THashFunction>(ReadOnlySpan<T> span, scoped ref THashFunction hashFunction, bool salted)
        where T : unmanaged
        where THashFunction : struct, IConsumer<long>
    {
        if (!span.IsEmpty)
            Intrinsics.GetHashCode64Unaligned(ref hashFunction, ref As<T, byte>(ref MemoryMarshal.GetReference(span)), checked((nuint)span.Length * (nuint)sizeof(T)));

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

        return Intrinsics.GetHashCode64Unaligned(ref As<T, byte>(ref MemoryMarshal.GetReference(span)), checked((nuint)span.Length * (nuint)sizeof(T)), salted);
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
    /// <param name="rest">The rest of <paramref name="span"/>.</param>
    /// <returns>Trimmed span.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is less than zero.</exception>
    public static Span<T> TrimLength<T>(this Span<T> span, int maxLength, out Span<T> rest)
    {
        switch (maxLength)
        {
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(maxLength));
            case 0:
                rest = span;
                return Span<T>.Empty;
            default:
                return TrimLengthCore(span, maxLength, out rest);
        }
    }

    private static Span<T> TrimLengthCore<T>(Span<T> span, int maxLength, out Span<T> rest)
    {
        var length = span.Length;
        if (length > maxLength)
        {
            ref var ptr = ref MemoryMarshal.GetReference(span);
            span = MemoryMarshal.CreateSpan(ref ptr, maxLength);
            rest = MemoryMarshal.CreateSpan(ref Add(ref ptr, maxLength), length - maxLength);
        }
        else
        {
            rest = default;
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
        if (action is null)
            throw new ArgumentNullException(nameof(action));

        foreach (ref var item in span)
            action(ref item, arg);
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

        switch (length)
        {
            case 0:
                result = default;
                break;
            case < 0:
                throw new OutOfMemoryException();
            default:
                result = allocator is null
                    ? new(ArrayPool<T>.Shared, length)
                    : allocator(length);

                var output = result.Span;
                first.CopyTo(output);
                second.CopyTo(output.Slice(first.Length));
                break;
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
        // TODO: Remove in .NET 8: https://github.com/dotnet/runtime/issues/73864
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
    [Obsolete("Use FirstOrNone() extension method instead")]
    public static Optional<T> FirstOrEmpty<T>(this ReadOnlySpan<T> span)
        => FirstOrNone(span);

    /// <summary>
    /// Gets first element in the span.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <param name="span">The span of elements.</param>
    /// <returns>The first element in the span; or <see cref="Optional{T}.None"/> if span is empty.</returns>
    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1010", Justification = "False positive")]
    public static Optional<T> FirstOrNone<T>(this ReadOnlySpan<T> span) // TODO: Remove in the next version because of list pattern matching
        => span is [var result, ..] ? result : Optional<T>.None;

    /// <summary>
    /// Gets the last element in the span.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <param name="span">The span of elements.</param>
    /// <returns>The last element in the span; or <see cref="Optional{T}.None"/> if span is empty.</returns>
    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1010", Justification = "False positive")]
    public static Optional<T> LastOrNone<T>(this ReadOnlySpan<T> span)
        => span is [.., var result] ? result : Optional<T>.None;

    /// <summary>
    /// Returns the first element in a span that satisfies a specified condition.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the span.</typeparam>
    /// <param name="span">The source span.</param>
    /// <param name="filter">A function to test each element for a condition.</param>
    /// <returns>The first element in the span that matches to the specified filter; or <see cref="Optional{T}.None"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> is <see langword="null"/>.</exception>
    public static Optional<T> FirstOrNone<T>(this ReadOnlySpan<T> span, Predicate<T> filter)
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
    /// Chooses the random element in the span.
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

    /// <summary>
    /// Concatenates multiple strings.
    /// </summary>
    /// <remarks>
    /// You can use methods from <see cref="TupleExtensions"/> to emulate variadic arguments.
    /// </remarks>
    /// <param name="values">An array of strings.</param>
    /// <param name="allocator">The allocator of the concatenated string.</param>
    /// <returns>A buffer containing characters from the concatenated strings.</returns>
    /// <exception cref="OutOfMemoryException">The concatenated string is too large.</exception>
    public static MemoryOwner<char> Concat(ReadOnlySpan<string?> values, MemoryAllocator<char>? allocator = null)
    {
        MemoryOwner<char> result;

        switch (values.Length)
        {
            default:
                var totalLength = 0L;
                foreach (var str in values)
                {
                    if (str is { Length: > 0 })
                    {
                        totalLength += str.Length;
                    }
                }

                if (totalLength is 0)
                    goto case 0;

                if (totalLength > Array.MaxLength)
                    throw new OutOfMemoryException();

                result = allocator is null
                            ? new(ArrayPool<char>.Shared, (int)totalLength)
                            : allocator((int)totalLength);

                var output = result.Span;
                foreach (ReadOnlySpan<char> str in values)
                {
                    str.CopyTo(output);
                    output = output.Slice(str.Length);
                }

                break;
            case 0:
                result = default;
                break;
            case 1:
                result = Copy(values[0], allocator);
                break;
        }

        return result;
    }

    /// <summary>
    /// Upcasts the span.
    /// </summary>
    /// <typeparam name="T">The source type.</typeparam>
    /// <typeparam name="TBase">The target type.</typeparam>
    /// <param name="span">The span over elements.</param>
    /// <returns>The span pointing to the same memory as <paramref name="span"/>.</returns>
    public static ReadOnlySpan<TBase> Contravariance<T, TBase>(this ReadOnlySpan<T> span)
        where T : class?, TBase
        where TBase : class?
        => MemoryMarshal.CreateReadOnlySpan(ref As<T, TBase>(ref MemoryMarshal.GetReference(span)), span.Length);

    /// <summary>
    /// Swaps contents of the two spans.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the span.</typeparam>
    /// <param name="x">The first span.</param>
    /// <param name="y">The second span.</param>
    /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="y"/> is not of the same length as <paramref name="x"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="x"/> overlaps with <paramref name="y"/>.</exception>
    public static void Swap<T>(this Span<T> x, Span<T> y)
    {
        if (x.Length != y.Length)
            throw new ArgumentOutOfRangeException(nameof(y));

        if (x.Overlaps(y))
            throw new ArgumentException(ExceptionMessages.OverlappedRange, nameof(y));

        SwapCore(x, y);
    }

    private static void SwapCore<T>(Span<T> x, Span<T> y)
    {
        Debug.Assert(x.Length == y.Length);

        var bufferSize = Math.Min(MemoryRental<T>.StackallocThreshold, x.Length);
        if (bufferSize is 0)
        {
            return;
        }
        else if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            T[] buffer = ArrayPool<T>.Shared.Rent(bufferSize);
            Swap(x, y, buffer);
            ArrayPool<T>.Shared.Return(buffer, clearArray: true);
        }
        else
        {
            unsafe
            {
                // Only T without references inside can be allocated on the stack.
                // GC cannot track references placed on the stack using `localloc` IL instruction.
                void* buffer = stackalloc byte[checked(Unsafe.SizeOf<T>() * bufferSize)];
                Swap(x, y, new Span<T>(buffer, bufferSize));
            }
        }

        static void Swap(Span<T> x, Span<T> y, Span<T> buffer)
        {
            Debug.Assert(x.Length == y.Length);
            Debug.Assert(buffer.IsEmpty is false);

            while (x.Length >= buffer.Length)
            {
                SwapMemory(TrimLengthCore(x, buffer.Length, out x), TrimLengthCore(y, buffer.Length, out y), buffer);
            }

            if (!x.IsEmpty)
            {
                Debug.Assert(x.Length <= buffer.Length);

                SwapMemory(x, y, buffer.Slice(0, x.Length));
            }
        }

        static void SwapMemory(Span<T> x, Span<T> y, Span<T> buffer)
        {
            x.CopyTo(buffer);
            y.CopyTo(x);
            buffer.CopyTo(y);
        }
    }

    /// <summary>
    /// Swaps two ranges within the same span.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the span.</typeparam>
    /// <param name="span">The source span.</param>
    /// <param name="range1">The first range.</param>
    /// <param name="range2">The second range.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="range1"/> or <paramref name="range2"/> is out of valid range.</exception>
    /// <exception cref="ArgumentException"><paramref name="range2"/> is overlapped with <paramref name="range1"/>.</exception>
    public static void Swap<T>(this Span<T> span, Range range1, Range range2)
    {
        var (start1, length1) = range1.GetOffsetAndLength(span.Length);
        var (start2, length2) = range2.GetOffsetAndLength(span.Length);

        if (start1 > start2)
        {
            Intrinsics.Swap(ref start1, ref start2);
            Intrinsics.Swap(ref length1, ref length2);
        }

        var endOfLeftSegment = start1 + length1;
        if (endOfLeftSegment > start2)
            throw new ArgumentException(ExceptionMessages.OverlappedRange, nameof(range2));

        if (length1 == length2)
        {
            // handle trivial case that allows to avoid allocation of a large buffer
            Span.SwapCore(span.Slice(start1, length1), span.Slice(start2, length2));
        }
        else
        {
            SwapCore(span, start1, length1, start2, length2, endOfLeftSegment);
        }

        static void SwapCore(Span<T> span, int start1, int length1, int start2, int length2, int endOfLeftSegment)
        {
            Span<T> sourceLarge,
                    sourceSmall,
                    destLarge,
                    destSmall;

            // prepare buffers
            var shift = length1 - length2;
            if (shift < 0)
            {
                // length1 < length2
                sourceLarge = span.Slice(start2, length2);
                destLarge = span.Slice(start1, length2);

                sourceSmall = span.Slice(start1, length1);
                destSmall = span.Slice(start2 - shift, length1); // -shift = start2 + -shift, shift is negative here
            }
            else
            {
                // length1 > length2
                sourceLarge = span.Slice(start1, length1);
                destLarge = span.Slice(start2 - shift, length1);

                sourceSmall = span.Slice(start2, length2);
                destSmall = span.Slice(start1, length2);
            }

            Debug.Assert(sourceLarge.Length == destLarge.Length);
            Debug.Assert(sourceSmall.Length == destSmall.Length);

            // prepare buffer
            MemoryRental<T> buffer;
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() || sourceLarge.Length > MemoryRental<T>.StackallocThreshold)
            {
                buffer = new(sourceLarge.Length);
            }
            else
            {
                unsafe
                {
                    void* bufferPtr = stackalloc byte[checked(Unsafe.SizeOf<T>() * sourceLarge.Length)];
                    buffer = new Span<T>(bufferPtr, sourceLarge.Length);
                }
            }

            // rearrange elements
            sourceLarge.CopyTo(buffer.Span);
            span[endOfLeftSegment..start2].CopyTo(span.Slice(start1 + length2));
            sourceSmall.CopyTo(destSmall);
            buffer.Span.CopyTo(destLarge);

            buffer.Dispose();
        }
    }

    /// <summary>
    /// Moves the range within the span to the specified index.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the span.</typeparam>
    /// <param name="span">The span of elements to modify.</param>
    /// <param name="range">The range of elements within <paramref name="span"/> to move.</param>
    /// <param name="destinationIndex">The index of the element before which <paramref name="range"/> of elements will be placed.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="destinationIndex"/> is not a valid index within <paramref name="span"/>.</exception>
    public static void Move<T>(this Span<T> span, Range range, Index destinationIndex)
    {
        var (sourceIndex, length) = range.GetOffsetAndLength(span.Length);

        if (length is not 0)
            MoveCore(span, sourceIndex, destinationIndex.GetOffset(span.Length), length);

        static void MoveCore(Span<T> span, int sourceIndex, int destinationIndex, int length)
        {
            // prepare buffers
            Span<T> source = span.Slice(sourceIndex, length),
                destination,
                sourceGap,
                destinationGap;
            if (sourceIndex > destinationIndex)
            {
                sourceGap = span[destinationIndex..sourceIndex];
                destinationGap = span.Slice(destinationIndex + length);

                destination = span.Slice(destinationIndex, length);
            }
            else
            {
                var endOfLeftSegment = sourceIndex + length;
                switch (endOfLeftSegment.CompareTo(destinationIndex))
                {
                    case 0:
                        return;
                    case > 0:
                        throw new ArgumentOutOfRangeException(nameof(destinationIndex));
                    case < 0:
                        sourceGap = span[endOfLeftSegment..destinationIndex];
                        destinationGap = span.Slice(sourceIndex);

                        destination = span.Slice(destinationIndex - length, length);
                        break;
                }
            }

            // Perf: allocate buffer for smallest part of the span
            if (source.Length > sourceGap.Length)
            {
                length = sourceGap.Length;

                var temp = source;
                source = sourceGap;
                sourceGap = temp;

                temp = destination;
                destination = destinationGap;
                destinationGap = temp;
            }
            else
            {
                Debug.Assert(length == source.Length);
            }

            // prepare buffer
            MemoryRental<T> buffer;
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() || length > MemoryRental<T>.StackallocThreshold)
            {
                buffer = new(length);
            }
            else
            {
                unsafe
                {
                    void* bufferPtr = stackalloc byte[checked(Unsafe.SizeOf<T>() * length)];
                    buffer = new Span<T>(bufferPtr, length);
                }
            }

            // rearrange buffers
            source.CopyTo(buffer.Span);
            sourceGap.CopyTo(destinationGap);
            buffer.Span.CopyTo(destination);

            buffer.Dispose();
        }
    }
}