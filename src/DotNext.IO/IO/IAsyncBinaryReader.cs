using System.Buffers;
using System.ComponentModel;
using System.Globalization;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext.IO;

using Buffers;
using Buffers.Binary;
using DecodingContext = Text.DecodingContext;
using PipeConsumer = Pipelines.PipeConsumer;

/// <summary>
/// Providers a uniform way to decode the data
/// from various sources such as streams, pipes, unmanaged memory etc.
/// </summary>
/// <seealso cref="IAsyncBinaryWriter"/>
public interface IAsyncBinaryReader
{
    /// <summary>
    /// Represents empty reader.
    /// </summary>
    public static IAsyncBinaryReader Empty => EmptyBinaryReader.Instance;

    /// <summary>
    /// Decodes the value of binary formattable type.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    ValueTask<T> ReadAsync<T>(CancellationToken token = default)
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
    ValueTask<T> ReadLittleEndianAsync<T>(CancellationToken token = default)
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
    ValueTask<T> ReadBigEndianAsync<T>(CancellationToken token = default)
        where T : notnull, IBinaryInteger<T>
    {
        var type = typeof(T);
        return type.IsPrimitive || type == typeof(Int128) || type == typeof(UInt128)
            ? ReadAsync<T, WellKnownIntegerReader<T>>(WellKnownIntegerReader<T>.BigEndian(), token)
            : ReadAsync<T, IntegerReader<T>>(IntegerReader<T>.BigEndian(), token);
    }

    /// <summary>
    /// Consumes memory block.
    /// </summary>
    /// <typeparam name="TReader">The type of the consumer.</typeparam>
    /// <param name="reader">The reader of the memory block.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The copy of <paramref name="reader"/>.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    ValueTask<TReader> ReadAsync<TReader>(TReader reader, CancellationToken token = default)
        where TReader : struct, IBufferReader;

    private async ValueTask<TResult> ReadAsync<TResult, TReader>(TReader reader, CancellationToken token)
        where TReader : struct, IBufferReader, ISupplier<TResult>
    {
        reader = await ReadAsync(reader, token).ConfigureAwait(false);
        return reader.Invoke();
    }

    private ValueTask<int> ReadLengthAsync(LengthFormat lengthFormat, CancellationToken token) => lengthFormat switch
    {
        LengthFormat.LittleEndian => ReadLittleEndianAsync<int>(token),
        LengthFormat.BigEndian => ReadBigEndianAsync<int>(token),
        LengthFormat.Compressed => ReadAsync<int, SevenBitEncodedInt.Reader>(new(), token),
        _ => ValueTask.FromException<int>(new ArgumentOutOfRangeException(nameof(lengthFormat))),
    };

    /// <summary>
    /// Reads the block of bytes.
    /// </summary>
    /// <param name="output">The block of memory to fill.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    async ValueTask ReadAsync(Memory<byte> output, CancellationToken token = default)
        => await ReadAsync<MemoryBlockReader>(new(output), token).ConfigureAwait(false);

