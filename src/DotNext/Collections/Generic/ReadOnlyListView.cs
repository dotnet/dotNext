using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DotNext.Collections.Generic
{
    /// <summary>
    /// Represents read-only view of the mutable list.
    /// </summary>
    /// <typeparam name="T">Type of items in the list.</typeparam>
    /// <remarks>
    /// Any changes in the original list are visible from the read-only view.
    /// </remarks>
    public readonly struct ReadOnlyListView<T> : IReadOnlyList<T>, IEquatable<ReadOnlyListView<T>>
    {
        private readonly IList<T> source;

        /// <summary>
        /// Initializes a new read-only view for the specified mutable list.
        /// </summary>
        /// <param name="list">A list to wrap.</param>
        public ReadOnlyListView(IList<T> list)
            => source = list ?? throw new ArgumentNullException(nameof(list));

        /// <summary>
        /// Determines the index of a specific item in this list.
        /// </summary>
        /// <param name="item">The object to locate in this list.</param>
        /// <returns>The index of <paramref name="item"/> if found in the list; otherwise, -1.</returns>
        public int IndexOf(T item) => source.IndexOf(item);

        /// <summary>
        /// Determines whether this list contains a specific value.
        /// </summary>
        /// <param name="item">The object to locate in this list.</param>
        /// <returns><see langword="true"/> if <paramref name="item"/> is found in this list; otherwise, <see langword="false"/>.</returns>
        public bool Contains(T item) => source.Contains(item);

        /// <summary>
        /// Number of items in the list.
        /// </summary>
        public int Count => source.Count;

        /// <summary>
        /// Gets item at the specified position in the list.
        /// </summary>
        /// <param name="index">Index of the item.</param>
        /// <returns>List item at the specified position.</returns>
        public T this[int index] => source[index];

        /// <summary>
        /// Gets enumerator over items in the list.
        /// </summary>
        /// <returns>The enumerator over items in the list.</returns>
        public IEnumerator<T> GetEnumerator() => source.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Determines whether the current view and the specified view points
        /// to the same list.
        /// </summary>
        /// <param name="other">Other view to compare.</param>
        /// <returns><see langword="true"/> if the current view points to the same list as other view; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// Comparison between two wrapped lists is 
        /// performed using method <see cref="object.ReferenceEquals(object, object)"/>.
        /// </remarks>
        public bool Equals(ReadOnlyListView<T> other) => ReferenceEquals(source, other.source);

        /// <summary>
        /// Returns identity hash code of the wrapped list.
        /// </summary>
        /// <returns>Identity hash code of the wrapped list.</returns>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(source);

        /// <summary>
        /// Determines whether wrapped list and the specified object 
        /// are equal by reference.
        /// </summary>
        /// <param name="other">Other list to compare.</param>
        /// <returns><see langword="true"/>, if wrapped list and the specified object are equal by reference; otherwise, <see lngword="false"/>.</returns>
        public override bool Equals(object other)
            => other is ReadOnlyCollectionView<T> view ? Equals(view) : Equals(source, other);

        /// <summary>
        /// Determines whether two views point to the same list.
        /// </summary>
        /// <param name="first">The first view to compare.</param>
        /// <param name="second">The second view to compare.</param>
        /// <returns><see langword="true"/> if both views point to the same list; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ReadOnlyListView<T> first, ReadOnlyListView<T> second)
            => first.Equals(second);

        /// <summary>
        /// Determines whether two views point to the different lists.
        /// </summary>
        /// <param name="first">The first view to compare.</param>
        /// <param name="second">The second view to compare.</param>
        /// <returns><see langword="true"/> if both views point to the different lists; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ReadOnlyListView<T> first, ReadOnlyListView<T> second)
            => !first.Equals(second);
    }

    /// <summary>
    /// Represents lazily converted read-only list.
    /// </summary>
    /// <typeparam name="I">Type of items in the source list.</typeparam>
    /// <typeparam name="O">Type of items in the converted list.</typeparam>
    public readonly struct ReadOnlyListView<I, O> : IReadOnlyList<O>, IEquatable<ReadOnlyListView<I, O>>
    {
        private readonly IReadOnlyList<I> source;
        private readonly Converter<I, O> mapper;

        /// <summary>
        /// Initializes a new lazily converted view.
        /// </summary>
        /// <param name="list">Read-only list to convert.</param>
        /// <param name="mapper">List items converter.</param>
        public ReadOnlyListView(IReadOnlyList<I> list, Converter<I, O> mapper)
        {
            source = list ?? throw new ArgumentNullException(nameof(list));
            this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        /// <summary>
        /// Gets item at the specified position.
        /// </summary>
        /// <param name="index">Zero-based index of the item.</param>
        /// <returns>Converted item at the specified position.</returns>
        public O this[int index] => mapper(source[index]);

        /// <summary>
        /// Count of items in the list.
        /// </summary>
        public int Count => source.Count;

        /// <summary>
        /// Returns enumerator over converted items.
        /// </summary>
        /// <returns>The enumerator over converted items.</returns>
        public IEnumerator<O> GetEnumerator() => source.Select(mapper.AsFunc()).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Determines whether two converted lists are same.
        /// </summary>
        /// <param name="other">Other list to compare.</param>
        /// <returns><see langword="true"/> if this view wraps the same source list and contains the same converter as other view; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ReadOnlyListView<I, O> other) => ReferenceEquals(source, other.source) && Equals(mapper, other.mapper);

        /// <summary>
        /// Returns hash code for the this list.
        /// </summary>
        /// <returns>The hash code of this list.</returns>
        public override int GetHashCode() => source is null || mapper is null ? 0 : RuntimeHelpers.GetHashCode(source) ^ mapper.GetHashCode();

        /// <summary>
        /// Determines whether two converted lists are same.
        /// </summary>
        /// <param name="other">Other list to compare.</param>
        /// <returns><see langword="true"/> if this collection wraps the same source collection and contains the same converter as other collection; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
            => other is ReadOnlyListView<I, O> view ? Equals(view) : Equals(source, other);

        /// <summary>
        /// Determines whether two views are same.
        /// </summary>
        /// <param name="first">The first list view to compare.</param>
        /// <param name="second">The second list view to compare.</param>
        /// <returns><see langword="true"/> if the first view wraps the same source collection and contains the same converter as the second view; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ReadOnlyListView<I, O> first, ReadOnlyListView<I, O> second)
            => first.Equals(second);

        /// <summary>
        /// Determines whether two views are not same.
        /// </summary>
        /// <param name="first">The first list view to compare.</param>
        /// <param name="second">The second list view to compare.</param>
        /// <returns><see langword="true"/> if the first view wraps the different source collection and contains the different converter as the second view; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ReadOnlyListView<I, O> first, ReadOnlyListView<I, O> second)
            => !first.Equals(second);
    }
}