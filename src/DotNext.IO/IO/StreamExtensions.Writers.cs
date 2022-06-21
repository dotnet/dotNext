using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace DotNext.IO;

using Buffers;
using Text;
using static Buffers.BufferReader;

public static partial class StreamExtensions
{
    private const int MaxBufferSize = int.MaxValue / 2;
    private const int InitialCharBufferSize = 128;

    [StructLayout(LayoutKind.Auto)]
    private readonly struct StreamWriter : IConsumer<byte>
    {
        private readonly Stream stream;

        internal StreamWriter(Stream stream) => this.stream = stream;

        void IConsumer<byte>.Invoke(byte value) => stream.WriteByte(value);
    }

    private static void Write7BitEncodedInt(this Stream stream, int value)
    {
        var writer = new StreamWriter(stream);
        SevenBitEncodedInt.Encode(ref writer, (uint)value);
    }

    private static ValueTask Write7BitEncodedIntAsync(this Stream stream, int value, Memory<byte> buffer, CancellationToken token)
    {
        var writer = new MemoryWriter(buffer);
        SevenBitEncodedInt.Encode(ref writer, (uint)value);
        return stream.WriteAsync(writer.Result, token);
    }

    private static void WriteLength(this Stream stream, int length, LengthFormat lengthFormat)
    {
        switch (lengthFormat)
        {
            default:
                throw new ArgumentOutOfRangeException(nameof(lengthFormat));
            case LengthFormat.Plain:
                stream.Write(length);
                break;
            case LengthFormat.PlainLittleEndian:
                length.ReverseIfNeeded(true);
                goto case LengthFormat.Plain;
            case LengthFormat.PlainBigEndian:
                length.ReverseIfNeeded(false);
                goto case LengthFormat.Plain;
            case LengthFormat.Compressed:
                stream.Write7BitEncodedInt(length);
                break;
        }
    }

    private static void WriteLength(this Stream stream, ReadOnlySpan<char> value, Encoding encoding, LengthFormat? lengthFormat)
    {
        if (lengthFormat.HasValue)
            WriteLength(stream, encoding.GetByteCount(value), lengthFormat.GetValueOrDefault());
    }

    /// <summary>
    /// Encodes the octet string.
    /// </summary>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The octet string to encode.</param>
    /// <param name="lengthFormat">The format of the octet string length that must be inserted before the payload.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static void WriteBlock(this Stream stream, ReadOnlySpan<byte> value, LengthFormat lengthFormat)
    {
        stream.WriteLength(value.Length, lengthFormat);
        stream.Write(value);
    }

