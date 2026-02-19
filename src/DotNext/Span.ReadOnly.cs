using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext;

using Buffers;

partial class Span
{
    /// <summary>
    /// Represents bitwise comparison operations.
    /// </summary>
    /// <param name="x">The first memory span to compare.</param>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    extension<T>(ReadOnlySpan<T> x) where T : unmanaged
    {
        /// <summary>
        /// Determines whether two memory blocks identified by the given spans contain the same set of elements.
        /// </summary>
        /// <remarks>
        /// This method performs bitwise equality between each pair of elements.
        /// </remarks>
        /// <param name="y">The second memory span to compare.</param>
        /// <returns><see langword="true"/>, if both memory blocks are equal; otherwise, <see langword="false"/>.</returns>
        public unsafe bool BitwiseEquals(ReadOnlySpan<T> y)
        {
            if (x.Length == y.Length)
            {
                for (int maxSize = Array.MaxLength / sizeof(T), size; !x.IsEmpty; x = x.Slice(size), y = y.Slice(size))
                {
                    size = Math.Min(maxSize, x.Length);
                    var sizeInBytes = size * sizeof(T);
                    var partX = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(x)), sizeInBytes);
                    var partY = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(y)), sizeInBytes);
                    if (partX.SequenceEqual(partY) is false)
                        return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Compares content of the two memory blocks identified by the given spans.
        /// </summary>
        /// <param name="y">The second array to compare.</param>
        /// <returns>Comparison result.</returns>
        public unsafe int BitwiseCompare(ReadOnlySpan<T> y)
        {
            var result = x.Length;
            result = result.CompareTo(y.Length);
            if (result is 0)
            {
                for (int maxSize = Array.MaxLength / sizeof(T), size; !x.IsEmpty; x = x.Slice(size), y = y.Slice(size))
                {
                    size = Math.Min(maxSize, x.Length);
                    var sizeInBytes = size * sizeof(T);
                    var partX = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(x)), sizeInBytes);
                    var partY = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(y)), sizeInBytes);
                    result = partX.SequenceCompareTo(partY);
                    if (result is not 0)
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Determines whether the specified value satisfies the given mask.
        /// </summary>
        /// <param name="mask">The mask.</param>
        /// <returns><see langword="true"/> if <c>value &amp; mask == mask</c>; otherwise, <see langword="false"/>.</returns>
        public unsafe bool CheckMask(ReadOnlySpan<T> mask)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(x.Length, mask.Length, nameof(mask));

            for (int maxLength = Array.MaxLength / sizeof(T), count; !x.IsEmpty; x = x.Slice(count), mask = mask.Slice(count))
            {
                count = Math.Min(maxLength, x.Length);

                if (CheckMask(
                        ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(x)),
                        ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(mask)),
                        count * sizeof(T)) is false)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified value and the given mask produces non-zero bitwise AND.
        /// </summary>
        /// <param name="mask">The mask.</param>
        /// <returns><see langword="true"/> if <c>value &amp; mask != 0</c>; otherwise, <see langword="false"/>.</returns>
        public unsafe bool IsBitwiseAndNonZero(ReadOnlySpan<T> mask)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(x.Length, mask.Length, nameof(mask));

            for (int maxLength = Array.MaxLength / sizeof(T), count; !x.IsEmpty; x = x.Slice(count), mask = mask.Slice(count))
            {
                count = Math.Min(maxLength, x.Length);

                if (IsBitwiseAndNonZero(
                        ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(x)),
                        ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(mask)),
                        count * sizeof(T)) is false)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified value and the given mask produces non-zero bitwise AND.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <param name="mask">The mask.</param>
        /// <returns><see langword="true"/> if <c>value &amp; mask != 0</c>; otherwise, <see langword="false"/>.</returns>
        public static bool operator &(ReadOnlySpan<T> value, ReadOnlySpan<T> mask)
            => value.IsBitwiseAndNonZero(mask);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> ReinterpretCast<TInput>(ReadOnlySpan<TInput> input)
            where TInput : unmanaged
        {
            Debug.Assert(Unsafe.SizeOf<TInput>() == Unsafe.SizeOf<T>());

            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<TInput, T>(ref MemoryMarshal.GetReference(input)), input.Length);
        }
    }

    /// <summary>
    /// Extends <see cref="ReadOnlySpan{T}"/> type.
    /// </summary>
    /// <param name="span">A contiguous region of arbitrary memory.</param>
    /// <typeparam name="T">The type of the elements in the memory.</typeparam>
    extension<T>(ReadOnlySpan<T> span)
    {
        /// <summary>
        /// Trims the span to specified length if it exceeds it.
        /// If length is less that <paramref name="maxLength" /> then the original span returned.
        /// </summary>
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>Trimmed span.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is less than zero.</exception>
        public ReadOnlySpan<T> TrimLength(int maxLength)
            => TrimLength(MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), span.Length), maxLength);
        
        /// <summary>
        /// Returns the zero-based index of the first occurrence of the specified value in the <see cref="Span{T}"/>. The search starts at a specified position.
        /// </summary>
        /// <param name="value">The value to search for.</param>
        /// <param name="startIndex">The search starting position.</param>
        /// <param name="comparer">The comparer used to compare the expected value and the actual value from the span.</param>
        /// <returns>The zero-based index position of <paramref name="value"/> from the start of the given span if that value is found, or -1 if it is not.</returns>
        public int IndexOf(T value, int startIndex, Func<T, T, bool> comparer)
            => IndexOf<T, DelegatingSupplier<T, T, bool>>(span, value, startIndex, comparer);

        /// <summary>
        /// Returns the zero-based index of the first occurrence of the specified value in the <see cref="Span{T}"/>. The search starts at a specified position.
        /// </summary>
        /// <param name="value">The value to search for.</param>
        /// <param name="startIndex">The search starting position.</param>
        /// <param name="comparer">The comparer used to compare the expected value and the actual value from the span.</param>
        /// <returns>The zero-based index position of <paramref name="value"/> from the start of the given span if that value is found, or -1 if it is not.</returns>
        [CLSCompliant(false)]
        public unsafe int IndexOf(T value, int startIndex, delegate*<T, T, bool> comparer)
            => IndexOf<T, Supplier<T, T, bool>>(span, value, startIndex, comparer);
        
        /// <summary>
        /// Concatenates memory blocks.
        /// </summary>
        /// <param name="first">The first memory block.</param>
        /// <param name="second">The second memory block.</param>
        /// <param name="third">The third memory block.</param>
        /// <param name="allocator">The memory allocator used to allocate buffer for the result.</param>
        /// <returns>The memory block containing elements from the specified two memory blocks.</returns>
        public static MemoryOwner<T> Concat(ReadOnlySpan<T> first, ReadOnlySpan<T> second, ReadOnlySpan<T> third, MemoryAllocator<T>? allocator = null)
        {
            if (first.IsEmpty && second.IsEmpty && third.IsEmpty)
                return default;

            var length = checked(first.Length + second.Length + third.Length);
            var result = allocator?.Invoke(length) ?? new MemoryOwner<T>(ArrayPool<T>.Shared, length);

            var output = result.Span;
            first.CopyTo(output);
            second.CopyTo(output = output.Slice(first.Length));
            third.CopyTo(output.Slice(second.Length));

            return result;
        }
        
        /// <summary>
        /// Concatenates memory blocks.
        /// </summary>
        /// <param name="first">The first memory block.</param>
        /// <param name="second">The second memory block.</param>
        /// <param name="allocator">The memory allocator used to allocate buffer for the result.</param>
        /// <returns>The memory block containing elements from the specified two memory blocks.</returns>
        public static MemoryOwner<T> Concat(ReadOnlySpan<T> first, ReadOnlySpan<T> second, MemoryAllocator<T>? allocator = null)
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
                    result = allocator?.Invoke(length) ?? new(ArrayPool<T>.Shared, length);

                    var output = result.Span;
                    first.CopyTo(output);
                    second.CopyTo(output.Slice(first.Length));
                    break;
            }

            return result;
        }

        /// <summary>
        /// Creates buffered copy of the memory block.
        /// </summary>
        /// <param name="allocator">Optional buffer allocator.</param>
        /// <returns>The copy of the elements from the source span.</returns>
        public MemoryOwner<T> Copy(MemoryAllocator<T>? allocator = null)
        {
            if (span.IsEmpty)
                return default;

            var result = allocator?.Invoke(span.Length) ?? new MemoryOwner<T>(ArrayPool<T>.Shared, span.Length);
            span.CopyTo(result.Span);
            return result;
        }

        /// <summary>
        /// Copies the contents from the source span into a destination span.
        /// </summary>
        /// <param name="destination">Destination memory.</param>
        /// <param name="writtenCount">The number of copied elements.</param>
        public void CopyTo(Span<T> destination, out int writtenCount)
            => writtenCount = span >>> destination;

        /// <summary>
        /// Copies the contents from the source span into a destination span.
        /// </summary>
        /// <param name="source">Source memory.</param>
        /// <param name="destination">Destination memory.</param>
        /// <returns>The number of copied elements.</returns>
        public static int operator >>>(ReadOnlySpan<T> source, Span<T> destination)
        {
            int writtenCount;
            if (source.Length > destination.Length)
            {
                source = MemoryMarshal.CreateReadOnlySpan(ref MemoryMarshal.GetReference(source), writtenCount = destination.Length);
            }
            else
            {
                writtenCount = source.Length;
            }

            source.CopyTo(destination);
            return writtenCount;
        }
        
        /// <summary>
        /// Returns the first element in a span that satisfies a specified condition.
        /// </summary>
        /// <param name="filter">A function to test each element for a condition.</param>
        /// <returns>The first element in the span that matches to the specified filter; or <see cref="Optional{T}.None"/>.</returns>
        public Optional<T> FirstOrNone(Predicate<T>? filter = null)
        {
            filter ??= Predicate<T>.Constant(true);
        
            foreach (var item in span)
            {
                if (filter(item))
                    return item;
            }

            return Optional<T>.None;
        }

        internal Optional<T> LastOrNone()
        {
            ref var elementRef = ref MemoryMarshal.GetReference(span);
            var length = span.Length;
            return length > 0 ? Unsafe.Add(ref elementRef, length - 1) : Optional<T>.None;
        }
    }
    
    /// <summary>
    /// Extends <see cref="ReadOnlySpan{T}"/> type.
    /// </summary>
    /// <param name="source">The source span.</param>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    extension<T>(ref ReadOnlySpan<T> source)
    {
        /// <summary>
        /// Takes the specified number of elements and adjusts the span.
        /// </summary>
        /// <param name="count">The number of elements to take.</param>
        /// <returns>The span containing <paramref name="count"/> elements.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is greater than the length of the source span.</exception>
        public ReadOnlySpan<T> Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)source.Length, nameof(count));

            ref var ptr = ref MemoryMarshal.GetReference(source);
            source = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref ptr, count), source.Length - count);
            return MemoryMarshal.CreateReadOnlySpan(ref ptr, count);
        }

        /// <summary>
        /// Takes the first element and adjusts the span.
        /// </summary>
        /// <returns>The reference to the first element in the span.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The source span is empty.</exception>
        public ref readonly T Advance()
        {
            ArgumentOutOfRangeException.ThrowIfZero(source.Length, nameof(source));

            ref T ptr = ref MemoryMarshal.GetReference(source);
            source = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref ptr, 1), source.Length - 1);
            return ref ptr;
        }
    }

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

    internal static void ForEach<T>(ReadOnlySpan<T> span, Action<T> action)
    {
        foreach (var item in span)
            action(item);
    }
    
    internal static bool BitwiseEquals<T>(T[] x, T[] y)
        where T : unmanaged
        => BitwiseEquals(new ReadOnlySpan<T>(x), new ReadOnlySpan<T>(y));
    
    private static bool CheckMask([In] ref byte data, [In]ref byte mask, int length)
    {
        // iterate by Vector
        if (Vector.IsHardwareAccelerated)
        {
            for (; length >= Vector<byte>.Count; length -= Vector<byte>.Count)
            {
                var dataVec = Vector.LoadUnsafe(ref data);
                var maskVec = Vector.LoadUnsafe(ref mask);

                if ((dataVec & maskVec) != maskVec)
                    return false;

                data = ref Unsafe.Add(ref data, Vector<byte>.Count);
                mask = ref Unsafe.Add(ref mask, Vector<byte>.Count);
            }
        }

        // iterate by nuint.Size
        for (; length >= UIntPtr.Size; length -= UIntPtr.Size)
        {
            var dataVal = Unsafe.ReadUnaligned<nuint>(ref data);
            var maskVal = Unsafe.ReadUnaligned<nuint>(ref mask);

            if ((dataVal & maskVal) != maskVal)
                return false;

            data = ref Unsafe.Add(ref data, UIntPtr.Size);
            mask = ref Unsafe.Add(ref mask, UIntPtr.Size);
        }

        // iterate by byte
        for (; length > 0; length -= 1)
        {
            var dataVal = data;
            var maskVal = mask;
            
            if ((dataVal & maskVal) != maskVal)
                return false;
            
            data = ref Unsafe.Add(ref data, 1);
            mask = ref Unsafe.Add(ref mask, 1);
        }

        return true;
    }
    
    private static bool IsBitwiseAndNonZero([In] ref byte data, [In]ref byte mask, int length)
    {
        // iterate by Vector
        if (Vector.IsHardwareAccelerated)
        {
            for (; length >= Vector<byte>.Count; length -= Vector<byte>.Count)
            {
                var dataVec = Vector.LoadUnsafe(ref data);
                var maskVec = Vector.LoadUnsafe(ref mask);

                if ((dataVec & maskVec) != Vector<byte>.Zero)
                    return true;

                data = ref Unsafe.Add(ref data, Vector<byte>.Count);
                mask = ref Unsafe.Add(ref mask, Vector<byte>.Count);
            }
        }

        // iterate by nuint.Size
        for (; length >= UIntPtr.Size; length -= UIntPtr.Size)
        {
            var dataVal = Unsafe.ReadUnaligned<nuint>(ref data);
            var maskVal = Unsafe.ReadUnaligned<nuint>(ref mask);

            if ((dataVal & maskVal) is not 0)
                return true;

            data = ref Unsafe.Add(ref data, UIntPtr.Size);
            mask = ref Unsafe.Add(ref mask, UIntPtr.Size);
        }

        // iterate by byte
        for (; length > 0; length -= 1)
        {
            if ((data & mask) is not 0)
                return true;
            
            data = ref Unsafe.Add(ref data, 1);
            mask = ref Unsafe.Add(ref mask, 1);
        }

        return false;
    }
}