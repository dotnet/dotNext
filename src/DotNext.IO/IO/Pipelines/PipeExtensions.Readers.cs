using System.Buffers;
using System.Globalization;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.IO.Pipelines;

using Buffers;
using Buffers.Binary;
using Text;
using AsyncEnumerable = Collections.Generic.AsyncEnumerable;

/// <summary>
/// Represents extension method for parsing data stored in pipe.
/// </summary>
public static partial class PipeExtensions
{
    internal static ValueTask<TResult> ReadAsync<TResult, TParser>(PipeReader reader, TParser parser, CancellationToken token)
        where TParser : struct, IBufferReader, ISupplier<TResult>
    {
        // parse synchronously as many as possible
        if (TryRead(reader, ref parser, out var canceled) is false || parser.RemainingBytes > 0)
            return ReadSlowAsync(reader, parser, token);

        return canceled
            ? ValueTask.FromCanceled<TResult>(token.IsCancellationRequested ? token : new(true))
            : ValueTask.FromResult(parser.Invoke());

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<TResult> ReadSlowAsync(PipeReader reader, TParser parser, CancellationToken token)
        {
            for (SequencePosition consumed; parser.RemainingBytes > 0; reader.AdvanceTo(consumed))
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                readResult.ThrowIfCancellationRequested(reader, token);
                var buffer = readResult.Buffer;
                if (buffer.IsEmpty)
                    break;

                consumed = parser.Append(buffer);
            }

            return parser.EndOfStream<TResult, TParser>();
        }
    }

    private static ValueTask ReadAsync<TParser>(PipeReader reader, TParser parser, CancellationToken token)
        where TParser : struct, IBufferReader
    {
        // parse synchronously as many as possible
        if (TryRead(reader, ref parser, out var canceled) is false || parser.RemainingBytes > 0)
            return ReadSlowAsync(reader, parser, token);

        return canceled
            ? ValueTask.FromCanceled(token.IsCancellationRequested ? token : new(true))
            : ValueTask.CompletedTask;

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask ReadSlowAsync(PipeReader reader, TParser parser, CancellationToken token)
        {
            for (SequencePosition consumed; parser.RemainingBytes > 0; reader.AdvanceTo(consumed))
            {
                var readResult = await reader.ReadAsync(token).ConfigureAwait(false);
                readResult.ThrowIfCancellationRequested(reader, token);
                var buffer = readResult.Buffer;
                if (buffer.IsEmpty)
                    break;

                consumed = parser.Append(buffer);
            }

            parser.EndOfStream();
        }
    }

    private static bool TryRead<TParser>(PipeReader reader, ref TParser parser, out bool canceled)
        where TParser : struct, IBufferReader
    {
        bool result;
        if (result = reader.TryRead(out var readResult))
        {
            var buffer = readResult.Buffer;
            var position = buffer.Start;
            try
            {
                position = parser.Append(buffer);
            }
            finally
            {
                reader.AdvanceTo(position);
            }
        }

        canceled = readResult.IsCanceled;
        return result;
    }

    /// <summary>
    /// Decodes the value of binary formattable type.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public static ValueTask<T> ReadAsync<T>(this PipeReader reader, CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
    {
        return T.Size <= BinaryFormattable256Reader<T>.MaxSize
            ? ReadAsync<T, BinaryFormattable256Reader<T>>(reader, new(), token)
            : ReadAsync<T, BinaryFormattableReader<T>>(reader, new(), token);
    }

    /// <summary>
    /// Reads integer encoded in little-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The integer value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public static ValueTask<T> ReadLittleEndianAsync<T>(this PipeReader reader, CancellationToken token = default)
        where T : notnull, IBinaryInteger<T>
    {
        var type = typeof(T);
        return type.IsPrimitive || type == typeof(Int128) || type == typeof(UInt128)
            ? ReadAsync<T, WellKnownIntegerReader<T>>(reader, WellKnownIntegerReader<T>.LittleEndian(), token)
            : ReadAsync<T, IntegerReader<T>>(reader, IntegerReader<T>.LittleEndian(), token);
    }

    /// <summary>
    /// Reads integer encoded in big-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The integer value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public static ValueTask<T> ReadBigEndianAsync<T>(this PipeReader reader, CancellationToken token = default)
        where T : notnull, IBinaryInteger<T>
    {
        var type = typeof(T);
        return type.IsPrimitive || type == typeof(Int128) || type == typeof(UInt128)
            ? ReadAsync<T, WellKnownIntegerReader<T>>(reader, WellKnownIntegerReader<T>.BigEndian(), token)
            : ReadAsync<T, IntegerReader<T>>(reader, IntegerReader<T>.BigEndian(), token);
    }

    private static ValueTask<int> ReadLengthAsync(this PipeReader reader, LengthFormat lengthFormat, CancellationToken token) => lengthFormat switch
    {
        LengthFormat.LittleEndian => reader.ReadLittleEndianAsync<int>(token),
        LengthFormat.BigEndian => reader.ReadBigEndianAsync<int>(token),
        LengthFormat.Compressed => ReadAsync<int, SevenBitEncodedInt32Reader>(reader, new(), token),
        _ => ValueTask.FromException<int>(new ArgumentOutOfRangeException(nameof(lengthFormat))),
    };

    /// <summary>
    /// Decodes string asynchronously from pipe.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="lengthFormat">Represents string length encoding format.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="EndOfStreamException"><paramref name="reader"/> doesn't contain the necessary number of bytes to restore string.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<MemoryOwner<char>> DecodeAsync(this PipeReader reader, DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator = null, CancellationToken token = default)
    {
        var length = await reader.ReadLengthAsync(lengthFormat, token).ConfigureAwait(false);

        MemoryOwner<char> result;
        if (length > 0)
        {
            result = allocator.AllocateAtLeast(context.Encoding.GetMaxCharCount(length));
            result.TryResize(await ReadAsync<int, CharBufferDecodingReader>(reader, new(in context, length, result.Memory), token).ConfigureAwait(false));
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
    /// <param name="reader">The pipe reader.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="buffer">The buffer of characters.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The enumerator of characters.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static IAsyncEnumerable<ReadOnlyMemory<char>> DecodeAsync(this PipeReader reader, DecodingContext context, LengthFormat lengthFormat, Memory<char> buffer, CancellationToken token = default)
        => buffer.IsEmpty ? AsyncEnumerable.Throw<ReadOnlyMemory<char>>(new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer))) : DecodeAsync(reader, context.GetDecoder(), lengthFormat, buffer, token);

    private static async IAsyncEnumerable<ReadOnlyMemory<char>> DecodeAsync(PipeReader reader, Decoder decoder, LengthFormat lengthFormat, Memory<char> buffer, [EnumeratorCancellation] CancellationToken token = default)
    {
        var lengthInBytes = await reader.ReadLengthAsync(lengthFormat, token).ConfigureAwait(false);
        for (DecodingReader state; lengthInBytes > 0; lengthInBytes -= state.RemainingBytes)
        {
            state = new(decoder, lengthInBytes, buffer);
            var writtenChars = await ReadAsync<int, DecodingReader>(reader, state, token).ConfigureAwait(false);
            yield return buffer.Slice(0, writtenChars);
        }
    }

    /// <summary>
    /// Parses the sequence of characters.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to <paramref name="parser"/>.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="arg">The argument to be passed to <paramref name="parser"/>.</param>
    /// <param name="parser">The parser of characters.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="lengthFormat">The format of the string length (in bytes) encoded in the stream.</param>
    /// <param name="allocator">The allocator of internal buffer.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parser"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<TResult> ParseAsync<TArg, TResult>(this PipeReader reader, TArg arg, ReadOnlySpanFunc<char, TArg, TResult> parser, DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(parser);

        using var chars = await reader.DecodeAsync(context, lengthFormat, allocator, token).ConfigureAwait(false);
        return parser(chars.Span, arg);
    }

    private static async ValueTask<TResult> ParseAsync<TResult, TArg>(this PipeReader reader, TArg arg, ReadOnlySpanFunc<byte, TArg, TResult> parser, LengthFormat lengthFormat, CancellationToken token)
    {
        var length = await reader.ReadLengthAsync(lengthFormat, token).ConfigureAwait(false);
        if (length > 0)
        {
            var readResult = await reader.ReadAtLeastAsync(length, token).ConfigureAwait(false);
            readResult.ThrowIfCancellationRequested(reader, token);
            return Parse(reader, arg, parser, length, readResult.Buffer);
        }

        return parser([], arg);

        static TResult Parse(PipeReader reader, TArg arg, ReadOnlySpanFunc<byte, TArg, TResult> parser, int length, ReadOnlySequence<byte> source)
        {
            try
            {
                if (source.Length < length)
                {
                    length = (int)source.Length;
                    throw new EndOfStreamException();
                }

                if (source.TryGetBlock(length, out var block))
                    return parser(block.Span, arg);

                using var destination = (uint)length <= (uint)SpanOwner<byte>.StackallocThreshold
                    ? stackalloc byte[length]
                    : new SpanOwner<byte>(length);

                source.CopyTo(destination.Span, out length);
                return parser(destination.Span.Slice(0, length), arg);
            }
            finally
            {
                reader.AdvanceTo(source.GetPosition(length));
            }
        }
    }

    /// <summary>
    /// Parses the sequence of characters encoded as UTF-8.
    /// </summary>
    /// <typeparam name="T">The type that supports parsing from UTF-8.</typeparam>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="lengthFormat">The format of the string length (in bytes) encoded in the stream.</param>
    /// <param name="provider">Culture-specific formatting information.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The result of parsing.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static ValueTask<T> ParseAsync<T>(this PipeReader reader, LengthFormat lengthFormat, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, IUtf8SpanParsable<T>
        => reader.ParseAsync(provider, T.Parse, lengthFormat, token);

    /// <summary>
    /// Parses the numeric value from UTF-8 encoded characters.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="lengthFormat">The format of the string length (in bytes) encoded in the stream.</param>
    /// <param name="style">A combination of number styles.</param>
    /// <param name="provider">Culture-specific formatting information.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The result of parsing.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static ValueTask<T> ParseAsync<T>(this PipeReader reader, LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, INumberBase<T>
    {
        return reader.ParseAsync((style, provider), Parse, lengthFormat, token);

        static T Parse(ReadOnlySpan<byte> source, (NumberStyles, IFormatProvider?) args)
            => T.Parse(source, args.Item1, args.Item2);
    }

    /// <summary>
    /// Reads the entire content using the specified consumer.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="reader">The pipe to read from.</param>
    /// <param name="consumer">The content reader.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask CopyToAsync<TConsumer>(this PipeReader reader, TConsumer consumer, CancellationToken token = default)
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        await foreach (var chunk in ReadAllAsync(reader, token).ConfigureAwait(false))
        {
            await consumer.Invoke(chunk, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads the entire content using the specified consumer.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="reader">The pipe to read from.</param>
    /// <param name="consumer">The content reader.</param>
    /// <param name="count">The number of bytes to copy.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask CopyToAsync<TConsumer>(this PipeReader reader, TConsumer consumer, long count, CancellationToken token = default)
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        while (count > 0L)
        {
            var result = await reader.ReadAsync(token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(reader, token);

            var buffer = result.Buffer;
            var consumed = buffer.Start;

            try
            {
                if (buffer.Length >= count)
                {
                    buffer = buffer.Slice(consumed, count);
                }
                else if (buffer.IsEmpty)
                {
                    throw new EndOfStreamException();
                }

                for (ReadOnlyMemory<byte> block; buffer.TryGet(ref consumed, out block, advance: false) && !block.IsEmpty; count -= block.Length, consumed = buffer.GetPosition(block.Length, consumed))
                    await consumer.Invoke(block, token).ConfigureAwait(false);
            }
            finally
            {
                reader.AdvanceTo(consumed);
            }
        }
    }

    /// <summary>
    /// Reads the block of memory.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="output">The block of memory to fill from the pipe.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous state of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">Reader doesn't have enough data.</exception>
    public static ValueTask ReadExactlyAsync(this PipeReader reader, Memory<byte> output, CancellationToken token = default)
        => output.IsEmpty ? ValueTask.CompletedTask : ReadAsync<MemoryBlockReader>(reader, new(output), token);

    /// <summary>
    /// Reads at least the specified number of bytes.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="destination">The buffer to write into.</param>
    /// <param name="minimumSize">The minimum number of bytes to read.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The actual number of bytes written to <paramref name="destination"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minimumSize"/> is negative or greater than the length of <paramref name="destination"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">Reader doesn't have enough data.</exception>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<int> ReadAtLeastAsync(this PipeReader reader, Memory<byte> destination, int minimumSize, CancellationToken token)
    {
        if ((uint)minimumSize > (uint)destination.Length)
            throw new ArgumentOutOfRangeException(nameof(minimumSize));

        var result = await reader.ReadAtLeastAsync(minimumSize, token).ConfigureAwait(false);
        result.ThrowIfCancellationRequested(reader, token);

        return Read(reader, result.Buffer, destination, minimumSize);

        static int Read(PipeReader reader, ReadOnlySequence<byte> source, in Memory<byte> destination, int minimumSize)
        {
            var readCount = 0;
            try
            {
                source.CopyTo(destination.Span, out readCount);
                if (minimumSize > readCount)
                    throw new EndOfStreamException();
            }
            finally
            {
                reader.AdvanceTo(source.GetPosition(readCount));
            }

            return readCount;
        }
    }

    /// <summary>
    /// Reads length-prefixed block of bytes.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="lengthFormat">The format of the block length encoded in the underlying pipe.</param>
    /// <param name="allocator">The memory allocator used to place the decoded block of bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded block of bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">Reader doesn't have enough data.</exception>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<MemoryOwner<byte>> ReadAsync(this PipeReader reader, LengthFormat lengthFormat, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
    {
        var length = await reader.ReadLengthAsync(lengthFormat, token).ConfigureAwait(false);
        MemoryOwner<byte> result;
        if (length > 0)
        {
            result = allocator.AllocateExactly(length);
            await ReadExactlyAsync(reader, result.Memory, token).ConfigureAwait(false);
        }
        else
        {
            result = default;
        }

        return result;
    }

    /// <summary>
    /// Attempts to read block of data synchronously.
    /// </summary>
    /// <remarks>
    /// This method doesn't advance the reader position.
    /// </remarks>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="length">The length of the block to consume, in bytes.</param>
    /// <param name="result">
    /// The requested block of data which length is equal to <paramref name="length"/> in case of success;
    /// otherwise, empty block.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the block of requested length is obtained successfully;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryReadExactly(this PipeReader reader, long length, out ReadResult result)
    {
        if (reader.TryRead(out result))
        {
            if (length <= result.Buffer.Length)
            {
                result = new(result.Buffer.Slice(0L, length), result.IsCanceled, result.IsCompleted);
                return true;
            }

            reader.AdvanceTo(result.Buffer.Start, result.Buffer.End);
        }

        return false;
    }

    /// <summary>
    /// Drops the specified number of bytes from the pipe.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="length">The number of bytes to skip.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">Reader doesn't have enough data to skip.</exception>
    public static ValueTask SkipAsync(this PipeReader reader, long length, CancellationToken token = default)
    {
        return length switch
        {
            0L => ValueTask.CompletedTask,
            < 0L => ValueTask.FromException(new ArgumentOutOfRangeException(nameof(length))),
            _ => ReadAsync(reader, new SkippingReader(length), token),
        };
    }

    /// <summary>
    /// Reads the block of memory.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="output">The block of memory to fill from the pipe.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The actual number of copied bytes.</returns>
    public static ValueTask<int> ReadAsync(this PipeReader reader, Memory<byte> output, CancellationToken token = default)
        => output.IsEmpty ? ValueTask.FromResult(0) : ReadAsync<int, MemoryReader>(reader, new(output), token);

    /// <summary>
    /// Reads all chunks of data from the pipe.
    /// </summary>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>A sequence of data chunks.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(this PipeReader reader, [EnumeratorCancellation] CancellationToken token = default)
    {
        ReadResult result;
        ReadOnlySequence<byte> buffer;
        do
        {
            result = await reader.ReadAsync(token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(reader, token);
            buffer = result.Buffer;
            var consumed = buffer.Start;

            try
            {
                for (ReadOnlyMemory<byte> block; buffer.TryGet(ref consumed, out block, advance: false) && !block.IsEmpty; consumed = buffer.GetPosition(block.Length, consumed))
                    yield return block;
            }
            finally
            {
                reader.AdvanceTo(consumed);
            }
        }
        while (!result.IsCompleted);
    }

    /// <summary>
    /// Decodes null-terminated UTF-8 encoded string.
    /// </summary>
    /// <remarks>
    /// This method returns when writer side completed or null char reached.
    /// </remarks>
    /// <param name="reader">The pipe reader.</param>
    /// <param name="output">The output buffer for decoded characters.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="reader"/> is <see langword="null"/>;
    /// or <paramref name="output"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask ReadUtf8Async(this PipeReader reader, IBufferWriter<char> output, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(output);

        var decoder = Encoding.UTF8.GetDecoder();
        ReadResult result;

        do
        {
            result = await reader.ReadAsync(token).ConfigureAwait(false);
        }
        while (!Decode(decoder, reader, in result, output, token));

        static bool Decode(Decoder decoder, PipeReader reader, in ReadResult result, IBufferWriter<char> output, CancellationToken token)
        {
            bool completed;
            var buffer = result.Buffer;

            if (buffer.PositionOf(DecodingContext.Utf8NullChar).TryGetValue(out var consumed))
            {
                buffer = buffer.Slice(0, consumed);
                completed = true;
                consumed = result.Buffer.GetPosition(1L, consumed);
            }
            else
            {
                completed = result.IsCompleted;
                consumed = buffer.End;
            }

            decoder.Convert(in buffer, output, completed, out _, out _);
            result.ThrowIfCancellationRequested(reader, token);
            reader.AdvanceTo(consumed);

            return completed;
        }
    }
}