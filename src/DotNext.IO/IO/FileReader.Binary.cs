using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.IO;

using Buffers;
using Text;
using PipeConsumer = Pipelines.PipeConsumer;

public partial class FileReader : IAsyncBinaryReader
{
    private bool TryRead<T>(out T value)
        where T : unmanaged
    {
        bool result;
        if (result = MemoryMarshal.TryRead(TrimLength(Buffer, length).Span, out value))
        {
            var count = Unsafe.SizeOf<T>();
            Consume(count);
            length -= count;
        }

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
    public ValueTask<T> ReadAsync<T>(CancellationToken token = default)
        where T : unmanaged
    {
        ValueTask<T> result;

        if (IsDisposed)
        {
            result = new(GetDisposedTask<T>());
        }
        else if (length < Unsafe.SizeOf<T>())
        {
            result = ValueTask.FromException<T>(new EndOfStreamException());
        }
        else if (BufferLength >= Unsafe.SizeOf<T>())
        {
            try
            {
                result = TryRead(out T value)
                    ? new(value)
                    : throw new EndOfStreamException();
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<T>(e);
            }
        }
        else
        {
            result = ReadSlowAsync<T>(token);
        }

        return result;
    }

    private async ValueTask<T> ReadSlowAsync<T>(CancellationToken token)
        where T : unmanaged
    {
        using var buffer = MemoryAllocator.Allocate<byte>(Unsafe.SizeOf<T>(), exactSize: true);
        await ReadBlockAsync(buffer.Memory, token).ConfigureAwait(false);
        return MemoryMarshal.Read<T>(buffer.Span);
    }

    private int Read7BitEncodedInt()
    {
        var decoder = new SevenBitEncodedInt.Reader();
        var reader = new SpanReader<byte>(BufferSpan);
        bool moveNext;
        do
        {
            var b = length > 0L ? reader.Read() : -1;
            moveNext = b >= 0 ? decoder.Append((byte)b) : throw new EndOfStreamException();
            length -= 1L;
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
                if (TryRead(out result))
                    break;
                throw new EndOfStreamException();
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
            if (BufferLength < remainingBytes && !await ReadAsync(token).ConfigureAwait(false))
                throw new EndOfStreamException();

            var buffer = TrimLength(Buffer.TrimLength(remainingBytes), length);
            resultOffset += decoder.GetChars(buffer.Span, result.Span.Slice(resultOffset), remainingBytes <= BufferLength);
            consumedBytes = buffer.Length;
            length -= consumedBytes;
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

        if (this.length < length)
            throw new EndOfStreamException();

        using var result = MemoryAllocator.Allocate<char>(length, true);
        length = await ReadStringAsync(result.Memory, context, token).ConfigureAwait(false);
        return new string(result.Span.Slice(0, length));
    }

    /// <summary>
    /// Decodes the string.
    /// </summary>
    /// <param name="length">The length of the encoded string, in bytes.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The decoded string.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public async ValueTask<string> ReadStringAsync(int length, DecodingContext context, CancellationToken token = default)
    {
        if (length == 0)
            return string.Empty;

        if (this.length < length)
            throw new EndOfStreamException();

        using var result = MemoryAllocator.Allocate<char>(length, true);
        length = await ReadStringAsync(result.Memory, context, token).ConfigureAwait(false);
        return new string(result.Span.Slice(0, length));
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

        if (this.length < length)
            throw new EndOfStreamException();

        using var result = MemoryAllocator.Allocate<char>(length, true);
        length = await ReadStringAsync(result.Memory, context, token).ConfigureAwait(false);
        return parser(result.Span.Slice(0, length), provider);
    }

    /// <summary>
    /// Parses the value encoded as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    public ValueTask<T> ParseAsync<T>(CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
    {
        ValueTask<T> result;

        if (IsDisposed)
        {
            result = new(GetDisposedTask<T>());
        }
        else if (length < T.Size)
        {
            result = ValueTask.FromException<T>(new EndOfStreamException());
        }
        else if (BufferLength >= T.Size)
        {
            try
            {
                result = new(ParseFast<T>());
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<T>(e);
            }
        }
        else
        {
            result = ParseSlowAsync<T>(token);
        }

        return result;
    }

    private T ParseFast<T>()
        where T : notnull, IBinaryFormattable<T>
    {
        Debug.Assert(BufferLength >= T.Size);

        var reader = new SpanReader<byte>(BufferSpan.Slice(0, T.Size));
        var result = T.Parse(ref reader);
        Consume(reader.ConsumedCount);
        length -= reader.ConsumedCount;
        return result;
    }

    private async ValueTask<T> ParseSlowAsync<T>(CancellationToken token)
        where T : notnull, IBinaryFormattable<T>
    {
        using var buffer = MemoryAllocator.Allocate<byte>(T.Size, exactSize: true);
        await ReadBlockAsync(buffer.Memory, token).ConfigureAwait(false);
        return IBinaryFormattable<T>.Parse(buffer.Span);
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

        if (this.length < length)
            throw new EndOfStreamException();

        using var block = MemoryAllocator.Allocate<byte>(length, exactSize: true);
        await ReadBlockAsync(block.Memory, token).ConfigureAwait(false);
        return new BigInteger(block.Span, isBigEndian: !littleEndian);
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
    public async ValueTask ReadBlockAsync(Memory<byte> output, CancellationToken token = default)
    {
        if (length < output.Length)
            throw new EndOfStreamException();

        output = TrimLength(output, length);

        for (int writtenBytes; !output.IsEmpty; output = output.Slice(writtenBytes))
        {
            writtenBytes = await ReadAsync(output, token).ConfigureAwait(false);

            if (writtenBytes == 0)
                throw new EndOfStreamException();

            length -= writtenBytes;
        }
    }

    /// <inheritdoc />
    ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
        => ReadBlockAsync(output, token);

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
            result = new();
            try
            {
                Skip(bytes);
                length -= bytes;
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