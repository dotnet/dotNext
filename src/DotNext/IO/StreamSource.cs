using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.IO
{
    using Buffers;

    /// <summary>
    /// Represents <see cref="Stream"/> factory methods.
    /// </summary>
    public static class StreamSource
    {
        /// <summary>
        /// Converts segment of an array to the stream.
        /// </summary>
        /// <param name="segment">The array of bytes.</param>
        /// <param name="writable">Determines whether the stream supports writing.</param>
        /// <returns>The stream representing the array segment.</returns>
        public static Stream AsStream(this ArraySegment<byte> segment, bool writable = false)
        {
            if (segment.Array.IsNullOrEmpty())
                return Stream.Null;

            return new MemoryStream(segment.Array, segment.Offset, segment.Count, writable, false);
        }

        /// <summary>
        /// Converts read-only sequence of bytes to a read-only stream.
        /// </summary>
        /// <param name="sequence">The sequence of bytes.</param>
        /// <returns>The stream over sequence of bytes.</returns>
        public static Stream AsStream(this ReadOnlySequence<byte> sequence)
        {
            if (sequence.IsEmpty)
                return Stream.Null;

            if (sequence.IsSingleSegment && TryGetArray(sequence.First, out var segment))
                return AsStream(segment);

            return new ReadOnlyMemoryStream(sequence);
        }

        /// <summary>
        /// Converts read-only memory to a read-only stream.
        /// </summary>
        /// <param name="memory">The read-only memory.</param>
        /// <returns>The stream over memory of bytes.</returns>
        public static Stream AsStream(this ReadOnlyMemory<byte> memory)
            => AsStream(new ReadOnlySequence<byte>(memory));

        /// <summary>
        /// Returns writable stream that wraps the provided delegate for writing data.
        /// </summary>
        /// <param name="writer">The callback that is called automatically.</param>
        /// <param name="arg">The arg to be passed to the callback.</param>
        /// <param name="flush">Optional synchronous flush action.</param>
        /// <param name="flushAsync">Optiona asynchronous flush action.</param>
        /// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
        /// <returns>The writable stream wrapping the callback.</returns>
        public static Stream AsStream<TArg>(this ValueReadOnlySpanAction<byte, TArg> writer, TArg arg, Action<TArg>? flush = null, Func<TArg, CancellationToken, Task>? flushAsync = null)
            => new SpanWriterStream<TArg>(writer, arg, flush, flushAsync);

        /// <summary>
        /// Returns writable stream associated with the buffer writer.
        /// </summary>
        /// <typeparam name="TWriter">The type of the writer.</typeparam>
        /// <param name="writer">The writer to be wrapped by the stream.</param>
        /// <param name="flush">Optional synchronous flush action.</param>
        /// <param name="flushAsync">Optiona asynchronous flush action.</param>
        /// <returns>The writable stream wrapping buffer writer.</returns>
        public static unsafe Stream AsStream<TWriter>(this TWriter writer, Action<TWriter>? flush = null, Func<TWriter, CancellationToken, Task>? flushAsync = null)
            where TWriter : class, IBufferWriter<byte>
        {
            flush ??= IFlushable.TryReflectFlushMethod(writer);
            flushAsync ??= IFlushable.TryReflectAsyncFlushMethod(writer);

            delegate*<ReadOnlySpan<byte>, TWriter, void> callback = writer is IGrowableBuffer<byte> ?
                &WriteToGrowableBuffer :
                &Span.CopyTo<byte>;

            return AsStream(new ValueReadOnlySpanAction<byte, TWriter>(callback), writer, flush, flushAsync);

            static void WriteToGrowableBuffer(ReadOnlySpan<byte> input, TWriter output)
            {
                Debug.Assert(output is IGrowableBuffer<byte>);
                Unsafe.As<IGrowableBuffer<byte>>(output).Write(input);
            }
        }

        /// <summary>
        /// Creates a stream over sparse memory.
        /// </summary>
        /// <param name="writer">Sparse memory buffer.</param>
        /// <param name="readable"><see langword="true"/> to create readable stream; <see langword="false"/> to create writable stream.</param>
        /// <returns>Sparse memory stream.</returns>
        public static unsafe Stream AsStream(this SparseBufferWriter<byte> writer, bool readable)
        {
            if (!readable)
                return AsStream(new ValueReadOnlySpanAction<byte, SparseBufferWriter<byte>>(&WriteToBuffer), writer);

            var chunk = writer.FirstChunk;
            if (chunk is null)
                return Stream.Null;

            if (chunk.Next is null)
                return AsStream(chunk.WrittenMemory);

            return new SparseMemoryStream(writer);

            static void WriteToBuffer(ReadOnlySpan<byte> input, SparseBufferWriter<byte> output)
                => output.Write(input);
        }

        /// <summary>
        /// Returns writable stream that wraps the provided delegate for writing data.
        /// </summary>
        /// <param name="writer">The callback that is called automatically.</param>
        /// <param name="arg">The arg to be passed to the callback.</param>
        /// <param name="flush">Optional synchronous flush action.</param>
        /// <param name="flushAsync">Optiona asynchronous flush action.</param>
        /// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
        /// <returns>The writable stream wrapping the callback.</returns>
        public static Stream AsStream<TArg>(this Func<ReadOnlyMemory<byte>, TArg, CancellationToken, ValueTask> writer, TArg arg, Action<TArg>? flush = null, Func<TArg, CancellationToken, Task>? flushAsync = null)
            => new MemoryWriterStream<TArg>(writer, arg, flush, flushAsync);
    }
}