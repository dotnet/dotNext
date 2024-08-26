using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Specialized;

using Generic;

/// <summary>
/// Represents a list with one element.
/// </summary>
/// <typeparam name="T">The type of the element in the list.</typeparam>
[StructLayout(LayoutKind.Auto)]
public struct SingletonList<T> : IReadOnlyList<T>, IList<T>, ITuple, IReadOnlySet<T>
{
    /// <summary>
    /// Represents an enumerator over the collection containing a single element.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator : IEnumerator<Enumerator, T>
    {
        private const byte NotRequestedState = 1;
        private const byte RequestedState = 2;

        private byte state;

        internal Enumerator(T item)
        {
            Current = item;
            state = NotRequestedState;
        }

        /// <summary>
        /// Gets the current element.
        /// </summary>
        public T Current { get; }

        /// <summary>
        /// Advances the position of the enumerator to the next element.
        /// </summary>
        /// <returns><see langword="true"/> if the enumerator advanced successfully; otherwise, <see langword="false"/>.</returns>
        public bool MoveNext()
        {
            if (state is NotRequestedState)
            {
                state = RequestedState;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resets state of this enumerator.
        /// </summary>
        public void Reset()
        {
            if (state is RequestedState)
                state = NotRequestedState;
        }
    }

    /// <summary>
    /// The item of the list.
    /// </summary>
    required public T Item;

    /// <summary>
    /// Gets or sets the item in this list.
    /// </summary>
    /// <param name="index">The index of the item. Must be zero.</param>
    /// <returns>The item.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is not equal to zero.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [IndexerName("Element")]
    public T this[int index]
    {
        readonly get
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(index, 0);

            return Item;
        }

        set
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(index, 0);

            Item = value;
        }
    }

    /// <inheritdoc />
    readonly object? ITuple.this[int index] => this[index];

    /// <inheritdoc />
    readonly int ITuple.Length => 1;

    /// <inheritdoc />
    readonly bool ICollection<T>.IsReadOnly => true;

    /// <inheritdoc />
    readonly int IReadOnlyCollection<T>.Count => 1;

    /// <inheritdoc />
    readonly int ICollection<T>.Count => 1;

    /// <inheritdoc />
    readonly void ICollection<T>.Clear() => throw new NotSupportedException();

    /// <inheritdoc />
    readonly bool ICollection<T>.Contains(T item) => EqualityComparer<T>.Default.Equals(Item, item);

    /// <inheritdoc />
    readonly void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        => array.AsSpan(arrayIndex)[0] = Item;

    /// <inheritdoc />
    readonly bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

    /// <inheritdoc />
    readonly void ICollection<T>.Add(T item) => throw new NotSupportedException();

    /// <inheritdoc />
    readonly int IList<T>.IndexOf(T item) => Unsafe.BitCast<bool, byte>(EqualityComparer<T>.Default.Equals(Item, item)) - 1;

    /// <inheritdoc />
    readonly void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

    /// <inheritdoc />
    readonly void IList<T>.RemoveAt(int index) => throw new NotSupportedException();

    /// <summary>
    /// Gets enumerator for the single element in the list.
    /// </summary>
    /// <returns>The enumerator over single element.</returns>
    public readonly Enumerator GetEnumerator() => new(Item);

    /// <inheritdoc />
    readonly IEnumerator<T> IEnumerable<T>.GetEnumerator()
        => GetEnumerator().ToClassicEnumerator<Enumerator, T>();

    /// <inheritdoc />
    readonly IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator().ToClassicEnumerator<Enumerator, T>();

    /// <summary>
    /// Converts a value to the read-only list.
    /// </summary>
    /// <param name="item">The item in the list.</param>
    /// <returns>The collection containing the list.</returns>
    public static implicit operator SingletonList<T>(T item) => new() { Item = item };

    /// <inheritdoc />
    readonly bool IReadOnlySet<T>.Contains(T item) => EqualityComparer<T>.Default.Equals(Item, item);

    /// <inheritdoc/>
    readonly bool IReadOnlySet<T>.Overlaps(IEnumerable<T> other)
        => other.Contains(Item);

    /// <inheritdoc/>
    readonly bool IReadOnlySet<T>.SetEquals(IEnumerable<T> other)
    {
        if (other.TryGetNonEnumeratedCount(out var count))
            return count is 1 && other.Contains(Item);

        using var enumerator = other.GetEnumerator();
        return enumerator.MoveNext()
            && EqualityComparer<T>.Default.Equals(Item, enumerator.Current)
            && !enumerator.MoveNext();
    }

    /// <inheritdoc/>
    readonly bool IReadOnlySet<T>.IsSubsetOf(IEnumerable<T> other)
        => other.Contains(Item);

    /// <inheritdoc/>
    readonly bool IReadOnlySet<T>.IsProperSubsetOf(IEnumerable<T> other)
    {
        if (other.TryGetNonEnumeratedCount(out var count))
            return count > 1 && other.Contains(Item);

        var matched = false;
        foreach (var candidate in other)
        {
            if (matched)
                return true;

            if (EqualityComparer<T>.Default.Equals(Item, candidate))
                matched = true;
        }

        return false;
    }

    /// <inheritdoc/>
    readonly bool IReadOnlySet<T>.IsProperSupersetOf(IEnumerable<T> other)
    {
        if (other.TryGetNonEnumeratedCount(out var count))
            return count is 0;

        using var enumerator = other.GetEnumerator();
        return enumerator.MoveNext() is false;
    }

    /// <inheritdoc/>
    readonly bool IReadOnlySet<T>.IsSupersetOf(IEnumerable<T> other)
    {
        if (other.TryGetNonEnumeratedCount(out var count))
            return count is 0 || (count is 1 && other.Contains(Item));

        using var enumerator = other.GetEnumerator();
        return !enumerator.MoveNext()
            || EqualityComparer<T>.Default.Equals(Item, enumerator.Current)
            && !enumerator.MoveNext();
    }
}