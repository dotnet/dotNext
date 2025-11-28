using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext;

using Buffers;
using Runtime.CompilerServices;

partial class Span
{
    /// <summary>
    /// Represents extensions for <see cref="Span{T}"/> type.
    /// </summary>
    /// <param name="span">The span to iterate.</param>
    /// <typeparam name="T">The type of the elements.</typeparam>
    extension<T>(Span<T> span)
    {
        /// <summary>
        /// Iterates over elements of the span.
        /// </summary>
        /// <param name="action">The action to be applied for each element of the span.</param>
        public void ForEach(Action<LocalReference<T>, int> action)
        {
            for (var i = 0; i < span.Length; i++)
                action(new(ref span[i]), i);
        }

        /// <summary>
        /// Iterates over elements of the span.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the action.</typeparam>
        /// <param name="action">The action to be applied for each element of the span.</param>
        /// <param name="arg">The argument to be passed to the action.</param>
        /// <exception cref="ArgumentNullException"><paramref name="action"/> is zero.</exception>
        [CLSCompliant(false)]
        public unsafe void ForEach<TArg>(delegate*<ref T, TArg, void> action, TArg arg)
            where TArg : allows ref struct
        {
            ArgumentNullException.ThrowIfNull(action);

            foreach (ref var item in span)
                action(ref item, arg);
        }
        
        /// <summary>
        /// Sorts the elements.
        /// </summary>
        /// <param name="comparison">The comparer used for sorting.</param>
        [CLSCompliant(false)]
        public unsafe void Sort(delegate*<T?, T?, int> comparison)
            => span.Sort<T, ComparerWrapper<T>>(comparison);

        /// <summary>
        /// Trims the span to specified length if it exceeds it.
        /// If length is less that <paramref name="maxLength" /> then the original span returned.
        /// </summary>
        /// <param name="maxLength">Maximum length.</param>
        /// <returns>Trimmed span.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is less than zero.</exception>
        public Span<T> TrimLength(int maxLength)
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
        /// <param name="maxLength">Maximum length.</param>
        /// <param name="rest">The rest of <paramref name="span"/>.</param>
        /// <returns>Trimmed span.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is less than zero.</exception>
        public Span<T> TrimLength(int maxLength, out Span<T> rest)
        {
            switch (maxLength)
            {
                case < 0:
                    throw new ArgumentOutOfRangeException(nameof(maxLength));
                case 0:
                    rest = span;
                    span = default;
                    break;
                default:
                    span = TrimLengthCore(span, maxLength, out rest);
                    break;
            }

            return span;
        }

        /// <summary>
        /// Copies the contents from the source span into a destination span.
        /// </summary>
        /// <param name="destination">Destination memory.</param>
        /// <param name="writtenCount">The number of copied elements.</param>
        /// <typeparam name="T">The type of the elements in the span.</typeparam>
        public void CopyTo(Span<T> destination, out int writtenCount)
            => writtenCount = span >> destination;

        /// <summary>
        /// Copies the contents from the source span into a destination span.
        /// </summary>
        /// <param name="source">Source memory.</param>
        /// <param name="destination">Destination memory.</param>
        /// <returns>The number of copied elements.</returns>
        public static int operator >> (Span<T> source, Span<T> destination)
            => (ReadOnlySpan<T>)source >> destination;
        
        /// <summary>
        /// Swaps two ranges within the same span.
        /// </summary>
        /// <param name="range1">The first range.</param>
        /// <param name="range2">The second range.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="range1"/> or <paramref name="range2"/> is out of valid range.</exception>
        /// <exception cref="ArgumentException"><paramref name="range2"/> is overlapped with <paramref name="range1"/>.</exception>
        public void Swap(Range range1, Range range2)
        {
            var (start1, length1) = range1.GetOffsetAndLength(span.Length);
            var (start2, length2) = range2.GetOffsetAndLength(span.Length);

            if (start1 > start2)
            {
                RuntimeHelpers.Swap(ref start1, ref start2);
                RuntimeHelpers.Swap(ref length1, ref length2);
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
                SpanOwner<T> buffer;
                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() || sourceLarge.Length > SpanOwner<T>.StackallocThreshold)
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
        /// <param name="range">The range of elements within <paramref name="span"/> to move.</param>
        /// <param name="destinationIndex">The index of the element before which <paramref name="range"/> of elements will be placed.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="destinationIndex"/> is not a valid index within <paramref name="span"/>.</exception>
        public void Move(Range range, Index destinationIndex)
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
                SpanOwner<T> buffer;
                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() || length > SpanOwner<T>.StackallocThreshold)
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
        
        /// <summary>
        /// Swaps contents of the two spans.
        /// </summary>
        /// <param name="y">The second span.</param>
        /// <exception cref="ArgumentOutOfRangeException">The length of <paramref name="y"/> is not of the same length as <paramref name="span"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="span"/> overlaps with <paramref name="y"/>.</exception>
        public void Swap(Span<T> y)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(span.Length, y.Length, nameof(y));

            if (span.Overlaps(y))
                throw new ArgumentException(ExceptionMessages.OverlappedRange, nameof(y));

            SwapCore(span, y);
        }
    }
    
    /// <summary>
    /// Extends <see cref="Span{T}"/> type.
    /// </summary>
    /// <param name="source">The source span.</param>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    extension<T>(ref Span<T> source)
    {
        /// <summary>
        /// Takes the specified number of elements and adjusts the span.
        /// </summary>
        /// <param name="count">The number of elements to take.</param>
        /// <returns>The span containing <paramref name="count"/> elements.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is greater than the length of <paramref name="source"/>.</exception>
        public Span<T> Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)source.Length, nameof(count));

            ref var ptr = ref MemoryMarshal.GetReference(source);
            source = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref ptr, count), source.Length - count);
            return MemoryMarshal.CreateSpan(ref ptr, count);
        }

        /// <summary>
        /// Takes the first element and adjusts the span.
        /// </summary>
        /// <returns>The reference to the first element in the span.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="source"/> is empty.</exception>
        public ref T Advance()
        {
            ArgumentOutOfRangeException.ThrowIfZero(source.Length, nameof(source));

            ref T ptr = ref MemoryMarshal.GetReference(source);
            source = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref ptr, 1), source.Length - 1);
            return ref ptr;
        }
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
    
    private static Span<T> TrimLengthCore<T>(Span<T> span, int maxLength, out Span<T> rest)
    {
        var length = span.Length;
        if (length > maxLength)
        {
            ref var ptr = ref MemoryMarshal.GetReference(span);
            span = MemoryMarshal.CreateSpan(ref ptr, maxLength);
            rest = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref ptr, maxLength), length - maxLength);
        }
        else
        {
            rest = default;
        }

        return span;
    }
    
    private static void SwapCore<T>(Span<T> x, Span<T> y)
    {
        Debug.Assert(x.Length == y.Length);

        var bufferSize = Math.Min(SpanOwner<T>.StackallocThreshold, x.Length);
        if (bufferSize is 0)
        {
            return;
        }
        else if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            var buffer = ArrayPool<T>.Shared.Rent(bufferSize);
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
}