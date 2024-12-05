using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Utf8 = System.Text.Unicode.Utf8;

namespace DotNext.IO;

using Buffers;
using Buffers.Binary;
using Numerics;
using Text;
using AsyncEnumerable = Collections.Generic.AsyncEnumerable;

public static partial class StreamExtensions
{
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    internal static async ValueTask<TResult> ReadAsync<TResult, TParser>(Stream stream, TParser parser, Memory<byte> buffer, CancellationToken token)
        where TParser : struct, IBufferReader, ISupplier<TResult>
    {
        for (int count; (count = parser.RemainingBytes) > 0; parser.Invoke(buffer.Span.Slice(0, count)))
        {
            count = await stream.ReadAsync(buffer.TrimLength(count), token).ConfigureAwait(false);

            if (count is 0)
                break;
        }

        return parser.EndOfStream<TResult, TParser>();
    }

    /// <summary>
    /// Parses the value encoded as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer allocated by the caller.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding value.</exception>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<T> ReadAsync<T>(this Stream stream, Memory<byte> buffer, CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
    {
        if (buffer.Length < T.Size)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

        buffer = buffer.Slice(0, T.Size);
        await stream.ReadExactlyAsync(buffer, token).ConfigureAwait(false);
        return T.Parse(buffer.Span);
    }

    /// <summary>
    /// Reads integer encoded in little-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer allocated by the caller.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The integer value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<T> ReadLittleEndianAsync<T>(this Stream stream, Memory<byte> buffer, CancellationToken token = default)
        where T : notnull, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        ThrowIfEmpty(buffer);

