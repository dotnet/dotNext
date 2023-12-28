using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.IO;

using Buffers;
using Buffers.Binary;
using Text;
using PipeConsumer = Pipelines.PipeConsumer;

public partial class FileReader : IAsyncBinaryReader
{
    private ValueTask<TResult> ReadAsync<TResult, TParser>(TParser parser, CancellationToken token)
        where TParser : struct, IBufferReader, ISupplier<TResult>
    {
        return HasBufferedData && Read(ref parser) && parser.RemainingBytes is 0
            ? ValueTask.FromResult(parser.Invoke())
            : ReadSlowAsync<TResult, TParser>(parser, token);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<TResult> ReadSlowAsync<TResult, TParser>(TParser parser, CancellationToken token)
        where TParser : struct, IBufferReader, ISupplier<TResult>
    {
        while (parser.RemainingBytes > 0 && await ReadAsync(token).ConfigureAwait(false) && Read(ref parser)) ;

        return parser.EndOfStream<TResult, TParser>();
    }

    private ValueTask ReadAsync<TParser>(TParser parser, CancellationToken token)
        where TParser : struct, IBufferReader
    {
        return HasBufferedData && Read(ref parser) && parser.RemainingBytes is 0
            ? ValueTask.CompletedTask
            : ReadSlowAsync(parser, token);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask ReadSlowAsync<TParser>(TParser parser, CancellationToken token)
        where TParser : struct, IBufferReader
    {
        while (parser.RemainingBytes > 0 && await ReadAsync(token).ConfigureAwait(false) && Read(ref parser)) ;

        parser.EndOfStream();
    }

    private bool Read<TParser>(ref TParser parser)
        where TParser : struct, IBufferReader
    {
        Debug.Assert(HasBufferedData);

        var buffer = TrimLength(Buffer, length);
        buffer = buffer.TrimLength(parser.RemainingBytes);

        bool result;
        if (result = buffer.IsEmpty is false)
        {
            parser.Invoke(buffer.Span);
            Consume(buffer.Length);
            length -= buffer.Length;
        }

        return result;
    }

    /// <summary>
    /// Decodes the value of binary formattable type.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public ValueTask<T> ReadAsync<T>(CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
    {
        return T.Size <= BinaryFormattable256Reader<T>.MaxSize
            ? ReadAsync<T, BinaryFormattable256Reader<T>>(new(), token)
            : ReadAsync<T, BinaryFormattableReader<T>>(new(), token);
    }

    /// <summary>
    /// Reads integer encoded in little-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The integer value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public ValueTask<T> ReadLittleEndianAsync<T>(CancellationToken token = default)
        where T : notnull, IBinaryInteger<T>
    {
        var type = typeof(T);
        return type.IsPrimitive || type == typeof(Int128) || type == typeof(UInt128)
            ? ReadAsync<T, WellKnownIntegerReader<T>>(WellKnownIntegerReader<T>.LittleEndian(), token)
            : ReadAsync<T, IntegerReader<T>>(IntegerReader<T>.LittleEndian(), token);
    }

    /// <summary>
    /// Reads integer encoded in big-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The integer value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public ValueTask<T> ReadBigEndianAsync<T>(CancellationToken token = default)
        where T : notnull, IBinaryInteger<T>
    {
        var type = typeof(T);
        return type.IsPrimitive || type == typeof(Int128) || type == typeof(UInt128)
            ? ReadAsync<T, WellKnownIntegerReader<T>>(WellKnownIntegerReader<T>.BigEndian(), token)
            : ReadAsync<T, IntegerReader<T>>(IntegerReader<T>.BigEndian(), token);
    }

    /// <inheritdoc/>
    ValueTask<TReader> IAsyncBinaryReader.ReadAsync<TReader>(TReader reader, CancellationToken token)
        => ReadAsync<TReader, BufferReader<TReader>>(reader, token);

    private ValueTask<int> ReadLengthAsync(LengthFormat lengthFormat, CancellationToken token) => lengthFormat switch
    {
        LengthFormat.LittleEndian => ReadLittleEndianAsync<int>(token),
        LengthFormat.BigEndian => ReadBigEndianAsync<int>(token),
        LengthFormat.Compressed => ReadAsync<int, SevenBitEncodedInt.Reader>(new(), token),
        _ => ValueTask.FromException<int>(new ArgumentOutOfRangeException(nameof(lengthFormat))),
    };

    /// <summary>
    /// Reads the memory block.
    /// </summary>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="allocator">An allocator of the resulting buffer.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The rented buffer containing the memory block.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public async ValueTask<MemoryOwner<byte>> ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
    {
        MemoryOwner<byte> result;
        var length = await ReadLengthAsync(lengthFormat, token).ConfigureAwait(false);
        if (length > 0)
        {
            result = allocator.AllocateExactly(length);
            await ReadAsync<MemoryBlockReader>(new(result.Memory), token).ConfigureAwait(false);
        }
        else
        {
            result = default;
        }

        return result;
    }

    /// <summary>
    /// Decodes the sequence of characters.
    /// </summary>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public async ValueTask<MemoryOwner<char>> DecodeAsync(DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator = null, CancellationToken token = default)
    {
        MemoryOwner<char> result;
        var lengthInBytes = await ReadLengthAsync(lengthFormat, token).ConfigureAwait(false);

        if (lengthInBytes > 0)
        {
            result = allocator.AllocateExactly(context.Encoding.GetMaxCharCount(lengthInBytes));

            result.TryResize(await ReadAsync<int, CharBufferDecodingReader>(new(in context, lengthInBytes, result.Memory), token).ConfigureAwait(false));
        }
        else
        {
            result = default;
        }

        return result;
    }

    /// <summary>
    /// Decodes the sequence of characters.
    /// </summary>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="buffer">The buffer of characters.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The enumerator of characters.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public async IAsyncEnumerable<ReadOnlyMemory<char>> DecodeAsync(DecodingContext context, LengthFormat lengthFormat, Memory<char> buffer, [EnumeratorCancellation] CancellationToken token = default)
    {
        var lengthInBytes = await ReadLengthAsync(lengthFormat, token).ConfigureAwait(false);

        for (var decoder = context.GetDecoder(); lengthInBytes > 0;)
        {
            var state = new DecodingReader(context.Encoding, decoder, lengthInBytes, buffer);
            var writtenChars = await ReadAsync<int, DecodingReader>(state, token).ConfigureAwait(false);
            yield return buffer.Slice(0, writtenChars);
            lengthInBytes -= state.RemainingBytes;
        }
    }

    /// <summary>
    /// Parses the sequence of characters encoded as UTF-8.
    /// </summary>
    /// <typeparam name="T">The type that supports parsing from UTF-8.</typeparam>
    /// <param name="lengthFormat">The format of the string length (in bytes) encoded in the stream.</param>
    /// <param name="provider">Culture-specific formatting information.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The result of parsing.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public async ValueTask<T> ParseAsync<T>(LengthFormat lengthFormat, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, IUtf8SpanParsable<T>
    {
        var length = await ReadLengthAsync(lengthFormat, token).ConfigureAwait(false);

        if (length <= 0)
            return T.Parse([], provider);

        // fast path without allocation of temp buffer
        if (TryConsume(length, out var block))
        {
            block = TrimLength(block, this.length);
            this.length -= block.Length;
            return block.Length == length
                ? T.Parse(block.Span, provider)
                : throw new EndOfStreamException();
        }

        // slow path with temp buffer
        using var buffer = allocator.AllocateExactly(length);
        await ReadAsync<MemoryBlockReader>(new(buffer.Memory), token).ConfigureAwait(false);
        return T.Parse(buffer.Span, provider);
    }

    /// <summary>
    /// Parses the numeric value from UTF-8 encoded characters.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="lengthFormat">The format of the string length (in bytes) encoded in the stream.</param>
    /// <param name="style">A combination of number styles.</param>
    /// <param name="provider">Culture-specific formatting information.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The result of parsing.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public async ValueTask<T> ParseAsync<T>(LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, INumberBase<T>
    {
        var length = await ReadLengthAsync(lengthFormat, token).ConfigureAwait(false);

        if (length <= 0)
            return T.Parse(ReadOnlySpan<byte>.Empty, style, provider);

        // fast path without allocation of temp buffer
        if (TryConsume(length, out var block))
        {
            block = TrimLength(block, this.length);
            this.length -= block.Length;
            return block.Length == length
                ? T.Parse(block.Span, provider)
                : throw new EndOfStreamException();
        }

        using var buffer = allocator.AllocateExactly(length);
        await ReadAsync<MemoryBlockReader>(new(buffer.Memory), token).ConfigureAwait(false);
        return T.Parse(buffer.Span, provider);
    }

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <param name="consumer">The content reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token = default)
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        for (ReadOnlyMemory<byte> buffer; length > 0L && (HasBufferedData || await ReadAsync(token).ConfigureAwait(false)); Consume(buffer.Length))
        {
            buffer = TrimLength(Buffer, length);
            await consumer.Invoke(buffer, token).ConfigureAwait(false);
            length -= buffer.Length;
        }
    }

