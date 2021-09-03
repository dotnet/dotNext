using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Numerics;
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

    /// <summary>
    /// Represents high-level read/write methods for the stream.
    /// </summary>
    /// <remarks>
    /// This class provides alternative way to read and write typed data from/to the stream
    /// without instantiation of <see cref="BinaryReader"/> and <see cref="BinaryWriter"/>.
    /// </remarks>
    public static partial class StreamExtensions
    {
        private const int BufferSizeForLength = 5;
        private const int DefaultBufferSize = 256;

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

        [SkipLocalsInit]
        private static TResult Read<TResult, TDecoder>(Stream stream, TDecoder decoder, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer)
            where TResult : struct
            where TDecoder : ISpanDecoder<TResult>
        {
            var length = ReadLength(stream, lengthFormat);
            if (length <= 0)
                throw new EndOfStreamException();

            using var result = length <= MemoryRental<char>.StackallocThreshold ? stackalloc char[length] : new MemoryRental<char>(length);
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
        [SkipLocalsInit]
        public static string ReadString(this Stream stream, int length, in DecodingContext context, Span<byte> buffer)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (length == 0)
                return string.Empty;

            using var result = length <= MemoryRental<char>.StackallocThreshold ? stackalloc char[length] : new MemoryRental<char>(length);
            return new string(result.Span.Slice(0, ReadString(stream, result.Span, in context, buffer)));
        }

        /// <summary>
        /// Decodes an arbitrary large big integer.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="length">The length of the value, in bytes.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding the value.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        public static BigInteger ReadBigInteger(this Stream stream, int length, bool littleEndian, Span<byte> buffer)
        {
            if (length == 0)
                return BigInteger.Zero;
            if (buffer.Length < length)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

            buffer = buffer.Slice(0, length);
            stream.ReadBlock(buffer);
            return new BigInteger(buffer, isBigEndian: !littleEndian);
        }

        /// <summary>
        /// Decodes an arbitrary large big integer.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="length">The length of the value, in bytes.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        [SkipLocalsInit]
        public static BigInteger ReadBigInteger(this Stream stream, int length, bool littleEndian)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (length == 0)
                return BigInteger.Zero;

            using MemoryRental<byte> buffer = length <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[length] : new MemoryRental<byte>(length);
            stream.ReadBlock(buffer.Span);
            return new BigInteger(buffer.Span, isBigEndian: !littleEndian);
        }

        /// <summary>
        /// Decodes an arbitrary large big integer.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the value length encoded in the underlying stream.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding the value.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static BigInteger ReadBigInteger(this Stream stream, LengthFormat lengthFormat, bool littleEndian, Span<byte> buffer)
            => ReadBigInteger(stream, stream.ReadLength(lengthFormat), littleEndian, buffer);

        /// <summary>
        /// Decodes an arbitrary large big integer.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the value length encoded in the underlying stream.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static BigInteger ReadBigInteger(this Stream stream, LengthFormat lengthFormat, bool littleEndian)
            => ReadBigInteger(stream, stream.ReadLength(lengthFormat), littleEndian);

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
        public static byte ReadByte(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
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
        public static sbyte ReadSByte(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
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
        public static short ReadInt16(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
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
        public static ushort ReadUInt16(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
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
        public static int ReadInt32(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
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
        public static uint ReadUInt32(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
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
        public static long ReadInt64(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
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
        public static ulong ReadUInt64(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
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
        public static float ReadSingle(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null)
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
        public static double ReadDouble(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null)
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
        public static decimal ReadDecimal(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Number, IFormatProvider? provider = null)
            => Read<decimal, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, in context, buffer);

        /// <summary>
        /// Decodes <see cref="BigInteger"/> from its string representation.
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
        public static BigInteger ReadBigInteger(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<BigInteger, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, in context, buffer);

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
        public static Guid ReadGuid(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer)
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
        public static Guid ReadGuid(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, string format)
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
        public static DateTime ReadDateTime(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
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
        public static DateTime ReadDateTime(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, string[] formats, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
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
        public static DateTimeOffset ReadDateTimeOffset(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
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
        public static DateTimeOffset ReadDateTimeOffset(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, string[] formats, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
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
        public static TimeSpan ReadTimeSpan(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, IFormatProvider? provider = null)
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
        public static TimeSpan ReadTimeSpan(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, string[] formats, TimeSpanStyles style = TimeSpanStyles.None, IFormatProvider? provider = null)
            => Read<TimeSpan, TimeSpanDecoder>(stream, new TimeSpanDecoder(style, formats, provider), lengthFormat, in context, buffer);

        private static int ReadLength(this Stream stream, LengthFormat lengthFormat)
        {
            int result;
            var littleEndian = BitConverter.IsLittleEndian;
            switch (lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case LengthFormat.Plain:
                    result = stream.Read<int>();
                    break;
                case LengthFormat.PlainLittleEndian:
                    littleEndian = true;
                    goto case LengthFormat.Plain;
                case LengthFormat.PlainBigEndian:
                    littleEndian = false;
                    goto case LengthFormat.Plain;
                case LengthFormat.Compressed:
                    result = stream.Read7BitEncodedInt();
                    break;
            }

            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        private static async ValueTask<int> ReadLengthAsync(this Stream stream, LengthFormat lengthFormat, Memory<byte> buffer, CancellationToken token)
        {
            ValueTask<int> result;
            var littleEndian = BitConverter.IsLittleEndian;
            switch (lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case LengthFormat.Plain:
                    result = stream.ReadAsync<int>(buffer, token);
                    break;
                case LengthFormat.PlainLittleEndian:
                    littleEndian = true;
                    goto case LengthFormat.Plain;
                case LengthFormat.PlainBigEndian:
                    littleEndian = false;
                    goto case LengthFormat.Plain;
                case LengthFormat.Compressed:
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
        public static string ReadString(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer)
            => ReadString(stream, stream.ReadLength(lengthFormat), in context, buffer);

        /// <summary>
        /// Reads the string using the specified encoding.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="length">The length of the string, in bytes.</param>
        /// <param name="encoding">The encoding used to decode bytes from stream into characters.</param>
        /// <returns>The string decoded from the log entry content stream.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        [SkipLocalsInit]
        public static string ReadString(this Stream stream, int length, Encoding encoding)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (length == 0)
                return string.Empty;

            int charCount;
            using MemoryRental<char> charBuffer = length <= MemoryRental<char>.StackallocThreshold ? stackalloc char[length] : new MemoryRental<char>(length);

            using (MemoryRental<byte> bytesBuffer = length <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[length] : new MemoryRental<byte>(length))
            {
                stream.ReadBlock(bytesBuffer.Span);
                charCount = encoding.GetChars(bytesBuffer.Span, charBuffer.Span);
            }

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
        public static string ReadString(this Stream stream, LengthFormat lengthFormat, Encoding encoding)
            => ReadString(stream, stream.ReadLength(lengthFormat), encoding);

        private static async ValueTask<TResult> ReadAsync<TResult, TDecoder>(Stream stream, TDecoder decoder, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, CancellationToken token)
            where TResult : struct
            where TDecoder : ISpanDecoder<TResult>
        {
            var length = await ReadLengthAsync(stream, lengthFormat, buffer, token).ConfigureAwait(false);
            if (length == 0)
                throw new EndOfStreamException();
            using var result = MemoryAllocator.Allocate<char>(length, true);
            length = await ReadStringAsync(stream, result.Memory, context, buffer, token).ConfigureAwait(false);
            return decoder.Decode(result.Memory.Slice(0, length).Span);
        }

        private static async ValueTask<TResult> ReadAsync<TResult, TDecoder>(Stream stream, TDecoder decoder, LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
            where TResult : struct
            where TDecoder : ISpanDecoder<TResult>
        {
            int length;
            MemoryOwner<byte> buffer;
            using (buffer = MemoryAllocator.Allocate<byte>(BufferSizeForLength, false))
                length = await ReadLengthAsync(stream, lengthFormat, buffer.Memory, token).ConfigureAwait(false);

            if (length == 0)
                throw new EndOfStreamException();

            using var result = MemoryAllocator.Allocate<char>(length, true);
            using (buffer = MemoryAllocator.Allocate<byte>(length, false))
            {
                length = await ReadStringAsync(stream, result.Memory, context, buffer.Memory, token).ConfigureAwait(false);
                return decoder.Decode(result.Memory.Slice(0, length).Span);
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
        /// Decodes an arbitrary integer value asynchronously.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="length">The length of the value, in bytes.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding the value.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask<BigInteger> ReadBigIntegerAsync(this Stream stream, int length, bool littleEndian, Memory<byte> buffer, CancellationToken token = default)
        {
            if (length == 0)
                return BigInteger.Zero;
            if (buffer.Length < length)
                throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

            buffer = buffer.Slice(0, length);
            await stream.ReadBlockAsync(buffer, token).ConfigureAwait(false);
            return new BigInteger(buffer.Span, isBigEndian: !littleEndian);
        }

        /// <summary>
        /// Decodes an arbitrary integer value asynchronously.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="length">The length of the value, in bytes.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask<BigInteger> ReadBigIntegerAsync(this Stream stream, int length, bool littleEndian, CancellationToken token = default)
        {
            if (length == 0)
                return BigInteger.Zero;

            using var buffer = MemoryAllocator.Allocate<byte>(length, true);
            await stream.ReadBlockAsync(buffer.Memory, token).ConfigureAwait(false);
            return new BigInteger(buffer.Memory.Span, isBigEndian: !littleEndian);
        }

        /// <summary>
        /// Decodes an arbitrary integer value asynchronously.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the value length encoded in the underlying stream.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding the value.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static async ValueTask<BigInteger> ReadBigIntegerAsync(this Stream stream, LengthFormat lengthFormat, bool littleEndian, Memory<byte> buffer, CancellationToken token = default)
            => await ReadBigIntegerAsync(stream, await stream.ReadLengthAsync(lengthFormat, buffer, token).ConfigureAwait(false), littleEndian, buffer, token).ConfigureAwait(false);

        /// <summary>
        /// Decodes an arbitrary integer value asynchronously.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the value length encoded in the underlying stream.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        public static async ValueTask<BigInteger> ReadBigIntegerAsync(this Stream stream, LengthFormat lengthFormat, bool littleEndian, CancellationToken token = default)
        {
            using var lengthDecodingBuffer = MemoryAllocator.Allocate<byte>(BufferSizeForLength, false);
            return await ReadBigIntegerAsync(stream, await stream.ReadLengthAsync(lengthFormat, lengthDecodingBuffer.Memory, token).ConfigureAwait(false), littleEndian, token).ConfigureAwait(false);
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
            using var result = MemoryAllocator.Allocate<char>(length, true);
            length = await ReadStringAsync(stream, result.Memory, context, buffer, token).ConfigureAwait(false);
            return new string(result.Memory.Slice(0, length).Span);
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
        public static async ValueTask<string> ReadStringAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, CancellationToken token = default)
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

            using var charBuffer = MemoryAllocator.Allocate<char>(length, true);
            int charCount;

            using (var bytesBuffer = MemoryAllocator.Allocate<byte>(length, true))
            {
                await stream.ReadBlockAsync(bytesBuffer.Memory, token).ConfigureAwait(false);
                charCount = encoding.GetChars(bytesBuffer.Memory.Span, charBuffer.Memory.Span);
            }

            return new string(charBuffer.Memory.Span.Slice(0, charCount));
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
        public static async ValueTask<string> ReadStringAsync(this Stream stream, LengthFormat lengthFormat, Encoding encoding, CancellationToken token = default)
        {
            using var lengthDecodingBuffer = MemoryAllocator.Allocate<byte>(BufferSizeForLength, false);
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
        public static ValueTask<byte> ReadByteAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<sbyte> ReadSByteAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<short> ReadInt16Async(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<ushort> ReadUInt16Async(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<int> ReadInt32Async(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<uint> ReadUInt32Async(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<long> ReadInt64Async(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<ulong> ReadUInt64Async(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<float> ReadSingleAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<double> ReadDoubleAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<decimal> ReadDecimalAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Number, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<decimal, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, buffer, token);

        /// <summary>
        /// Decodes <see cref="BigInteger"/> from its string representation.
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
        public static ValueTask<BigInteger> ReadBigIntegerAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<BigInteger, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, buffer, token);

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
        public static ValueTask<Guid> ReadGuidAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, CancellationToken token = default)
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
        public static ValueTask<Guid> ReadGuidAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, string format, CancellationToken token = default)
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
        public static ValueTask<DateTime> ReadDateTimeAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<DateTime> ReadDateTimeAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, string[] formats, Memory<byte> buffer, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<DateTimeOffset> ReadDateTimeOffsetAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<DateTimeOffset> ReadDateTimeOffsetAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, string[] formats, Memory<byte> buffer, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<TimeSpan> ReadTimeSpanAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<TimeSpan> ReadTimeSpanAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, string[] formats, TimeSpanStyles style = TimeSpanStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<byte> ReadByteAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<sbyte> ReadSByteAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<short> ReadInt16Async(this Stream stream, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<ushort> ReadUInt16Async(this Stream stream, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<int> ReadInt32Async(this Stream stream, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<uint> ReadUInt32Async(this Stream stream, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<long> ReadInt64Async(this Stream stream, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<ulong> ReadUInt64Async(this Stream stream, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<float> ReadSingleAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<double> ReadDoubleAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<decimal> ReadDecimalAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Number, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<decimal, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, token);

        /// <summary>
        /// Decodes <see cref="BigInteger"/> from its string representation.
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
        public static ValueTask<BigInteger> ReadBigIntegerAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null, CancellationToken token = default)
            => ReadAsync<BigInteger, NumberDecoder>(stream, new NumberDecoder(style, provider), lengthFormat, context, token);

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
        public static ValueTask<Guid> ReadGuidAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, CancellationToken token = default)
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
        public static ValueTask<Guid> ReadGuidAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, string format, CancellationToken token = default)
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
        public static ValueTask<DateTime> ReadDateTimeAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<DateTime> ReadDateTimeAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<DateTimeOffset> ReadDateTimeOffsetAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<DateTimeOffset> ReadDateTimeOffsetAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<TimeSpan> ReadTimeSpanAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider = null, CancellationToken token = default)
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
        public static ValueTask<TimeSpan> ReadTimeSpanAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, string[] formats, TimeSpanStyles style = TimeSpanStyles.None, IFormatProvider? provider = null, CancellationToken token = default)
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
        /// Decodes the block of bytes.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the block length encoded in the underlying stream.</param>
        /// <param name="allocator">The memory allocator used to place the decoded block of bytes.</param>
        /// <returns>The decoded block of bytes.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        public static MemoryOwner<byte> ReadBlock(this Stream stream, LengthFormat lengthFormat, MemoryAllocator<byte>? allocator = null)
        {
            var length = stream.ReadLength(lengthFormat);
            MemoryOwner<byte> result;
            if (length > 0)
            {
                result = allocator.Invoke(length, true);
                stream.ReadBlock(result.Memory.Span);
            }
            else
            {
                result = default;
            }

            return result;
        }

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
        /// Decodes the block of bytes asynchronously.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="lengthFormat">The format of the block length encoded in the underlying stream.</param>
        /// <param name="buffer">The buffer that is allocated by the caller.</param>
        /// <param name="allocator">The memory allocator used to place the decoded block of bytes.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The decoded block of bytes.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async ValueTask<MemoryOwner<byte>> ReadBlockAsync(this Stream stream, LengthFormat lengthFormat, Memory<byte> buffer, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
        {
            var length = await stream.ReadLengthAsync(lengthFormat, buffer, token).ConfigureAwait(false);
            MemoryOwner<byte> result;
            if (length > 0)
            {
                result = allocator.Invoke(length, true);
                await stream.ReadBlockAsync(result.Memory, token).ConfigureAwait(false);
            }
            else
            {
                result = default;
            }

            return result;
        }

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
            using var buffer = MemoryAllocator.Allocate<byte>(Unsafe.SizeOf<T>(), true);
            await stream.ReadBlockAsync(buffer.Memory, token).ConfigureAwait(false);
            return MemoryMarshal.Read<T>(buffer.Memory.Span);
        }

        /// <summary>
        /// Asynchronously reads the bytes from the source stream and passes them to the consumer, using a specified buffer.
        /// </summary>
        /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
        /// <param name="source">The source stream to read from.</param>
        /// <param name="consumer">The destination stream to write into.</param>
        /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
        /// <param name="token">The token that can be used to cancel this operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async Task CopyToAsync<TConsumer>(this Stream source, TConsumer consumer, Memory<byte> buffer, CancellationToken token = default)
            where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
        {
            for (int count; (count = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0;)
            {
                await consumer.Invoke(buffer.Slice(0, count), token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously reads the bytes from the source stream and writes them to another stream, using a specified buffer.
        /// </summary>
        /// <param name="source">The source stream to read from.</param>
        /// <param name="destination">The destination stream to write into.</param>
        /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
        /// <param name="token">The token that can be used to cancel this operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static Task CopyToAsync(this Stream source, Stream destination, Memory<byte> buffer, CancellationToken token = default)
            => CopyToAsync<StreamConsumer>(source, destination, buffer, token);

        /// <summary>
        /// Synchronously reads the bytes from the source stream and passes them to the consumer, using a specified buffer.
        /// </summary>
        /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
        /// <param name="source">The source stream to read from.</param>
        /// <param name="consumer">The destination stream to write into.</param>
        /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
        /// <param name="token">The token that can be used to cancel this operation.</param>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static void CopyTo<TConsumer>(this Stream source, TConsumer consumer, Span<byte> buffer, CancellationToken token = default)
            where TConsumer : notnull, IReadOnlySpanConsumer<byte>
        {
            for (int count; (count = source.Read(buffer)) > 0; token.ThrowIfCancellationRequested())
            {
                consumer.Invoke(buffer.Slice(0, count));
            }
        }

        /// <summary>
        /// Synchronously reads the bytes from the source stream and writes them to another stream, using a specified buffer.
        /// </summary>
        /// <param name="source">The source stream to read from.</param>
        /// <param name="destination">The destination stream to write into.</param>
        /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
        /// <param name="token">The token that can be used to cancel this operation.</param>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static void CopyTo(this Stream source, Stream destination, Span<byte> buffer, CancellationToken token = default)
            => CopyTo<StreamConsumer>(source, destination, buffer, token);

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
        public static void CopyTo<TArg>(this Stream stream, ReadOnlySpanAction<byte, TArg> reader, TArg arg, Span<byte> buffer, CancellationToken token = default)
            => CopyTo(stream, new DelegatingReadOnlySpanConsumer<byte, TArg>(reader, arg), buffer, token);

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
        [SkipLocalsInit]
        public static void CopyTo<TArg>(this Stream stream, ReadOnlySpanAction<byte, TArg> reader, TArg arg, int bufferSize = DefaultBufferSize, CancellationToken token = default)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            using var owner = bufferSize <= MemoryRental<byte>.StackallocThreshold ? new MemoryRental<byte>(stackalloc byte[bufferSize]) : new MemoryRental<byte>(bufferSize);
            CopyTo(stream, reader, arg, owner.Span, token);
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
        public static Task CopyToAsync<TArg>(this Stream stream, ReadOnlySpanAction<byte, TArg> reader, TArg arg, Memory<byte> buffer, CancellationToken token = default)
            => CopyToAsync(stream, new DelegatingReadOnlySpanConsumer<byte, TArg>(reader, arg), buffer, token);

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
        public static async Task CopyToAsync<TArg>(this Stream stream, ReadOnlySpanAction<byte, TArg> reader, TArg arg, int bufferSize = DefaultBufferSize, CancellationToken token = default)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            using var owner = MemoryAllocator.Allocate<byte>(bufferSize, false);
            await CopyToAsync(stream, reader, arg, owner.Memory, token).ConfigureAwait(false);
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
        public static Task CopyToAsync<TArg>(this Stream stream, Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> reader, TArg arg, Memory<byte> buffer, CancellationToken token = default)
            => CopyToAsync(stream, new DelegatingMemoryConsumer<byte, TArg>(reader, arg), buffer, token);

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
        public static async Task CopyToAsync<TArg>(this Stream stream, Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> reader, TArg arg, int bufferSize = DefaultBufferSize, CancellationToken token = default)
        {
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            using var owner = MemoryAllocator.Allocate<byte>(bufferSize, false);
            await CopyToAsync(stream, reader, arg, owner.Memory, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously reads the bytes from the current stream and writes them to buffer
        /// writer, using a specified cancellation token.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="destination">The writer to which the contents of the current stream will be copied.</param>
        /// <param name="bufferSize">The size, in bytes, of the buffer.</param>
        /// <param name="token">The token to monitor for cancellation requests.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is negative or zero.</exception>
        /// <exception cref="NotSupportedException"><paramref name="source"/> doesn't support reading.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static async Task CopyToAsync(this Stream source, IBufferWriter<byte> destination, int bufferSize = 0, CancellationToken token = default)
        {
            if (destination is null)
                throw new ArgumentNullException(nameof(destination));
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            for (int count; ; destination.Advance(count))
            {
                var buffer = destination.GetMemory(bufferSize);
                count = await source.ReadAsync(buffer, token).ConfigureAwait(false);
                if (count <= 0)
                    break;
            }
        }

        /// <summary>
        /// Synchronously reads the bytes from the current stream and writes them to buffer
        /// writer, using a specified cancellation token.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="destination">The writer to which the contents of the current stream will be copied.</param>
        /// <param name="bufferSize">The size, in bytes, of the buffer.</param>
        /// <param name="token">The token to monitor for cancellation requests.</param>
        /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is negative or zero.</exception>
        /// <exception cref="NotSupportedException"><paramref name="source"/> doesn't support reading.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static void CopyTo(this Stream source, IBufferWriter<byte> destination, int bufferSize = 0, CancellationToken token = default)
        {
            if (destination is null)
                throw new ArgumentNullException(nameof(destination));
            if (bufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));

            for (int count; ; token.ThrowIfCancellationRequested())
            {
                var buffer = destination.GetSpan(bufferSize);
                count = source.Read(buffer);
                if (count <= 0)
                    break;
                destination.Advance(count);
            }
        }
    }
}
