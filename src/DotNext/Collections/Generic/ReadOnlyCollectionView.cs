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
    /// <typeparam name="TInput">Type of items in the source collection.</typeparam>
    /// <typeparam name="TOutput">Type of items in the converted collection.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ReadOnlyCollectionView<TInput, TOutput> : IReadOnlyCollection<TOutput>, IEquatable<ReadOnlyCollectionView<TInput, TOutput>>
    {
        private readonly IReadOnlyCollection<TInput>? source;
        private readonly ValueFunc<TInput, TOutput> mapper;

        /// <summary>
        /// Initializes a new lazily converted view.
        /// </summary>
        /// <param name="collection">Read-only collection to convert.</param>
        /// <param name="mapper">Collection items converter.</param>
        public ReadOnlyCollectionView(IReadOnlyCollection<TInput> collection, in ValueFunc<TInput, TOutput> mapper)
        {
            source = collection ?? throw new ArgumentNullException(nameof(collection));
            this.mapper = mapper;
        }

        /// <summary>
        /// Count of items in the collection.
        /// </summary>
        public int Count => source?.Count ?? 0;

        /// <summary>
        /// Returns enumerator over converted items.
        /// </summary>
        /// <returns>The enumerator over converted items.</returns>
        public IEnumerator<TOutput> GetEnumerator()
        {
            var selector = mapper.ToDelegate();
            if (source is null || selector is null)
                return Enumerable.Empty<TOutput>().GetEnumerator();

            return source.Select(selector).GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private bool Equals(in ReadOnlyCollectionView<TInput, TOutput> other)
            => ReferenceEquals(source, other.source) && mapper == other.mapper;

        /// <summary>
        /// Determines whether two converted collections are same.
        /// </summary>
        /// <param name="other">Other collection to compare.</param>
        /// <returns><see langword="true"/> if this view wraps the same source collection and contains the same converter as other view; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ReadOnlyCollectionView<TInput, TOutput> other) => Equals(in other);

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
        public override bool Equals(object? other)
            => other is ReadOnlyCollectionView<TInput, TOutput> view ? Equals(in view) : Equals(source, other);

        /// <summary>
        /// Determines whether two collections are same.
        /// </summary>
        /// <param name="first">The first collection to compare.</param>
        /// <param name="second">The second collection to compare.</param>
        /// <returns><see langword="true"/> if the first view wraps the same source collection and contains the same converter as the second view; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in ReadOnlyCollectionView<TInput, TOutput> first, in ReadOnlyCollectionView<TInput, TOutput> second)
            => first.Equals(in second);

        /// <summary>
        /// Determines whether two collections are not same.
        /// </summary>
        /// <param name="first">The first collection to compare.</param>
        /// <param name="second">The second collection to compare.</param>
        /// <returns><see langword="true"/> if the first view wraps the different source collection and contains the different converter as the second view; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in ReadOnlyCollectionView<TInput, TOutput> first, in ReadOnlyCollectionView<TInput, TOutput> second)
            => !first.Equals(in second);
    }
}