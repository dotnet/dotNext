using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic
{
    /// <summary>
    /// Represents lazily converted read-only list.
    /// </summary>
    /// <typeparam name="I">Type of items in the source list.</typeparam>
    /// <typeparam name="O">Type of items in the converted list.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ReadOnlyListView<I, O> : IReadOnlyList<O>, IEquatable<ReadOnlyListView<I, O>>
    {
        private readonly IReadOnlyList<I> source;
        private readonly ValueFunc<I, O> mapper;

        /// <summary>
        /// Initializes a new lazily converted view.
        /// </summary>
        /// <param name="list">Read-only list to convert.</param>
        /// <param name="mapper">List items converter.</param>
        public ReadOnlyListView(IReadOnlyList<I> list, in ValueFunc<I, O> mapper)
        {
            source = list ?? throw new ArgumentNullException(nameof(list));
            this.mapper = mapper;
        }

        /// <summary>
        /// Gets item at the specified position.
        /// </summary>
        /// <param name="index">Zero-based index of the item.</param>
        /// <returns>Converted item at the specified position.</returns>
        public O this[int index] => mapper.Invoke(source[index]);

        /// <summary>
        /// Count of items in the list.
        /// </summary>
        public int Count => source.Count;

        /// <summary>
        /// Returns enumerator over converted items.
        /// </summary>
        /// <returns>The enumerator over converted items.</returns>
        public IEnumerator<O> GetEnumerator() => source.Select(mapper.ToDelegate()).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Determines whether two converted lists are same.
        /// </summary>
        /// <param name="other">Other list to compare.</param>
        /// <returns><see langword="true"/> if this view wraps the same source list and contains the same converter as other view; otherwise, <see langword="false"/>.</returns>
        public bool Equals(ReadOnlyListView<I, O> other) => ReferenceEquals(source, other.source) && mapper == other.mapper;

        /// <summary>
        /// Returns hash code for the this list.
        /// </summary>
        /// <returns>The hash code of this list.</returns>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(source) ^ mapper.GetHashCode();

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