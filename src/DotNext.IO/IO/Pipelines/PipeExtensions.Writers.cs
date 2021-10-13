using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.Versioning;
using System.Text;

namespace DotNext.IO.Pipelines;

using Buffers;
using Text;

public static partial class PipeExtensions
{
    /// <summary>
    /// Encodes value of blittable type.
    /// </summary>
    /// <typeparam name="T">The blittable type to encode.</typeparam>
    /// <param name="writer">The pipe writer.</param>
    /// <param name="value">The value to be encoded in binary form.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous result of operation.</returns>
    public static ValueTask<FlushResult> WriteAsync<T>(this PipeWriter writer, T value, CancellationToken token = default)
        where T : unmanaged
    {
        writer.Write(in value);
        return writer.FlushAsync(token);
    }

    /// <summary>
    /// Writes the memory blocks supplied by the specified delegate.
    /// </summary>
    /// <remarks>
    /// Copy process will be stopped when <paramref name="supplier"/> returns empty <see cref="ReadOnlyMemory{T}"/>.
    /// </remarks>
    /// <param name="writer">The pipe writer.</param>
    /// <param name="supplier">The delegate supplying memory blocks.</param>
    /// <param name="arg">The argument to be passed to the supplier.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <typeparam name="TArg">The type of the argument to be passed to the supplier.</typeparam>
    /// <returns>The number of written bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async Task<long> WriteAsync<TArg>(this PipeWriter writer, Func<TArg, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> supplier, TArg arg, CancellationToken token = default)
    {
        var count = 0L;
        for (ReadOnlyMemory<byte> source; !(source = await supplier(arg, token).ConfigureAwait(false)).IsEmpty;)
        {
            var result = await writer.WriteAsync(source, token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(token);
            count += source.Length;
            if (result.IsCompleted)
                break;
        }

        return count;
    }

    /// <summary>
    /// Encodes formattable value as a set of characters using the specified encoding.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="lengthFormat">String length encoding format.</param>
    /// <param name="context">The context describing encoding of characters.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static ValueTask<FlushResult> WriteFormattableAsync<T>(this PipeWriter writer, T value, LengthFormat lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, ISpanFormattable
    {
        writer.WriteFormattable(value, lengthFormat, in context, format, provider);
        return writer.FlushAsync(token);
    }

    /// <summary>
    /// Encodes formattable value as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [RequiresPreviewFeatures]
    public static ValueTask<FlushResult> WriteFormattableAsync<T>(this PipeWriter writer, T value, CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
    {
        writer.WriteFormattable(value);
        return writer.FlushAsync(token);
    }

    /// <summary>
    /// Encodes 64-bit signed integer asynchronously.
    /// </summary>
    /// <param name="writer">The pipe writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<FlushResult> WriteInt64Async(this PipeWriter writer, long value, bool littleEndian, CancellationToken token = default)
    {
        writer.WriteInt64(value, littleEndian);
        return writer.FlushAsync(token);
    }

    /// <summary>
    /// Encodes 64-bit unsigned integer asynchronously.
    /// </summary>
    /// <param name="writer">The pipe writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [CLSCompliant(false)]
    public static ValueTask<FlushResult> WriteUInt64Async(this PipeWriter writer, ulong value, bool littleEndian, CancellationToken token = default)
    {
        writer.WriteUInt64(value, littleEndian);
        return writer.FlushAsync(token);
    }

    /// <summary>
    /// Encodes 32-bit signed integer asynchronously.
    /// </summary>
    /// <param name="writer">The pipe writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<FlushResult> WriteInt32Async(this PipeWriter writer, int value, bool littleEndian, CancellationToken token = default)
    {
        writer.WriteInt32(value, littleEndian);
        return writer.FlushAsync(token);
    }

    /// <summary>
    /// Encodes 32-bit unsigned integer asynchronously.
    /// </summary>
    /// <param name="writer">The pipe writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [CLSCompliant(false)]
    public static ValueTask<FlushResult> WriteUInt32Async(this PipeWriter writer, uint value, bool littleEndian, CancellationToken token = default)
    {
        writer.WriteUInt32(value, littleEndian);
        return writer.FlushAsync(token);
    }

    /// <summary>
    /// Encodes 16-bit signed integer asynchronously.
    /// </summary>
    /// <param name="writer">The pipe writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<FlushResult> WriteInt16Async(this PipeWriter writer, short value, bool littleEndian, CancellationToken token = default)
    {
        writer.WriteInt16(value, littleEndian);
        return writer.FlushAsync(token);
    }

    /// <summary>
    /// Encodes 16-bit unsigned integer asynchronously.
    /// </summary>
    /// <param name="writer">The pipe writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [CLSCompliant(false)]
    public static ValueTask<FlushResult> WriteUInt16Async(this PipeWriter writer, ushort value, bool littleEndian, CancellationToken token = default)
    {
        writer.WriteUInt16(value, littleEndian);
        return writer.FlushAsync(token);
    }

    /// <summary>
    /// Encodes an arbitrary large integer as raw bytes.
    /// </summary>
    /// <param name="writer">The pipe to write into.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <param name="lengthFormat">Indicates how the length of the BLOB must be encoded; or <see langword="null"/> to prevent length encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static ValueTask<FlushResult> WriteBigIntegerAsync(this PipeWriter writer, BigInteger value, bool littleEndian, LengthFormat? lengthFormat, CancellationToken token = default)
    {
        writer.WriteBigInteger(in value, littleEndian, lengthFormat);
        return writer.FlushAsync(token);
    }

    private static ValueTask<FlushResult> WriteLengthAsync(this PipeWriter writer, ReadOnlyMemory<char> value, Encoding encoding, LengthFormat? lengthFormat, CancellationToken token)
    {
        if (lengthFormat is null)
            return new ValueTask<FlushResult>(new FlushResult(false, false));

        writer.WriteLength(value.Span, lengthFormat.GetValueOrDefault(), encoding);
        return writer.FlushAsync(token);
    }

    /// <summary>
    /// Encodes the string to bytes and write them to pipe asynchronously.
    /// </summary>
    /// <param name="writer">The pipe writer.</param>
    /// <param name="value">The block of characters to encode.</param>
    /// <param name="context">The text encoding context.</param>
    /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
    /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The result of operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    /// <exception cref="EndOfStreamException">Pipe closed unexpectedly.</exception>
    public static async ValueTask WriteStringAsync(this PipeWriter writer, ReadOnlyMemory<char> value, EncodingContext context, int bufferSize = 0, LengthFormat? lengthFormat = null, CancellationToken token = default)
    {
        var result = await writer.WriteLengthAsync(value, context.Encoding, lengthFormat, token).ConfigureAwait(false);
        result.ThrowIfCancellationRequested(token);
        if (value.IsEmpty)
            return;
        var encoder = context.GetEncoder();
        for (int charsLeft = value.Length, charsUsed, maxChars, bytesPerChar = context.Encoding.GetMaxByteCount(1); charsLeft > 0; value = value.Slice(charsUsed), charsLeft -= charsUsed)
        {
            if (result.IsCompleted)
                throw new EndOfStreamException();
            var buffer = writer.GetMemory(bufferSize);
            maxChars = buffer.Length / bytesPerChar;
            charsUsed = Math.Min(maxChars, charsLeft);
            encoder.Convert(value.Span.Slice(0, charsUsed), buffer.Span, charsUsed == charsLeft, out charsUsed, out var bytesUsed, out _);
            writer.Advance(bytesUsed);
            result = await writer.FlushAsync(token).ConfigureAwait(false);
            result.ThrowIfCancellationRequested(token);
        }
    }

    /// <summary>
    /// Writes sequence of bytes to the underlying stream asynchronously.
    /// </summary>
    /// <param name="writer">The pipe to write into.</param>
    /// <param name="sequence">The sequence of bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The actual number of bytes written to the pipe.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<long> WriteAsync(this PipeWriter writer, ReadOnlySequence<byte> sequence, CancellationToken token = default)
    {
        var count = 0L;
        var flushResult = new FlushResult(false, false);

        for (var position = sequence.Start; !flushResult.IsCompleted && sequence.TryGet(ref position, out var block); count += block.Length, flushResult.ThrowIfCancellationRequested(token))
        {
            flushResult = await writer.WriteAsync(block, token).ConfigureAwait(false);
        }

        return count;
    }

    /// <summary>
    /// Encodes the octet string asynchronously.
    /// </summary>
    /// <param name="writer">The pipe to write into.</param>
    /// <param name="input">The octet string to encode.</param>
    /// <param name="lengthFormat">The format of the octet string length that must be inserted before the payload.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static ValueTask<FlushResult> WriteBlockAsync(this PipeWriter writer, ReadOnlyMemory<byte> input, LengthFormat? lengthFormat, CancellationToken token = default)
    {
        if (lengthFormat.HasValue)
            writer.WriteLength(input.Length, lengthFormat.GetValueOrDefault());

        return writer.WriteAsync(input, token);
    }
}