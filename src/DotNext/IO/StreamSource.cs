using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.MemoryMarshal;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.IO
{
    using Buffers;

    /// <summary>
    /// Represents conversion of various buffer types to stream.
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
            => new MemoryStream(segment.Array, segment.Offset, segment.Count, writable, false);

        /// <summary>
        /// Converts read-only sequence of bytes to a read-only stream.
        /// </summary>
        /// <param name="sequence">The sequence of bytes.</param>
        /// <returns>The stream over sequence of bytes.</returns>
        public static Stream AsStream(this ReadOnlySequence<byte> sequence)
            => sequence.IsSingleSegment && TryGetArray(sequence.First, out var segment) ? AsStream(segment) : new ReadOnlyMemoryStream(sequence);

        /// <summary>
        /// Converts read-only memory to a read-only stream.
        /// </summary>
        /// <param name="memory">The read-only memory.</param>
        /// <returns>The stream over memory of bytes.</returns>
        public static Stream AsStream(this ReadOnlyMemory<byte> memory)
            => AsStream(new ReadOnlySequence<byte>(memory));

        /// <summary>
        /// Gets written content as a read-only stream.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <returns>The stream representing written bytes.</returns>
        [Obsolete("Use DotNext.IO.StreamSource.AsStream in combination with WrittenArray or WrittenMemory property instead", true)]
        public static Stream GetWrittenBytesAsStream(this PooledArrayBufferWriter<byte> writer)
            => AsStream(writer.WrittenArray);

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
                flush ??= CreateFlushAction(writer);
                flushAsync ??= CreateAsyncFlushAction(writer);
            }

            return new BufferWriterStream<TWriter>(writer, flush, flushAsync);

            static Action<TWriter> CreateFlushAction(TWriter writer)
            {
                Debug.Assert(writer is IFlushable);
                Ldnull();
                Push(writer);
                Ldvirtftn(Method(Type<IFlushable>(), nameof(IFlushable.Flush)));
                Newobj(Constructor(Type<Action<TWriter>>(), Type<object>(), Type<IntPtr>()));
                return Return<Action<TWriter>>();
            }

            static Func<TWriter, CancellationToken, Task> CreateAsyncFlushAction(TWriter writer)
            {
                Debug.Assert(writer is IFlushable);
                Ldnull();
                Push(writer);
                Ldvirtftn(Method(Type<IFlushable>(), nameof(IFlushable.FlushAsync)));
                Newobj(Constructor(Type<Func<TWriter, CancellationToken, Task>>(), Type<object>(), Type<IntPtr>()));
                return Return<Func<TWriter, CancellationToken, Task>>();
            }
        }
    }
}