using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.IO;

using Buffers;
using Buffers.Binary;
using Collections.Generic;
using static Pipelines.PipeExtensions;
using DecodingContext = Text.DecodingContext;

/// <summary>
/// Represents binary reader for the sequence of bytes.
/// </summary>
/// <seealso cref="IAsyncBinaryReader.Create(ReadOnlySequence{byte})"/>
/// <seealso cref="IAsyncBinaryReader.Create(ReadOnlyMemory{byte})"/>
/// <remarks>
/// Initializes a new sequence reader.
/// </remarks>
/// <param name="sequence">A sequence of elements.</param>
[StructLayout(LayoutKind.Auto)]
public struct SequenceReader(ReadOnlySequence<byte> sequence) : IAsyncBinaryReader, IResettable
{
    private SequencePosition position = sequence.Start;

    internal SequenceReader(ReadOnlyMemory<byte> memory)
        : this(new ReadOnlySequence<byte>(memory))
    {
    }

    /// <summary>
    /// Resets the reader so it can be used again.
    /// </summary>
    public void Reset() => position = sequence.Start;

    /// <summary>
    /// Gets unread part of the sequence.
    /// </summary>
    public readonly ReadOnlySequence<byte> RemainingSequence => sequence.Slice(position);

    /// <summary>
    /// Gets position in the underlying sequence.
    /// </summary>
    public readonly SequencePosition Position => position;

    private void Read<TParser>(ref TParser parser)
        where TParser : struct, IBufferReader
    {
        position = parser.Append(RemainingSequence);
        parser.EndOfStream();
    }

    private TResult Read<TResult, TParser>(ref TParser parser)
        where TParser : struct, IBufferReader, ISupplier<TResult>
    {
        position = parser.Append(RemainingSequence);
        return parser.EndOfStream<TResult, TParser>();
    }

    /// <summary>
    /// Parses the value encoded as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <returns>The parsed value.</returns>
    public T Read<T>()
        where T : notnull, IBinaryFormattable<T>
    {
        if (T.Size <= BinaryFormattable256Reader<T>.MaxSize)
        {
            var parser = new BinaryFormattable256Reader<T>();
            return Read<T, BinaryFormattable256Reader<T>>(ref parser);
        }
        else
        {
            var parser = new BinaryFormattableReader<T>();
            return Read<T, BinaryFormattableReader<T>>(ref parser);
        }
    }

    /// <summary>
    /// Reads integer encoded in little-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <returns>The integer value.</returns>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public T ReadLittleEndian<T>()
        where T : notnull, IBinaryInteger<T>
    {
        var type = typeof(T);
        if (type.IsPrimitive || type == typeof(Int128) || type == typeof(UInt128))
        {
            var parser = WellKnownIntegerReader<T>.LittleEndian();
            return Read<T, WellKnownIntegerReader<T>>(ref parser);
        }
        else
        {
            var parser = IntegerReader<T>.LittleEndian();
            return Read<T, IntegerReader<T>>(ref parser);
        }
    }

    /// <summary>
    /// Reads integer encoded in big-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <returns>The integer value.</returns>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public T ReadBigEndian<T>()
        where T : notnull, IBinaryInteger<T>
    {
        var type = typeof(T);
        if (type.IsPrimitive || type == typeof(Int128) || type == typeof(UInt128))
        {
            var parser = WellKnownIntegerReader<T>.BigEndian();
            return Read<T, WellKnownIntegerReader<T>>(ref parser);
        }
        else
        {
            var parser = IntegerReader<T>.BigEndian();
            return Read<T, IntegerReader<T>>(ref parser);
        }
    }

    /// <summary>
    /// Copies the bytes from the sequence into contiguous block of memory.
    /// </summary>
    /// <param name="output">The block of memory to fill.</param>
    /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
    public void Read(Span<byte> output)
        => Read(output.Length).CopyTo(output);

