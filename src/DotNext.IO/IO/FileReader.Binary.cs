using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.IO;

using Buffers;
using Text;
using PipeConsumer = Pipelines.PipeConsumer;

public partial class FileReader : IAsyncBinaryReader
{
    private T Read<T>()
        where T : unmanaged
    {
        var reader = new SpanReader<byte>(Buffer.Span);
        var result = reader.Read<T>();
        Consume(reader.ConsumedCount);
        return result;
    }

    /// <summary>
    /// Decodes the value of blittable type.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public async ValueTask<T> ReadAsync<T>(CancellationToken token = default)
        where T : unmanaged
    {
        if (Unsafe.SizeOf<T>() > BufferLength)
            await ReadAsync(token).ConfigureAwait(false);

        return Read<T>();
    }

    private int Read7BitEncodedInt()
    {
        var decoder = new SevenBitEncodedInt.Reader();
        var reader = new SpanReader<byte>(Buffer.Span);
        bool moveNext;
        do
        {
            var b = reader.Read();
            moveNext = b >= 0 ? decoder.Append((byte)b) : throw new EndOfStreamException();
        }
        while (moveNext);

        Consume(reader.ConsumedCount);
        return (int)decoder.Result;
    }

    private int ReadLength(LengthFormat lengthFormat)
    {
        int result;
        var littleEndian = BitConverter.IsLittleEndian;

        switch (lengthFormat)
        {
            default:
                throw new ArgumentOutOfRangeException(nameof(lengthFormat));
            case LengthFormat.Plain:
                result = Read<int>();
                break;
            case LengthFormat.PlainLittleEndian:
                littleEndian = true;
                goto case LengthFormat.Plain;
            case LengthFormat.PlainBigEndian:
                littleEndian = false;
                goto case LengthFormat.Plain;
            case LengthFormat.Compressed:
                result = Read7BitEncodedInt();
                break;
        }

        result.ReverseIfNeeded(littleEndian);
        return result;
    }

    /// <summary>
    /// Reads length-prefixed block of bytes.
    /// </summary>
    /// <param name="lengthFormat">The format of the block length encoded in the underlying stream.</param>
    /// <param name="allocator">The memory allocator used to place the decoded block of bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded block of bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public async ValueTask<MemoryOwner<byte>> ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
    {
        if (BufferLength < SevenBitEncodedInt.MaxSize)
            await ReadAsync(token).ConfigureAwait(false);

        var length = ReadLength(lengthFormat);
        MemoryOwner<byte> result;
        if (length > 0)
        {
            result = allocator.Invoke(length, true);
            await ReadBlockAsync(result.Memory, token).ConfigureAwait(false);
        }
        else
        {
            result = default;
        }

        return result;
    }

    private async ValueTask<int> ReadStringAsync(Memory<char> result, DecodingContext context, CancellationToken token)
    {
        var decoder = context.GetDecoder();
        var resultOffset = 0;

        for (int remainingBytes = result.Length, consumedBytes; remainingBytes > 0; Consume(consumedBytes), remainingBytes -= consumedBytes)
        {
            if (BufferLength < remainingBytes)
            {
                if (!await ReadAsync(token).ConfigureAwait(false))
                    throw new EndOfStreamException();
            }

            var buffer = Buffer.TrimLength(remainingBytes);
            resultOffset += decoder.GetChars(buffer.Span, result.Span.Slice(resultOffset), remainingBytes <= BufferLength);
            consumedBytes = buffer.Length;
        }

        return resultOffset;
    }

    /// <summary>
    /// Decodes the string.
    /// </summary>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded string.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public async ValueTask<string> ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token = default)
    {
        if (BufferLength < SevenBitEncodedInt.MaxSize)
            await ReadAsync(token).ConfigureAwait(false);

        var length = ReadLength(lengthFormat);

        if (length <= 0)
            return string.Empty;

        using var result = MemoryAllocator.Allocate<char>(length, true);
        length = await ReadStringAsync(result.Memory, context, token).ConfigureAwait(false);
        return new string(result.Memory.Span.Slice(0, length));
    }

