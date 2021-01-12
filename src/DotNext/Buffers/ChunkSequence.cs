using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;
using static System.Linq.Enumerable;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents sequence of memory chunks.
    /// </summary>
    /// <typeparam name="T">The type of elements stored in contiguous memory.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    [Obsolete("Use ReadOnlySequence<T> data type instead")]
    public readonly struct ChunkSequence<T> : IEnumerable<ReadOnlyMemory<T>>
    {
        /// <summary>
        /// Represents enumerator of memory chunks.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        [Obsolete("Use ReadOnlySequence<T> data type instead")]
        public struct Enumerator : IEnumerator<ReadOnlyMemory<T>>
        {
            private readonly ReadOnlyMemory<T> source;
            private readonly int chunkSize;
            private int startIndex, length;

            internal Enumerator(ReadOnlyMemory<T> source, int chunkSize)
            {
                this.source = source;
                this.chunkSize = chunkSize;
                startIndex = length = -1;
            }

            /// <summary>
            /// Gets currently iterating memory segment.
            /// </summary>
            public readonly ReadOnlyMemory<T> Current => source.Slice(startIndex, length);

            /// <inheritdoc/>
            readonly object IEnumerator.Current => Current;

            /// <inheritdoc/>
            void IDisposable.Dispose() => this = default;

            /// <summary>
            /// Moves to the next memory segment.
            /// </summary>
            /// <returns><see langword="true"/> if the next segment exists; otherwise, <see langword="langword"/>.</returns>
            public bool MoveNext()
            {
                if (startIndex == -1)
                {
                    startIndex = 0;
                    length = Math.Min(chunkSize, source.Length);
                }
                else
                {
                    startIndex += chunkSize;
                    length = Math.Min(source.Length - startIndex, chunkSize);
                }

                return startIndex < source.Length;
            }

            /// <summary>
            /// Resets enumerator to its initial state.
            /// </summary>
            public void Reset() => startIndex = -1;
        }

        private readonly ReadOnlyMemory<T> memory;
        private readonly int chunkSize;

        /// <summary>
        /// Initializes a split view over contiguous memory.
        /// </summary>
        /// <param name="memory">Contiguous memory.</param>
        /// <param name="chunkSize">The number of elements in single chunk.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="chunkSize"/> is less than 1.</exception>
        public ChunkSequence(ReadOnlyMemory<T> memory, int chunkSize)
        {
            if (chunkSize < 1)
                throw new ArgumentOutOfRangeException(nameof(chunkSize));
            this.chunkSize = chunkSize;
            this.memory = memory;
        }

        /// <summary>
        /// Gets enumerator over memory chunks.
        /// </summary>
        /// <returns>The enumerator over memory chunks.</returns>
        public Enumerator GetEnumerator() => new Enumerator(memory, chunkSize);

        /// <inheritdoc/>
        IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator()
            => GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        /// <summary>
        /// Converts this instance into <see cref="ReadOnlySequence{T}"/>.
        /// </summary>
        /// <returns>The sequence of memory chunks.</returns>
        public ReadOnlySequence<T> ToReadOnlySequence()
        {
            if (memory.IsEmpty)
                return ReadOnlySequence<T>.Empty;
            if (memory.Length < chunkSize)
                return new ReadOnlySequence<T>(memory);

            Chunk<T>? head = null, tail = null;
            foreach (var segment in this)
                Chunk<T>.AddChunk(segment, ref head, ref tail);

            Assert(head != null);
            Assert(tail != null);
            return Chunk<T>.CreateSequence(head, tail);
        }

        /// <summary>
        /// Converts this instance into <see cref="ReadOnlySequence{T}"/>.
        /// </summary>
        /// <param name="sequence">The sequence to be converted.</param>
        /// <returns>The sequence of memory chunks.</returns>
        public static explicit operator ReadOnlySequence<T>(ChunkSequence<T> sequence) => sequence.ToReadOnlySequence();
    }

    /// <summary>
    /// Represents extension methods for <see cref="ChunkSequence{T}"/>.
    /// </summary>
    [Obsolete("Use BufferHelpers class instead", true)]
    public static class ChunkSequence
    {
        /// <summary>
        /// Converts the sequence of memory blocks to <see cref="ReadOnlySequence{T}"/> data type.
        /// </summary>
        /// <param name="chunks">The sequence of memory blocks.</param>
        /// <typeparam name="T">The type of elements in the memory blocks.</typeparam>
        /// <returns>The constructed <see cref="ReadOnlySequence{T}"/> instance containing memory blocks.</returns>
        public static ReadOnlySequence<T> ToReadOnlySequence<T>(IEnumerable<Memory<T>> chunks)
        {
            return ToReadOnlySequence(chunks.Select(Cast));

            static ReadOnlyMemory<T> Cast(Memory<T> memory) => memory;
        }

        /// <summary>
        /// Converts the sequence of memory blocks to <see cref="ReadOnlySequence{T}"/> data type.
        /// </summary>
        /// <param name="chunks">The sequence of memory blocks.</param>
        /// <typeparam name="T">The type of elements in the memory blocks.</typeparam>
        /// <returns>The constructed <see cref="ReadOnlySequence{T}"/> instance containing memory blocks.</returns>
        public static ReadOnlySequence<T> ToReadOnlySequence<T>(IEnumerable<ReadOnlyMemory<T>> chunks)
            => BufferHelpers.ToReadOnlySequence(chunks);

        /// <summary>
        /// Converts two memory blocks to <see cref="ReadOnlySequence{T}"/> data type.
        /// </summary>
        /// <param name="first">The first memory block.</param>
        /// <param name="second">The second memory block.</param>
        /// <typeparam name="T">The type of elements in the memory blocks.</typeparam>
        /// <returns>The constructed <see cref="ReadOnlySequence{T}"/> instance containing memory blocks.</returns>
        public static ReadOnlySequence<T> Concat<T>(Memory<T> first, Memory<T> second)
            => Concat((ReadOnlyMemory<T>)first, (ReadOnlyMemory<T>)second);

        /// <summary>
        /// Converts two memory blocks to <see cref="ReadOnlySequence{T}"/> data type.
        /// </summary>
        /// <param name="first">The first memory block.</param>
        /// <param name="second">The second memory block.</param>
        /// <typeparam name="T">The type of elements in the memory blocks.</typeparam>
        /// <returns>The constructed <see cref="ReadOnlySequence{T}"/> instance containing memory blocks.</returns>
        public static ReadOnlySequence<T> Concat<T>(ReadOnlyMemory<T> first, ReadOnlyMemory<T> second)
            => BufferHelpers.Concat(first, second);

        /// <summary>
        /// Copies chunks of bytes into the stream.
        /// </summary>
        /// <param name="sequence">The sequence of chunks.</param>
        /// <param name="output">The output stream.</param>
        /// <param name="token">The token that can be used to cancel execution of this method.</param>
        /// <returns>The task representing asynchronouos execution of this method.</returns>
        public static async ValueTask CopyToAsync(ChunkSequence<byte> sequence, Stream output, CancellationToken token = default)
        {
            foreach (var segment in sequence)
            {
                token.ThrowIfCancellationRequested();
                await output.WriteAsync(segment, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Copies chunks of bytes into the text writer.
        /// </summary>
        /// <param name="sequence">The sequence of chunks.</param>
        /// <param name="output">The text writer.</param>
        /// <param name="token">The token that can be used to cancel execution of this method.</param>
        /// <returns>The task representing asynchronouos execution of this method.</returns>
        public static async ValueTask CopyToAsync(ChunkSequence<char> sequence, TextWriter output, CancellationToken token = default)
        {
            foreach (var segment in sequence)
            {
                token.ThrowIfCancellationRequested();
                await output.WriteAsync(segment, token).ConfigureAwait(false);
            }
        }
    }
}