    /// <summary>
    /// Reads single byte.
    /// </summary>
    /// <returns>A byte.</returns>
    /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
    public byte ReadByte()
        => MemoryMarshal.GetReference(Read(1).FirstSpan);

    /// <summary>
    /// Reads the specified number of bytes.
    /// </summary>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>The buffer of length <paramref name="count"/>.</returns>
    /// <exception cref="EndOfStreamException"><paramref name="count"/> is larger than the available buffer to read.</exception>
    public ReadOnlySequence<byte> Read(long count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        ReadOnlySequence<byte> result;
        try
        {
            result = sequence.Slice(position, count);
        }
        catch (ArgumentOutOfRangeException e)
        {
            throw new EndOfStreamException(e.Message, e);
        }

        position = result.End;
        return result;
    }

    /// <summary>
    /// Gets unread part of the buffer and advances the current reader
    /// to the end of the buffer.
    /// </summary>
    /// <returns>Unread part of the buffer.</returns>
    public ReadOnlySequence<byte> ReadToEnd()
    {
        var result = RemainingSequence;
        position = result.End;
        return result;
    }

    /// <summary>
    /// Indicates that the current reader has no more data to read.
    /// </summary>
    public readonly bool IsEmpty => position.Equals(sequence.End);

    /// <summary>
    /// Tries to read the next chunk.
    /// </summary>
    /// <param name="chunk">The chunk of bytes.</param>
    /// <returns><see langword="true"/> if the next chunk is obtained successfully; <see langword="false"/> if the end of the stream reached.</returns>
    public bool TryRead(out ReadOnlyMemory<byte> chunk)
    {
        var remaining = RemainingSequence;
        if (remaining.TryGet(ref position, out chunk, advance: false))
        {
            position = remaining.GetPosition(chunk.Length, position);
        }

        return !chunk.IsEmpty;
    }

    /// <summary>
    /// Tries to read the next chunk.
    /// </summary>
    /// <param name="maxLength">The maximum length of the requested chunk.</param>
    /// <param name="chunk">The chunk of bytes. Its length is less than or equal to <paramref name="maxLength"/>.</param>
    /// <returns><see langword="true"/> if next chunk is obtained successfully; <see langword="false"/> if the end of the stream reached.</returns>
    public bool TryRead(int maxLength, out ReadOnlyMemory<byte> chunk)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        if (RemainingSequence is { IsEmpty: false } remaining)
        {
            chunk = remaining.First.TrimLength(maxLength);
            position = remaining.GetPosition(chunk.Length);
        }
        else
        {
            chunk = default;
        }

