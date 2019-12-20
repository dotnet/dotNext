using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic
{
    /// <summary>
    /// Represents lazily converted read-only collection.
    /// </summary>
    /// <typeparam name="I">Type of items in the source collection.</typeparam>
    /// <typeparam name="O">Type of items in the converted collection.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ReadOnlyCollectionView<I, O> : IReadOnlyCollection<O>, IEquatable<ReadOnlyCollectionView<I, O>>
    {
        private readonly IReadOnlyCollection<I> source;
        private readonly ValueFunc<I, O> mapper;

        /// <summary>
        /// Initializes a new lazily converted view.
        /// </summary>
        /// <param name="collection">Read-only collection to convert.</param>
        /// <param name="mapper">Collection items converter.</param>
        public ReadOnlyCollectionView(IReadOnlyCollection<I> collection, in ValueFunc<I, O> mapper)
        {
            source = collection ?? throw new ArgumentNullException(nameof(collection));
            this.mapper = mapper;
        }

        /// <summary>
        /// Count of items in the collection.
        /// </summary>
        public int Count => source.Count;

        /// <summary>
        /// Returns enumerator over converted items.
        /// </summary>
        /// <returns>The enumerator over converted items.</returns>
        public IEnumerator<O> GetEnumerator() => source.Select(mapper.ToDelegate()).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Determines whether two converted collections are same.
        /// </summary>
        /// <param name="other">Other collection to compare.</param>
        /// <returns><see langword="true"/> if this view wraps the same source collection and contains the same converter as other view; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ReadOnlyCollectionView<I, O> other) => ReferenceEquals(source, other.source) && mapper == other.mapper;

        /// <summary>
        /// Returns hash code for the this view.
        /// </summary>
        /// <returns>The hash code of this view.</returns>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(source) ^ mapper.GetHashCode();

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
        /// <returns><see langword="true"/> if the first view wraps the different source collection and contains the different converter as the second view; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(ReadOnlyCollectionView<I, O> first, ReadOnlyCollectionView<I, O> second)
            => !first.Equals(second);
    }
}