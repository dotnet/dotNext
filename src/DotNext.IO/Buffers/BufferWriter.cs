using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.Buffers
{
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
                throw new System.IO.InternalBufferOverflowException();

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

        private static bool WriteString<T>(IBufferWriter<byte> writer, ref T value, Span<char> buffer, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format, IFormatProvider? provider, int bufferSize)
            where T : struct, ISpanFormattable
        {
            if (!value.TryFormat(buffer, out var charsWritten, format, provider))
                return false;

            ReadOnlySpan<char> result = buffer.Slice(0, charsWritten);
            WriteLength(writer, result, lengthFormat, context.Encoding);
            WriteString(writer, result, context.GetEncoder(), context.Encoding.GetMaxByteCount(1), bufferSize);
            return true;
        }

        [SkipLocalsInit]
        private static void Write<T>(IBufferWriter<byte> writer, ref T value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format, IFormatProvider? provider, int bufferSize)
            where T : struct, ISpanFormattable
        {
            const int initialCharBufferSize = 128;

            // attempt to allocate char buffer on the stack
            Span<char> charBuffer = stackalloc char[initialCharBufferSize];
            if (!WriteString(writer, ref value, charBuffer, lengthFormat, in context, format, provider, bufferSize))
            {
                for (var charBufferSize = initialCharBufferSize * 2; ; charBufferSize = charBufferSize <= MaxBufferSize ? charBufferSize * 2 : throw new InsufficientMemoryException())
                {
                    using var owner = new MemoryRental<char>(charBufferSize, false);
                    if (WriteString(writer, ref value, owner.Span, lengthFormat, in context, format, provider, bufferSize))
                        break;
                    charBufferSize = owner.Length;
                }
            }
        }

        /// <summary>
        /// Encodes 64-bit signed integer as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        public static void WriteInt64(this IBufferWriter<byte> writer, long value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes 64-bit unsigned integer as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        [CLSCompliant(false)]
        public static void WriteUInt64(this IBufferWriter<byte> writer, ulong value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes 32-bit signed integer as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        public static void WriteInt32(this IBufferWriter<byte> writer, int value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes 32-bit unsigned integer as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        [CLSCompliant(false)]
        public static void WriteUInt32(this IBufferWriter<byte> writer, uint value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes 16-bit signed integer as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        public static void WriteInt16(this IBufferWriter<byte> writer, short value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes 16-bit unsigned integer as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        [CLSCompliant(false)]
        public static void WriteUInt16(this IBufferWriter<byte> writer, ushort value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes 8-bit signed integer as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        [CLSCompliant(false)]
        public static void WriteSByte(this IBufferWriter<byte> writer, sbyte value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes 8-bit unsigned integer as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        public static void WriteByte(this IBufferWriter<byte> writer, byte value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes <see cref="decimal"/> as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        public static void WriteDecimal(this IBufferWriter<byte> writer, decimal value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes single-precision floating-point number as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        public static void WriteSingle(this IBufferWriter<byte> writer, float value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes double-precision floating-point number as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        public static void WriteDouble(this IBufferWriter<byte> writer, double value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes <see cref="Guid"/> as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        public static void WriteGuid(this IBufferWriter<byte> writer, Guid value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes <see cref="DateTime"/> as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        public static void WriteDateTime(this IBufferWriter<byte> writer, DateTime value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes <see cref="DateTimeOffset"/> as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        public static void WriteDateTimeOffset(this IBufferWriter<byte> writer, DateTimeOffset value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes <see cref="TimeSpan"/> as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        public static void WriteTimeSpan(this IBufferWriter<byte> writer, TimeSpan value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes <see cref="BigInteger"/> as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        public static void WriteBigInteger(this IBufferWriter<byte> writer, BigInteger value, LengthFormat lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write(writer, ref value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Writes line termination symbols to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        public static void WriteLine(this IBufferWriter<char> writer)
            => writer.Write(Environment.NewLine);

        /// <summary>
        /// Writes a string to the buffer, followed by a line terminator.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="characters">The characters to write.</param>
        public static void WriteLine(this IBufferWriter<char> writer, ReadOnlySpan<char> characters)
        {
            writer.Write(characters);
            writer.Write(Environment.NewLine);
        }

        private static void Write<T>(IBufferWriter<char> writer, ref T value, ReadOnlySpan<char> format, IFormatProvider? provider)
            where T : struct, ISpanFormattable
        {
            for (int bufferSize = 0; ;)
            {
                var buffer = writer.GetSpan(bufferSize);
                if (value.TryFormat(buffer, out var charsWritten, format, provider))
                {
                    writer.Advance(charsWritten);
                    break;
                }

                bufferSize = bufferSize <= MaxBufferSize ? buffer.Length * 2 : throw new InsufficientMemoryException();
            }
        }

        /// <summary>
        /// Writes string representation of 8-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteByte(this IBufferWriter<char> writer, byte value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 8-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteSByte(this IBufferWriter<char> writer, sbyte value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 16-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt16(this IBufferWriter<char> writer, short value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 16-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt16(this IBufferWriter<char> writer, ushort value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 32-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt32(this IBufferWriter<char> writer, int value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 32-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt32(this IBufferWriter<char> writer, uint value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 64-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt64(this IBufferWriter<char> writer, long value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 64-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt64(this IBufferWriter<char> writer, ulong value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="Guid"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteGuid(this IBufferWriter<char> writer, Guid value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="DateTime"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDateTime(this IBufferWriter<char> writer, DateTime value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="DateTimeOffset"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDateTimeOffset(this IBufferWriter<char> writer, DateTimeOffset value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="decimal"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDecimal(this IBufferWriter<char> writer, decimal value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of single-precision floating-point number to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteSingle(this IBufferWriter<char> writer, float value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of double-precision floating-point number to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDouble(this IBufferWriter<char> writer, double value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="TimeSpan"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteTimeSpan(this IBufferWriter<char> writer, TimeSpan value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, ref value, format, provider);

        private static void Write<T>(ref BufferWriterSlim<char> writer, ref T value, ReadOnlySpan<char> format, IFormatProvider? provider)
            where T : struct, ISpanFormattable
        {
            for (int bufferSize = 0; ;)
            {
                var buffer = writer.GetSpan(bufferSize);
                if (value.TryFormat(buffer, out var charsWritten, format, provider))
                {
                    writer.Advance(charsWritten);
                    break;
                }

                bufferSize = bufferSize <= MaxBufferSize ? buffer.Length * 2 : throw new InsufficientMemoryException();
            }
        }

        /// <summary>
        /// Writes line termination symbols to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        public static void WriteLine(this ref BufferWriterSlim<char> writer)
            => writer.Write(Environment.NewLine);

        /// <summary>
        /// Writes a string to the buffer, followed by a line terminator.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="characters">The characters to write.</param>
        public static void WriteLine(this ref BufferWriterSlim<char> writer, ReadOnlySpan<char> characters)
        {
            writer.Write(characters);
            writer.Write(Environment.NewLine);
        }

        /// <summary>
        /// Writes string representation of 8-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteByte(this ref BufferWriterSlim<char> writer, byte value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 8-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteSByte(this ref BufferWriterSlim<char> writer, sbyte value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 16-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt16(this ref BufferWriterSlim<char> writer, short value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 16-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt16(this ref BufferWriterSlim<char> writer, ushort value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 32-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt32(this ref BufferWriterSlim<char> writer, int value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 32-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt32(this ref BufferWriterSlim<char> writer, uint value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 64-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt64(this ref BufferWriterSlim<char> writer, long value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of 64-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt64(this ref BufferWriterSlim<char> writer, ulong value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="Guid"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteGuid(this ref BufferWriterSlim<char> writer, Guid value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="DateTime"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDateTime(this ref BufferWriterSlim<char> writer, DateTime value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="DateTimeOffset"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDateTimeOffset(this ref BufferWriterSlim<char> writer, DateTimeOffset value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="decimal"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDecimal(this ref BufferWriterSlim<char> writer, decimal value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of single-precision floating-point number to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteSingle(this ref BufferWriterSlim<char> writer, float value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of double-precision floating-point number to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDouble(this ref BufferWriterSlim<char> writer, double value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="TimeSpan"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteTimeSpan(this ref BufferWriterSlim<char> writer, TimeSpan value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(ref writer, ref value, format, provider);
    }
}