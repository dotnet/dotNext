using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.Buffers;

using Text;
using LengthFormat = IO.LengthFormat;

/// <summary>
/// Represents extension methods for writting typed data into buffer.
/// </summary>
public static partial class BufferWriter
{
    private const int MaxBufferSize = int.MaxValue / 2;

    /// <summary>
    /// Encodes value of blittable type.
    /// </summary>
    /// <typeparam name="T">The blittable type to encode.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to write.</param>
    public static void Write<T>(this IBufferWriter<byte> writer, in T value)
        where T : unmanaged
        => writer.Write(Span.AsReadOnlyBytes(in value));

    /// <summary>
    /// Encodes an arbitrary large integer as raw bytes.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <param name="lengthFormat">Indicates how the length of the BLOB must be encoded; or <see langword="null"/> to prevent length encoding.</param>
    public static void WriteBigInteger(this IBufferWriter<byte> writer, in BigInteger value, bool littleEndian, LengthFormat? lengthFormat = null)
    {
        var length = value.GetByteCount();
        if (lengthFormat.HasValue)
            WriteLength(writer, length, lengthFormat.GetValueOrDefault());

        if (!value.TryWriteBytes(writer.GetSpan(length), out length, isBigEndian: !littleEndian))
            throw new InternalBufferOverflowException();

        writer.Advance(length);
    }

    /// <summary>
    /// Encodes 64-bit signed integer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    public static void WriteInt64(this IBufferWriter<byte> writer, long value, bool littleEndian)
    {
        value.ReverseIfNeeded(littleEndian);
        Write(writer, value);
    }

    /// <summary>
    /// Encodes 64-bit unsigned integer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    [CLSCompliant(false)]
    public static void WriteUInt64(this IBufferWriter<byte> writer, ulong value, bool littleEndian)
    {
        value.ReverseIfNeeded(littleEndian);
        Write(writer, value);
    }

    /// <summary>
    /// Encodes 32-bit signed integer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    public static void WriteInt32(this IBufferWriter<byte> writer, int value, bool littleEndian)
    {
        value.ReverseIfNeeded(littleEndian);
        Write(writer, value);
    }

    /// <summary>
    /// Encodes 32-bit unsigned integer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    [CLSCompliant(false)]
    public static void WriteUInt32(this IBufferWriter<byte> writer, uint value, bool littleEndian)
    {
        value.ReverseIfNeeded(littleEndian);
        Write(writer, value);
    }

    /// <summary>
    /// Encodes 16-bit signed integer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    public static void WriteInt16(this IBufferWriter<byte> writer, short value, bool littleEndian)
    {
        value.ReverseIfNeeded(littleEndian);
        Write(writer, value);
    }

    /// <summary>
    /// Encodes 16-bit unsigned integer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    [CLSCompliant(false)]
    public static void WriteUInt16(this IBufferWriter<byte> writer, ushort value, bool littleEndian)
    {
        value.ReverseIfNeeded(littleEndian);
        Write(writer, value);
    }

    internal static void Write7BitEncodedInt(this IBufferWriter<byte> output, int value)
    {
        var writer = new MemoryWriter(output.GetMemory(SevenBitEncodedInt.MaxSize));
        SevenBitEncodedInt.Encode(ref writer, (uint)value);
        output.Advance(writer.ConsumedBytes);
    }

    internal static void WriteLength(this IBufferWriter<byte> writer, int length, LengthFormat lengthFormat)
    {
        switch (lengthFormat)
        {
            default:
                throw new ArgumentOutOfRangeException(nameof(lengthFormat));
            case LengthFormat.PlainLittleEndian:
                length.ReverseIfNeeded(true);
                goto case LengthFormat.Plain;
            case LengthFormat.PlainBigEndian:
                length.ReverseIfNeeded(false);
                goto case LengthFormat.Plain;
            case LengthFormat.Plain:
                Write(writer, length);
                break;
            case LengthFormat.Compressed:
                Write7BitEncodedInt(writer, length);
                break;
        }
    }

    internal static void WriteLength(this IBufferWriter<byte> writer, ReadOnlySpan<char> value, LengthFormat lengthFormat, Encoding encoding)
        => WriteLength(writer, encoding.GetByteCount(value), lengthFormat);

    private static void WriteString(IBufferWriter<byte> writer, ReadOnlySpan<char> value, Encoder encoder, int bytesPerChar, int bufferSize)
    {
        for (int charsLeft = value.Length, charsUsed, maxChars; charsLeft > 0; value = value.Slice(charsUsed), charsLeft -= charsUsed)
        {
            var buffer = writer.GetMemory(bufferSize);
            if (buffer.Length < bytesPerChar)
                buffer = writer.GetMemory(bytesPerChar);

            maxChars = buffer.Length / bytesPerChar;
            charsUsed = Math.Min(maxChars, charsLeft);
            encoder.Convert(value.Slice(0, charsUsed), buffer.Span, charsUsed == charsLeft, out charsUsed, out var bytesUsed, out _);
            writer.Advance(bytesUsed);
        }
    }

    /// <summary>
    /// Encodes string using the specified encoding.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The sequence of characters.</param>
    /// <param name="context">The encoding context.</param>
    /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
    /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
    public static void WriteString(this IBufferWriter<byte> writer, ReadOnlySpan<char> value, in EncodingContext context, int bufferSize = 0, LengthFormat? lengthFormat = null)
    {
        if (lengthFormat.HasValue)
            WriteLength(writer, value, lengthFormat.GetValueOrDefault(), context.Encoding);

        if (!value.IsEmpty)
            WriteString(writer, value, context.GetEncoder(), context.Encoding.GetMaxByteCount(1), bufferSize);
    }

    private static bool TryWriteFormattable<T>(IBufferWriter<byte> writer, T value, Span<char> buffer, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format, IFormatProvider? provider, int bufferSize)
        where T : notnull, ISpanFormattable
    {
        if (!value.TryFormat(buffer, out var charsWritten, format, provider))
            return false;

        ReadOnlySpan<char> result = buffer.Slice(0, charsWritten);
        WriteLength(writer, result, lengthFormat, context.Encoding);
        WriteString(writer, result, context.GetEncoder(), context.Encoding.GetMaxByteCount(1), bufferSize);
        return true;
    }

    /// <summary>
    /// Encodes formatted value as a set of characters using the specified encoding. 
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="lengthFormat">String length encoding format.</param>
    /// <param name="context">The context describing encoding of characters.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="bufferSize">The buffer size to be rented from the writer.</param>
    [SkipLocalsInit]
    public static void WriteFormattable<T>(this IBufferWriter<byte> writer, T value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
        where T : notnull, ISpanFormattable
    {
        const int initialCharBufferSize = 128;

        // attempt to allocate char buffer on the stack
        Span<char> charBuffer = stackalloc char[initialCharBufferSize];
        if (!TryWriteFormattable(writer, value, charBuffer, lengthFormat, in context, format, provider, bufferSize))
        {
            for (var charBufferSize = initialCharBufferSize * 2; ; charBufferSize = charBufferSize <= MaxBufferSize ? charBufferSize * 2 : throw new InsufficientMemoryException())
            {
                using var owner = new MemoryRental<char>(charBufferSize, false);
                if (TryWriteFormattable(writer, value, owner.Span, lengthFormat, in context, format, provider, bufferSize))
                    break;
                charBufferSize = owner.Length;
            }
        }
    }
}