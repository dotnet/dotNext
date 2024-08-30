using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic;

public static partial class Collection
{
    /// <summary>
    /// Represents a wrapped for method <see cref="IProducerConsumerCollection{T}.TryTake(out T)"/>
    /// in the form of enumerable collection.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ConsumingEnumerable<T> : IEnumerable<T>
    {
        /// <summary>
        /// Represents consumer enumerator.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public struct Enumerator : IEnumerator<Enumerator, T>
        {
            private readonly IProducerConsumerCollection<T>? collection;

            private T? current;

            internal Enumerator(IProducerConsumerCollection<T>? collection)
            {
                this.collection = collection;
                current = default!;
            }

            /// <summary>
            /// Gets consumed item from the underlying collection.
            /// </summary>
            public readonly T Current => current!;

            /// <summary>
            /// Consumes the item from the underlying collection.
            /// </summary>
            /// <returns><see langword="true"/> if the item has been consumed successfully; <see langword="false"/> if underlying collection is empty.</returns>
            public bool MoveNext() => collection?.TryTake(out current) ?? false;
        }

        private readonly IProducerConsumerCollection<T>? collection;

        internal ConsumingEnumerable(IProducerConsumerCollection<T> collection)
            => this.collection = collection;

        /// <summary>
        /// Gets consumer enumerator.
        /// </summary>
        /// <returns>The enumerator wrapping method <see cref="IProducerConsumerCollection{T}.TryTake(out T)"/>.</returns>
        public Enumerator GetEnumerator() => new(collection);

        /// <inheritdoc />
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
            => GetEnumerator().ToClassicEnumerator<Enumerator, T>();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator().ToClassicEnumerator<Enumerator, T>();
    }

    /// <summary>
    /// Gets consumer of thread-safe concurrent collection.
    /// </summary>
    /// <param name="collection">The concurrent collection.</param>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <returns>The consumer in the form of enumerable collection.</returns>
    public static ConsumingEnumerable<T> GetConsumer<T>(this IProducerConsumerCollection<T> collection)
        => new(collection ?? throw new ArgumentNullException(nameof(collection)));
}