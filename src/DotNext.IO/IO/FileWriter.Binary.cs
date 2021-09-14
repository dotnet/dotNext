using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.IO;

using Buffers;
using Text;
using static Pipelines.ResultExtensions;

public partial class FileWriter : IAsyncBinaryWriter
{
    private void Write<T>(in T value)
        where T : unmanaged
    {
        var writer = new SpanWriter<byte>(Buffer.Span);
        writer.Write(in value);
        Produce(writer.WrittenCount);
    }

    /// <summary>
    /// Encodes value of blittable type.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="T">The type of the value to encode.</typeparam>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask WriteAsync<T>(T value, CancellationToken token = default)
        where T : unmanaged
    {
        if (Unsafe.SizeOf<T>() > FreeCapacity)
            await FlushCoreAsync(token).ConfigureAwait(false);

        Write(in value);
    }

    private void Write7BitEncodedInt(int value)
    {
        var writer = new MemoryWriter(Buffer);
        SevenBitEncodedInt.Encode(ref writer, (uint)value);
        Produce(writer.ConsumedBytes);
    }

    private void WriteLength(int length, LengthFormat lengthFormat)
    {
        switch (lengthFormat)
        {
            default:
                throw new ArgumentOutOfRangeException(nameof(lengthFormat));
            case LengthFormat.Plain:
                Write(in length);
                break;
            case LengthFormat.PlainLittleEndian:
                length.ReverseIfNeeded(true);
                goto case LengthFormat.Plain;
            case LengthFormat.PlainBigEndian:
                length.ReverseIfNeeded(false);
                goto case LengthFormat.Plain;
            case LengthFormat.Compressed:
                Write7BitEncodedInt(length);
                break;
        }
    }