        return !chunk.IsEmpty;
    }

    /// <summary>
    /// Skips the specified number of bytes.
    /// </summary>
    /// <param name="length">The number of bytes to skip.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
    public void Skip(long length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        try
        {
            position = sequence.GetPosition(length, position);
        }
        catch (ArgumentOutOfRangeException e)
        {
            throw new EndOfStreamException(e.Message, e);
        }
    }

    /// <summary>
    /// Reads length-prefixed block of bytes.
    /// </summary>
    /// <param name="lengthFormat">The format of the block length encoded in the underlying stream.</param>
    /// <param name="allocator">The memory allocator used to place the decoded block of bytes.</param>
    /// <returns>The decoded block of bytes.</returns>
    /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
    public MemoryOwner<byte> ReadBlock(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator = null)
    {
        var length = ReadLength(lengthFormat);
        MemoryOwner<byte> result;
        if (length > 0)
        {
            result = allocator.AllocateExactly(length);
            Read(result.Span);
        }
        else
        {
            result = default;
        }

        return result;
    }

    private int Read7BitEncodedInt32()
    {
        var parser = new SevenBitEncodedIntReader();
        return Read<int, SevenBitEncodedIntReader>(ref parser);
    }

    private int ReadLength(LengthFormat lengthFormat) => lengthFormat switch
    {
        LengthFormat.LittleEndian => ReadLittleEndian<int>(),
        LengthFormat.BigEndian => ReadBigEndian<int>(),
        LengthFormat.Compressed => Read7BitEncodedInt32(),
        _ => throw new ArgumentOutOfRangeException(nameof(lengthFormat)),
    };

    /// <summary>
    /// Decodes a block of characters.
    /// </summary>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public MemoryOwner<char> Decode(in DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator = null)
    {
        MemoryOwner<char> result;

        var length = ReadLength(lengthFormat);
        if (length > 0)
        {
            result = allocator.AllocateExactly(context.Encoding.GetMaxCharCount(length));
            var parser = new CharBufferDecodingReader(in context, length, result.Memory);
            result.TryResize(Read<int, CharBufferDecodingReader>(ref parser));
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
    /// <returns>The enumerator of characters.</returns>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    [UnscopedRef]
    public SpanDecodingEnumerable Decode(in DecodingContext context, LengthFormat lengthFormat, Span<char> buffer)
    {
        if (buffer.IsEmpty)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

        var length = ReadLength(lengthFormat);
        var bytes = RemainingSequence;
        try
        {
            bytes = bytes.Slice(0, length);
        }
        catch (ArgumentOutOfRangeException e)
        {
            throw new EndOfStreamException(e.Message, e);
        }

        return new(in bytes, ref position, context, buffer);
    }

    /// <summary>
    /// Decodes the sequence of characters.
    /// </summary>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="buffer">The buffer of characters.</param>
    /// <returns>The enumerator of characters.</returns>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public DecodingEnumerable Decode(in DecodingContext context, LengthFormat lengthFormat, Memory<char> buffer)
    {
        if (buffer.IsEmpty)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

        var length = ReadLength(lengthFormat);
        return new(Read(length), in context, in buffer);
    }

    /// <summary>
    /// Parses a block of characters.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to <paramref name="parser"/>.</typeparam>
    /// <typeparam name="TResult">The type of the parsing result.</typeparam>
    /// <param name="arg">The argument to be passed to <paramref name="parser"/>.</param>
    /// <param name="parser">The parser.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <returns>The parsed block of characters.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parser"/> is <see langword="null"/>.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public TResult Parse<TArg, TResult>(TArg arg, ReadOnlySpanFunc<char, TArg, TResult> parser, in DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator = null)
    {
        ArgumentNullException.ThrowIfNull(parser);

        using var buffer = Decode(context, lengthFormat, allocator);
        return parser(buffer.Span, arg);
    }

    private unsafe TResult Parse<TArg, TResult>(TArg arg, delegate*<ReadOnlySpan<byte>, TArg, TResult> parser, LengthFormat lengthFormat)
    {
        var length = ReadLength(lengthFormat);
        if (length is 0)
            return parser([], arg);

        if (length <= Parsing256Reader<IFormatProvider?, TResult>.MaxSize)
        {
            var reader = new Parsing256Reader<TArg, TResult>(arg, parser, length);
            return Read<TResult, Parsing256Reader<TArg, TResult>>(ref reader);
        }
        else
        {
            var reader = new ParsingReader<TArg, TResult>(arg, parser, length);
            return Read<TResult, ParsingReader<TArg, TResult>>(ref reader);
        }
    }

    /// <summary>
    /// Parses the sequence of characters encoded as UTF-8.
    /// </summary>
    /// <typeparam name="T">The type that supports parsing from UTF-8.</typeparam>
    /// <param name="lengthFormat">The format of the string length (in bytes) encoded in the stream.</param>
    /// <param name="provider">Culture-specific formatting information.</param>
    /// <returns>The result of parsing.</returns>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public T Parse<T>(LengthFormat lengthFormat, IFormatProvider? provider = null)
        where T : notnull, IUtf8SpanParsable<T>
    {
        unsafe
        {
            return Parse(provider, &T.Parse, lengthFormat);
        }
    }

    /// <summary>
    /// Parses the numeric value from UTF-8 encoded characters.
    /// </summary>
    /// <typeparam name="T">The numeric type.</typeparam>
    /// <param name="lengthFormat">The format of the string length (in bytes) encoded in the stream.</param>
    /// <param name="style">A combination of number styles.</param>
    /// <param name="provider">Culture-specific formatting information.</param>
    /// <returns>The result of parsing.</returns>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public T Parse<T>(LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider = null)
        where T : notnull, INumberBase<T>
    {
        unsafe
        {
            return Parse((style, provider), &ParseCore, lengthFormat);
        }

        static T ParseCore(ReadOnlySpan<byte> source, (NumberStyles, IFormatProvider?) args)
            => T.Parse(source, args.Item1, args.Item2);
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
    /// <returns>The result of parsing.</returns>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public T Parse<T>(in DecodingContext context, LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider = null, MemoryAllocator<char>? allocator = null)
        where T : notnull, INumberBase<T>
    {
        using var buffer = Decode(context, lengthFormat, allocator);
        return T.Parse(buffer.Span, style, provider);
    }

    /// <inheritdoc/>
    ValueTask<T> IAsyncBinaryReader.ReadAsync<T>(CancellationToken token)
    {
        ValueTask<T> result;

        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<T>(token);
        }
        else
        {
            try
            {
                result = new(Read<T>());
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<T>(e);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    ValueTask<T> IAsyncBinaryReader.ReadLittleEndianAsync<T>(CancellationToken token)
    {
        ValueTask<T> result;

        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<T>(token);
        }
        else
        {
            try
            {
                result = new(ReadLittleEndian<T>());
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<T>(e);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    ValueTask<T> IAsyncBinaryReader.ReadBigEndianAsync<T>(CancellationToken token)
    {
        ValueTask<T> result;

        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<T>(token);
        }
        else
        {
            try
            {
                result = new(ReadBigEndian<T>());
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<T>(e);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    ValueTask<TReader> IAsyncBinaryReader.ReadAsync<TReader>(TReader reader, CancellationToken token)
    {
        ValueTask<TReader> result;

        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<TReader>(token);
        }
        else
        {
            try
            {
                Read(ref reader);
                result = new(reader);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<TReader>(e);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
    {
        ValueTask result;

        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = ValueTask.CompletedTask;
            try
            {
                Read(output.Span);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    ValueTask IAsyncBinaryReader.SkipAsync(long length, CancellationToken token)
    {
        ValueTask result;

        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = ValueTask.CompletedTask;
            try
            {
                Skip(length);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    ValueTask<MemoryOwner<byte>> IAsyncBinaryReader.ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator, CancellationToken token)
    {
        ValueTask<MemoryOwner<byte>> result;

        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<MemoryOwner<byte>>(token);
        }
        else
        {
            try
            {
                result = new(ReadBlock(lengthFormat, allocator));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<MemoryOwner<byte>>(e);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    ValueTask<TResult> IAsyncBinaryReader.ParseAsync<TArg, TResult>(TArg arg, ReadOnlySpanFunc<char, TArg, TResult> parser, DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator, CancellationToken token)
    {
        ValueTask<TResult> result;

        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<TResult>(token);
        }
        else
        {
            try
            {
                result = new(Parse(arg, parser, context, lengthFormat, allocator));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<TResult>(e);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(LengthFormat lengthFormat, IFormatProvider? provider, CancellationToken token)
    {
        ValueTask<T> result;

        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<T>(token);
        }
        else
        {
            try
            {
                result = new(Parse<T>(lengthFormat, provider));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<T>(e);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider, CancellationToken token)
    {
        ValueTask<T> result;

        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<T>(token);
        }
        else
        {
            try
            {
                result = new(Parse<T>(lengthFormat, style, provider));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<T>(e);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(DecodingContext context, LengthFormat lengthFormat, NumberStyles style, IFormatProvider? provider, MemoryAllocator<char>? allocator, CancellationToken token)
    {
        ValueTask<T> result;

        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<T>(token);
        }
        else
        {
            try
            {
                result = new(Parse<T>(in context, lengthFormat, style, provider));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<T>(e);
            }
        }

        return result;
    }

    /// <inheritdoc />
    ValueTask<MemoryOwner<char>> IAsyncBinaryReader.DecodeAsync(DecodingContext context, LengthFormat lengthFormat, MemoryAllocator<char>? allocator, CancellationToken token)
    {
        ValueTask<MemoryOwner<char>> result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<MemoryOwner<char>>(token);
        }
        else
        {
            try
            {
                result = new(Decode(in context, lengthFormat, allocator));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<MemoryOwner<char>>(e);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    IAsyncEnumerable<ReadOnlyMemory<char>> IAsyncBinaryReader.DecodeAsync(DecodingContext context, LengthFormat lengthFormat, Memory<char> buffer,
        CancellationToken token)
        => Decode(context, lengthFormat, buffer);

    /// <inheritdoc/>
    ValueTask IAsyncBinaryReader.CopyToAsync(Stream destination, long? count, CancellationToken token)
    {
        ValueTask result;
        try
        {
            var remaining = count.HasValue
                ? Read(count.GetValueOrDefault())
                : ReadToEnd();

            result = destination.WriteAsync(remaining, token);
        }
        catch (Exception e)
        {
            result = ValueTask.FromException(e);
        }

        return result;
    }

    /// <inheritdoc/>
    ValueTask IAsyncBinaryReader.CopyToAsync(PipeWriter destination, long? count, CancellationToken token)
    {
        ValueTask result;
        try
        {
            var remaining = count.HasValue
                ? Read(count.GetValueOrDefault())
                : ReadToEnd();

            result = destination.WriteAsync(remaining, token);
        }
        catch (Exception e)
        {
            result = ValueTask.FromException(e);
        }

        return result;
    }

    /// <inheritdoc/>
    ValueTask IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, long? count, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = ValueTask.CompletedTask;
            try
            {
                writer.Write(count.HasValue ? Read(count.GetValueOrDefault()) : ReadToEnd());
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    async ValueTask IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, long? count, CancellationToken token)
    {
        foreach (var segment in count.HasValue ? Read(count.GetValueOrDefault()) : ReadToEnd())
            await consumer.Invoke(segment, token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    readonly bool IAsyncBinaryReader.TryGetSequence(out ReadOnlySequence<byte> bytes)
    {
        bytes = RemainingSequence;
        return true;
    }

    /// <inheritdoc />
    readonly bool IAsyncBinaryReader.TryGetRemainingBytesCount(out long count)
    {
        count = RemainingSequence.Length;
        return true;
    }

    /// <inheritdoc/>
    public readonly override string ToString() => RemainingSequence.ToString();

    /// <summary>
    /// Represents decoding enumerable.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct DecodingEnumerable : IEnumerable<ReadOnlyMemory<char>>, IAsyncEnumerable<ReadOnlyMemory<char>>
    {
        private readonly ReadOnlySequence<byte> bytes;
        private readonly DecodingContext context;
        private readonly Memory<char> buffer;

        internal DecodingEnumerable(ReadOnlySequence<byte> bytes, in DecodingContext context, in Memory<char> buffer)
        {
            Debug.Assert(!buffer.IsEmpty);

            this.bytes = bytes;
            this.context = context;
            this.buffer = buffer;
        }

        /// <summary>
        /// Gets enumerator over decoded chunks of characters.
        /// </summary>
        /// <returns>The enumerator over decoded chunks of characters.</returns>
        public Enumerator GetEnumerator() => new(in bytes, in context, buffer);
        
        /// <inheritdoc />
        IEnumerator<ReadOnlyMemory<char>> IEnumerable<ReadOnlyMemory<char>>.GetEnumerator()
            => GetEnumerator().ToClassicEnumerator<Enumerator, ReadOnlyMemory<char>>();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator().ToClassicEnumerator<Enumerator, ReadOnlyMemory<char>>();

        /// <inheritdoc />
        IAsyncEnumerator<ReadOnlyMemory<char>> IAsyncEnumerable<ReadOnlyMemory<char>>.GetAsyncEnumerator(CancellationToken token)
            => GetEnumerator().ToAsyncEnumerator<Enumerator, ReadOnlyMemory<char>>(token);

        /// <summary>
        /// Represents enumerator over decoded characters.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public struct Enumerator : IEnumerator<Enumerator, ReadOnlyMemory<char>>
        {
            private readonly ReadOnlySequence<byte> bytes;
            private readonly Decoder decoder;
            private readonly Memory<char> buffer;
            private SequencePosition position;
            private int charsWritten;

            internal Enumerator(in ReadOnlySequence<byte> bytes, in DecodingContext context, in Memory<char> buffer)
            {
                this.bytes = bytes;
                decoder = context.GetDecoder();
                this.buffer = buffer;
                position = bytes.Start;
            }

            /// <summary>
            /// Gets the current chunk of decoded characters.
            /// </summary>
            public readonly ReadOnlyMemory<char> Current => buffer.Slice(0, charsWritten);

            /// <summary>
            /// Decodes the next chunk of bytes.
            /// </summary>
            /// <returns><see langword="true"/> if decoding is successful; <see langword="false"/> if nothing to decode.</returns>
            public bool MoveNext()
                => (charsWritten = GetChars(in bytes, ref position, decoder, buffer.Span)) > 0;
        }
    }

    /// <summary>
    /// Represents decoding enumerable.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct SpanDecodingEnumerable
    {
        private readonly ReadOnlySequence<byte> bytes;
        private readonly DecodingContext context;
        private readonly Span<char> buffer;
        private readonly ref SequencePosition position;

        internal SpanDecodingEnumerable(scoped in ReadOnlySequence<byte> bytes, ref SequencePosition position, scoped in DecodingContext context, Span<char> buffer)
        {
            Debug.Assert(!buffer.IsEmpty);

            this.bytes = bytes;
            this.position = ref position;
            this.context = context;
            this.buffer = buffer;
        }

        /// <summary>
        /// Gets enumerator over decoded chunks of characters.
        /// </summary>
        /// <returns>The enumerator over decoded chunks of characters.</returns>
        public Enumerator GetEnumerator() => new(in bytes, ref position, context, buffer);

        /// <summary>
        /// Represents enumerator over decoded characters.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public ref struct Enumerator
        {
            private readonly ReadOnlySequence<byte> bytes;
            private readonly Decoder decoder;
            private readonly Span<char> buffer;
            private readonly ref SequencePosition position;
            private int charsWritten;

            internal Enumerator(scoped in ReadOnlySequence<byte> bytes, ref SequencePosition position, scoped in DecodingContext context, Span<char> buffer)
            {
                this.bytes = bytes;
                this.position = ref position;
                decoder = context.GetDecoder();
                this.buffer = buffer;
            }

            /// <summary>
            /// Gets the current chunk of decoded characters.
            /// </summary>
            public readonly ReadOnlySpan<char> Current => buffer.Slice(0, charsWritten);

            /// <summary>
            /// Decodes the next chunk of bytes.
            /// </summary>
            /// <returns><see langword="true"/> if decoding is successful; <see langword="false"/> if nothing to decode.</returns>
            public bool MoveNext()
                => (charsWritten = GetChars(in bytes, ref position, decoder, buffer)) > 0;
        }
    }

    private static int GetChars(in ReadOnlySequence<byte> bytes, ref SequencePosition position, Decoder decoder, Span<char> buffer)
    {
        int charsWritten;
        if (bytes.TryGet(ref position, out var source, advance: false) && !source.IsEmpty)
        {
            var length = Math.Min(source.Length, buffer.Length);
            position = bytes.GetPosition(length, position);

            charsWritten = decoder.GetChars(source.Span.Slice(0, length), buffer, position.Equals(bytes.End));
        }
        else
        {
            charsWritten = 0;
        }

        return charsWritten;
    }
}