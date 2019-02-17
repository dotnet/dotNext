using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;

namespace DotNext.Collections.Generic
{
    /// <summary>
    /// Represents read-only view of the mutable collection.
    /// </summary>
    /// <typeparam name="T">Type of collection items.</typeparam>
    /// <remarks>
    /// Any changes in the original list are visible from the read-only view.
    /// </remarks>
    public readonly struct ReadOnlyCollectionView<T>: IReadOnlyCollection<T>, IEquatable<ReadOnlyCollectionView<T>>
    {
        private readonly ICollection<T> source;

        /// <summary>
        /// Initializes a new read-only view for the mutable collection.
        /// </summary>
        /// <param name="collection">A collection to wrap.</param>
        public ReadOnlyCollectionView(ICollection<T> collection)
            => source = collection ?? throw new ArgumentNullException(nameof(collection));

        /// <summary>
        /// Count of items in the collection.
        /// </summary>
        public int Count => source.Count;
        
        /// <summary>
        /// Gets enumerator over items in the collection.
        /// </summary>
        /// <returns>The enumerator over items in the collection.</returns>
        public IEnumerator<T> GetEnumerator() => source.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Determines whether the current view and the specified view points
        /// to the same collection.
        /// </summary>
        /// <param name="other">Other view to compare.</param>
        /// <returns><see langword="true"/> if the current view points to the same collection as other view; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// Comparison between two wrapped collections is 
        /// performed using method <see cref="object.ReferenceEquals(object, object)"/>.
        /// </remarks>
        public bool Equals(ReadOnlyCollectionView<T> other) => ReferenceEquals(source, other.source);

        /// <summary>
        /// Returns identity hash code of the wrapped collection.
        /// </summary>
        /// <returns>Identity hash code of the wrapped collection.</returns>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(source);

        /// <summary>
        /// Determines whether wrapped collection and the specified object 
        /// are equal by reference.
        /// </summary>
        /// <param name="other">Other collection to compare.</param>
        /// <returns><see langword="true"/>, if wrapped collection and the specified object are equal by reference; otherwise, <see lngword="false"/>.</returns>
		public override bool Equals(object other)
            => other is ReadOnlyCollectionView<T> view ? Equals(view) : Equals(source, other);
        
        /// <summary>
        /// Determines whether two views point to the same collection.
        /// </summary>
        /// <param name="first">The first view to compare.</param>
        /// <param name="second">The second view to compare.</param>
        /// <returns><see langword="true"/> if both views point to the same collection; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(ReadOnlyCollectionView<T> first, ReadOnlyCollectionView<T> second)
			=> first.Equals(second);

        /// <summary>
        /// Determines whether two views point to the different collections.
        /// </summary>
        /// <param name="first">The first view to compare.</param>
        /// <param name="second">The second view to compare.</param>
        /// <returns><see langword="true"/> if both views point to the different collections; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ReadOnlyCollectionView<T> first, ReadOnlyCollectionView<T> second)
			=> !first.Equals(second);
    }

    /// <summary>
    /// Represents lazily converted read-only collection.
    /// </summary>
    /// <typeparam name="I">Type of items in the source collection.</typeparam>
    /// <typeparam name="O">Type of items in the converted collection.</typeparam>
    public readonly struct ReadOnlyCollectionView<I, O>: IReadOnlyCollection<O>, IEquatable<ReadOnlyCollectionView<I, O>>
    {
        private readonly IReadOnlyCollection<I> source;
        private readonly Converter<I, O> mapper;

        /// <summary>
        /// Initializes a new lazily converted view.
        /// </summary>
        /// <param name="collection">Read-only collection to convert.</param>
        /// <param name="mapper">Collection items converter.</param>
        public ReadOnlyCollectionView(IReadOnlyCollection<I> collection, Converter<I, O> mapper)
        {
            source = collection ?? throw new ArgumentNullException(nameof(collection));
			this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        /// <summary>
        /// Count of items in the collection.
        /// </summary>
        public int Count => source.Count;

        /// <summary>
        /// Returns enumerator over converted items.
        /// </summary>
        /// <returns>The enumerator over converted items.</returns>
        public IEnumerator<O> GetEnumerator() => source.Select(mapper.AsFunc()).GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Determines whether two converted collections are same.
        /// </summary>
        /// <param name="other">Other collection to compare.</param>
        /// <returns><see langword="true"/> if this view wraps the same source collection and contains the same converter as other view; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ReadOnlyCollectionView<I, O> other) => ReferenceEquals(source, other.source) && Equals(mapper, other.mapper);

        /// <summary>
        /// Returns hash code for the this view.
        /// </summary>
        /// <returns>The hash code of this view.</returns>
        public override int GetHashCode() => source is null || mapper is null ? 0 : RuntimeHelpers.GetHashCode(source) ^ mapper.GetHashCode();

        /// <summary>
        /// Determines whether two converted collections are same.
        /// </summary>
        /// <param name="other">Other collection to compare.</param>
        /// <returns><see langword="true"/> if this view wraps the same source collection and contains the same converter as other view; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
			=> other is ReadOnlyCollectionView<I, O> view ? Equals(view) : Equals(source, other);

        /// <summary>
        /// Determines whether two collections are same.
        /// </summary>
        /// <param name="first">The first collection to compare.</param>
        /// <param name="second">The second collection to compare.</param>
        /// <returns><see langword="true"/> if the first view wraps the same source collection and contains the same converter as the second view; otherwise, <see langword="false"/>.</returns>
		public static bool operator ==(ReadOnlyCollectionView<I, O> first, ReadOnlyCollectionView<I, O> second)
			=> first.Equals(second);

        /// <summary>
        /// Determines whether two collections are not same.
        /// </summary>
        /// <param name="first">The first collection to compare.</param>
        /// <param name="second">The second collection to compare.</param>
        /// <returns><see langword="true"/> if the first view wraps the diferent source collection and contains the different converter as the second view; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ReadOnlyCollectionView<I, O> first, ReadOnlyCollectionView<I, O> second)
			=> !first.Equals(second);
    }
}