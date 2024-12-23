using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Debug = System.Diagnostics.Debug;

namespace DotNext.IO;

using Buffers;
using Buffers.Binary;
using Text;

public partial class FileReader : IAsyncBinaryReader
{
    private ValueTask<TResult> ReadAsync<TResult, TParser>(TParser parser, CancellationToken token)
        where TParser : struct, IBufferReader, ISupplier<TResult>
    {
        return HasBufferedData && Read(ref parser)
            ? ValueTask.FromResult(parser.Invoke())
            : ReadSlowAsync<TResult, TParser>(parser, token);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private async ValueTask<TResult> ReadSlowAsync<TResult, TParser>(TParser parser, CancellationToken token)
        where TParser : struct, IBufferReader, ISupplier<TResult>
    {
        while (await ReadAsync(token).ConfigureAwait(false) && !Read(ref parser)) ;

        return parser.EndOfStream<TResult, TParser>();
    }

    private ValueTask ReadAsync<TParser>(TParser parser, CancellationToken token)
        where TParser : struct, IBufferReader
    {
        return HasBufferedData && Read(ref parser)
            ? ValueTask.CompletedTask
            : ReadSlowAsync(parser, token);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask ReadSlowAsync<TParser>(TParser parser, CancellationToken token)
        where TParser : struct, IBufferReader
    {
        while (await ReadAsync(token).ConfigureAwait(false) && !Read(ref parser)) ;

        parser.EndOfStream();
    }

    private bool Read<TParser>(ref TParser parser)
        where TParser : struct, IBufferReader
    {
        Debug.Assert(HasBufferedData);

        do
        {
            var buffer = TrimLength(Buffer, length);
            buffer = buffer.TrimLength(parser.RemainingBytes);

            if (buffer.IsEmpty)
                return false;

            parser.Invoke(buffer.Span);
            ConsumeUnsafe(buffer.Length);
            length -= buffer.Length;
        }
        while (parser.RemainingBytes > 0);

        return true;
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
        => ReadAsync<TReader, ProxyReader<TReader>>(reader, token);

    private ValueTask<int> ReadLengthAsync(LengthFormat lengthFormat, CancellationToken token) => lengthFormat switch
    {
        LengthFormat.LittleEndian => ReadLittleEndianAsync<int>(token),
        LengthFormat.BigEndian => ReadBigEndianAsync<int>(token),
        LengthFormat.Compressed => ReadAsync<int, SevenBitEncodedIntReader>(new(), token),
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
    public IAsyncEnumerable<ReadOnlyMemory<char>> DecodeAsync(DecodingContext context, LengthFormat lengthFormat, Memory<char> buffer, CancellationToken token = default)
        => DecodeAsync(context.GetDecoder(), lengthFormat, buffer, token);

    private async IAsyncEnumerable<ReadOnlyMemory<char>> DecodeAsync(Decoder decoder, LengthFormat lengthFormat, Memory<char> buffer, [EnumeratorCancellation] CancellationToken token = default)
    {
        var lengthInBytes = await ReadLengthAsync(lengthFormat, token).ConfigureAwait(false);

        for (DecodingReader state; lengthInBytes > 0; lengthInBytes -= state.RemainingBytes)
        {
            state = new(decoder, lengthInBytes, buffer);
            var writtenChars = await ReadAsync<int, DecodingReader>(state, token).ConfigureAwait(false);
            yield return buffer.Slice(0, writtenChars);
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
        T result;

        if (length <= 0)
        {
            result = T.Parse([], provider);
        }
        else if (TryRead(length, out var block))
        {
            // fast path without allocation of temp buffer
            block = TrimLength(block, this.length);
            this.length -= block.Length;
            result = block.Length == length
                ? T.Parse(block.Span, provider)
                : throw new EndOfStreamException();
            
            ResetIfNeeded();
        }
        else
        {
            // slow path with temp buffer
            using var buffer = allocator.AllocateExactly(length);
            await ReadAsync<MemoryBlockReader>(new(buffer.Memory), token).ConfigureAwait(false);
            result = T.Parse(buffer.Span, provider);
        }

        return result;
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
        if (TryRead(length, out var block))
        {
            block = TrimLength(block, this.length);
            this.length -= block.Length;
            return block.Length == length
                ? T.Parse(block.Span, style, provider)
                : throw new EndOfStreamException();
        }

        using var buffer = allocator.AllocateExactly(length);
        await ReadAsync<MemoryBlockReader>(new(buffer.Memory), token).ConfigureAwait(false);
        return T.Parse(buffer.Span, style, provider);
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
        for (ReadOnlyMemory<byte> buffer; length > 0L && (HasBufferedData || await ReadAsync(token).ConfigureAwait(false)); ConsumeUnsafe(buffer.Length))
        {
            buffer = TrimLength(Buffer, length);
            await consumer.Invoke(buffer, token).ConfigureAwait(false);
            length -= buffer.Length;
        }
    }

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <param name="consumer">The content reader.</param>
    /// <param name="count">The number of bytes to copy.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying file doesn't have enough bytes to read.</exception>
    public async ValueTask CopyToAsync<TConsumer>(TConsumer consumer, long count, CancellationToken token = default)
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        for (ReadOnlyMemory<byte> buffer; count > 0L && length > 0L && (HasBufferedData || await ReadAsync(token).ConfigureAwait(false)); ConsumeUnsafe(buffer.Length))
        {
            buffer = TrimLength(Buffer, length).TrimLength(int.CreateSaturating(count));
            await consumer.Invoke(buffer, token).ConfigureAwait(false);
            length -= buffer.Length;
            count -= buffer.Length;
        }

        if (count > 0L)
            throw new EndOfStreamException();
    }

    /// <inheritdoc/>
    ValueTask IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, long? count, CancellationToken token)
        => count.HasValue ? CopyToAsync(consumer, count.GetValueOrDefault(), token) : CopyToAsync(consumer, token);

    /// <inheritdoc />
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    async ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
    {
        var count = await ReadAsync(TrimLength(output, length), token).ConfigureAwait(false);
        if (count < output.Length)
            throw new EndOfStreamException();

        length -= count;
    }

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