    /// <summary>
    /// Encodes a block of memory, optionally prefixed with the length encoded as a sequence of bytes
    /// according with the specified format.
    /// </summary>
    /// <param name="input">A block of memory.</param>
    /// <param name="lengthFormat">Indicates how the length of the BLOB must be encoded.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> input, LengthFormat lengthFormat, CancellationToken token = default)
    {
        if (FreeCapacity < SevenBitEncodedInt.MaxSize)
            await FlushCoreAsync(token).ConfigureAwait(false);

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
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public async ValueTask WriteStringAsync(ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, CancellationToken token = default)
    {
        if (lengthFormat.HasValue)
        {
            if (FreeCapacity < SevenBitEncodedInt.MaxSize)
                await FlushCoreAsync(token).ConfigureAwait(false);

            WriteLength(context.Encoding.GetByteCount(chars.Span), lengthFormat.GetValueOrDefault());
        }

        if (chars.IsEmpty)
            return;

        var maxByteCount = context.Encoding.GetMaxByteCount(1);
        if (FreeCapacity < maxByteCount)
            await FlushCoreAsync(token).ConfigureAwait(false);

        var encoder = context.GetEncoder();

        for (int charsLeft = chars.Length, charsUsed; charsLeft > 0; chars = chars.Slice(charsUsed), charsLeft -= charsUsed)
        {
            if (FreeCapacity < maxByteCount)
                await FlushCoreAsync(token).ConfigureAwait(false);

            charsUsed = Math.Min(Buffer.Length / maxByteCount, charsLeft);
            encoder.Convert(chars.Span.Slice(0, charsUsed), Buffer.Span, charsUsed == charsLeft, out charsUsed, out var bytesUsed, out _);
            Produce(bytesUsed);
        }
    }

    /// <summary>
    /// Encodes an arbitrary large integer as raw bytes.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <param name="lengthFormat">Indicates how the length of the BLOB must be encoded; or <see langword="null"/> to prevent length encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async ValueTask WriteBigIntegerAsync(BigInteger value, bool littleEndian, LengthFormat? lengthFormat = null, CancellationToken token = default)
    {
        var bytesCount = value.GetByteCount();

        if (lengthFormat.HasValue)
        {
            if (FreeCapacity < SevenBitEncodedInt.MaxSize)
                await FlushCoreAsync(token).ConfigureAwait(false);

            WriteLength(bytesCount, lengthFormat.GetValueOrDefault());
        }

        if (bytesCount == 0)
            return;

        if (FreeCapacity < bytesCount)
            await FlushCoreAsync(token).ConfigureAwait(false);

        if (value.TryWriteBytes(Buffer.Span, out var bytesWritten, isBigEndian: !littleEndian))
        {
            Produce(bytesWritten);
        }
        else
        {
            Debug.Assert(bufferOffset == 0);
            using var buffer = MemoryAllocator.Allocate<byte>(bytesCount, true);
            value.TryWriteBytes(buffer.Memory.Span, out bytesWritten, isBigEndian: !littleEndian);
            await RandomAccess.WriteAsync(handle, buffer.Memory, fileOffset, token).ConfigureAwait(false);
            fileOffset += bytesCount;
        }
    }

    /// <summary>
    /// Encodes formatted value as a set of characters using the specified encoding.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="lengthFormat">String length encoding format.</param>
    /// <param name="context">The context describing encoding of characters.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public async ValueTask WriteFormattableAsync<T>(T value, LengthFormat lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, ISpanFormattable
    {
        const int initialCharBufferSize = 128;
        const int maxBufferSize = int.MaxValue / 2;

        for (var charBufferSize = initialCharBufferSize; ; charBufferSize = charBufferSize <= maxBufferSize ? charBufferSize * 2 : throw new InsufficientMemoryException())
        {
            using var charBuffer = MemoryAllocator.Allocate<char>(charBufferSize, false);

            if (value.TryFormat(charBuffer.Memory.Span, out var charsWritten, format, provider))
            {
                await WriteStringAsync(charBuffer.Memory.Slice(0, charsWritten), context, lengthFormat, token).ConfigureAwait(false);
                break;
            }

            charBufferSize = charBuffer.Length;
        }
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
    public async Task CopyFromAsync(Stream input, CancellationToken token = default)
    {
        await WriteAsync(token).ConfigureAwait(false);

        var buffer = this.buffer.Memory;

        for (int count; (count = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0; fileOffset += count)
        {
            await RandomAccess.WriteAsync(handle, buffer.Slice(0, count), fileOffset, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes the content from the specified pipe.
    /// </summary>
    /// <param name="input">The pipe to read from.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async Task CopyFromAsync(PipeReader input, CancellationToken token = default)
    {
        await WriteAsync(token).ConfigureAwait(false);

        ReadResult result;
        do
        {
            result = await input.ReadAsync(token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(token);
            var buffer = result.Buffer;
            for (SequencePosition position = buffer.Start; buffer.TryGet(ref position, out var block); input.AdvanceTo(position))
                await WriteAsync(block, token).ConfigureAwait(false);
        }
        while (!result.IsCompleted);
    }

    /// <inheritdoc />
    async Task IAsyncBinaryWriter.CopyFromAsync<TArg>(Func<TArg, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> supplier, TArg arg, CancellationToken token)
    {
        await WriteAsync(token).ConfigureAwait(false);

        for (ReadOnlyMemory<byte> source; !(source = await supplier(arg, token).ConfigureAwait(false)).IsEmpty;)
            await WriteAsync(source, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
        => WriteAsync(input, token);

    /// <inheritdoc />
    async ValueTask IAsyncBinaryWriter.WriteAsync<TArg>(Action<TArg, IBufferWriter<byte>> writer, TArg arg, CancellationToken token)
    {
        if (FreeCapacity == 0)
            await FlushCoreAsync(token).ConfigureAwait(false);

        var buffer = Buffer;
        using var output = new PreallocatedBufferWriter(buffer);
        writer(arg, output);

        var result = output.WrittenMemory;
        if (result.Length <= buffer.Length)
        {
            bufferOffset += result.Length;
        }
        else
        {
            await FlushCoreAsync(token).ConfigureAwait(false);
            await WriteAsync(result, token).ConfigureAwait(false);
        }
    }
}