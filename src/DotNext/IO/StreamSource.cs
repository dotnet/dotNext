using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.IO
{
    using Buffers;

    /// <summary>
    /// Represents conversion of various buffer types to stream.
    /// </summary>
    public static class StreamSource
    {
        /// <summary>
        /// Converts read-only sequence of bytes to a read-only stream.
        /// </summary>
        /// <param name="sequence">The sequence of bytes.</param>
        /// <returns>The stream over sequence of bytes.</returns>
        public static Stream AsStream(this ReadOnlySequence<byte> sequence)
            => new ReadOnlyMemoryStream(sequence);

        /// <summary>
        /// Converts read-only memory to a read-only stream.
        /// </summary>
        /// <param name="memory">The read-only memory.</param>
        /// <returns>The stream over memory of bytes.</returns>
        public static Stream AsStream(this ReadOnlyMemory<byte> memory)
            => AsStream(new ReadOnlySequence<byte>(memory));

        private static MemoryStream CreateStream(byte[] buffer, int length)
            => new MemoryStream(buffer, 0, length, false, false);

        /// <summary>
        /// Gets written content as a read-only stream.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <returns>The stream representing written bytes.</returns>
        public static Stream GetWrittenBytesAsStream(this PooledArrayBufferWriter<byte> writer)
            => writer.WrapBuffer(new ValueFunc<byte[], int, MemoryStream>(CreateStream));

        /// <summary>
        /// Returns the writable stream associated with the buffer writer.
        /// </summary>
        /// <typeparam name="TWriter">The type of the writer.</typeparam>
        /// <param name="writer">The writer to be wrapped by the stream.</param>
        /// <param name="flush">Optional synchronous flush action.</param>
        /// <param name="flushAsync">Optiona asynchronous flush action.</param>
        /// <returns>The stream wrapping buffer writer.</returns>
        public static Stream AsStream<TWriter>(this TWriter writer, Action<TWriter>? flush = null, Func<TWriter, CancellationToken, Task>? flushAsync = null)
            where TWriter : class, IBufferWriter<byte>
        {
            if (writer is IFlushable)
            {
                flush ??= Flush;
                flushAsync ??= FlushAsync;
            }

            return new BufferWriterStream<TWriter>(writer, flush, flushAsync);

            static void Flush(TWriter writer) => Unsafe.As<IFlushable>(writer).Flush();

            static Task FlushAsync(TWriter writer, CancellationToken token) => Unsafe.As<IFlushable>(writer).FlushAsync(token);
        }
    }
}