using System.Buffers;
using System.Numerics;

namespace DotNext.IO;

using Buffers;
using Buffers.Binary;
using Text;

public static partial class StreamExtensions
{
    private static int WriteLength(Span<byte> buffer, int length, LengthFormat lengthFormat)
    {
        var writer = new SpanWriter<byte>(buffer);
        return writer.WriteLength(length, lengthFormat);
    }

    /// <summary>
    /// Encodes formattable value as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="buffer">The buffer to be used for characters encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small to encode <paramref name="value"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask WriteAsync<T>(this Stream stream, T value, Memory<byte> buffer, CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
    {
        if (buffer.Length < T.Size)
            return ValueTask.FromException(new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer)));

        buffer = buffer.Slice(0, T.Size);
        value.Format(buffer.Span);
        return stream.WriteAsync(buffer, token);
    }

    /// <summary>
    /// Writes the integer to the stream in little-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The value to be written in little-endian format.</param>
    /// <param name="buffer">The buffer to be used for characters encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small to encode <paramref name="value"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask WriteLittleEndianAsync<T>(this Stream stream, T value, Memory<byte> buffer, CancellationToken token = default)
        where T : notnull, IBinaryInteger<T>
    {
        return value.TryWriteLittleEndian(buffer.Span, out var bytesWritten)
            ? stream.WriteAsync(buffer.Slice(0, bytesWritten), token)
            : ValueTask.FromException(new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer)));
    }

    /// <summary>
    /// Writes the integer to the stream in big-endian format.
    /// </summary>
    /// <typeparam name="T">The integer type.</typeparam>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The value to be written in big-endian format.</param>
    /// <param name="buffer">The buffer to be used for characters encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small to encode <paramref name="value"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask WriteBigEndianAsync<T>(this Stream stream, T value, Memory<byte> buffer, CancellationToken token = default)
        where T : notnull, IBinaryInteger<T>
    {
        return value.TryWriteBigEndian(buffer.Span, out var bytesWritten)
            ? stream.WriteAsync(buffer.Slice(0, bytesWritten), token)
            : ValueTask.FromException(new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer)));
    }

    /// <summary>
    /// Encodes the octet string asynchronously.
    /// </summary>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The octet string to encode.</param>
    /// <param name="lengthFormat">The format of the octet string length that must be inserted before the payload.</param>
    /// <param name="buffer">The buffer for internal I/O operations.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small for encoding minimal portion of <paramref name="value"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask WriteAsync(this Stream stream, ReadOnlyMemory<byte> value, LengthFormat lengthFormat, Memory<byte> buffer, CancellationToken token = default)
    {
        await stream.WriteAsync(buffer.Slice(0, WriteLength(buffer.Span, value.Length, lengthFormat)), token).ConfigureAwait(false);
        await stream.WriteAsync(value, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a length-prefixed or raw string to the stream asynchronously using supplied reusable buffer.
    /// </summary>
    /// <remarks>
    /// This method doesn't encode the length of the string.
    /// </remarks>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="chars">The string to be encoded.</param>
    /// <param name="context">The encoding context.</param>
    /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
    /// <param name="buffer">The buffer allocated by the caller needed for characters encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small for encoding minimal portion of <paramref name="chars"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<long> EncodeAsync(this Stream stream, ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, Memory<byte> buffer, CancellationToken token = default)
    {
        var maxChars = buffer.Length / context.Encoding.GetMaxByteCount(1);
        if (maxChars is 0)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

        long result;
        int bytesWritten;
        if (lengthFormat.HasValue)
        {
            result = bytesWritten = WriteLength(buffer.Span, context.Encoding.GetByteCount(chars.Span), lengthFormat.GetValueOrDefault());
            await stream.WriteAsync(buffer.Slice(0, bytesWritten), token).ConfigureAwait(false);
        }
        else
        {
            result = 0L;
        }

        var encoder = context.GetEncoder();
        for (int charsUsed; !chars.IsEmpty; chars = chars.Slice(charsUsed), result += bytesWritten)
        {
            encoder.Convert(chars.Span, buffer.Span, chars.Length <= maxChars, out charsUsed, out bytesWritten, out _);
            await stream.WriteAsync(buffer.Slice(0, bytesWritten), token).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Encodes formattable value as a set of characters using the specified encoding.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="context">The context describing encoding of characters.</param>
    /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
    /// <param name="buffer">The buffer to be used for characters encoding.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="allocator">A memory allocator for internal buffer of chars.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of written bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<long> FormatAsync<T>(this Stream stream, T value, EncodingContext context, LengthFormat? lengthFormat, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, MemoryAllocator<char>? allocator = null, CancellationToken token = default)
        where T : notnull, ISpanFormattable
    {
        ThrowIfEmpty(buffer);

        const int maxBufferSize = int.MaxValue / 2;

        for (var charBufferSize = SpanOwner<char>.StackallocThreshold; ; charBufferSize = charBufferSize <= maxBufferSize ? charBufferSize * 2 : throw new InsufficientMemoryException())
        {
            using var owner = allocator.AllocateAtLeast(charBufferSize);

            if (value.TryFormat(owner.Span, out var charsWritten, format, provider))
                return await EncodeAsync(stream, owner.Memory.Slice(0, charsWritten), context, lengthFormat, buffer, token).ConfigureAwait(false);

            charBufferSize = owner.Length;
        }
    }

    /// <summary>
    /// Encodes formattable value as a set of UTf-8 encoded characters.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="lengthFormat">String length encoding format.</param>
    /// <param name="buffer">The buffer to be used for characters encoding.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The number of written bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<int> FormatAsync<T>(this Stream stream, T value, LengthFormat? lengthFormat, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, IUtf8SpanFormattable
    {
        Memory<byte> bufferForLength;
        if (lengthFormat.HasValue)
        {
            bufferForLength = buffer.Slice(0, SevenBitEncodedInt.MaxSize);
            buffer = buffer.Slice(bufferForLength.Length);
        }
        else
        {
            bufferForLength = Memory<byte>.Empty;
        }

        if (!value.TryFormat(buffer.Span, out var dataSize, format, provider))
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

        if (!bufferForLength.IsEmpty)
        {
            bufferForLength = bufferForLength.Slice(0, WriteLength(bufferForLength.Span, dataSize, lengthFormat.GetValueOrDefault()));
            await stream.WriteAsync(bufferForLength, token).ConfigureAwait(false);
        }

        await stream.WriteAsync(buffer.Slice(0, dataSize), token).ConfigureAwait(false);
        return dataSize + bufferForLength.Length;
    }

    /// <summary>
    /// Writes sequence of bytes to the underlying stream asynchronously.
    /// </summary>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="sequence">The sequence of bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask WriteAsync(this Stream stream, ReadOnlySequence<byte> sequence, CancellationToken token = default)
    {
        foreach (var block in sequence)
            await stream.WriteAsync(block, token).ConfigureAwait(false);
    }
}