    /// <summary>
    /// Parses the value encoded as a set of characters.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="parser">The parser.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="FormatException">The string is in wrong format.</exception>
    public async ValueTask<T> ParseAsync<T>(Parser<T> parser, LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull
    {
        if (BufferLength < SevenBitEncodedInt.MaxSize)
            await ReadAsync(token).ConfigureAwait(false);

        var length = ReadLength(lengthFormat);

        if (length <= 0)
            return parser(ReadOnlySpan<char>.Empty, provider);

        using var result = MemoryAllocator.Allocate<char>(length, true);
        length = await ReadStringAsync(result.Memory, context, token).ConfigureAwait(false);
        return parser(result.Memory.Span.Slice(0, length), provider);
    }

    /// <summary>
    /// Decodes an arbitrary integer value.
    /// </summary>
    /// <param name="lengthFormat">The format of the value length encoded in the underlying stream.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public async ValueTask<BigInteger> ReadBigIntegerAsync(LengthFormat lengthFormat, bool littleEndian, CancellationToken token = default)
    {
        if (BufferLength < SevenBitEncodedInt.MaxSize)
            await ReadAsync(token).ConfigureAwait(false);

        var length = ReadLength(lengthFormat);
        if (length <= 0)
            return BigInteger.Zero;

        using var block = MemoryAllocator.Allocate<byte>(length, true);
        await ReadBlockAsync(block.Memory, token).ConfigureAwait(false);
        return new BigInteger(block.Memory.Span, isBigEndian: !littleEndian);
    }

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <param name="consumer">The content reader.</param>
    /// <param name="count">The number of bytes to copy.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">Unable to read <paramref name="count"/> bytes.</exception>
    public async Task CopyToAsync<TConsumer>(TConsumer consumer, long count, CancellationToken token = default)
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        for (ReadOnlyMemory<byte> buffer; count > 0L && (HasBufferedData || await ReadAsync(token).ConfigureAwait(false)); Consume(buffer.Length))
        {
            buffer = Buffer;
            await consumer.Invoke(buffer, token).ConfigureAwait(false);
            count -= buffer.Length;
        }

        if (count > 0L)
            throw new EndOfStreamException();
    }

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <param name="consumer">The content reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async Task CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token = default)
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        for (ReadOnlyMemory<byte> buffer; HasBufferedData || await ReadAsync(token).ConfigureAwait(false); Consume(buffer.Length))
        {
            buffer = Buffer;
            await consumer.Invoke(buffer, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Copies the content to the specified stream.
    /// </summary>
    /// <param name="output">The output stream receiving object content.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task CopyToAsync(Stream output, CancellationToken token = default)
        => CopyToAsync<StreamConsumer>(output, token);

    /// <summary>
    /// Copies the content to the specified pipe writer.
    /// </summary>
    /// <param name="output">The writer.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task CopyToAsync(PipeWriter output, CancellationToken token = default)
        => CopyToAsync<PipeConsumer>(output, token);

    /// <summary>
    /// Copies the content to the specified buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task CopyToAsync(IBufferWriter<byte> writer, CancellationToken token = default)
        => CopyToAsync(new BufferConsumer<byte>(writer), token);

    /// <summary>
    /// Reads the block of bytes.
    /// </summary>
    /// <param name="output">The output buffer.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    /// <exception cref="EndOfStreamException">The expected block cannot be obtained.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask ReadBlockAsync(Memory<byte> output, CancellationToken token)
    {
        for (int writtenBytes; !output.IsEmpty; output = output.Slice(writtenBytes))
        {
            writtenBytes = await ReadAsync(output, token).ConfigureAwait(false);
            if (writtenBytes == 0)
                throw new EndOfStreamException();
        }
    }

    /// <inheritdoc />
    ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
        => ReadBlockAsync(output, token);

    /// <inheritdoc />
    ValueTask IAsyncBinaryReader.SkipAsync(int length, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = new();
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
}