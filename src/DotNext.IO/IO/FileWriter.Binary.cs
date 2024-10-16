using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using Encoder = System.Text.Encoder;

namespace DotNext.IO;

using Buffers;
using Buffers.Binary;
using Numerics;
using Text;
using static Pipelines.PipeExtensions;

public partial class FileWriter : IAsyncBinaryWriter
{
    private ValueTask WriteAsync<T>(T arg, SpanAction<byte, T> writer, int length, CancellationToken token)
    {
        ValueTask task;

        if (FreeCapacity >= length)
        {
            task = ValueTask.CompletedTask;
            try
            {
                writer(Buffer.Span, arg);
                bufferOffset += length;
            }
            catch (Exception e)
            {
                task = ValueTask.FromException(e);
            }
        }
        else if (MaxBufferSize >= length)
        {
            task = WriteBufferedAsync(arg, writer, length, token);
        }
        else
        {
            task = WriteDirectAsync(arg, writer, length, token);
        }

        return task;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask WriteBufferedAsync<T>(T arg, SpanAction<byte, T> writer, int length, CancellationToken token)
    {
        await FlushAsync(token).ConfigureAwait(false);
        writer(BufferSpan, arg);

        Debug.Assert(bufferOffset is 0);
        bufferOffset = length;
    }

    private async ValueTask WriteDirectAsync<T>(T arg, SpanAction<byte, T> writer, int length, CancellationToken token)
    {
        using var buffer = allocator.AllocateExactly(length);
        writer(buffer.Span, arg);
        await WriteDirectAsync(buffer.Memory, token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    Memory<byte> IAsyncBinaryWriter.Buffer => Buffer;

    /// <inheritdoc/>
    ValueTask IAsyncBinaryWriter.AdvanceAsync(int bytesWritten, CancellationToken token)
    {
        Produce(bytesWritten);
        return WriteAsync(token);
    }

    /// <summary>
    /// Encodes formattable value as a set of bytes.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WriteAsync<T>(T value, CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
        => WriteAsync(value, static (destination, value) => value.Format(destination), T.Size, token);

    /// <summary>
    /// Writes integer value in little-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="value">The value to be written in little-endian format.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WriteLittleEndianAsync<T>(T value, CancellationToken token = default)
        where T : notnull, IBinaryInteger<T>
    {
        return WriteAsync(value, Write, Number.GetMaxByteCount<T>(), token);

        static void Write(Span<byte> destination, T value)
        {
            if (!value.TryWriteLittleEndian(destination, out _))
                throw new InternalBufferOverflowException();
        }
    }

    /// <summary>
    /// Writes integer value in big-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="value">The value to be written in big-endian format.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WriteBigEndianAsync<T>(T value, CancellationToken token = default)
        where T : notnull, IBinaryInteger<T>
    {
        return WriteAsync(value, Write, Number.GetMaxByteCount<T>(), token);

        static void Write(Span<byte> destination, T value)
        {
            if (!value.TryWriteBigEndian(destination, out _))
                throw new InternalBufferOverflowException();
        }
    }

    private int WriteLength(int length, LengthFormat lengthFormat)
    {
        var writer = new SpanWriter<byte>(BufferSpan);
        writer.WriteLength(length, lengthFormat);
        Produce(writer.WrittenCount);
        return writer.WrittenCount;
    }

    /// <summary>
    /// Encodes a block of memory, optionally prefixed with the length encoded as a sequence of bytes
    /// according to the specified format.
    /// </summary>
    /// <param name="input">A block of memory.</param>
    /// <param name="lengthFormat">Indicates how the length of the BLOB must be encoded.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> input, LengthFormat lengthFormat, CancellationToken token = default)
    {
        if (FreeCapacity < SevenBitEncodedInt.MaxSize)
            await FlushAsync(token).ConfigureAwait(false);

        WriteLength(input.Length, lengthFormat);
        await WriteAsync(input, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Encodes a block of characters using the specified encoding.
    /// </summary>
    /// <param name="chars">The characters to encode.</param>
    /// <param name="context">The context describing encoding of characters.</param>
    /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of written bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public async ValueTask<long> EncodeAsync(ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, CancellationToken token = default)
    {
        long result;
        if (lengthFormat.HasValue)
        {
            if (FreeCapacity < SevenBitEncodedInt.MaxSize)
                await FlushAsync(token).ConfigureAwait(false);

            result = WriteLength(context.Encoding.GetByteCount(chars.Span), lengthFormat.GetValueOrDefault());
        }
        else
        {
            result = 0L;
        }

        if (chars.IsEmpty is false)
        {
            var maxByteCount = context.Encoding.GetMaxByteCount(1);
            var encoder = context.GetEncoder();

            for (int charsUsed, bytesUsed; !chars.IsEmpty; chars = chars.Slice(charsUsed), result += bytesUsed)
            {
                if (FreeCapacity < maxByteCount)
                    await FlushAsync(token).ConfigureAwait(false);

                Convert(encoder, chars.Span, BufferSpan, maxByteCount, chars.Length, out charsUsed, out bytesUsed);
                Produce(bytesUsed);
            }
        }

        return result;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Convert(Encoder encoder, ReadOnlySpan<char> input, Span<byte> output, int maxByteCount, int charsLeft, out int charsUsed, out int bytesUsed)
        {
            charsUsed = Math.Min(output.Length / maxByteCount, charsLeft);
            encoder.Convert(input.Slice(0, charsUsed), output, charsUsed == charsLeft, out charsUsed, out bytesUsed, out _);
        }
    }

    /// <summary>
    /// Encodes formatted value as a set of characters using the specified encoding.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="context">The context describing encoding of characters.</param>
    /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="allocator">Characters buffer allocator.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of written bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public async ValueTask<long> FormatAsync<T>(T value, EncodingContext context, LengthFormat? lengthFormat, string? format = null, IFormatProvider? provider = null, MemoryAllocator<char>? allocator = null, CancellationToken token = default)
        where T : notnull, ISpanFormattable
    {
        const int initialCharBufferSize = 128;
        const int maxBufferSize = int.MaxValue / 2;

        for (var charBufferSize = initialCharBufferSize; ; charBufferSize = charBufferSize <= maxBufferSize ? charBufferSize * 2 : throw new InsufficientMemoryException())
        {
            using var charBuffer = allocator.AllocateAtLeast(charBufferSize);

            if (value.TryFormat(charBuffer.Span, out var charsWritten, format, provider))
                return await EncodeAsync(charBuffer.Memory.Slice(0, charsWritten), context, lengthFormat, token).ConfigureAwait(false);

            charBufferSize = charBuffer.Length;
        }
    }

    /// <summary>
    /// Converts the value to UTF-8 encoded characters.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <param name="lengthFormat">String length encoding format.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of written bytes.</returns>
    /// <exception cref="InternalBufferOverflowException">The internal buffer cannot place all UTF-8 bytes exposed by <paramref name="value"/>.</exception>
    public ValueTask<int> FormatAsync<T>(T value, LengthFormat? lengthFormat, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, IUtf8SpanFormattable
    {
        return TryFormat(value, lengthFormat, format, provider, out var bytesWritten)
            ? ValueTask.FromResult(bytesWritten)
            : FormatSlowAsync(value, lengthFormat, format, provider, token);
    }

    private bool TryFormat<T>(T value, LengthFormat? lengthFormat, ReadOnlySpan<char> format, IFormatProvider? provider, out int bytesWritten)
        where T : notnull, IUtf8SpanFormattable
    {
        var expectedLengthSize = lengthFormat switch
        {
            null => 0,
            LengthFormat.BigEndian or LengthFormat.LittleEndian => sizeof(int),
            LengthFormat.Compressed => SevenBitEncodedInt.MaxSize,
            _ => throw new ArgumentOutOfRangeException(nameof(lengthFormat)),
        };

        var buffer = BufferSpan;
        bool result;
        if (result = value.TryFormat(buffer.Slice(expectedLengthSize), out bytesWritten, format, provider))
        {
            var actualLengthSize = lengthFormat.HasValue
                ? BufferWriter.WriteLength(buffer, bytesWritten, lengthFormat.GetValueOrDefault())
                : 0;

            if (actualLengthSize < expectedLengthSize)
            {
                Debug.Assert(lengthFormat is LengthFormat.Compressed);

                buffer.Slice(expectedLengthSize).CopyTo(buffer.Slice(actualLengthSize));
            }

            bytesWritten += actualLengthSize;
            Produce(bytesWritten);
        }

        return result;
    }

    private async ValueTask<int> FormatSlowAsync<T>(T value, LengthFormat? lengthFormat, string? format, IFormatProvider? provider, CancellationToken token)
        where T : notnull, IUtf8SpanFormattable
    {
        await FlushAsync(token).ConfigureAwait(false);
        if (!TryFormat(value, lengthFormat, format, provider, out var bytesWritten))
        {
            const int maxBufferSize = int.MaxValue / 2;
            for (var bufferSize = MaxBufferSize + SevenBitEncodedInt.MaxSize; ; bufferSize = bufferSize <= maxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
            {
                using var buffer = allocator.AllocateAtLeast(bufferSize);
                if (value.TryFormat(buffer.Span, out bytesWritten, format, provider))
                {
                    if (lengthFormat.HasValue)
                    {
                        bufferSize = WriteLength(bytesWritten, lengthFormat.GetValueOrDefault());
                        await WriteAsync(buffer.Memory.Slice(0, bytesWritten), token).ConfigureAwait(false);
                        bytesWritten += bufferSize;
                    }
                    else
                    {
                        await RandomAccess.WriteAsync(handle, buffer.Memory.Slice(0, bytesWritten), fileOffset, token).ConfigureAwait(false);
                        fileOffset += bytesWritten;
                    }

                    break;
                }

                bufferSize = buffer.Length;
            }
        }

        return bytesWritten;
    }

    /// <inheritdoc />
    ValueTask IAsyncBinaryWriter.WriteAsync(ReadOnlyMemory<byte> input, LengthFormat? lengthFormat, CancellationToken token)
        => lengthFormat is null ? WriteAsync(input, token) : WriteAsync(input, lengthFormat.GetValueOrDefault(), token);

    /// <summary>
    /// Writes the content from the specified stream.
    /// </summary>
    /// <param name="input">The stream to read from.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask CopyFromAsync(Stream input, CancellationToken token = default)
    {
        await WriteAsync(token).ConfigureAwait(false);

        var buffer = this.buffer.Memory;

        for (int bytesWritten; (bytesWritten = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0; fileOffset += bytesWritten)
        {
            await RandomAccess.WriteAsync(handle, buffer.Slice(0, bytesWritten), fileOffset, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes the content from the specified stream.
    /// </summary>
    /// <param name="source">The stream to read from.</param>
    /// <param name="count">The number of bytes to copy.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException"><paramref name="source"/> doesn't have enough data to read.</exception>
    public async ValueTask CopyFromAsync(Stream source, long count, CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        await WriteAsync(token).ConfigureAwait(false);

        var buffer = this.buffer.Memory;

        for (int bytesWritten; count > 0L; fileOffset += bytesWritten, count -= bytesWritten)
        {
            bytesWritten = await source.ReadAsync(buffer.TrimLength(int.CreateSaturating(count)), token).ConfigureAwait(false);
            if (bytesWritten <= 0)
                throw new EndOfStreamException();

            await RandomAccess.WriteAsync(handle, buffer.Slice(0, bytesWritten), fileOffset, token).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    ValueTask IAsyncBinaryWriter.CopyFromAsync(Stream source, long? count, CancellationToken token)
        => count.HasValue ? CopyFromAsync(source, count.GetValueOrDefault(), token) : CopyFromAsync(source, token);

    /// <inheritdoc/>
    ValueTask IAsyncBinaryWriter.CopyFromAsync(PipeReader source, long? count, CancellationToken token)
        => count.HasValue ? source.CopyToAsync(this, count.GetValueOrDefault(), token) : source.CopyToAsync(this, token);

    /// <inheritdoc />
    ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
        => WriteAsync(input, token);

    /// <inheritdoc />
    IBufferWriter<byte> IAsyncBinaryWriter.TryGetBufferWriter() => this;
}