using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Buffers;
    using Text;
    using static Buffers.BufferReader;
    using static Buffers.BufferWriter;

    /// <summary>
    /// Represents high-level read/write methods for the stream.
    /// </summary>
    /// <remarks>
    /// This class provides alternative way to read and write typed data from/to the stream
    /// without instantiation of <see cref="BinaryReader"/> and <see cref="BinaryWriter"/>.
    /// </remarks>
    public static class StreamExtensions
    {
        private const int BufferSizeForLength = 5;
        private const int MaxBufferSize = int.MaxValue / 2;
        private const int InitialCharBufferSize = 128;
        private const int DefaultBufferSize = 256;

        [StructLayout(LayoutKind.Auto)]
        private readonly struct StreamWriter : SevenBitEncodedInt.IWriter
        {
            private readonly Stream stream;

            internal StreamWriter(Stream stream) => this.stream = stream;

            void SevenBitEncodedInt.IWriter.WriteByte(byte value) => stream.WriteByte(value);
        }

        [StructLayout(LayoutKind.Auto)]
        private struct BufferedMemoryWriter : SevenBitEncodedInt.IWriter
        {
            private readonly Memory<byte> buffer;
            private int offset;

            internal BufferedMemoryWriter(Memory<byte> output)
            {
                buffer = output;
                offset = 0;
            }

            internal readonly Memory<byte> Result => buffer.Slice(0, offset);

            void SevenBitEncodedInt.IWriter.WriteByte(byte value) => buffer.Span[offset++] = value;
        }

        private static void Write7BitEncodedInt(this Stream stream, int value)
        {
            var writer = new StreamWriter(stream);
            SevenBitEncodedInt.Encode(ref writer, (uint)value);
        }

        private static ValueTask Write7BitEncodedIntAsync(this Stream stream, int value, Memory<byte> buffer, CancellationToken token)
        {
            var writer = new BufferedMemoryWriter(buffer);
            SevenBitEncodedInt.Encode(ref writer, (uint)value);
            return stream.WriteAsync(writer.Result, token);
        }

        private static int Read7BitEncodedInt(this Stream stream)
        {
            var reader = new SevenBitEncodedInt.Reader();
            bool moveNext;
            do
            {
                var b = stream.ReadByte();
                moveNext = b >= 0 ? reader.Append((byte)b) : throw new EndOfStreamException();
            }
            while (moveNext);
            return (int)reader.Result;
        }

        private static async ValueTask<int> Read7BitEncodedIntAsync(this Stream stream, Memory<byte> buffer, CancellationToken token)
        {
            buffer = buffer.Slice(0, 1);
            var reader = new SevenBitEncodedInt.Reader();
            for (var moveNext = true; moveNext; moveNext = reader.Append(buffer.Span[0]))
            {
                var count = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
                if (count == 0)
                    throw new EndOfStreamException();
            }

            return (int)reader.Result;
        }

        private static void WriteLength(this Stream stream, ReadOnlySpan<char> value, Encoding encoding, StringLengthEncoding? lengthFormat)
        {
            if (lengthFormat is null)
                return;
            var length = encoding.GetByteCount(value);
            switch (lengthFormat.GetValueOrDefault())
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case StringLengthEncoding.Plain:
                    stream.Write(length);
                    break;
                case StringLengthEncoding.PlainLittleEndian:
                    length.ReverseIfNeeded(true);
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.PlainBigEndian:
                    length.ReverseIfNeeded(false);
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.Compressed:
                    stream.Write7BitEncodedInt(length);
                    break;
            }
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
        public static void WriteString(this Stream stream, ReadOnlySpan<char> value, in EncodingContext context, Span<byte> buffer, StringLengthEncoding? lengthFormat = null)
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
        private static void WriteLength(this Stream stream, int length, StringLengthEncoding? lengthFormat)
        {
            switch (lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case null:
                    break;
                case StringLengthEncoding.Plain:
                    stream.Write(length);
                    break;
                case StringLengthEncoding.PlainLittleEndian:
                    length.ReverseIfNeeded(true);
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.PlainBigEndian:
                    length.ReverseIfNeeded(false);
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.Compressed:
                    stream.Write7BitEncodedInt(length);
                    break;
            }
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
        public static void WriteString(this Stream stream, ReadOnlySpan<char> value, Encoding encoding, StringLengthEncoding? lengthFormat = null)
        {
            var bytesCount = encoding.GetByteCount(value);
            stream.WriteLength(bytesCount, lengthFormat);
            if (bytesCount == 0)
                return;
            using MemoryRental<byte> buffer = bytesCount <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[bytesCount] : new MemoryRental<byte>(bytesCount);
            encoding.GetBytes(value, buffer.Span);
            stream.Write(buffer.Span);
        }

        private static bool WriteString<T>(Stream stream, T value, Span<char> buffer, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format, IFormatProvider? provider)
            where T : struct, ISpanFormattable
        {
            if (!value.TryFormat(buffer, out var charsWritten, format, provider))
                return false;

            WriteString(stream, buffer.Slice(0, charsWritten), encoding, lengthFormat);
            return true;
        }

        private static void Write<T>(Stream stream, T value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format, IFormatProvider? provider)
            where T : struct, ISpanFormattable
        {
            // attempt to allocate char buffer on the stack
            Span<char> charBuffer = stackalloc char[InitialCharBufferSize];
            if (!WriteString(stream, value, charBuffer, lengthFormat, encoding, format, provider))
            {
                for (var charBufferSize = InitialCharBufferSize * 2; ; charBufferSize = charBufferSize <= MaxBufferSize ? charBufferSize * 2 : throw new InsufficientMemoryException())
                {
                    using var owner = DefaultAllocator.Invoke(charBufferSize, false);
                    if (WriteString(stream, value, charBuffer, lengthFormat, encoding, format, provider))
                        break;
                    charBufferSize = owner.Length;
                }
            }
        }

        /// <summary>
        /// Encodes 8-bit unsigned integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteByte(this Stream stream, byte value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<ByteFormatter>(stream, value, lengthFormat, encoding, format, provider);

        /// <summary>
        /// Encodes 8-bit signed integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteSByte(this Stream stream, sbyte value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<SByteFormatter>(stream, value, lengthFormat, encoding, format, provider);

        /// <summary>
        /// Encodes 16-bit signed integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt16(this Stream stream, short value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<Int16Formatter>(stream, value, lengthFormat, encoding, format, provider);

        /// <summary>
        /// Encodes 16-bit unsigned integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt16(this Stream stream, ushort value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<UInt16Formatter>(stream, value, lengthFormat, encoding, format, provider);

        /// <summary>
        /// Encodes 32-bit signed integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt32(this Stream stream, int value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<Int32Formatter>(stream, value, lengthFormat, encoding, format, provider);

        /// <summary>
        /// Encodes 32-bit unsigned integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt32(this Stream stream, uint value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<UInt32Formatter>(stream, value, lengthFormat, encoding, format, provider);

        /// <summary>
        /// Encodes 64-bit signed integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt64(this Stream stream, long value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<Int64Formatter>(stream, value, lengthFormat, encoding, format, provider);

        /// <summary>
        /// Encodes 64-bit unsigned integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt64(this Stream stream, ulong value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<UInt64Formatter>(stream, value, lengthFormat, encoding, format, provider);

        /// <summary>
        /// Encodes single-precision floating-point number as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteSingle(this Stream stream, float value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<SingleFormatter>(stream, value, lengthFormat, encoding, format, provider);

        /// <summary>
        /// Encodes double-precision floating-point number as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDouble(this Stream stream, double value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<DoubleFormatter>(stream, value, lengthFormat, encoding, format, provider);

        /// <summary>
        /// Encodes <see cref="decimal"/> as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDecimal(this Stream stream, decimal value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<DecimalFormatter>(stream, value, lengthFormat, encoding, format, provider);

        /// <summary>
        /// Encodes <see cref="Guid"/> as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        public static void WriteGuid(this Stream stream, Guid value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default)
            => Write<GuidFormatter>(stream, value, lengthFormat, encoding, format, null);

        /// <summary>
        /// Encodes <see cref="DateTime"/> as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDateTime(this Stream stream, DateTime value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<DateTimeFormatter>(stream, value, lengthFormat, encoding, format, provider);

        /// <summary>
        /// Encodes <see cref="DateTimeOffset"/> as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDateTimeOffset(this Stream stream, DateTimeOffset value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<DateTimeOffsetFormatter>(stream, value, lengthFormat, encoding, format, provider);

        /// <summary>
        /// Encodes <see cref="TimeSpan"/> as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="encoding">The string encoding.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteTimeSpan(this Stream stream, TimeSpan value, StringLengthEncoding lengthFormat, Encoding encoding, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<TimeSpanFormatter>(stream, value, lengthFormat, encoding, format, provider);

        private static ValueTask WriteLengthAsync(this Stream stream, ReadOnlySpan<char> value, Encoding encoding, StringLengthEncoding? lengthFormat, Memory<byte> buffer, CancellationToken token)
        {
            if (lengthFormat is null)
                return new ValueTask();
            var length = encoding.GetByteCount(value);
            switch (lengthFormat.GetValueOrDefault())
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case StringLengthEncoding.Plain:
                    return stream.WriteAsync(length, token);
                case StringLengthEncoding.PlainLittleEndian:
                    length.ReverseIfNeeded(true);
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.PlainBigEndian:
                    length.ReverseIfNeeded(false);
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.Compressed:
                    return stream.Write7BitEncodedIntAsync(length, buffer, token);
            }
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
        public static async ValueTask WriteStringAsync(this Stream stream, ReadOnlyMemory<char> value, EncodingContext context, Memory<byte> buffer, StringLengthEncoding? lengthFormat = null, CancellationToken token = default)
        {
            await stream.WriteLengthAsync(value.Span, context.Encoding, lengthFormat, buffer, token).ConfigureAwait(false);
            if (value.IsEmpty)
                return;
            var encoder = context.GetEncoder();
            var maxChars = buffer.Length / context.Encoding.GetMaxByteCount(1);
            if (maxChars == 0)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
            for (int charsLeft = value.Length, charsUsed; charsLeft > 0; value = value.Slice(charsUsed), charsLeft -= charsUsed)
            {
                charsUsed = Math.Min(maxChars, charsLeft);
                encoder.Convert(value.Span.Slice(0, charsUsed), buffer.Span, charsUsed == charsLeft, out charsUsed, out var bytesUsed, out _);
                await stream.WriteAsync(buffer.Slice(0, bytesUsed), token).ConfigureAwait(false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ValueTask WriteLengthAsync(this Stream stream, int length, StringLengthEncoding? lengthFormat, Memory<byte> buffer, CancellationToken token)
        {
            switch (lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case null:
                    return new ValueTask();
                case StringLengthEncoding.Plain:
                    return stream.WriteAsync(length, token);
                case StringLengthEncoding.PlainLittleEndian:
                    length.ReverseIfNeeded(true);
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.PlainBigEndian:
                    length.ReverseIfNeeded(false);
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.Compressed:
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
        public static async ValueTask WriteStringAsync(this Stream stream, ReadOnlyMemory<char> value, Encoding encoding, StringLengthEncoding? lengthFormat = null, CancellationToken token = default)
        {
            var bytesCount = encoding.GetByteCount(value.Span);
            using var buffer = new ArrayRental<byte>(bytesCount);
            await stream.WriteLengthAsync(bytesCount, lengthFormat, buffer.Memory, token).ConfigureAwait(false);
            if (bytesCount == 0)
                return;

            encoding.GetBytes(value.Span, buffer.Span);
            await stream.WriteAsync(buffer.Memory, token).ConfigureAwait(false);
        }

        private static async ValueTask WriteAsync<T>(Stream stream, T value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format, IFormatProvider? provider, CancellationToken token)
            where T : struct, ISpanFormattable
        {
            for (var charBufferSize = InitialCharBufferSize; ; charBufferSize = charBufferSize <= MaxBufferSize ? charBufferSize * 2 : throw new InsufficientMemoryException())
            {
                using var owner = DefaultAllocator.Invoke(charBufferSize, false);

                if (value.TryFormat(owner.Memory.Span, out var charsWritten, format, provider))
                {
                    await WriteStringAsync(stream, owner.Memory.Slice(0, charsWritten), context, buffer, lengthFormat, token).ConfigureAwait(false);
                    break;
                }

                charBufferSize = owner.Length;
            }
        }

        private static async ValueTask WriteAsync<T>(Stream stream, T value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            where T : struct, ISpanFormattable
        {
            using var owner = new ArrayRental<byte>(DefaultBufferSize);
            await WriteAsync(stream, value, lengthFormat, context, owner.Memory, format, provider, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Encodes 8-bit unsigned integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteByteAsync(this Stream stream, byte value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<ByteFormatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes 8-bit unsigned integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteByteAsync(this Stream stream, byte value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<ByteFormatter>(stream, value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes 8-bit signed integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        [CLSCompliant(false)]
        public static ValueTask WriteSByteAsync(this Stream stream, sbyte value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<SByteFormatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes 8-bit unsigned integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        [CLSCompliant(false)]
        public static ValueTask WriteSByteAsync(this Stream stream, sbyte value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<SByteFormatter>(stream, value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes 16-bit signed integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteInt16Async(this Stream stream, short value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<Int16Formatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes 16-bit signed integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteInt16Async(this Stream stream, short value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<Int16Formatter>(stream, value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes 16-bit unsigned integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        [CLSCompliant(false)]
        public static ValueTask WriteUInt16Async(this Stream stream, ushort value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<UInt16Formatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes 16-bit unsigned integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        [CLSCompliant(false)]
        public static ValueTask WriteUInt16Async(this Stream stream, ushort value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<UInt16Formatter>(stream, value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes 32-bit signed integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteInt32Async(this Stream stream, int value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<Int32Formatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes 16-bit signed integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteInt32Async(this Stream stream, int value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<Int32Formatter>(stream, value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes 32-bit unsigned integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        [CLSCompliant(false)]
        public static ValueTask WriteUInt32Async(this Stream stream, uint value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<UInt32Formatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes 16-bit unsigned integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        [CLSCompliant(false)]
        public static ValueTask WriteUInt32Async(this Stream stream, uint value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<UInt32Formatter>(stream, value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes 64-bit signed integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteInt64Async(this Stream stream, long value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<Int64Formatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes 64-bit signed integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteInt64Async(this Stream stream, long value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<Int64Formatter>(stream, value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes 64-bit unsigned integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        [CLSCompliant(false)]
        public static ValueTask WriteUInt64Async(this Stream stream, ulong value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<UInt64Formatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes 64-bit unsigned integer as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        [CLSCompliant(false)]
        public static ValueTask WriteUInt64Async(this Stream stream, ulong value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<UInt64Formatter>(stream, value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes single-precision floating-point number as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteSingleAsync(this Stream stream, float value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<SingleFormatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes single-precision floating-pointer number as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteSingleAsync(this Stream stream, float value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<SingleFormatter>(stream, value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes double-precision floating-point number as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteDoubleAsync(this Stream stream, double value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<DoubleFormatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes double-precision floating-pointer number as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteDoubleAsync(this Stream stream, double value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<DoubleFormatter>(stream, value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes double-precision floating-point number as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteDecimalAsync(this Stream stream, decimal value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<DecimalFormatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes double-precision floating-pointer number as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteDecimalAsync(this Stream stream, decimal value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<DecimalFormatter>(stream, value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes <see cref="Guid"/> as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteGuidAsync(this Stream stream, Guid value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, CancellationToken token = default)
            => WriteAsync<GuidFormatter>(stream, value, lengthFormat, context, buffer, format, null, token);

        /// <summary>
        /// Encodes <see cref="Guid"/> as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteGuidAsync(this Stream stream, Guid value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, CancellationToken token = default)
            => WriteAsync<GuidFormatter>(stream, value, lengthFormat, context, format, null, token);

        /// <summary>
        /// Encodes <see cref="DateTime"/> as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteDateTimeAsync(this Stream stream, DateTime value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<DateTimeFormatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes <see cref="DateTime"/> as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteDateTimeAsync(this Stream stream, DateTime value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<DateTimeFormatter>(stream, value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes <see cref="DateTimeOffset"/> as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteDateTimeOffsetAsync(this Stream stream, DateTimeOffset value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<DateTimeOffsetFormatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes <see cref="DateTimeOffset"/> as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteDateTimeOffsetAsync(this Stream stream, DateTimeOffset value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<DateTimeOffsetFormatter>(stream, value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes <see cref="TimeSpan"/> as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteTimeSpanAsync(this Stream stream, TimeSpan value, StringLengthEncoding lengthFormat, EncodingContext context, Memory<byte> buffer, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<TimeSpanFormatter>(stream, value, lengthFormat, context, buffer, format, provider, token);

        /// <summary>
        /// Encodes <see cref="TimeSpan"/> as a string.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">The format to use.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous state of the operation.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static ValueTask WriteTimeSpanAsync(this Stream stream, TimeSpan value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync<TimeSpanFormatter>(stream, value, lengthFormat, context, format, provider, token);

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

        private static TResult Read<TResult, TDecoder>(Stream stream, TDecoder decoder, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer)
            where TResult : struct
            where TDecoder : ISpanDecoder<TResult>
        {
            var length = ReadLength(stream, lengthFormat);
            if (length == 0)
                throw new EndOfStreamException();
            using var result = length <= MemoryRental<byte>.StackallocThreshold ? stackalloc char[length] : new MemoryRental<char>(length);
            length = ReadString(stream, result.Span, in context, buffer);
            return decoder.Decode(result.Span.Slice(0, length));
        }

        private static int ReadString(Stream stream, Span<char> result, in DecodingContext context, Span<byte> buffer)
        {
            var maxChars = context.Encoding.GetMaxCharCount(buffer.Length);
            if (maxChars == 0)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
            var decoder = context.GetDecoder();
            var resultOffset = 0;
            for (int length = result.Length, n; length > 0; resultOffset += decoder.GetChars(buffer.Slice(0, n), result.Slice(resultOffset), length == 0))
            {
                n = stream.Read(buffer.Slice(0, Math.Min(length, buffer.Length)));
                if (n == 0)
                    throw new EndOfStreamException();
                length -= n;
            }

            return resultOffset;
        }

        /// <summary>
        /// Reads the string using the specified encoding and supplied reusable buffer.
        /// </summary>
        /// <remarks>
        /// <paramref name="buffer"/> length can be less than <paramref name="length"/>
        /// but should be enough to decode at least one character of the specified encoding.
        /// </remarks>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="length">The length of the string, in bytes.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <returns>The string decoded from the log entry content stream.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        public static string ReadString(this Stream stream, int length, in DecodingContext context, Span<byte> buffer)
        {
            if (length == 0)
                return string.Empty;
            using var result = length <= MemoryRental<byte>.StackallocThreshold ? stackalloc char[length] : new MemoryRental<char>(length);
            return new string(result.Span.Slice(0, ReadString(stream, result.Span, in context, buffer)));
        }

        /// <summary>
        /// Decodes 8-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        public static byte ReadByte(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<byte, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes 8-bit signed integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        [CLSCompliant(false)]
        public static sbyte ReadSByte(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<sbyte, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes 16-bit signed integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        public static short ReadInt16(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<short, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes 16-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        [CLSCompliant(false)]
        public static ushort ReadUInt16(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<ushort, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes 32-bit signed integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        public static int ReadInt32(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<int, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes 32-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        [CLSCompliant(false)]
        public static uint ReadUInt32(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<uint, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes 64-bit signed integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        public static long ReadInt64(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<long, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes 64-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        [CLSCompliant(false)]
        public static ulong ReadUInt64(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<ulong, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes single-precision floating-point number from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        public static float ReadSingle(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null)
            => Read<float, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes double-precision floating-point number from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        public static double ReadDouble(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null)
            => Read<double, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes <see cref="decimal"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        public static decimal ReadDecimal(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Number, IFormatProvider? provider = null)
            => Read<decimal, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes <see cref="Guid"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">GUID value is in incorrect format.</exception>
        public static Guid ReadGuid(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer)
            => Read<Guid, GuidDecoder>(stream, new GuidDecoder(), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes <see cref="Guid"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The expected format of GUID value.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">GUID value is in incorrect format.</exception>
        public static Guid ReadGuid(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, string format)
            => Read<Guid, GuidDecoder>(stream, new GuidDecoder(format), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes <see cref="DateTime"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        public static DateTime ReadDateTime(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
            => Read<DateTime, DateTimeDecoder>(stream, new DateTimeDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes <see cref="DateTime"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        public static DateTime ReadDateTime(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, string[] formats, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
            => Read<DateTime, DateTimeDecoder>(stream, new DateTimeDecoder(style, formats, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes <see cref="DateTimeOffset"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        public static DateTimeOffset ReadDateTimeOffset(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
            => Read<DateTimeOffset, DateTimeDecoder>(stream, new DateTimeDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes <see cref="DateTimeOffset"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        public static DateTimeOffset ReadDateTimeOffset(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, string[] formats, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
            => Read<DateTimeOffset, DateTimeDecoder>(stream, new DateTimeDecoder(style, formats, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Parses <see cref="TimeSpan"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The time span is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public static TimeSpan ReadTimeSpan(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, IFormatProvider? provider = null)
            => Read<TimeSpan, TimeSpanDecoder>(stream, new TimeSpanDecoder(provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Parses <see cref="DateTimeOffset"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The time span is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public static TimeSpan ReadTimeSpan(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer, string[] formats, TimeSpanStyles style = TimeSpanStyles.None, IFormatProvider? provider = null)
            => Read<TimeSpan, TimeSpanDecoder>(stream, new TimeSpanDecoder(style, formats, provider), lengthFormat, in context, buffer);

        private static int ReadLength(this Stream stream, StringLengthEncoding lengthFormat)
        {
            int result;
            var littleEndian = BitConverter.IsLittleEndian;
            switch (lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case StringLengthEncoding.Plain:
                    result = stream.Read<int>();
                    break;
                case StringLengthEncoding.PlainLittleEndian:
                    littleEndian = true;
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.PlainBigEndian:
                    littleEndian = false;
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.Compressed:
                    result = stream.Read7BitEncodedInt();
                    break;
            }

            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        private static async ValueTask<int> ReadLengthAsync(this Stream stream, StringLengthEncoding lengthFormat, Memory<byte> buffer, CancellationToken token)
        {
            ValueTask<int> result;
            var littleEndian = BitConverter.IsLittleEndian;
            switch (lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case StringLengthEncoding.Plain:
                    result = stream.ReadAsync<int>(buffer, token);
                    break;
                case StringLengthEncoding.PlainLittleEndian:
                    littleEndian = true;
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.PlainBigEndian:
                    littleEndian = false;
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.Compressed:
                    result = stream.Read7BitEncodedIntAsync(buffer, token);
                    break;
            }

            var length = await result.ConfigureAwait(false);
            length.ReverseIfNeeded(littleEndian);
            return length;
        }

        /// <summary>
        /// Reads a length-prefixed string using the specified encoding and supplied reusable buffer.
        /// </summary>
        /// <remarks>
        /// This method decodes string length (in bytes) from
        /// stream in contrast to <see cref="ReadString(Stream, int, in DecodingContext, Span{byte})"/>.
        /// </remarks>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <returns>The string decoded from the log entry content stream.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static string ReadString(this Stream stream, StringLengthEncoding lengthFormat, in DecodingContext context, Span<byte> buffer)
            => ReadString(stream, stream.ReadLength(lengthFormat), in context, buffer);

        /// <summary>
        /// Reads the string using the specified encoding.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="length">The length of the string, in bytes.</param>
        /// <param name="encoding">The encoding used to decode bytes from stream into characters.</param>
        /// <returns>The string decoded from the log entry content stream.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        public static string ReadString(this Stream stream, int length, Encoding encoding)
        {
            if (length == 0)
                return string.Empty;
            using MemoryRental<byte> bytesBuffer = length <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[length] : new MemoryRental<byte>(length);
            using MemoryRental<char> charBuffer = length <= MemoryRental<char>.StackallocThreshold ? stackalloc char[length] : new MemoryRental<char>(length);
            if (bytesBuffer.Length != stream.Read(bytesBuffer.Span))
                throw new EndOfStreamException();
            var charCount = encoding.GetChars(bytesBuffer.Span, charBuffer.Span);
            return new string(charBuffer.Span.Slice(0, charCount));
        }

        /// <summary>
        /// Reads a length-prefixed string using the specified encoding.
        /// </summary>
        /// <remarks>
        /// This method decodes string length (in bytes) from
        /// stream in contrast to <see cref="ReadString(Stream, int, Encoding)"/>.
        /// </remarks>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="encoding">The encoding used to decode bytes from stream into characters.</param>
        /// <returns>The string decoded from the log entry content stream.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static string ReadString(this Stream stream, StringLengthEncoding lengthFormat, Encoding encoding)
            => ReadString(stream, stream.ReadLength(lengthFormat), encoding);

        private static async ValueTask<TResult> ReadAsync<TResult, TDecoder>(Stream stream, TDecoder decoder, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, CancellationToken token)
            where TResult : struct
            where TDecoder : ISpanDecoder<TResult>
        {
            var length = await ReadLengthAsync(stream, lengthFormat, buffer, token).ConfigureAwait(false);
            if (length == 0)
                throw new EndOfStreamException();
            using var result = new ArrayRental<char>(length);
            length = await ReadStringAsync(stream, result.Memory, context, buffer, token).ConfigureAwait(false);
            return decoder.Decode(result.Span.Slice(0, length));
        }

        private static async ValueTask<TResult> ReadAsync<TResult, TDecoder>(Stream stream, TDecoder decoder, StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token)
            where TResult : struct
            where TDecoder : ISpanDecoder<TResult>
        {
            int length;
            ArrayRental<byte> buffer;
            using (buffer = new ArrayRental<byte>(BufferSizeForLength))
                length = await ReadLengthAsync(stream, lengthFormat, buffer.Memory, token).ConfigureAwait(false);
            if (length == 0)
                throw new EndOfStreamException();
            using var result = new ArrayRental<char>(length);
            using (buffer = new ArrayRental<byte>(length))
            {
                length = await ReadStringAsync(stream, result.Memory, context, buffer.Memory, token).ConfigureAwait(false);
                return decoder.Decode(result.Span.Slice(0, length));
            }
        }

        private static async ValueTask<int> ReadStringAsync(this Stream stream, Memory<char> result, DecodingContext context, Memory<byte> buffer, CancellationToken token = default)
        {
            var maxChars = context.Encoding.GetMaxCharCount(buffer.Length);
            if (maxChars == 0)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
            var decoder = context.GetDecoder();
            var resultOffset = 0;
            for (int length = result.Length, n; length > 0; resultOffset += decoder.GetChars(buffer.Span.Slice(0, n), result.Span.Slice(resultOffset), length == 0))
            {
                n = await stream.ReadAsync(buffer.Slice(0, Math.Min(length, buffer.Length)), token).ConfigureAwait(false);
                if (n == 0)
                    throw new EndOfStreamException();
                length -= n;
            }

            return resultOffset;
        }

        /// <summary>
        /// Reads the string asynchronously using the specified encoding and supplied reusable buffer.
        /// </summary>
        /// <remarks>
        /// <paramref name="buffer"/> length can be less than <paramref name="length"/>
        /// but should be enough to decode at least one character of the specified encoding.
        /// </remarks>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="length">The length of the string, in bytes.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The string decoded from the log entry content stream.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask<string> ReadStringAsync(this Stream stream, int length, DecodingContext context, Memory<byte> buffer, CancellationToken token = default)
        {
            if (length == 0)
                return string.Empty;
            using var result = new ArrayRental<char>(length);
            length = await ReadStringAsync(stream, result.Memory, context, buffer, token).ConfigureAwait(false);
            return new string(result.Span.Slice(0, length));
        }

        /// <summary>
        /// Reads a length-prefixed string asynchronously using the specified encoding and supplied reusable buffer.
        /// </summary>
        /// <remarks>
        /// This method decodes string length (in bytes) from
        /// stream in contrast to <see cref="ReadStringAsync(Stream, int, DecodingContext, Memory{byte}, CancellationToken)"/>.
        /// </remarks>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The string decoded from the log entry content stream.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static async ValueTask<string> ReadStringAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, CancellationToken token = default)
            => await ReadStringAsync(stream, await stream.ReadLengthAsync(lengthFormat, buffer, token).ConfigureAwait(false), context, buffer, token).ConfigureAwait(false);

        /// <summary>
        /// Reads the string asynchronously using the specified encoding and supplied reusable buffer.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="length">The length of the string, in bytes.</param>
        /// <param name="encoding">The encoding used to decode bytes from stream into characters.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The string decoded from the log entry content stream.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask<string> ReadStringAsync(this Stream stream, int length, Encoding encoding, CancellationToken token = default)
        {
            if (length == 0)
                return string.Empty;
            using var bytesBuffer = new ArrayRental<byte>(length);
            using var charBuffer = new ArrayRental<char>(length);
            if (bytesBuffer.Length != await stream.ReadAsync(bytesBuffer.Memory, token).ConfigureAwait(false))
                throw new EndOfStreamException();
            var charCount = encoding.GetChars(bytesBuffer.Span, charBuffer.Span);
            return new string(charBuffer.Span.Slice(0, charCount));
        }

        /// <summary>
        /// Reads a length-prefixed string asynchronously using the specified encoding and supplied reusable buffer.
        /// </summary>
        /// <remarks>
        /// This method decodes string length (in bytes) from
        /// stream in contrast to <see cref="ReadStringAsync(Stream, int, Encoding, CancellationToken)"/>.
        /// </remarks>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="encoding">The encoding used to decode bytes from stream into characters.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The string decoded from the log entry content stream.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static async ValueTask<string> ReadStringAsync(this Stream stream, StringLengthEncoding lengthFormat, Encoding encoding, CancellationToken token = default)
        {
            using var lengthDecodingBuffer = new ArrayRental<byte>(BufferSizeForLength);
            return await ReadStringAsync(stream, await stream.ReadLengthAsync(lengthFormat, lengthDecodingBuffer.Memory, token).ConfigureAwait(false), encoding, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Decodes 8-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<byte> ReadByteAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<byte, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes 8-bit signed integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [CLSCompliant(false)]
        public static ValueTask<sbyte> ReadSByteAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<sbyte, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes 16-bit signed integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<short> ReadInt16Async(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<short, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes 16-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [CLSCompliant(false)]
        public static ValueTask<ushort> ReadUInt16Async(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<ushort, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes 32-bit signed integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<int> ReadInt32Async(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<int, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes 32-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [CLSCompliant(false)]
        public static ValueTask<uint> ReadUInt32Async(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<uint, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes 64-bit signed integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<long> ReadInt64Async(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<long, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes 64-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [CLSCompliant(false)]
        public static ValueTask<ulong> ReadUInt64Async(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<ulong, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes single-precision floating-point number from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<float> ReadSingleAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<float, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes double-precision floating-point number from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<double> ReadDoubleAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<double, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes <see cref="decimal"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<decimal> ReadDecimalAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Number, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<decimal, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes <see cref="Guid"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">GUID value is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<Guid> ReadGuidAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, CancellationToken token = default)
            => ReadAsync<Guid, GuidDecoder>(stream, new GuidDecoder(), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes <see cref="Guid"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="format">The expected format of GUID value.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">GUID value is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<Guid> ReadGuidAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, string format, CancellationToken token = default)
            => ReadAsync<Guid, GuidDecoder>(stream, new GuidDecoder(format), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes <see cref="DateTime"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<DateTime> ReadDateTimeAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<DateTime, DateTimeDecoder>(stream, new DateTimeDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes <see cref="DateTime"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<DateTime> ReadDateTimeAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, string[] formats, Memory<byte> buffer, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<DateTime, DateTimeDecoder>(stream, new DateTimeDecoder(style, formats, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes <see cref="DateTimeOffset"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<DateTimeOffset> ReadDateTimeOffsetAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<DateTimeOffset, DateTimeDecoder>(stream, new DateTimeDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes <see cref="DateTimeOffset"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<DateTimeOffset> ReadDateTimeOffsetAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, string[] formats, Memory<byte> buffer, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<DateTimeOffset, DateTimeDecoder>(stream, new DateTimeDecoder(style, formats, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Parses <see cref="TimeSpan"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="FormatException">The time span is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public static ValueTask<TimeSpan> ReadTimeSpanAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<TimeSpan, TimeSpanDecoder>(stream, new TimeSpanDecoder(provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Parses <see cref="TimeSpan"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
        /// <exception cref="FormatException">The time span is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public static ValueTask<TimeSpan> ReadTimeSpanAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, Memory<byte> buffer, string[] formats, TimeSpanStyles style = TimeSpanStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<TimeSpan, TimeSpanDecoder>(stream, new TimeSpanDecoder(style, formats, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes 8-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<byte> ReadByteAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<byte, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 8-bit signed integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [CLSCompliant(false)]
        public static ValueTask<sbyte> ReadSByteAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<sbyte, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 16-bit signed integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<short> ReadInt16Async(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<short, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 16-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [CLSCompliant(false)]
        public static ValueTask<ushort> ReadUInt16Async(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<ushort, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 32-bit signed integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<int> ReadInt32Async(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<int, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 32-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [CLSCompliant(false)]
        public static ValueTask<uint> ReadUInt32Async(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<uint, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 64-bit signed integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<long> ReadInt64Async(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<long, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes 64-bit unsigned integer from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [CLSCompliant(false)]
        public static ValueTask<ulong> ReadUInt64Async(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<ulong, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes single-precision floating-point number from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<float> ReadSingleAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<float, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes double-precision floating-point number from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<double> ReadDoubleAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<double, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="decimal"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<decimal> ReadDecimalAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Number, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<decimal, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="Guid"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">GUID value is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<Guid> ReadGuidAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token = default)
            => ReadAsync<Guid, GuidDecoder>(stream, new GuidDecoder(), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="Guid"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="format">The expected format of GUID value.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">GUID value is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<Guid> ReadGuidAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, string format, CancellationToken token = default)
            => ReadAsync<Guid, GuidDecoder>(stream, new GuidDecoder(format), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="DateTime"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<DateTime> ReadDateTimeAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<DateTime, DateTimeDecoder>(stream, new DateTimeDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="DateTime"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<DateTime> ReadDateTimeAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<DateTime, DateTimeDecoder>(stream, new DateTimeDecoder(style, formats, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="DateTimeOffset"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<DateTimeOffset> ReadDateTimeOffsetAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<DateTimeOffset, DateTimeDecoder>(stream, new DateTimeDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="DateTimeOffset"/> from its string representation.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The text decoding context.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="FormatException">The date/time string is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<DateTimeOffset> ReadDateTimeOffsetAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<DateTimeOffset, DateTimeDecoder>(stream, new DateTimeDecoder(style, formats, provider), lengthFormat, context, token);

        /// <summary>
        /// Parses <see cref="TimeSpan"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The time span is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public static ValueTask<TimeSpan> ReadTimeSpanAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<TimeSpan, TimeSpanDecoder>(stream, new TimeSpanDecoder(provider), lengthFormat, context, token);

        /// <summary>
        /// Parses <see cref="TimeSpan"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The time span is in incorrect format.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public static ValueTask<TimeSpan> ReadTimeSpanAsync(this Stream stream, StringLengthEncoding lengthFormat, DecodingContext context, string[] formats, TimeSpanStyles style = TimeSpanStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<TimeSpan, TimeSpanDecoder>(stream, new TimeSpanDecoder(style, formats, provider), lengthFormat, context, token);

        /// <summary>
        /// Reads exact number of bytes.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="output">A region of memory. When this method returns, the contents of this region are replaced by the bytes read from the current source.</param>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        public static void ReadBlock(this Stream stream, Span<byte> output)
        {
            for (int size = output.Length, bytesRead, offset = 0; size > 0; size -= bytesRead, offset += bytesRead)
            {
                bytesRead = stream.Read(output.Slice(offset, size));
                if (bytesRead == 0)
                    throw new EndOfStreamException();
            }
        }

        /// <summary>
        /// Reads exact number of bytes.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="output">A region of memory. When this method returns, the contents of this region are replaced by the bytes read from the current source.</param>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        [Obsolete("Use ReadBlock extension method instead")]
        public static void ReadBytes(Stream stream, Span<byte> output)
            => ReadBlock(stream, output);

        /// <summary>
        /// Deserializes the value type from the stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <typeparam name="T">The value type to be deserialized.</typeparam>
        /// <returns>The value deserialized from the stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        public static unsafe T Read<T>(this Stream stream)
            where T : unmanaged
        {
            var result = default(T);
            stream.ReadBlock(Span.AsBytes(ref result));
            return result;
        }

        /// <summary>
        /// Reads exact number of bytes asynchronously.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="output">A region of memory. When this method returns, the contents of this region are replaced by the bytes read from the current source.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask ReadBlockAsync(this Stream stream, Memory<byte> output, CancellationToken token = default)
        {
            for (int size = output.Length, bytesRead, offset = 0; size > 0; size -= bytesRead, offset += bytesRead)
            {
                bytesRead = await stream.ReadAsync(output.Slice(offset, size), token).ConfigureAwait(false);
                if (bytesRead == 0)
                    throw new EndOfStreamException();
            }
        }

        /// <summary>
        /// Reads exact number of bytes asynchronously.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="output">A region of memory. When this method returns, the contents of this region are replaced by the bytes read from the current source.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [Obsolete("Use ReadBlockAsync extension method instead")]
        public static ValueTask ReadBytesAsync(Stream stream, Memory<byte> output, CancellationToken token = default)
            => ReadBlockAsync(stream, output, token);

        /// <summary>
        /// Asynchronously deserializes the value type from the stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <typeparam name="T">The value type to be deserialized.</typeparam>
        /// <returns>The value deserialized from the stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask<T> ReadAsync<T>(this Stream stream, Memory<byte> buffer, CancellationToken token = default)
            where T : unmanaged
        {
            await stream.ReadBlockAsync(buffer.Slice(0, Unsafe.SizeOf<T>()), token).ConfigureAwait(false);
            return MemoryMarshal.Read<T>(buffer.Span);
        }

        /// <summary>
        /// Asynchronously deserializes the value type from the stream.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <typeparam name="T">The value type to be deserialized.</typeparam>
        /// <returns>The value deserialized from the stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask<T> ReadAsync<T>(this Stream stream, CancellationToken token = default)
            where T : unmanaged
        {
            using var buffer = new ArrayRental<byte>(Unsafe.SizeOf<T>());
            await stream.ReadBlockAsync(buffer.Memory, token).ConfigureAwait(false);
            return MemoryMarshal.Read<T>(buffer.Span);
        }

        /// <summary>
        /// Serializes value to the stream.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        /// <param name="value">The value to be written into the stream.</param>
        /// <typeparam name="T">The value type to be serialized.</typeparam>
        public static unsafe void Write<T>(this Stream stream, in T value)
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
            using var buffer = new ArrayRental<byte>(Unsafe.SizeOf<T>());
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

        /// <summary>
        /// Asynchronously reads the bytes from the source stream and writes them to another stream, using a specified buffer.
        /// </summary>
        /// <param name="source">The source stream to read from.</param>
        /// <param name="destination">The destination stream to write into.</param>
        /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
        /// <param name="token">The token that can be used to cancel this operation.</param>
        /// <returns>The total number of copied bytes.</returns>
        public static async ValueTask<long> CopyToAsync(this Stream source, Stream destination, Memory<byte> buffer, CancellationToken token = default)
        {
            var totalBytes = 0L;
            for (int count; (count = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0; )
            {
                totalBytes += count;
                await destination.WriteAsync(buffer.Slice(0, count), token).ConfigureAwait(false);
            }

            return totalBytes;
        }

        /// <summary>
        /// Synchronously reads the bytes from the source stream and writes them to another stream, using a specified buffer.
        /// </summary>
        /// <param name="source">The source stream to read from.</param>
        /// <param name="destination">The destination stream to write into.</param>
        /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
        /// <param name="token">The token that can be used to cancel this operation.</param>
        /// <returns>The total number of copied bytes.</returns>
        public static long CopyTo(this Stream source, Stream destination, Span<byte> buffer, CancellationToken token = default)
        {
            var totalBytes = 0L;
            for (int count; (count = source.Read(buffer)) > 0; token.ThrowIfCancellationRequested())
            {
                totalBytes += count;
                destination.Write(buffer.Slice(0, count));
            }

            return totalBytes;
        }

        /// <summary>
        /// Converts the stream to <see cref="System.Buffers.IBufferWriter{T}"/>.
        /// </summary>
        /// <param name="output">The stream to convert.</param>
        /// <param name="allocator">The allocator of the buffer.</param>
        /// <returns>The buffered stream writer.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="output"/> is not writable stream.</exception>
        public static IFlushableBufferWriter<byte> AsBufferWriter(this Stream output, MemoryAllocator<byte>? allocator = null)
        {
            if (output is null)
                throw new ArgumentNullException(nameof(output));
            if (!output.CanWrite)
                throw new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(output));

            return new BufferedStreamWriter(output, allocator);
        }

        /// <summary>
        /// Reads the entire content using the specified delegate.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="reader">The content reader.</param>
        /// <param name="arg">The argument to be passed to the content reader.</param>
        /// <param name="buffer">The buffer allocated by the caller.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static void Read<TArg>(this Stream stream, ReadOnlySpanAction<byte, TArg> reader, TArg arg, Span<byte> buffer, CancellationToken token = default)
        {
            if (buffer.IsEmpty)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall);

            for (int count; (count = stream.Read(buffer)) > 0; token.ThrowIfCancellationRequested())
                reader(buffer.Slice(0, count), arg);
        }

        /// <summary>
        /// Reads the entire content using the specified delegate.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="reader">The content reader.</param>
        /// <param name="arg">The argument to be passed to the content reader.</param>
        /// <param name="bufferSize">The size of the buffer used to read data.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is less than or equal to zero.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static void Read<TArg>(this Stream stream, ReadOnlySpanAction<byte, TArg> reader, TArg arg, int bufferSize = DefaultBufferSize, CancellationToken token = default)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            using var owner = bufferSize <= MemoryRental<byte>.StackallocThreshold ? new MemoryRental<byte>(stackalloc byte[bufferSize]) : new MemoryRental<byte>(bufferSize);
            Read(stream, reader, arg, owner.Span, token);
        }

        /// <summary>
        /// Reads the entire content using the specified delegate.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="reader">The content reader.</param>
        /// <param name="arg">The argument to be passed to the content reader.</param>
        /// <param name="buffer">The buffer allocated by the caller.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async Task ReadAsync<TArg>(this Stream stream, ReadOnlySpanAction<byte, TArg> reader, TArg arg, Memory<byte> buffer, CancellationToken token = default)
        {
            if (buffer.IsEmpty)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall);

            for (int count; (count = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0; token.ThrowIfCancellationRequested())
                reader(buffer.Span.Slice(0, count), arg);
        }

        /// <summary>
        /// Reads the entire content using the specified delegate.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="reader">The content reader.</param>
        /// <param name="arg">The argument to be passed to the content reader.</param>
        /// <param name="bufferSize">The size of the buffer used to read data.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is less than or equal to zero.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async Task ReadAsync<TArg>(this Stream stream, ReadOnlySpanAction<byte, TArg> reader, TArg arg, int bufferSize = DefaultBufferSize, CancellationToken token = default)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            using var owner = new ArrayRental<byte>(bufferSize);
            await ReadAsync(stream, reader, arg, owner.Memory, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads the entire content using the specified delegate.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="reader">The content reader.</param>
        /// <param name="arg">The argument to be passed to the content reader.</param>
        /// <param name="buffer">The buffer allocated by the caller.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async Task ReadAsync<TArg>(this Stream stream, Func<ReadOnlyMemory<byte>, TArg, CancellationToken, ValueTask> reader, TArg arg, Memory<byte> buffer, CancellationToken token = default)
        {
            if (buffer.IsEmpty)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall);

            for (int count; (count = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0; )
                await reader(buffer.Slice(0, count), arg, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads the entire content using the specified delegate.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="reader">The content reader.</param>
        /// <param name="arg">The argument to be passed to the content reader.</param>
        /// <param name="bufferSize">The size of the buffer used to read data.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is less than or equal to zero.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async Task ReadAsync<TArg>(this Stream stream, Func<ReadOnlyMemory<byte>, TArg, CancellationToken, ValueTask> reader, TArg arg, int bufferSize = DefaultBufferSize, CancellationToken token = default)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            using var owner = new ArrayRental<byte>(bufferSize);
            await ReadAsync(stream, reader, arg, owner.Memory, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously reads the bytes from the current stream and writes them to buffer
        /// writer, using a specified cancellation token.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="destination">The writer to which the contents of the current stream will be copied.</param>
        /// <param name="bufferSize">The size, in bytes, of the buffer.</param>
        /// <param name="token">The token to monitor for cancellation requests.</param>
        /// <returns>The number of copied bytes.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is negative or zero.</exception>
        /// <exception cref="NotSupportedException"><paramref name="source"/> doesn't support reading.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async Task<long> CopyToAsync(this Stream source, IBufferWriter<byte> destination, int bufferSize = 0, CancellationToken token = default)
        {
            if (destination is null)
                throw new ArgumentNullException(nameof(destination));
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            var totalBytes = 0L;
            for (int count; ; totalBytes += count)
            {
                var buffer = destination.GetMemory(bufferSize);
                count = await source.ReadAsync(buffer, token).ConfigureAwait(false);
                if (count <= 0)
                    break;
                destination.Advance(count);
            }

            return totalBytes;
        }

        /// <summary>
        /// Synchronously reads the bytes from the current stream and writes them to buffer
        /// writer, using a specified cancellation token.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="destination">The writer to which the contents of the current stream will be copied.</param>
        /// <param name="bufferSize">The size, in bytes, of the buffer.</param>
        /// <param name="token">The token to monitor for cancellation requests.</param>
        /// <returns>The number of copied bytes.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is negative or zero.</exception>
        /// <exception cref="NotSupportedException"><paramref name="source"/> doesn't support reading.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static long CopyTo(this Stream source, IBufferWriter<byte> destination, int bufferSize = 0, CancellationToken token = default)
        {
            if (destination is null)
                throw new ArgumentNullException(nameof(destination));
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            var totalBytes = 0L;
            for (int count; !token.IsCancellationRequested; totalBytes += count, token.ThrowIfCancellationRequested())
            {
                var buffer = destination.GetSpan(bufferSize);
                count = source.Read(buffer);
                if (count <= 0)
                    break;
                destination.Advance(count);
            }

            return totalBytes;
        }
    }
}
