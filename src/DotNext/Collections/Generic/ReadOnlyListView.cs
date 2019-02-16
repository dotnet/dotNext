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
    public readonly struct ReadOnlyListView<T>: IReadOnlyList<T>, IEquatable<ReadOnlyListView<T>>
    {
        private readonly IList<T> source;

        /// <summary>
        /// Initializes a new read-only view for the specified mutable list.
        /// </summary>
        /// <param name="list"></param>
        public ReadOnlyListView(IList<T> list)
            => source = list ?? throw new ArgumentNullException(nameof(list));

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
        /// <returns>Enumerator over items in the list.</returns>
        public IEnumerator<T> GetEnumerator() => source.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Determines whether the current view and the specified view points
        /// to the same list.
        /// </summary>
        /// <param name="other">Other view to compare.</param>
        /// <returns><see langword="true"/> if the current view points to the same list as other view; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ReadOnlyListView<T> other) => ReferenceEquals(source, other.source);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(source);

		public override bool Equals(object other)
			=> other is ReadOnlyCollectionView<T> view ? Equals(view) : Equals(source, other);

		public static bool operator ==(ReadOnlyListView<T> first, ReadOnlyListView<T> second)
			=> first.Equals(second);

		public static bool operator !=(ReadOnlyListView<T> first, ReadOnlyListView<T> second)
			=> !first.Equals(second);
    }

    public readonly struct ReadOnlyListView<I, O>: IReadOnlyList<O>, IEquatable<ReadOnlyListView<I, O>>
    {
        private readonly IReadOnlyList<I> source;
        private readonly Converter<I, O> mapper;

        public ReadOnlyListView(IReadOnlyList<I> list, Converter<I, O> mapper)
        {
            source = list ?? throw new ArgumentNullException(nameof(list));
			this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public O this[int index] => mapper(source[index]);

        public int Count => source.Count;

        public IEnumerator<O> GetEnumerator() => source.Select(mapper.AsFunc()).GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(ReadOnlyListView<I, O> other) => ReferenceEquals(source, other.source) && Equals(mapper, other.mapper);

        public override int GetHashCode() => source is null || mapper is null ? 0 : RuntimeHelpers.GetHashCode(source) ^ mapper.GetHashCode();

		public override bool Equals(object other)
			=> other is ReadOnlyListView<I, O> view ? Equals(view) : Equals(source, other);

		public static bool operator ==(ReadOnlyListView<I, O> first, ReadOnlyListView<I, O> second)
			=> first.Equals(second);

		public static bool operator !=(ReadOnlyListView<I, O> first, ReadOnlyListView<I, O> second)
			=> !first.Equals(second);
    }
}