    /// <summary>
    /// Copies the content to the specified stream.
    /// </summary>
    /// <param name="output">The output stream receiving object content.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask CopyToAsync(Stream output, CancellationToken token = default)
        => CopyToAsync<StreamConsumer>(output, token);

    /// <summary>
    /// Copies the content to the specified pipe writer.
    /// </summary>
    /// <param name="output">The writer.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask CopyToAsync(PipeWriter output, CancellationToken token = default)
        => CopyToAsync<PipeConsumer>(output, token);

    /// <summary>
    /// Copies the content to the specified buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask CopyToAsync(IBufferWriter<byte> writer, CancellationToken token = default)
        => CopyToAsync(new BufferConsumer<byte>(writer), token);

    /// <inheritdoc />
    ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
        => ReadAsync<MemoryBlockReader>(new(output), token);

    /// <inheritdoc />
    ValueTask IAsyncBinaryReader.SkipAsync(long bytes, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else if (length < bytes)
        {
            result = ValueTask.FromException(new ArgumentOutOfRangeException(nameof(bytes)));
        }
        else
        {
            result = ValueTask.CompletedTask;
            try
            {
                Skip(bytes);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    /// <inheritdoc />
    bool IAsyncBinaryReader.TryGetSequence(out ReadOnlySequence<byte> bytes)
    {
        var buffer = Buffer;
        if (length <= buffer.Length)
        {
            bytes = new(TrimLength(buffer, length));
            return true;
        }

        bytes = default;
        return false;
    }

    /// <inheritdoc />
    bool IAsyncBinaryReader.TryGetRemainingBytesCount(out long count)
    {
        count = length.IsInfinite
            ? Math.Max(0L, RandomAccess.GetLength(handle) - fileOffset)
            : (long)length;

        return true;
    }
}