        buffer = buffer.Slice(0, Number.GetMaxByteCount<T>());
        await stream.ReadExactlyAsync(buffer, token).ConfigureAwait(false);
        return T.ReadLittleEndian(buffer.Span, Number.IsSigned<T>() is false);
    }

    /// <summary>
    /// Reads integer encoded in big-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer allocated by the caller.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The integer value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<T> ReadBigEndianAsync<T>(this Stream stream, Memory<byte> buffer, CancellationToken token = default)
        where T : notnull, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(stream);
        ThrowIfEmpty(buffer);

        buffer = buffer.Slice(0, Number.GetMaxByteCount<T>());
        await stream.ReadExactlyAsync(buffer, token).ConfigureAwait(false);
        return T.ReadBigEndian(buffer.Span, Number.IsSigned<T>() is false);
    }

    private static unsafe ValueTask<int> ReadLengthAsync(this Stream stream, LengthFormat lengthFormat, Memory<byte> buffer, CancellationToken token)
    {
        delegate*<Stream, Memory<byte>, CancellationToken, ValueTask<int>> reader;
        switch (lengthFormat)
        {
            default:
                return ValueTask.FromException<int>(new ArgumentOutOfRangeException(nameof(lengthFormat)));
            case LengthFormat.LittleEndian:
                reader = &ReadLittleEndianAsync<int>;
                break;
            case LengthFormat.BigEndian:
                reader = &ReadBigEndianAsync<int>;
                break;
            case LengthFormat.Compressed:
                reader = &Read7BitEncodedIntAsync;
                break;
        }

        return reader(stream, buffer, token);

        static ValueTask<int> Read7BitEncodedIntAsync(Stream stream, Memory<byte> buffer, CancellationToken token)
            => ReadAsync<int, SevenBitEncodedInt32Reader>(stream, new(), buffer, token);
    }

    /// <summary>
    /// Decodes the block of bytes asynchronously.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the block length encoded in the underlying stream.</param>
    /// <param name="allocator">The memory allocator used to place the decoded block of bytes.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The decoded block of bytes.</returns>
    /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<MemoryOwner<byte>> ReadBlockAsync(this Stream stream, LengthFormat lengthFormat, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
    {
        MemoryOwner<byte> result;
        int length;
        using (result = allocator.AllocateExactly(ULeb128<uint>.MaxSizeInBytes))
        {
            length = await stream.ReadLengthAsync(lengthFormat, result.Memory, token).ConfigureAwait(false);
        }

        if (length > 0)
        {
            result = allocator.AllocateExactly(length);

            try
            {
                await stream.ReadExactlyAsync(result.Memory, token).ConfigureAwait(false);
            }
            catch
            {
                // avoid memory leak in case of exception
                result.Dispose();
                throw;
            }
        }
        else
        {
            result = default;
        }

        return result;
    }

    /// <summary>
    /// Reads a length-prefixed string asynchronously using the specified encoding and supplied reusable buffer.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<MemoryOwner<char>> DecodeAsync(this Stream stream, DecodingContext context, LengthFormat lengthFormat, Memory<byte> buffer, MemoryAllocator<char>? allocator = null, CancellationToken token = default)
    {
        ThrowIfEmpty(buffer);

        MemoryOwner<char> result;
        var length = await stream.ReadLengthAsync(lengthFormat, buffer, token).ConfigureAwait(false);
        if (length > 0)
        {
            result = allocator.AllocateExactly(context.Encoding.GetMaxCharCount(length));

            try
            {
                length = await ReadAsync<int, CharBufferDecodingReader>(stream, new(in context, length, result.Memory), buffer, token).ConfigureAwait(false);
            }
            catch
            {
                // avoid memory leak in case of exception
                result.Dispose();
                throw;
            }

            result.TryResize(length);
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
    /// <param name="stream">The stream to read from.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="charBuffer">The buffer of characters.</param>
    /// <param name="byteBuffer">The buffer of bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The enumerator of characters.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static IAsyncEnumerable<ReadOnlyMemory<char>> DecodeAsync(this Stream stream, DecodingContext context, LengthFormat lengthFormat, Memory<char> charBuffer, Memory<byte> byteBuffer, CancellationToken token = default)
    {
        string paramName;
        if (byteBuffer.IsEmpty)
        {
            paramName = nameof(byteBuffer);
        }
        else if (charBuffer.IsEmpty)
        {
            paramName = nameof(charBuffer);
        }
        else
        {
            return DecodeAsync(stream, context.GetDecoder(), lengthFormat, charBuffer, byteBuffer, token);
        }

        return AsyncEnumerable.Throw<ReadOnlyMemory<char>>(new ArgumentException(ExceptionMessages.BufferTooSmall, paramName));
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<char>> DecodeAsync(Stream stream, Decoder decoder, LengthFormat lengthFormat, Memory<char> charBuffer, Memory<byte> byteBuffer, [EnumeratorCancellation] CancellationToken token = default)
    {
        var lengthInBytes = await ReadLengthAsync(stream, lengthFormat, byteBuffer, token).ConfigureAwait(false);
        for (DecodingReader state; lengthInBytes > 0; lengthInBytes -= state.RemainingBytes)
        {
            state = new(decoder, lengthInBytes, charBuffer);
            var writtenChars = await ReadAsync<int, DecodingReader>(stream, state, byteBuffer, token).ConfigureAwait(false);
            yield return charBuffer.Slice(0, writtenChars);
        }
    }

    /// <summary>
    /// Parses the sequence of characters.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to <paramref name="parser"/>.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="arg">The argument to be passed to <paramref name="parser"/>.</param>
    /// <param name="parser">The parser of characters.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="lengthFormat">The format of the string length (in bytes) encoded in the stream.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="allocator">The allocator of internal buffer.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parser"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<TResult> ParseAsync<TArg, TResult>(this Stream stream, TArg arg, ReadOnlySpanFunc<char, TArg, TResult> parser, DecodingContext context, LengthFormat lengthFormat, Memory<byte> buffer, MemoryAllocator<char>? allocator = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ThrowIfEmpty(buffer);

        using var chars = await stream.DecodeAsync(context, lengthFormat, buffer, allocator, token).ConfigureAwait(false);
        return parser(chars.Span, arg);
    }

    /// <summary>
    /// Parses the sequence of characters encoded as UTF-8.
    /// </summary>
    /// <typeparam name="T">The type that supports parsing from UTF-8.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the string length (in bytes) encoded in the stream.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="provider">Culture-specific formatting information.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The result of parsing.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<T> ParseAsync<T>(this Stream stream, LengthFormat lengthFormat, Memory<byte> buffer, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, IUtf8SpanParsable<T>
    {
        var length = await stream.ReadLengthAsync(lengthFormat, buffer, token).ConfigureAwait(false);
        if (length > 0)
        {
            buffer = buffer.Slice(0, length);
            await stream.ReadExactlyAsync(buffer, token).ConfigureAwait(false);
        }
        else
        {
            buffer = Memory<byte>.Empty;
        }

        return T.Parse(buffer.Span, provider);
    }

    /// <summary>
    /// Parses the numeric value from UTF-8 encoded characters.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the string length (in bytes) encoded in the stream.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="style">A combination of number styles.</param>
    /// <param name="provider">Culture-specific formatting information.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The result of parsing.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<T> ParseAsync<T>(this Stream stream, LengthFormat lengthFormat, Memory<byte> buffer, NumberStyles style, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, INumberBase<T>
    {
        var length = await stream.ReadLengthAsync(lengthFormat, buffer, token).ConfigureAwait(false);
        if (length > 0)
        {
            buffer = buffer.Slice(0, length);
            await stream.ReadExactlyAsync(buffer, token).ConfigureAwait(false);
        }
        else
        {
            buffer = Memory<byte>.Empty;
        }

        return T.Parse(buffer.Span, style, provider);
    }

    /// <summary>
    /// Asynchronously reads the bytes from the source stream and passes them to the consumer, using a specified buffer.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="source">The source stream to read from.</param>
    /// <param name="consumer">The destination stream to write into.</param>
    /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
    /// <param name="token">The token that can be used to cancel this operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
    public static async ValueTask CopyToAsync<TConsumer>(this Stream source, TConsumer consumer, Memory<byte> buffer, CancellationToken token = default)
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        ThrowIfEmpty(buffer);

        for (int bytesRead; (bytesRead = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0;)
        {
            await consumer.Invoke(buffer.Slice(0, bytesRead), token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Asynchronously reads the bytes from the source stream and passes them to the consumer, using a specified buffer.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="source">The source stream to read from.</param>
    /// <param name="consumer">The destination stream to write into.</param>
    /// <param name="count">The number of bytes to copy.</param>
    /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
    /// <param name="token">The token that can be used to cancel this operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes.</exception>
    public static async ValueTask CopyToAsync<TConsumer>(this Stream source, TConsumer consumer, long count, Memory<byte> buffer, CancellationToken token = default)
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ThrowIfEmpty(buffer);

        for (int bytesRead; count > 0L; count -= bytesRead)
        {
            bytesRead = await source.ReadAsync(buffer.TrimLength(int.CreateSaturating(count)), token).ConfigureAwait(false);
            if (bytesRead <= 0)
                throw new EndOfStreamException();

            await consumer.Invoke(buffer.Slice(0, bytesRead), token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Asynchronously reads the bytes from the source stream and writes them to another stream, using a specified buffer.
    /// </summary>
    /// <param name="source">The source stream to read from.</param>
    /// <param name="destination">The destination stream to write into.</param>
    /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
    /// <param name="token">The token that can be used to cancel this operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask CopyToAsync(this Stream source, Stream destination, Memory<byte> buffer, CancellationToken token = default)
        => CopyToAsync<StreamConsumer>(source, destination, buffer, token);

    /// <summary>
    /// Asynchronously reads the bytes from the source stream and writes them to another stream, using a specified buffer.
    /// </summary>
    /// <param name="source">The source stream to read from.</param>
    /// <param name="destination">The destination stream to write into.</param>
    /// <param name="count">The number of bytes to copy.</param>
    /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
    /// <param name="token">The token that can be used to cancel this operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes.</exception>
    public static ValueTask CopyToAsync(this Stream source, Stream destination, long count, Memory<byte> buffer, CancellationToken token = default)
        => CopyToAsync<StreamConsumer>(source, destination, count, buffer, token);

    /// <summary>
    /// Asynchronously reads the bytes from the current stream and writes them to buffer
    /// writer, using a specified cancellation token.
    /// </summary>
    /// <param name="source">The source stream.</param>
    /// <param name="destination">The writer to which the contents of the current stream will be copied.</param>
    /// <param name="bufferSize">The size, in bytes, of the buffer.</param>
    /// <param name="token">The token to monitor for cancellation requests.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is negative or zero.</exception>
    /// <exception cref="NotSupportedException"><paramref name="source"/> doesn't support reading.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask CopyToAsync(this Stream source, IBufferWriter<byte> destination, int bufferSize = 0, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(bufferSize);

        for (int bytesRead; ; destination.Advance(bytesRead))
        {
            bytesRead = await source.ReadAsync(destination.GetMemory(bufferSize), token).ConfigureAwait(false);
            if (bytesRead <= 0)
                break;
        }
    }

    /// <summary>
    /// Asynchronously reads the bytes from the current stream and writes them to buffer
    /// writer, using a specified cancellation token.
    /// </summary>
    /// <param name="source">The source stream.</param>
    /// <param name="destination">The writer to which the contents of the current stream will be copied.</param>
    /// <param name="count">The number of bytes to copy.</param>
    /// <param name="bufferSize">The size, in bytes, of the buffer.</param>
    /// <param name="token">The token to monitor for cancellation requests.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> or <paramref name="count"/> is negative.</exception>
    /// <exception cref="NotSupportedException"><paramref name="source"/> doesn't support reading.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask CopyToAsync(this Stream source, IBufferWriter<byte> destination, long count, int bufferSize = 0, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfNegative(bufferSize);

        for (int bytesRead; count > 0L; destination.Advance(bytesRead), count -= bytesRead)
        {
            var buffer = destination.GetMemory(bufferSize).TrimLength(int.CreateSaturating(count));
            bytesRead = await source.ReadAsync(buffer, token).ConfigureAwait(false);
            if (bytesRead <= 0)
                throw new EndOfStreamException();
        }
    }

    /// <summary>
    /// Reads the stream sequentially.
    /// </summary>
    /// <remarks>
    /// The returned memory block should not be used between iterations.
    /// </remarks>
    /// <param name="stream">Readable stream.</param>
    /// <param name="bufferSize">The buffer size.</param>
    /// <param name="allocator">The allocator of the buffer.</param>
    /// <param name="token">The token that can be used to cancel the enumeration.</param>
    /// <returns>A collection of memory blocks that can be obtained sequentially to read a whole stream.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is less than 1.</exception>
    public static async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(this Stream stream, int bufferSize, MemoryAllocator<byte>? allocator = null, [EnumeratorCancellation] CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        using var bufferOwner = allocator.AllocateAtLeast(bufferSize);
        var buffer = bufferOwner.Memory;

        for (int count; (count = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0;)
            yield return buffer.Slice(0, count);
    }

    /// <summary>
    /// Decodes null-terminated UTF-8 encoded string asynchronously.
    /// </summary>
    /// <remarks>
    /// This method returns when end of stream or null char reached.
    /// </remarks>
    /// <param name="stream">The stream containing encoded string.</param>
    /// <param name="buffer">The buffer used to read from stream.</param>
    /// <param name="output">The output buffer for decoded characters.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of used bytes in <paramref name="buffer"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small to decode at least one character.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<int> ReadUtf8Async(this Stream stream, Memory<byte> buffer, IBufferWriter<char> output, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(output);

        if (Encoding.UTF8.GetMaxCharCount(buffer.Length) is 0)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

        int consumedBufferBytes, bytesRead, bufferOffset = 0;

        do
        {
            bytesRead = await stream.ReadAsync(buffer.Slice(bufferOffset), token).ConfigureAwait(false);
        }
        while (!DecodeUtf8(buffer.Span.Slice(0, bufferOffset + bytesRead), output, out consumedBufferBytes, out bufferOffset));

        return consumedBufferBytes;
    }

    /// <summary>
    /// Decodes null-terminated UTF-8 encoded string asynchronously.
    /// </summary>
    /// <remarks>
    /// This method returns when end of stream or null char reached.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to <paramref name="action"/>.</typeparam>
    /// <param name="stream">The stream containing encoded string.</param>
    /// <param name="bytesBuf">The buffer used to read from stream.</param>
    /// <param name="charsBuf">The buffer used to place decoded characters.</param>
    /// <param name="action">The callback to be executed for each decoded portion of char data.</param>
    /// <param name="arg">The argument to be passed to <paramref name="action"/>.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of used bytes in <paramref name="bytesBuf"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="bytesBuf"/> is too small to decode at least one character;
    /// or <paramref name="charsBuf"/> is empty.
    /// </exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public static async ValueTask<int> ReadUtf8Async<TArg>(this Stream stream, Memory<byte> bytesBuf, Memory<char> charsBuf, Func<ReadOnlyMemory<char>, TArg, CancellationToken, ValueTask> action, TArg arg, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(action);
        ThrowIfEmpty(charsBuf);

        if (Encoding.UTF8.GetMaxCharCount(bytesBuf.Length) is 0)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(bytesBuf));

        int consumedBufferBytes, bufferOffset = 0;
        bool completed;

        do
        {
            var bytesRead = await stream.ReadAsync(bytesBuf.Slice(bufferOffset), token).ConfigureAwait(false);
            completed = DecodeUtf8(bytesBuf.Span.Slice(0, bytesRead + bufferOffset), charsBuf.Span, out consumedBufferBytes, out bufferOffset, out var charsWritten);
            await action(charsBuf.Slice(0, charsWritten), arg, token).ConfigureAwait(false);
        }
        while (!completed);

        return consumedBufferBytes;
    }

    /// <summary>
    /// Decodes null-terminated UTF-8 encoded string synchronously.
    /// </summary>
    /// <remarks>
    /// This method returns when end of stream or null char reached.
    /// </remarks>
    /// <param name="stream">The stream containing encoded string.</param>
    /// <param name="buffer">The buffer used to read from stream.</param>
    /// <param name="output">The output buffer for decoded characters.</param>
    /// <returns>The number of used bytes in <paramref name="buffer"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small to decode at least one character.</exception>
    public static int ReadUtf8(this Stream stream, Span<byte> buffer, IBufferWriter<char> output)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(output);

        if (Encoding.UTF8.GetMaxCharCount(buffer.Length) is 0)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

        int consumedBufferBytes, bytesRead, bufferOffset = 0;

        do
        {
            bytesRead = stream.Read(buffer.Slice(bufferOffset));
        }
        while (!DecodeUtf8(buffer.Slice(0, bufferOffset + bytesRead), output, out consumedBufferBytes, out bufferOffset));

        return consumedBufferBytes;
    }

    /// <summary>
    /// Decodes null-terminated UTF-8 encoded string synchronously.
    /// </summary>
    /// <remarks>
    /// This method returns when end of stream or null char reached.
    /// </remarks>
    /// <typeparam name="TArg">The type of the argument to be passed to <paramref name="action"/>.</typeparam>
    /// <param name="stream">The stream containing encoded string.</param>
    /// <param name="bytesBuf">The buffer used to read from stream.</param>
    /// <param name="charsBuf">The buffer used to place decoded characters.</param>
    /// <param name="action">The callback to be executed for each decoded portion of char data.</param>
    /// <param name="arg">The argument to be passed to <paramref name="action"/>.</param>
    /// <returns>The number of used bytes in <paramref name="bytesBuf"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="bytesBuf"/> is too small to decode at least one character;
    /// or <paramref name="charsBuf"/> is empty.
    /// </exception>
    public static int ReadUtf8<TArg>(this Stream stream, Span<byte> bytesBuf, Span<char> charsBuf, ReadOnlySpanAction<char, TArg> action, TArg arg)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(action);

        if (Encoding.UTF8.GetMaxCharCount(bytesBuf.Length) is 0)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(bytesBuf));

        if (charsBuf.IsEmpty)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(charsBuf));

        int consumedBufferBytes, bytesRead, bufferOffset = 0;

        do
        {
            bytesRead = stream.Read(bytesBuf.Slice(bufferOffset));
        }
        while (!Decode(bytesBuf.Slice(0, bufferOffset + bytesRead), charsBuf, action, arg, out consumedBufferBytes, out bufferOffset));

        return consumedBufferBytes;

        static bool Decode(Span<byte> input, Span<char> output, ReadOnlySpanAction<char, TArg> action, TArg arg, out int inputBytes, out int inputOffset)
        {
            var result = DecodeUtf8(input, output, out inputBytes, out inputOffset, out var charsWritten);
            action(output.Slice(0, charsWritten), arg);
            return result;
        }
    }

    private static bool DecodeUtf8(Span<byte> input, IBufferWriter<char> output, out int inputBytes, out int inputOffset)
    {
        var result = DecodeUtf8(
            input,
            output.GetSpan(Encoding.UTF8.GetMaxCharCount(input.Length)),
            out inputBytes,
            out inputOffset,
            out var charsWritten);

        output.Advance(charsWritten);
        return result;
    }

    private static bool DecodeUtf8(Span<byte> input, Span<char> output, out int inputBytes, out int inputOffset, out int charsWritten)
    {
        bool flush;
        var nullCharIndex = input.IndexOf(DecodingContext.Utf8NullChar);

        if (nullCharIndex >= 0)
        {
            inputBytes = nullCharIndex + 1;
            input = input.Slice(0, nullCharIndex);
            flush = true;
        }
        else
        {
            inputBytes = input.Length;
            flush = input.IsEmpty;
        }

        switch (Utf8.ToUtf16(input, output, out var bytesRead, out charsWritten, replaceInvalidSequences: false, flush))
        {
            case OperationStatus.NeedMoreData:
                // we need more data, copy undecoded bytes to the beginning of the buffer
                var bufferTail = input.Slice(bytesRead);
                inputOffset = bufferTail.Length;
                bufferTail.CopyTo(input);
                break;
            case OperationStatus.Done:
                inputOffset = 0;
                break;
            default:
                throw new DecoderFallbackException();
        }

        return flush;
    }
}