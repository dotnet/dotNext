using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents sequence of memory chunks.
    /// </summary>
    /// <typeparam name="T">The type of elements stored in contiguous memory.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ChunkSequence<T> : IEnumerable<ReadOnlyMemory<T>>
    {
        /// <summary>
        /// Represents enumerator of memory chunks.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public struct Enumerator : IEnumerator<ReadOnlyMemory<T>>
        {
            private readonly ReadOnlyMemory<T> source;
            private int startIndex, length;
            private readonly int chunkSize;

            internal Enumerator(ReadOnlyMemory<T> source, int chunkSize)
            {
                this.source = source;
                this.chunkSize = chunkSize;
                startIndex = length = -1;
            }

            /// <summary>
            /// Gets currently iterating memory segment.
            /// </summary>
            public ReadOnlyMemory<T> Current => source.Slice(startIndex, length);

            object IEnumerator.Current => Current;

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

        IEnumerator<ReadOnlyMemory<T>> IEnumerable<ReadOnlyMemory<T>>.GetEnumerator()
            => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        /// <summary>
        /// Converts this instance into <see cref="ReadOnlySequence{T}"/>.
        /// </summary>
        /// <returns>The sequence of memory chunks.</returns>
        public ReadOnlySequence<T> ToReadOnlySequence()
        {
            if (memory.IsEmpty)
                return default;
            if (memory.Length < chunkSize)
                return new ReadOnlySequence<T>(memory);
            Chunk<T> first = null, last = null;
            foreach (var segment in this)
                Chunk<T>.AddChunk(segment, ref first, ref last);
            Assert(first != null);
            Assert(last != null);
            return new ReadOnlySequence<T>(first, 0, last, last.Memory.Length);
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
    public static class ChunkSequence
    {
        /// <summary>
        /// Copies chunks of bytes into the stream.
        /// </summary>
        /// <param name="sequence">The sequence of chunks.</param>
        /// <param name="output">The output stream.</param>
        /// <param name="token">The token that can be used to cancel execution of this method.</param>
        /// <returns>The task representing asynchronouos execution of this method.</returns>
        public static async Task CopyToAsync(this ChunkSequence<byte> sequence, Stream output, CancellationToken token = default)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            foreach (var segment in sequence)
                using (var array = new ArrayRental<byte>(segment.Length))
                {
                    token.ThrowIfCancellationRequested();
                    segment.CopyTo(array.Memory);
                    await output.WriteAsync((byte[])array, 0, segment.Length, token).ConfigureAwait(false);
                }
        }

        /// <summary>
        /// Copies chunks of bytes into the text writer.
        /// </summary>
        /// <param name="sequence">The sequence of chunks.</param>
        /// <param name="output">The text writer.</param>
        /// <param name="token">The token that can be used to cancel execution of this method.</param>
        /// <returns>The task representing asynchronouos execution of this method.</returns>
        public static async Task CopyToAsync(this ChunkSequence<char> sequence, TextWriter output, CancellationToken token = default)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            foreach (var segment in sequence)
                using (var array = new ArrayRental<char>(segment.Length))
                {
                    token.ThrowIfCancellationRequested();
                    segment.CopyTo(array.Memory);
                    await output.WriteAsync((char[])array, 0, segment.Length).ConfigureAwait(false);
                }
        }
    }
}
