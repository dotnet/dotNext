using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.Buffers;

using EncodingContext = DotNext.Text.EncodingContext;
using LengthFormat = IO.LengthFormat;
using SevenBitEncodedInt = Binary.SevenBitEncodedInteger<uint>;

/// <summary>
/// Represents extension methods for writing typed data into buffer.
/// </summary>
public static class BufferWriter
{
    private const int MaxBufferSize = int.MaxValue / 2;

    /// <summary>
    /// Writes the sequence of elements to the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The sequence of elements to be written.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(this IBufferWriter<T> writer, in ReadOnlySequence<T> value)
    {
        if (value.IsSingleSegment)
        {
            writer.Write(value.FirstSpan);
        }
        else
        {
            WriteSlow(writer, in value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void WriteSlow(IBufferWriter<T> writer, in ReadOnlySequence<T> value)
        {
            foreach (var segment in value)
                writer.Write(segment.Span);
        }
    }

    internal static unsafe int WriteLength(this ref SpanWriter<byte> destination, int value, LengthFormat lengthFormat)
    {
        delegate*<ref SpanWriter<byte>, int, int> writer = lengthFormat switch
        {
            LengthFormat.LittleEndian => &ByteBuffer.WriteLittleEndian,
            LengthFormat.BigEndian => &ByteBuffer.WriteBigEndian,
            LengthFormat.Compressed => &Write7BitEncodedInteger,
            _ => throw new ArgumentOutOfRangeException(nameof(lengthFormat)),
        };

        return writer(ref destination, value);

        static int Write7BitEncodedInteger(ref SpanWriter<byte> writer, int value)
            => writer.Write7BitEncodedInteger((uint)value);
    }

    internal static int WriteLength(this IBufferWriter<byte> buffer, int length, LengthFormat lengthFormat)
    {
        var bytesWritten = WriteLength(buffer.GetSpan(SevenBitEncodedInt.MaxSizeInBytes), length, lengthFormat);
        buffer.Advance(bytesWritten);
        return bytesWritten;
    }

    internal static int WriteLength(Span<byte> buffer, int length, LengthFormat lengthFormat)
    {
        var writer = new SpanWriter<byte>(buffer);
        return writer.WriteLength(length, lengthFormat);
    }

    private static int WriteLength(this ref BufferWriterSlim<byte> buffer, int length, LengthFormat lengthFormat)
    {
        var bytesWritten = WriteLength(buffer.GetSpan(SevenBitEncodedInt.MaxSizeInBytes), length, lengthFormat);
        buffer.Advance(bytesWritten);
        return bytesWritten;
    }

    /// <summary>
    /// Encodes string using the specified encoding.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="chars">The sequence of characters.</param>
    /// <param name="context">The encoding context.</param>
    /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
    /// <returns>The number of written bytes.</returns>
    public static long Encode(this IBufferWriter<byte> writer, ReadOnlySpan<char> chars, in EncodingContext context, LengthFormat? lengthFormat = null)
    {
        var result = lengthFormat.HasValue
            ? writer.WriteLength(context.Encoding.GetByteCount(chars), lengthFormat.GetValueOrDefault())
            : 0L;

        context.GetEncoder().Convert(chars, writer, true, out var bytesWritten, out _);
        result += bytesWritten;

        return result;
    }

    /// <summary>
    /// Encodes string using the specified encoding.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="chars">The sequence of characters.</param>
    /// <param name="context">The encoding context.</param>
    /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
    /// <returns>The number of written bytes.</returns>
    public static int Encode(this ref SpanWriter<byte> writer, scoped ReadOnlySpan<char> chars, in EncodingContext context, LengthFormat? lengthFormat = null)
    {
        var result = lengthFormat.HasValue
            ? writer.WriteLength(context.Encoding.GetByteCount(chars), lengthFormat.GetValueOrDefault())
            : 0;

        var bytesWritten = context.TryGetEncoder() is { } encoder
            ? encoder.GetBytes(chars, writer.RemainingSpan, flush: true)
            : context.Encoding.GetBytes(chars, writer.RemainingSpan);
        result += bytesWritten;
        writer.Advance(bytesWritten);

        return result;
    }

    /// <summary>
    /// Writes a sequence of bytes prefixed with the length.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">A sequence of bytes to be written.</param>
    /// <param name="lengthFormat">A format of the buffer length to be written.</param>
    /// <returns>A number of bytes written.</returns>
    public static int Write(this ref SpanWriter<byte> writer, scoped ReadOnlySpan<byte> value, LengthFormat lengthFormat)
    {
        var result = writer.WriteLength(value.Length, lengthFormat);
        result += writer.Write(value);
        
        return result;
    }
    
    /// <summary>
    /// Encodes string using the specified encoding.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="chars">The sequence of characters.</param>
    /// <param name="context">The encoding context.</param>
    /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
    /// <returns>The number of written bytes.</returns>
    public static int Encode(this ref BufferWriterSlim<byte> writer, scoped ReadOnlySpan<char> chars, in EncodingContext context,
        LengthFormat? lengthFormat = null)
    {
        Span<byte> buffer;
        int byteCount, result;
        if (lengthFormat.HasValue)
        {
            byteCount = context.Encoding.GetByteCount(chars);
            result = writer.WriteLength(byteCount, lengthFormat.GetValueOrDefault());

            buffer = writer.GetSpan(byteCount);
            byteCount = context.TryGetEncoder() is { } encoder
                ? encoder.GetBytes(chars, buffer, flush: true)
                : context.Encoding.GetBytes(chars, buffer);

            result += byteCount;
            writer.Advance(byteCount);
        }
        else
        {
            result = 0;
            var encoder = context.GetEncoder();
            byteCount = context.Encoding.GetMaxByteCount(1);
            for (int charsUsed, bytesWritten; !chars.IsEmpty; chars = chars.Slice(charsUsed), result += bytesWritten)
            {
                buffer = writer.GetSpan(byteCount);
                var maxChars = buffer.Length / byteCount;

                encoder.Convert(chars, buffer, chars.Length <= maxChars, out charsUsed, out bytesWritten, out _);
                writer.Advance(bytesWritten);
            }
        }

        return result;
    }
    
    /// <summary>
    /// Writes a sequence of bytes prefixed with the length.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">A sequence of bytes to be written.</param>
    /// <param name="lengthFormat">A format of the buffer length to be written.</param>
    /// <returns>A number of bytes written.</returns>
    public static int Write(this ref BufferWriterSlim<byte> writer, scoped ReadOnlySpan<byte> value, LengthFormat lengthFormat)
    {
        var result = writer.WriteLength(value.Length, lengthFormat);
        writer.Write(value);
        result += value.Length;
        
        return result;
    }

    private static bool TryFormat<T>(IBufferWriter<byte> writer, T value, Span<char> buffer, in EncodingContext context, LengthFormat? lengthFormat, ReadOnlySpan<char> format, IFormatProvider? provider, out long bytesWritten)
        where T : notnull, ISpanFormattable
    {
        if (!value.TryFormat(buffer, out var charsWritten, format, provider))
        {
            bytesWritten = 0L;
            return false;
        }

        ReadOnlySpan<char> result = buffer.Slice(0, charsWritten);
        bytesWritten = lengthFormat.HasValue
            ? writer.WriteLength(context.Encoding.GetByteCount(result), lengthFormat.GetValueOrDefault())
            : 0L;

        context.GetEncoder().Convert(result, writer, true, out var bytesUsed, out _);
        bytesWritten += bytesUsed;
        return true;
    }

    /// <summary>
    /// Encodes formatted value as a set of characters using the specified encoding.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="context">The context describing encoding of characters.</param>
    /// <param name="lengthFormat">String length encoding format.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="allocator">The allocator of internal buffer of characters.</param>
    /// <returns>The number of written bytes.</returns>
    [SkipLocalsInit]
    public static long Format<T>(this IBufferWriter<byte> writer, T value, in EncodingContext context, LengthFormat? lengthFormat, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, MemoryAllocator<char>? allocator = null)
        where T : notnull, ISpanFormattable
    {
        // attempt to allocate char buffer on the stack
        Span<char> charBuffer = stackalloc char[SpanOwner<char>.StackallocThreshold];
        if (!TryFormat(writer, value, charBuffer, in context, lengthFormat, format, provider, out var bytesWritten))
        {
            for (var charBufferSize = charBuffer.Length << 1; ; charBufferSize = charBufferSize <= MaxBufferSize ? charBufferSize * 2 : throw new InsufficientMemoryException())
            {
                using var owner = allocator.AllocateAtLeast(charBufferSize);
                if (TryFormat(writer, value, owner.Span, in context, lengthFormat, format, provider, out bytesWritten))
                    break;

                charBufferSize = owner.Length;
            }
        }

        return bytesWritten;
    }

    /// <summary>
    /// Encodes formatted value as a set of UTF-8 bytes using the specified encoding.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="lengthFormat">String length encoding format.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>The number of written bytes.</returns>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough free space to place UTF-8 bytes.</exception>
    public static int Format<T>(this IBufferWriter<byte> writer, T value, LengthFormat? lengthFormat, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : notnull, IUtf8SpanFormattable
    {
        var expectedLengthSize = lengthFormat switch
        {
            null => 0,
            LengthFormat.BigEndian or LengthFormat.LittleEndian => sizeof(int),
            LengthFormat.Compressed => SevenBitEncodedInt.MaxSizeInBytes,
            _ => throw new ArgumentOutOfRangeException(nameof(lengthFormat)),
        };

        int bytesWritten;
        for (int bufferSize = 0; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
        {
            var buffer = writer.GetSpan(bufferSize);

            if (buffer.Length < expectedLengthSize || !value.TryFormat(buffer.Slice(expectedLengthSize), out bytesWritten, format, provider))
            {
                bufferSize = buffer.Length;
                continue;
            }

            var actualLengthSize = lengthFormat.HasValue
                ? WriteLength(buffer.Slice(0, expectedLengthSize), bytesWritten, lengthFormat.GetValueOrDefault())
                : 0;

            if (actualLengthSize < expectedLengthSize)
            {
                Debug.Assert(lengthFormat is LengthFormat.Compressed);

                // this is possible for Compressed format only
                buffer.Slice(expectedLengthSize).CopyTo(buffer.Slice(actualLengthSize));
            }

            writer.Advance(bytesWritten += actualLengthSize);
            break;
        }

        return bytesWritten;
    }

    /// <summary>
    /// Encodes formattable string as a sequence of bytes.
    /// </summary>
    /// <param name="writer">The output buffer.</param>
    /// <param name="context">The encoding context.</param>
    /// <param name="buffer">The preallocated buffer to be used for placing characters during encoding.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>The number of produced bytes.</returns>
    public static int Interpolate(this IBufferWriter<byte> writer, in EncodingContext context, Span<char> buffer, IFormatProvider? provider, [InterpolatedStringHandlerArgument(nameof(writer), nameof(context), nameof(buffer), nameof(provider))] in EncodingInterpolatedStringHandler handler)
        => handler.WrittenCount;

    /// <summary>
    /// Encodes formattable string as a sequence of bytes.
    /// </summary>
    /// <param name="writer">The output buffer.</param>
    /// <param name="context">The encoding context.</param>
    /// <param name="buffer">The preallocated buffer to be used for placing characters during encoding.</param>
    /// <param name="handler">The interpolated string handler.</param>
    /// <returns>The number of produced bytes.</returns>
    public static int Interpolate(this IBufferWriter<byte> writer, in EncodingContext context, Span<char> buffer, [InterpolatedStringHandlerArgument(nameof(writer), nameof(context), nameof(buffer))] in EncodingInterpolatedStringHandler handler)
        => Interpolate(writer, in context, buffer, provider: null, in handler);
}