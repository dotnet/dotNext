#if !NETSTANDARD2_1
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent
{
    public partial class RingBuffer<T> : IEnumerable<T>
    {
        /// <summary>
        /// Represents consuming enumerator.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public struct ConsumingEnumerator : IEnumerator<T>
        {
            private RingBuffer<T>? buffer;
            private T current;

            internal ConsumingEnumerator(RingBuffer<T> buffer)
            {
                this.buffer = buffer;
                current = default!;
            }

            /// <summary>
            /// Attempts to read the next value from the buffer.
            /// </summary>
            /// <returns><see langword="true"/> if there is available value in the buffer; otherwise, <see langword="false"/>.</returns>
            public bool MoveNext()
                => buffer?.TryRemove(out current!) ?? false;

            /// <summary>
            /// The consumed value.
            /// </summary>
            public T Current => current;

            /// <inheritdoc/>
            void IEnumerator.Reset() => throw new NotSupportedException();

            /// <inheritdoc/>
            object? IEnumerator.Current => Current;

            /// <inheritdoc/>
            void IDisposable.Dispose() => this = default;
        }

        /// <summary>
        /// Gets consuming enumerator.
        /// </summary>
        /// <returns>The enumerator that can be used to consume values in stream-like manner.</returns>
        public ConsumingEnumerator GetEnumerator() => new(this);

        /// <inheritdoc/>
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
#endif