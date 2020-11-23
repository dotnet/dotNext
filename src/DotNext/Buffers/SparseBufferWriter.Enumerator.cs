using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    public partial class SparseBufferWriter<T>
    {
        /// <summary>
        /// Represents enumerator over memory segments.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public struct Enumerator : IEnumerator<ReadOnlyMemory<T>>
        {
            private MemoryChunk? current;
            private bool initialized;

            internal Enumerator(MemoryChunk? head)
            {
                current = head;
                initialized = false;
            }

            /// <inheritdoc />
            public readonly ReadOnlyMemory<T> Current
                => current is null ? ReadOnlyMemory<T>.Empty : current.WrittenMemory;

            /// <inheritdoc />
            readonly object IEnumerator.Current => Current;

            /// <inheritdoc />
            public bool MoveNext()
            {
                if (initialized)
                    current = current?.Next;
                else
                    initialized = true;

                return !(current is null);
            }

            /// <inheritdoc />
            readonly void IEnumerator.Reset() => throw new NotSupportedException();

            /// <inheritdoc />
            void IDisposable.Dispose() => this = default;
        }
    }
}