    /// <summary>
    /// Skips the block of bytes.
    /// </summary>
    /// <param name="length">The length of the block to skip, in bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    async ValueTask SkipAsync(long length, CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        await ReadAsync<SkippingReader>(new(length), token).ConfigureAwait(false);
    }

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
    async ValueTask<MemoryOwner<byte>> ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
    {
        MemoryOwner<byte> result;
        var length = await ReadLengthAsync(lengthFormat, token).ConfigureAwait(false);
        if (length > 0)
        {
            result = allocator.AllocateExactly(length);
            await ReadAsync(result.Memory, token).ConfigureAwait(false);
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
    async ValueTask<MemoryOwner<char>> DecodeAsync(DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator = null, CancellationToken token = default)
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
    async IAsyncEnumerable<ReadOnlyMemory<char>> DecodeAsync(DecodingContext context, LengthFormat lengthFormat, Memory<char> buffer, [EnumeratorCancellation] CancellationToken token = default)
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
    /// Parses the sequence of characters.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to <paramref name="parser"/>.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
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
    async ValueTask<TResult> ParseAsync<TArg, TResult>(TArg arg, ReadOnlySpanFunc<char, TArg, TResult> parser, DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(parser);

        using var buffer = await DecodeAsync(context, lengthFormat, allocator, token).ConfigureAwait(false);
        return parser(buffer.Span, arg);
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
    async ValueTask<T> ParseAsync<T>(LengthFormat lengthFormat, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, IUtf8SpanParsable<T>
    {
        using var buffer = await ReadAsync(lengthFormat, allocator: null, token).ConfigureAwait(false);
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
    async ValueTask<T> ParseAsync<T>(LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, INumberBase<T>
    {
        using var buffer = await ReadAsync(lengthFormat, allocator: null, token).ConfigureAwait(false);
        return T.Parse(buffer.Span, style, provider);
    }

    /// <summary>
    /// Parses the numeric value.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="lengthFormat">The format of the string length (in bytes) encoded in the stream.</param>
    /// <param name="style">A combination of number styles.</param>
    /// <param name="provider">Culture-specific formatting information.</param>
    /// <param name="allocator">The allocator of internal buffer.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The result of parsing.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    ValueTask<T> ParseAsync<T>(DecodingContext context, LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider = null, MemoryAllocator<char>? allocator = null, CancellationToken token = default)
        where T : notnull, INumberBase<T>
        => ParseAsync((style, provider), Parse<T>, context, lengthFormat, allocator, token);

    internal static T Parse<T>(ReadOnlySpan<char> source, (NumberStyles, IFormatProvider?) args)
        where T : notnull, INumberBase<T>
        => T.Parse(source, args.Item1, args.Item2);

    /// <summary>
    /// Copies the content to the specified stream.
    /// </summary>
    /// <param name="output">The output stream receiving object content.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask CopyToAsync(Stream output, CancellationToken token = default)
        => CopyToAsync<StreamConsumer>(output, token);

    /// <summary>
    /// Copies the content to the specified pipe writer.
    /// </summary>
    /// <param name="output">The writer.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask CopyToAsync(PipeWriter output, CancellationToken token = default)
        => CopyToAsync<PipeConsumer>(output, token);

    /// <summary>
    /// Copies the content to the specified buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask CopyToAsync(IBufferWriter<byte> writer, CancellationToken token = default)
    {
        ValueTask result;
        if (TryGetSequence(out var sequence))
        {
            result = ValueTask.CompletedTask;
            try
            {
                foreach (var segment in sequence)
                {
                    writer.Write(segment.Span);
                    token.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException e)
            {
                result = ValueTask.FromCanceled(e.CancellationToken);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }
        else
        {
            result = CopyToAsync(new BufferConsumer<byte>(writer), token);
        }

        return result;
    }

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <param name="consumer">The content reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    ValueTask CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token = default)
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>;

    /// <summary>
    /// Attempts to get the entire content represented by this reader.
    /// </summary>
    /// <remarks>
    /// This method can be used for efficient synchronous decoding.
    /// </remarks>
    /// <param name="bytes">The content represented by this reader.</param>
    /// <returns><see langword="true"/> if the content is available synchronously; otherwise, <see langword="false"/>.</returns>
    bool TryGetSequence(out ReadOnlySequence<byte> bytes)
    {
        bytes = default;
        return false;
    }

    /// <summary>
    /// Attempts to get the number of bytes available for read.
    /// </summary>
    /// <param name="count">The number of bytes available for read.</param>
    /// <returns><see langword="true"/> if the method is supported; otherwise, <see langword="false"/>.</returns>
    bool TryGetRemainingBytesCount(out long count)
    {
        count = default;
        return false;
    }

    /// <summary>
    /// Creates default implementation of binary reader for the stream.
    /// </summary>
    /// <remarks>
    /// It is recommended to use extension methods from <see cref="StreamExtensions"/> class
    /// for decoding data from the stream. This method is intended for situation
    /// when you need an object implementing <see cref="IAsyncBinaryReader"/> interface.
    /// </remarks>
    /// <param name="input">The stream to be wrapped into the reader.</param>
    /// <param name="buffer">The buffer used for decoding data from the stream.</param>
    /// <returns>The stream reader.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
    public static IAsyncBinaryReader Create(Stream input, Memory<byte> buffer)
        => ReferenceEquals(input, Stream.Null) ? Empty : new AsyncStreamBinaryAccessor(input, buffer);

    /// <summary>
    /// Creates default implementation of binary reader over sequence of bytes.
    /// </summary>
    /// <param name="sequence">The sequence of bytes.</param>
    /// <returns>The binary reader for the sequence of bytes.</returns>
    public static SequenceReader Create(ReadOnlySequence<byte> sequence) => new(sequence);

    /// <summary>
    /// Creates default implementation of binary reader over contiguous memory block.
    /// </summary>
    /// <param name="memory">The block of memory.</param>
    /// <returns>The binary reader for the memory block.</returns>
    public static SequenceReader Create(ReadOnlyMemory<byte> memory) => new(memory);

    /// <summary>
    /// Creates default implementation of binary reader for the specifed pipe reader.
    /// </summary>
    /// <remarks>
    /// It is recommended to use extension methods from <see cref="Pipelines.PipeExtensions"/> class
    /// for decoding data from the stream. This method is intended for situation
    /// when you need an object implementing <see cref="IAsyncBinaryReader"/> interface.
    /// </remarks>
    /// <param name="reader">The pipe reader.</param>
    /// <returns>The binary reader.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    public static IAsyncBinaryReader Create(PipeReader reader) => new Pipelines.PipeBinaryReader(reader);
}