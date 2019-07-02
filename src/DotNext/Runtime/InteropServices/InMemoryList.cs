using System;
using System.Collections;
using System.Collections.Generic;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Represents <see cref="ReadOnlyMemory{T}"/> as read-only list.
    /// </summary>
    /// <typeparam name="T">The type of the items in the list.</typeparam>
    public readonly struct InMemoryList<T> : IReadOnlyList<T>
    {
        /// <summary>
        /// Represents enumerator over memory elements.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private int position;
            private readonly ReadOnlyMemory<T> memory;

            internal Enumerator(ReadOnlyMemory<T> memory)
            {
                this.memory = memory;
                position = -1;
            }

            /// <summary>
            /// Advances the enumerator to the next element of the memory.
            /// </summary>
            /// <returns><see langword="true" /> if the enumerator was successfully advanced to the next element; <see langword="false" /> if the enumerator has passed the end of the memory block.</returns>
            public bool MoveNext()
            {
                position += 1;
                return position < memory.Length;
            }

            /// <summary>
            /// Sets the enumerator to its initial position, which is before the first element in the memory.
            /// </summary>
            public void Reset() => position = -1;

            /// <summary>
            /// Gets the element in the memory at the current position of the enumerator.
            /// </summary>
            public T Current => memory.Span[position];

            object IEnumerator.Current => Current;

            /// <summary>
            /// Releases all resources associated with this enumerator.
            /// </summary>
            public void Dispose() => this = default;
        }

        /// <summary>
        /// Wraps <see cref="ReadOnlyMemory{T}"/> into read-only list.
        /// </summary>
        /// <param name="memory">The memory to be wrapped.</param>
        public InMemoryList(ReadOnlyMemory<T> memory) => Memory = memory;

        /// <summary>
        /// Gets a memory wrapped into this list.
        /// </summary>
        public ReadOnlyMemory<T> Memory { get; }

        /// <summary>
        /// Returns enumerator over elements in the memory.
        /// </summary>
        /// <returns>An enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(Memory);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Gets count of elements in the memory.
        /// </summary>
        public int Count => Memory.Length;

        /// <summary>
        /// Obtains element at the specified position in the memory.
        /// </summary>
        /// <param name="index">The index of the requested element.</param>
        /// <returns>The element at the specified position.</returns>
        public T this[int index] => Memory.Span[index];
    }
}