    /// <summary>
    /// Writes a length-prefixed or raw string to the stream using supplied reusable buffer.
    /// </summary>
    /// <remarks>
    /// This method doesn't encode the length of the string.
    /// </remarks>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The string to be encoded.</param>
    /// <param name="context">The encoding.</param>
    /// <param name="buffer">The buffer allocated by the caller needed for characters encoding.</param>
    /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small for encoding minimal portion of <paramref name="value"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static void WriteString(this Stream stream, ReadOnlySpan<char> value, in EncodingContext context, Span<byte> buffer, LengthFormat? lengthFormat = null)
    {
        stream.WriteLength(value, context.Encoding, lengthFormat);
        if (value.IsEmpty)
            return;
        var encoder = context.GetEncoder();
        var maxChars = buffer.Length / context.Encoding.GetMaxByteCount(1);
        if (maxChars == 0)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
        for (int charsLeft = value.Length, charsUsed; charsLeft > 0; value = value.Slice(charsUsed), charsLeft -= charsUsed)
        {
            charsUsed = Math.Min(maxChars, charsLeft);
            encoder.Convert(value.Slice(0, charsUsed), buffer, charsUsed == charsLeft, out charsUsed, out var bytesUsed, out _);
            stream.Write(buffer.Slice(0, bytesUsed));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteLength(this Stream stream, int length, LengthFormat? lengthFormat)
    {
        switch (lengthFormat)
        {
            default:
                throw new ArgumentOutOfRangeException(nameof(lengthFormat));
            case null:
                break;
            case LengthFormat.Plain:
                stream.Write(length);
                break;
            case LengthFormat.PlainLittleEndian:
                length.ReverseIfNeeded(true);
                goto case LengthFormat.Plain;
            case LengthFormat.PlainBigEndian:
                length.ReverseIfNeeded(false);
                goto case LengthFormat.Plain;
            case LengthFormat.Compressed:
                stream.Write7BitEncodedInt(length);
                break;
        }
    }

    /// <summary>
    /// Encodes an arbitrary large integer as raw bytes.
    /// </summary>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <param name="lengthFormat">Indicates how the length of the BLOB must be encoded; or <see langword="null"/> to prevent length encoding.</param>
    [SkipLocalsInit]
    public static void WriteBigInteger(this Stream stream, in BigInteger value, bool littleEndian, LengthFormat? lengthFormat = null)
    {
        var bytesCount = value.GetByteCount();
        stream.WriteLength(bytesCount, lengthFormat);
        if (bytesCount == 0)
            return;

        using var buffer = (uint)bytesCount <= (uint)MemoryRental<byte>.StackallocThreshold ? stackalloc byte[bytesCount] : new MemoryRental<byte>(bytesCount);
        if (!value.TryWriteBytes(buffer.Span, out bytesCount, isBigEndian: !littleEndian))
            throw new InternalBufferOverflowException();

        stream.Write(buffer.Span.Slice(0, bytesCount));
    }

    /// <summary>
    /// Writes a length-prefixed or raw string to the stream.
    /// </summary>
    /// <remarks>
    /// This method doesn't encode the length of the string.
    /// </remarks>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The string to be encoded.</param>
    /// <param name="encoding">The string encoding.</param>
    /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    [SkipLocalsInit]
    public static void WriteString(this Stream stream, ReadOnlySpan<char> value, Encoding encoding, LengthFormat? lengthFormat = null)
    {
        var bytesCount = encoding.GetByteCount(value);
        stream.WriteLength(bytesCount, lengthFormat);
        if (bytesCount == 0)
            return;

        using MemoryRental<byte> buffer = (uint)bytesCount <= (uint)MemoryRental<byte>.StackallocThreshold ? stackalloc byte[bytesCount] : new MemoryRental<byte>(bytesCount);
        encoding.GetBytes(value, buffer.Span);
        stream.Write(buffer.Span);
    }

    private static bool TryWriteFormattable<T>(Stream stream, T value, LengthFormat lengthFormat, Encoding encoding, Span<char> buffer, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : notnull, ISpanFormattable
    {
        if (!value.TryFormat(buffer, out var charsWritten, format, provider))
            return false;

        WriteString(stream, buffer.Slice(0, charsWritten), encoding, lengthFormat);
        return true;
    }

    /// <summary>
    /// Encodes formattable value as a set of characters using the specified encoding.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="lengthFormat">String length encoding format.</param>
    /// <param name="encoding">The encoding of characters.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static void WriteFormattable<T>(this Stream stream, T value, LengthFormat lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : notnull, ISpanFormattable
    {
        // attempt to allocate char buffer on the stack
        Span<char> charBuffer = stackalloc char[InitialCharBufferSize];
        if (!TryWriteFormattable(stream, value, lengthFormat, encoding, charBuffer, format, provider))
        {
            for (var charBufferSize = InitialCharBufferSize * 2; ; charBufferSize = charBufferSize <= MaxBufferSize ? charBufferSize * 2 : throw new InsufficientMemoryException())
            {
                using var owner = new MemoryRental<char>(charBufferSize, false);
                if (TryWriteFormattable(stream, value, lengthFormat, encoding, owner.Span, format, provider))
                    break;
                charBufferSize = owner.Length;
            }
        }
    }

    /// <summary>
    /// Encodes formattable value as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The type value to be written as string.</param>
    [RequiresPreviewFeatures]
    public static void WriteFormattable<T>(this Stream stream, T value)
        where T : notnull, IBinaryFormattable<T>
    {
        using var buffer = (uint)T.Size <= (uint)MemoryRental<byte>.StackallocThreshold ? stackalloc byte[T.Size] : new MemoryRental<byte>(T.Size);
        var writer = new SpanWriter<byte>(buffer.Span);
        value.Format(ref writer);
        stream.Write(buffer.Span);
    }

    private static ValueTask WriteLengthAsync(this Stream stream, int length, LengthFormat lengthFormat, Memory<byte> buffer, CancellationToken token)
    {
        switch (lengthFormat)
        {
            default:
                throw new ArgumentOutOfRangeException(nameof(lengthFormat));
            case LengthFormat.Plain:
                return stream.WriteAsync(length, token);
            case LengthFormat.PlainLittleEndian:
                length.ReverseIfNeeded(true);
                goto case LengthFormat.Plain;
            case LengthFormat.PlainBigEndian:
                length.ReverseIfNeeded(false);
                goto case LengthFormat.Plain;
            case LengthFormat.Compressed:
                return stream.Write7BitEncodedIntAsync(length, buffer, token);
        }
    }

    private static ValueTask WriteLengthAsync(this Stream stream, ReadOnlySpan<char> value, Encoding encoding, LengthFormat? lengthFormat, Memory<byte> buffer, CancellationToken token)
        => lengthFormat.HasValue ? WriteLengthAsync(stream, encoding.GetByteCount(value), lengthFormat.GetValueOrDefault(), buffer, token) : new ValueTask();

    /// <summary>
    /// Encodes an arbitrary large integer as raw bytes.
    /// </summary>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <param name="buffer">The buffer for internal I/O operations.</param>
    /// <param name="lengthFormat">Indicates how the length of the BLOB must be encoded; or <see langword="null"/> to prevent length encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small for encoding minimal portion of <paramref name="value"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask WriteBigIntegerAsync(this Stream stream, BigInteger value, bool littleEndian, Memory<byte> buffer, LengthFormat? lengthFormat = null, CancellationToken token = default)
    {
        var bytesCount = value.GetByteCount();

        if (lengthFormat.HasValue)
            await stream.WriteLengthAsync(bytesCount, lengthFormat.GetValueOrDefault(), buffer, token).ConfigureAwait(false);

        if (bytesCount == 0)
            return;

        if (!value.TryWriteBytes(buffer.Span, out bytesCount, isBigEndian: !littleEndian))
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

        await stream.WriteAsync(buffer.Slice(0, bytesCount)).ConfigureAwait(false);
    }

    /// <summary>
    /// Encodes an arbitrary large integer as raw bytes.
    /// </summary>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <param name="allocator">The allocator of the temporary buffer used to place the bytes of an arbitrary large integer.</param>
    /// <param name="lengthFormat">Indicates how the length of the BLOB must be encoded; or <see langword="null"/> to prevent length encoding.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask WriteBigIntegerAsync(this Stream stream, BigInteger value, bool littleEndian, MemoryAllocator<byte>? allocator = null, LengthFormat? lengthFormat = null, CancellationToken token = default)
    {
        var bytesCount = value.GetByteCount();
        using var buffer = allocator.Invoke(Math.Max(1, bytesCount), false);

        if (lengthFormat.HasValue)
            await stream.WriteLengthAsync(bytesCount, lengthFormat.GetValueOrDefault(), buffer.Memory, token).ConfigureAwait(false);

        if (bytesCount == 0)
            return;

        if (!value.TryWriteBytes(buffer.Span, out bytesCount, isBigEndian: !littleEndian))
            throw new InternalBufferOverflowException();

        await stream.WriteAsync(buffer.Memory.Slice(0, bytesCount), token).ConfigureAwait(false);
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
    public static async ValueTask WriteBlockAsync(this Stream stream, ReadOnlyMemory<byte> value, LengthFormat lengthFormat, Memory<byte> buffer, CancellationToken token = default)
    {
        await stream.WriteLengthAsync(value.Length, lengthFormat, buffer, token).ConfigureAwait(false);
        await stream.WriteAsync(value, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a length-prefixed or raw string to the stream asynchronously using supplied reusable buffer.
    /// </summary>
    /// <remarks>
    /// This method doesn't encode the length of the string.
    /// </remarks>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The string to be encoded.</param>
    /// <param name="context">The encoding context.</param>
    /// <param name="buffer">The buffer allocated by the caller needed for characters encoding.</param>
    /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is too small for encoding minimal portion of <paramref name="value"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask WriteStringAsync(this Stream stream, ReadOnlyMemory<char> value, EncodingContext context, Memory<byte> buffer, LengthFormat? lengthFormat = null, CancellationToken token = default)
    {
        await stream.WriteLengthAsync(value.Span, context.Encoding, lengthFormat, buffer, token).ConfigureAwait(false);
        foreach (var chunk in context.GetBytes(value, buffer))
            await stream.WriteAsync(chunk, token).ConfigureAwait(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask WriteLengthAsync(this Stream stream, int length, LengthFormat? lengthFormat, Memory<byte> buffer, CancellationToken token)
    {
        switch (lengthFormat)
        {
            default:
                throw new ArgumentOutOfRangeException(nameof(lengthFormat));
            case null:
                return new ValueTask();
            case LengthFormat.Plain:
                return stream.WriteAsync(length, token);
            case LengthFormat.PlainLittleEndian:
                length.ReverseIfNeeded(true);
                goto case LengthFormat.Plain;
            case LengthFormat.PlainBigEndian:
                length.ReverseIfNeeded(false);
                goto case LengthFormat.Plain;
            case LengthFormat.Compressed:
                return stream.Write7BitEncodedIntAsync(length, buffer, token);
        }
    }

    /// <summary>
    /// Writes a length-prefixed or raw string to the stream asynchronously.
    /// </summary>
    /// <remarks>
    /// This method doesn't encode the length of the string.
    /// </remarks>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The string to be encoded.</param>
    /// <param name="encoding">The encoding context.</param>
    /// <param name="lengthFormat">Represents string length encoding format.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask WriteStringAsync(this Stream stream, ReadOnlyMemory<char> value, Encoding encoding, LengthFormat? lengthFormat = null, CancellationToken token = default)
    {
        var bytesCount = encoding.GetByteCount(value.Span);
        using var buffer = MemoryAllocator.Allocate<byte>(bytesCount, true);
        await stream.WriteLengthAsync(bytesCount, lengthFormat, buffer.Memory, token).ConfigureAwait(false);
        if (bytesCount == 0)
            return;

        encoding.GetBytes(value.Span, buffer.Span);
        await stream.WriteAsync(buffer.Memory, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Encodes formattable value as a set of characters using the specified encoding.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="lengthFormat">String length encoding format.</param>
    /// <param name="context">The context describing encoding of characters.</param>
    /// <param name="buffer">The buffer to be used for characters encoding.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask WriteFormattableAsync<T>(this Stream stream, T value, LengthFormat lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, ISpanFormattable
    {
        for (var charBufferSize = InitialCharBufferSize; ; charBufferSize = charBufferSize <= MaxBufferSize ? charBufferSize * 2 : throw new InsufficientMemoryException())
        {
            using var owner = MemoryAllocator.Allocate<char>(charBufferSize, false);

            if (value.TryFormat(owner.Span, out var charsWritten, format, provider))
            {
                await WriteStringAsync(stream, owner.Memory.Slice(0, charsWritten), context, buffer, lengthFormat, token).ConfigureAwait(false);
                break;
            }

            charBufferSize = owner.Length;
        }
    }

    /// <summary>
    /// Encodes formattable value as a set of characters using the specified encoding.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="lengthFormat">String length encoding format.</param>
    /// <param name="context">The context describing encoding of characters.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask WriteFormattableAsync<T>(this Stream stream, T value, LengthFormat lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull, ISpanFormattable
    {
        using var owner = MemoryAllocator.Allocate<byte>(DefaultBufferSize, false);
        await WriteFormattableAsync(stream, value, lengthFormat, context, owner.Memory, format, provider, token).ConfigureAwait(false);
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
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [RequiresPreviewFeatures]
    public static async ValueTask WriteFormattableAsync<T>(this Stream stream, T value, Memory<byte> buffer, CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
    {
        if (T.Size <= buffer.Length)
        {
            var tempBuffer = buffer.Slice(0, T.Size);
            IBinaryFormattable<T>.Format(value, tempBuffer.Span);
            await stream.WriteAsync(tempBuffer, token).ConfigureAwait(false);
        }
        else
        {
            using var tempBuffer = IBinaryFormattable<T>.Format(value);
            await stream.WriteAsync(tempBuffer.Memory, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Encodes formattable value as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [RequiresPreviewFeatures]
    public static async ValueTask WriteFormattableAsync<T>(this Stream stream, T value, CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
    {
        using var buffer = IBinaryFormattable<T>.Format(value);
        await stream.WriteAsync(buffer.Memory, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes sequence of bytes to the underlying stream synchronously.
    /// </summary>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="sequence">The sequence of bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static void Write(this Stream stream, in ReadOnlySequence<byte> sequence, CancellationToken token = default)
    {
        for (var position = sequence.Start; sequence.TryGet(ref position, out var block); token.ThrowIfCancellationRequested())
        {
            stream.Write(block.Span);
        }
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

    /// <summary>
    /// Serializes value to the stream.
    /// </summary>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The value to be written into the stream.</param>
    /// <typeparam name="T">The value type to be serialized.</typeparam>
    public static void Write<T>(this Stream stream, in T value)
        where T : unmanaged => stream.Write(Span.AsReadOnlyBytes(in value));

    /// <summary>
    /// Asynchronously serializes value to the stream.
    /// </summary>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The value to be written into the stream.</param>
    /// <param name="buffer">The buffer that is used for serialization.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <typeparam name="T">The value type to be serialized.</typeparam>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    public static ValueTask WriteAsync<T>(this Stream stream, T value, Memory<byte> buffer, CancellationToken token = default)
        where T : unmanaged
    {
        MemoryMarshal.Write(buffer.Span, ref value);
        return stream.WriteAsync(buffer.Slice(0, Unsafe.SizeOf<T>()), token);
    }

    /// <summary>
    /// Asynchronously serializes value to the stream.
    /// </summary>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="value">The value to be written into the stream.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <typeparam name="T">The value type to be serialized.</typeparam>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    public static async ValueTask WriteAsync<T>(this Stream stream, T value, CancellationToken token = default)
        where T : unmanaged
    {
        using var buffer = MemoryAllocator.Allocate<byte>(Unsafe.SizeOf<T>(), false);
        await WriteAsync(stream, value, buffer.Memory, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the memory blocks supplied by the specified delegate.
    /// </summary>
    /// <remarks>
    /// Copy process will be stopped when <paramref name="supplier"/> returns empty <see cref="ReadOnlyMemory{T}"/>.
    /// </remarks>
    /// <param name="stream">The destination stream.</param>
    /// <param name="supplier">The delegate supplying memory blocks.</param>
    /// <param name="arg">The argument to be passed to the supplier.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <typeparam name="TArg">The type of the argument to be passed to the supplier.</typeparam>
    /// <returns>The number of written bytes.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async Task<long> WriteAsync<TArg>(this Stream stream, Func<TArg, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> supplier, TArg arg, CancellationToken token = default)
    {
        var count = 0L;
        for (ReadOnlyMemory<byte> source; !(source = await supplier(arg, token).ConfigureAwait(false)).IsEmpty; count += source.Length)
            await stream.WriteAsync(source, token).ConfigureAwait(false);

        return count;
    }
}