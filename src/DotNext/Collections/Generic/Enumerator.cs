using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic;

/// <summary>
/// Various methods to work with classes implementing <see cref="IEnumerable{T}"/> interface.
/// </summary>
public static partial class Enumerator
{
    /// <summary>
    /// Bypasses a specified number of elements in a sequence.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="enumerator">Enumerator to modify. Cannot be <see langword="null"/>.</param>
    /// <param name="count">The number of elements to skip.</param>
    /// <returns><see langword="true"/>, if current element is available; otherwise, <see langword="false"/>.</returns>
    public static bool Skip<T>(this IEnumerator<T> enumerator, int count)
    {
        while (count > 0)
        {
            if (!enumerator.MoveNext())
                return false;

            count--;
        }

        return true;
    }

    /// <summary>
    /// Bypasses a specified number of elements in a sequence.
    /// </summary>
    /// <typeparam name="TEnumerator">The type of the sequence.</typeparam>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="enumerator">Enumerator to modify.</param>
    /// <param name="count">The number of elements to skip.</param>
    /// <returns><see langword="true"/>, if current element is available; otherwise, <see langword="false"/>.</returns>
    public static bool Skip<TEnumerator, T>(this ref TEnumerator enumerator, int count)
        where TEnumerator : struct, IEnumerator<T>
    {
        while (count > 0)
        {
            if (!enumerator.MoveNext())
                return false;

            count--;
        }

        return true;
    }

    /// <summary>
    /// Limits the number of the elements in the sequence.
    /// </summary>
    /// <typeparam name="T">The type of items in the sequence.</typeparam>
    /// <param name="enumerator">The sequence of the elements.</param>
    /// <param name="count">The maximum number of the elements in the returned sequence.</param>
    /// <param name="leaveOpen"><see langword="false"/> to dispose <paramref name="enumerator"/>; otherwise, <see langword="true"/>.</param>
    /// <returns>The enumerator which is limited by count.</returns>
    public static LimitedEnumerator<T> Limit<T>(this IEnumerator<T> enumerator, int count, bool leaveOpen = false)
        => new(enumerator, count, leaveOpen);

    /// <summary>
    /// Gets enumerator over all elements in the memory.
    /// </summary>
    /// <param name="memory">The memory block to be converted.</param>
    /// <typeparam name="T">The type of elements in the memory.</typeparam>
    /// <returns>The enumerator over all elements in the memory.</returns>
    /// <seealso cref="MemoryMarshal.ToEnumerable{T}(ReadOnlyMemory{T})"/>
    public static IEnumerator<T> ToEnumerator<T>(ReadOnlyMemory<T> memory)
    {
        return memory.IsEmpty
            ? Enumerable.Empty<T>().GetEnumerator()
            : MemoryMarshal.TryGetArray(memory, out var segment)
            ? segment.GetEnumerator()
            : ToEnumeratorSlow(memory);

        static IEnumerator<T> ToEnumeratorSlow(ReadOnlyMemory<T> memory)
        {
            for (nint i = 0; i < memory.Length; i++)
                yield return Unsafe.Add(ref MemoryMarshal.GetReference(memory.Span), i);
        }
    }

    /// <summary>
    /// Gets enumerator over all elements in the sequence.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <param name="sequence">A sequence of elements.</param>
    /// <returns>The enumerator over all elements in the sequence.</returns>
    public static IEnumerator<T> ToEnumerator<T>(in ReadOnlySequence<T> sequence)
    {
        return sequence.IsEmpty
            ? Enumerable.Empty<T>().GetEnumerator()
            : sequence.IsSingleSegment
            ? ToEnumerator(sequence.First)
            : ToEnumeratorSlow(sequence.GetEnumerator());

        static IEnumerator<T> ToEnumeratorSlow(ReadOnlySequence<T>.Enumerator enumerator)
        {
            while (enumerator.MoveNext())
            {
                var segment = enumerator.Current;

                for (nint i = 0; i < segment.Length; i++)
                    yield return Unsafe.Add(ref MemoryMarshal.GetReference(segment.Span), i);
            }
        }
    }

    /// <summary>
    /// Obtains asynchronous enumerator over the sequence of elements.
    /// </summary>
    /// <param name="enumerable">The collection of elements.</param>
    /// <param name="token">The token that can be used by consumer to cancel the enumeration.</param>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    /// <returns>The asynchronous wrapper over synchronous enumerator.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="enumerable"/> is <see langword="null"/>.</exception>
    public static IAsyncEnumerator<T> GetAsyncEnumerator<T>(this IEnumerable<T> enumerable, CancellationToken token = default)
        => new AsyncEnumerable.Proxy<T>.Enumerator(enumerable ?? throw new ArgumentNullException(nameof(enumerable)), token);
}