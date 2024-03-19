using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.IO;

using Buffers;

/// <summary>
/// Represents <see cref="Stream"/> factory methods.
/// </summary>
public static partial class StreamSource
{
    /// <summary>
    /// Converts segment of an array to the stream.
    /// </summary>
    /// <param name="segment">The array of bytes.</param>
    /// <param name="writable">Determines whether the stream supports writing.</param>
    /// <returns>The stream representing the array segment.</returns>
    public static Stream AsStream(this ArraySegment<byte> segment, bool writable = false)
        => segment.Array.IsNullOrEmpty() ? Stream.Null : new MemoryStream(segment.Array, segment.Offset, segment.Count, writable, false);

    /// <summary>
    /// Converts read-only sequence of bytes to a read-only stream.
    /// </summary>
    /// <param name="sequence">The sequence of bytes.</param>
    /// <returns>The stream over sequence of bytes.</returns>
    public static Stream AsStream(this ReadOnlySequence<byte> sequence)
    {
        if (sequence.IsEmpty)
            return Stream.Null;

        if (SequenceMarshal.TryGetArray(sequence, out var segment))
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
    /// Gets read-only stream that can be shared across async flows for independent reads.
    /// </summary>
    /// <remarks>
    /// You need to set a position explicitly before using stream for each parallel async flow.
    /// <see cref="Stream.SetLength(long)"/> is not supported to avoid different views of the same stream.
    /// </remarks>
    /// <param name="sequence">The sequence of bytes.</param>
    /// <returns>The stream over sequence of bytes.</returns>
    public static Stream AsSharedStream(this ReadOnlySequence<byte> sequence)
        => sequence.IsEmpty ? Stream.Null : new SharedReadOnlyMemoryStream(sequence);

    /// <summary>
    /// Returns writable synchronous stream.
    /// </summary>
    /// <param name="output">The consumer of the stream content.</param>
    /// <typeparam name="TOutput">The type of the consumer.</typeparam>
    /// <returns>The stream wrapping <typeparamref name="TOutput"/>.</returns>
    public static Stream AsSynchronousStream<TOutput>(TOutput output)
        where TOutput : notnull, IFlushable, IReadOnlySpanConsumer<byte>
        => new SyncWriterStream<TOutput>(output);

    /// <summary>
    /// Returns writable stream that wraps the provided delegate for writing data.
    /// </summary>
    /// <param name="writer">The callback that is called automatically.</param>
    /// <param name="arg">The arg to be passed to the callback.</param>
    /// <param name="flush">Optional synchronous flush action.</param>
    /// <param name="flushAsync">Optional asynchronous flush action.</param>
    /// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
    /// <returns>The writable stream wrapping the callback.</returns>
    public static Stream AsStream<TArg>(this ReadOnlySpanAction<byte, TArg> writer, TArg arg, Action<TArg>? flush = null, Func<TArg, CancellationToken, Task>? flushAsync = null)
        => AsSynchronousStream<ReadOnlySpanWriter<TArg>>(new(writer ?? throw new ArgumentNullException(nameof(writer)), arg, flush, flushAsync));

    /// <summary>
    /// Returns writable stream associated with the buffer writer.
    /// </summary>
    /// <typeparam name="TWriter">The type of the writer.</typeparam>
    /// <param name="writer">The writer to be wrapped by the stream.</param>
    /// <param name="flush">Optional synchronous flush action.</param>
    /// <param name="flushAsync">Optional asynchronous flush action.</param>
    /// <returns>The writable stream wrapping buffer writer.</returns>
    public static Stream AsStream<TWriter>(this TWriter writer, Action<TWriter>? flush = null, Func<TWriter, CancellationToken, Task>? flushAsync = null)
        where TWriter : class, IBufferWriter<byte>
    {
        IFlushable.DiscoverFlushMethods(writer, ref flush, ref flushAsync);
        return writer is IReadOnlySpanConsumer<byte>
            ? AsSynchronousStream<DelegatingWriter<TWriter>>(new(writer, flush, flushAsync))
            : AsSynchronousStream<BufferWriter<TWriter>>(new(writer, flush, flushAsync));
    }

    /// <summary>
    /// Creates a stream over sparse memory.
    /// </summary>
    /// <param name="writer">Sparse memory buffer.</param>
    /// <param name="readable"><see langword="true"/> to create readable stream; <see langword="false"/> to create writable stream.</param>
    /// <returns>Sparse memory stream.</returns>
    public static Stream AsStream(this SparseBufferWriter<byte> writer, bool readable)
    {
        if (!readable)
            return AsStream(writer);

        var chunk = writer.FirstChunk;
        if (chunk is null)
            return Stream.Null;

        if (chunk.Next is null)
            return AsStream(chunk.WrittenMemory);

        return new SparseMemoryStream(writer);
    }

    /// <summary>
    /// Returns writable asynchronous stream.
    /// </summary>
    /// <param name="output">The consumer of the stream content.</param>
    /// <typeparam name="TOutput">The type of the consumer.</typeparam>
    /// <returns>The stream wrapping <typeparamref name="TOutput"/>.</returns>
    public static Stream AsAsynchronousStream<TOutput>(TOutput output)
        where TOutput : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>, IFlushable
        => new AsyncWriterStream<TOutput>(output);

    /// <summary>
    /// Returns writable stream that wraps the provided delegate for writing data.
    /// </summary>
    /// <param name="writer">The callback to be called for each write.</param>
    /// <param name="arg">The arg to be passed to the callback.</param>
    /// <param name="flush">Optional synchronous flush action.</param>
    /// <param name="flushAsync">Optional asynchronous flush action.</param>
    /// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
    /// <returns>A writable stream wrapping the callback.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public static Stream AsStream<TArg>(this Func<ReadOnlyMemory<byte>, TArg, CancellationToken, ValueTask> writer, TArg arg, Action<TArg>? flush = null, Func<TArg, CancellationToken, Task>? flushAsync = null)
    {
        ArgumentNullException.ThrowIfNull(writer);

        return AsAsynchronousStream<ReadOnlyMemoryWriter<TArg>>(new(writer, arg, flush, flushAsync));
    }

    /// <summary>
    /// Returns read-only asynchronous stream that wraps the provided delegate for reading data.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the delegate.</typeparam>
    /// <param name="reader">The callback to be called for each read.</param>
    /// <param name="arg">The arg to be passed to the callback.</param>
    /// <returns>A readable stream wrapping the callback.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    public static Stream AsStream<TArg>(this Func<Memory<byte>, TArg, CancellationToken, ValueTask<int>> reader, TArg arg)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return new ReadOnlyStream<TArg>(reader, arg);
    }
}