using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Missing = System.Reflection.Missing;

namespace DotNext.IO
{
    using Buffers;
    using static Buffers.BufferReader;
    using static Pipelines.PipeExtensions;
    using DecodingContext = Text.DecodingContext;

    /// <summary>
    /// Represents binary reader for the sequence of bytes.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct SequenceBinaryReader : IAsyncBinaryReader // TODO: Rename to SequenceReader in .NEXT 4
    {
        private readonly ReadOnlySequence<byte> sequence;
        private SequencePosition position;

        internal SequenceBinaryReader(ReadOnlySequence<byte> sequence)
        {
            this.sequence = sequence;
            position = sequence.Start;
        }

        internal SequenceBinaryReader(ReadOnlyMemory<byte> memory)
            : this(new ReadOnlySequence<byte>(memory))
        {
        }

        /// <summary>
        /// Resets the reader so it can be used again.
        /// </summary>
        public void Reset() => position = sequence.Start;

        /// <summary>
        /// Gets unread part of the sequence.
        /// </summary>
        public ReadOnlySequence<byte> RemainingSequence => sequence.Slice(position);

        /// <summary>
        /// Gets position in the underlying sequence.
        /// </summary>
        public SequencePosition Position => position;

        private TResult Read<TResult, TParser>(TParser parser)
            where TParser : struct, IBufferReader<TResult>
        {
            parser.Append<TResult, TParser>(RemainingSequence, out position);
            return parser.RemainingBytes == 0 ? parser.Complete() : throw new EndOfStreamException();
        }

        private TResult Read<TResult, TDecoder, TBuffer>(ref TDecoder decoder, in DecodingContext context, TBuffer buffer)
            where TResult : struct
            where TBuffer : struct, IBuffer<char>
            where TDecoder : struct, ISpanDecoder<TResult>
        {
            var parser = new StringReader<TBuffer>(in context, buffer);
            parser.Append<string, StringReader<TBuffer>>(RemainingSequence, out position);
            return parser.RemainingBytes == 0 ? decoder.Decode(parser.Complete()) : throw new EndOfStreamException();
        }

#if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        private unsafe TResult Read<TResult, TDecoder>(TDecoder decoder, LengthFormat lengthFormat, in DecodingContext context)
            where TResult : struct
            where TDecoder : struct, ISpanDecoder<TResult>
        {
            var length = ReadLength(lengthFormat);
            if ((uint)length > MemoryRental<char>.StackallocThreshold)
            {
                using var buffer = new ArrayBuffer<char>(length);
                return Read<TResult, TDecoder, ArrayBuffer<char>>(ref decoder, in context, buffer);
            }
            else
            {
                var buffer = stackalloc char[length];
                return Read<TResult, TDecoder, UnsafeBuffer<char>>(ref decoder, in context, new UnsafeBuffer<char>(buffer, length));
            }
        }

        /// <summary>
        /// Decodes the value of blittable type from the sequence of bytes.
        /// </summary>
        /// <typeparam name="T">The type of the value to decode.</typeparam>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
        public T Read<T>()
            where T : unmanaged
        {
            var result = default(T);
            Read(Span.AsBytes(ref result));
            return result;
        }

        /// <summary>
        /// Copies the bytes from the sequence into contiguous block of memory.
        /// </summary>
        /// <param name="output">The block of memory to fill.</param>
        /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
        public void Read(Memory<byte> output) => Read(output.Span);

        /// <summary>
        /// Copies the bytes from the sequence into contiguous block of memory.
        /// </summary>
        /// <param name="output">The block of memory to fill.</param>
        /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
        public void Read(Span<byte> output)
        {
            RemainingSequence.CopyTo(output, out var writtenCount);
            if (writtenCount != output.Length)
                throw new EndOfStreamException();

            position = sequence.GetPosition(writtenCount, position);
        }

        /// <summary>
        /// Skips the specified number of bytes.
        /// </summary>
        /// <param name="length">The number of bytes to skip.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
        public void Skip(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            try
            {
                position = sequence.GetPosition(length, position);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new EndOfStreamException(e.Message, e);
            }
        }

        /// <summary>
        /// Reads length-prefixed block of bytes.
        /// </summary>
        /// <param name="lengthFormat">The format of the block length encoded in the underlying stream.</param>
        /// <param name="allocator">The memory allocator used to place the decoded block of bytes.</param>
        /// <returns>The decoded block of bytes.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
        public MemoryOwner<byte> Read(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator = null)
        {
            var length = ReadLength(lengthFormat);
            MemoryOwner<byte> result;
            if (length > 0)
            {
                result = allocator.Invoke(length, true);
                Read(result.Memory);
            }
            else
            {
                result = default;
            }

            return result;
        }

        /// <summary>
        /// Parses 64-bit signed integer from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public long ReadInt64(LengthFormat lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<long, NumberDecoder>(new NumberDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Parses 64-bit unsigned integer from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public ulong ReadUInt64(LengthFormat lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<ulong, NumberDecoder>(new NumberDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Decodes 64-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying sequence doesn't contain necessary amount of bytes to decode the value.</exception>
        public long ReadInt64(bool littleEndian)
        {
            var result = Read<long>();
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Decodes 64-bit unsigned integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying sequence doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public ulong ReadUInt64(bool littleEndian)
        {
            var result = Read<ulong>();
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Decodes 32-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying sequence doesn't contain necessary amount of bytes to decode the value.</exception>
        public int ReadInt32(bool littleEndian)
        {
            var result = Read<int>();
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Parses 32-bit signed integer from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public int ReadInt32(LengthFormat lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<int, NumberDecoder>(new NumberDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Parses 32-bit unsigned integer from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public uint ReadUInt32(LengthFormat lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<uint, NumberDecoder>(new NumberDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Decodes 32-bit unsigned integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying sequence doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public uint ReadUInt32(bool littleEndian)
        {
            var result = Read<uint>();
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Parses 16-bit signed integer from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public short ReadInt16(LengthFormat lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<short, NumberDecoder>(new NumberDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Decodes 16-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying sequence doesn't contain necessary amount of bytes to decode the value.</exception>
        public short ReadInt16(bool littleEndian)
        {
            var result = Read<short>();
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Parses 16-bit unsigned integer from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public ushort ReadUInt16(LengthFormat lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<ushort, NumberDecoder>(new NumberDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Decodes 16-bit unsigned integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying sequence doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public ushort ReadUInt16(bool littleEndian)
        {
            var result = Read<ushort>();
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Parses 8-bit unsigned integer from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public byte ReadByte(LengthFormat lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<byte, NumberDecoder>(new NumberDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Parses 8-bit unsigned integer from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public sbyte ReadSByte(LengthFormat lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<sbyte, NumberDecoder>(new NumberDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Parses single-precision floating-point number from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public float ReadSingle(LengthFormat lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null)
            => Read<float, NumberDecoder>(new NumberDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Parses double-precision floating-point number from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public double ReadDouble(LengthFormat lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null)
            => Read<double, NumberDecoder>(new NumberDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Parses <see cref="decimal"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public decimal ReadDecimal(LengthFormat lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Number, IFormatProvider? provider = null)
            => Read<decimal, NumberDecoder>(new NumberDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Parses <see cref="DateTime"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The date/time is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public DateTime ReadDateTime(LengthFormat lengthFormat, in DecodingContext context, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
            => Read<DateTime, DateTimeDecoder>(new DateTimeDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Parses <see cref="DateTime"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The date/time is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public DateTime ReadDateTime(LengthFormat lengthFormat, in DecodingContext context, string[] formats, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
            => Read<DateTime, DateTimeDecoder>(new DateTimeDecoder(style, formats, provider), lengthFormat, in context);

        /// <summary>
        /// Parses <see cref="DateTimeOffset"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The date/time is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public DateTimeOffset ReadDateTimeOffset(LengthFormat lengthFormat, in DecodingContext context, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
            => Read<DateTimeOffset, DateTimeDecoder>(new DateTimeDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Parses <see cref="DateTimeOffset"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The date/time is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public DateTimeOffset ReadDateTimeOffset(LengthFormat lengthFormat, in DecodingContext context, string[] formats, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
            => Read<DateTimeOffset, DateTimeDecoder>(new DateTimeDecoder(style, formats, provider), lengthFormat, in context);

        /// <summary>
        /// Parses <see cref="Guid"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">GUID value is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public Guid ReadGuid(LengthFormat lengthFormat, in DecodingContext context)
            => Read<Guid, GuidDecoder>(new GuidDecoder(), lengthFormat, in context);

        /// <summary>
        /// Parses <see cref="Guid"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="format">The expected format of GUID value.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">GUID value is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public Guid ReadGuid(LengthFormat lengthFormat, in DecodingContext context, string format)
            => Read<Guid, GuidDecoder>(new GuidDecoder(format), lengthFormat, in context);

        /// <summary>
        /// Parses <see cref="TimeSpan"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The time span is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public TimeSpan ReadTimeSpan(LengthFormat lengthFormat, in DecodingContext context, IFormatProvider? provider = null)
            => Read<TimeSpan, TimeSpanDecoder>(new TimeSpanDecoder(provider), lengthFormat, in context);

        /// <summary>
        /// Parses <see cref="TimeSpan"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="formats">An array of allowable formats.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The time span is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public TimeSpan ReadTimeSpan(LengthFormat lengthFormat, in DecodingContext context, string[] formats, TimeSpanStyles style = TimeSpanStyles.None, IFormatProvider? provider = null)
            => Read<TimeSpan, TimeSpanDecoder>(new TimeSpanDecoder(style, formats, provider), lengthFormat, in context);

        /// <summary>
        /// Parses <see cref="BigInteger"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The number is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public BigInteger ReadBigInteger(LengthFormat lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read<BigInteger, NumberDecoder>(new NumberDecoder(style, provider), lengthFormat, in context);

        /// <summary>
        /// Decodes an arbitrary large integer.
        /// </summary>
        /// <param name="length">The length of the value, in bytes.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
#if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        public unsafe BigInteger ReadBigInteger(int length, bool littleEndian)
        {
            BigInteger result;

            if (length == 0)
            {
                result = BigInteger.Zero;
            }
            else if ((uint)length > MemoryRental<byte>.StackallocThreshold)
            {
                using var buffer = new ArrayBuffer<byte>(length);
                result = Read<BigInteger, BigIntegerReader<ArrayBuffer<byte>>>(new BigIntegerReader<ArrayBuffer<byte>>(buffer, littleEndian));
            }
            else
            {
                var buffer = stackalloc byte[length];
                result = Read<BigInteger, BigIntegerReader<UnsafeBuffer<byte>>>(new BigIntegerReader<UnsafeBuffer<byte>>(new UnsafeBuffer<byte>(buffer, length), littleEndian));
            }

            return result;
        }

        /// <summary>
        /// Decodes an arbitrary large integer.
        /// </summary>
        /// <param name="lengthFormat">The format of the value length encoded in the underlying stream.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public BigInteger ReadBigInteger(LengthFormat lengthFormat, bool littleEndian)
            => ReadBigInteger(ReadLength(lengthFormat), littleEndian);

        /// <summary>
        /// Decodes the string.
        /// </summary>
        /// <param name="length">The length of the encoded string, in bytes.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <returns>The decoded string.</returns>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
#if !NETSTANDARD2_1
        [SkipLocalsInit]
#endif
        public unsafe string ReadString(int length, in DecodingContext context)
        {
            string result;

            if (length == 0)
            {
                result = string.Empty;
            }
            else if ((uint)length > MemoryRental<char>.StackallocThreshold)
            {
                using var buffer = new ArrayBuffer<char>(length);
                result = Read<string, StringReader<ArrayBuffer<char>>>(new StringReader<ArrayBuffer<char>>(in context, buffer));
            }
            else
            {
                var buffer = stackalloc char[length];
                result = Read<string, StringReader<UnsafeBuffer<char>>>(new StringReader<UnsafeBuffer<char>>(in context, new UnsafeBuffer<char>(buffer, length)));
            }

            return result;
        }

        private int ReadLength(LengthFormat lengthFormat)
        {
            int length;
            var littleEndian = BitConverter.IsLittleEndian;
            switch (lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case LengthFormat.Plain:
                    length = Read<int>();
                    break;
                case LengthFormat.PlainLittleEndian:
                    littleEndian = true;
                    goto case LengthFormat.Plain;
                case LengthFormat.PlainBigEndian:
                    littleEndian = false;
                    goto case LengthFormat.Plain;
                case LengthFormat.Compressed:
                    length = Read<int, SevenBitEncodedIntReader>(new SevenBitEncodedIntReader(5));
                    break;
            }

            length.ReverseIfNeeded(littleEndian);
            return length;
        }

        /// <summary>
        /// Decodes the string.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <returns>The decoded string.</returns>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public string ReadString(LengthFormat lengthFormat, in DecodingContext context)
            => ReadString(ReadLength(lengthFormat), in context);

        /// <inheritdoc/>
        ValueTask<T> IAsyncBinaryReader.ReadAsync<T>(CancellationToken token)
        {
            ValueTask<T> result;

            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<T>(token));
#else
                result = ValueTask.FromCanceled<T>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(Read<T>());
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<T>(e));
#else
                    result = ValueTask.FromException<T>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
        {
            ValueTask result;

            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new ValueTask();
                try
                {
                    Read(output);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask IAsyncBinaryReader.SkipAsync(int length, CancellationToken token)
        {
            ValueTask result;

            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new ValueTask();
                try
                {
                    Skip(length);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<MemoryOwner<byte>> IAsyncBinaryReader.ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator, CancellationToken token)
        {
            ValueTask<MemoryOwner<byte>> result;

            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<MemoryOwner<byte>>(token));
#else
                result = ValueTask.FromCanceled<MemoryOwner<byte>>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(Read(lengthFormat, allocator));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<MemoryOwner<byte>>(e));
#else
                    result = ValueTask.FromException<MemoryOwner<byte>>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(bool littleEndian, CancellationToken token)
        {
            ValueTask<long> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<long>(token));
#else
                result = ValueTask.FromCanceled<long>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadInt64(littleEndian));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<long>(e));
#else
                    result = ValueTask.FromException<long>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<long> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<long>(token));
#else
                result = ValueTask.FromCanceled<long>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadInt64(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<long>(e));
#else
                    result = ValueTask.FromException<long>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(bool littleEndian, CancellationToken token)
        {
            ValueTask<int> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<int>(token));
#else
                result = ValueTask.FromCanceled<int>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadInt32(littleEndian));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<int>(e));
#else
                    result = ValueTask.FromException<int>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<int> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<int>(token));
#else
                result = ValueTask.FromCanceled<int>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadInt32(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<int>(e));
#else
                    result = ValueTask.FromException<int>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(bool littleEndian, CancellationToken token)
        {
            ValueTask<short> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<short>(token));
#else
                result = ValueTask.FromCanceled<short>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadInt16(littleEndian));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<short>(e));
#else
                    result = ValueTask.FromException<short>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<short> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<short>(token));
#else
                result = ValueTask.FromCanceled<short>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadInt16(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<short>(e));
#else
                    result = ValueTask.FromException<short>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<byte> IAsyncBinaryReader.ReadByteAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<byte> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<byte>(token));
#else
                result = ValueTask.FromCanceled<byte>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadByte(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<byte>(e));
#else
                    result = ValueTask.FromException<byte>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<float> IAsyncBinaryReader.ReadSingleAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<float> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<float>(token));
#else
                result = ValueTask.FromCanceled<float>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadSingle(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<float>(e));
#else
                    result = ValueTask.FromException<float>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<double> IAsyncBinaryReader.ReadDoubleAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<double> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<double>(token));
#else
                result = ValueTask.FromCanceled<double>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadDouble(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<double>(e));
#else
                    result = ValueTask.FromException<double>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<decimal> IAsyncBinaryReader.ReadDecimalAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<decimal> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<decimal>(token));
#else
                result = ValueTask.FromCanceled<decimal>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadDecimal(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<decimal>(e));
#else
                    result = ValueTask.FromException<decimal>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<BigInteger> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<BigInteger>(token));
#else
                result = ValueTask.FromCanceled<BigInteger>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadBigInteger(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<BigInteger>(e));
#else
                    result = ValueTask.FromException<BigInteger>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<DateTime> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<DateTime>(token));
#else
                result = ValueTask.FromCanceled<DateTime>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadDateTime(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<DateTime>(e));
#else
                    result = ValueTask.FromException<DateTime>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<DateTime> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<DateTime>(token));
#else
                result = ValueTask.FromCanceled<DateTime>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadDateTime(lengthFormat, in context, formats, style, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<DateTime>(e));
#else
                    result = ValueTask.FromException<DateTime>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<DateTimeOffset> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<DateTimeOffset>(token));
#else
                result = ValueTask.FromCanceled<DateTimeOffset>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadDateTimeOffset(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<DateTimeOffset>(e));
#else
                    result = ValueTask.FromException<DateTimeOffset>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<DateTimeOffset> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<DateTimeOffset>(token));
#else
                result = ValueTask.FromCanceled<DateTimeOffset>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadDateTimeOffset(lengthFormat, in context, formats, style, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<DateTimeOffset>(e));
#else
                    result = ValueTask.FromException<DateTimeOffset>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
        {
            ValueTask<Guid> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<Guid>(token));
#else
                result = ValueTask.FromCanceled<Guid>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadGuid(lengthFormat, in context));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<Guid>(e));
#else
                    result = ValueTask.FromException<Guid>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(LengthFormat lengthFormat, DecodingContext context, string format, CancellationToken token)
        {
            ValueTask<Guid> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<Guid>(token));
#else
                result = ValueTask.FromCanceled<Guid>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadGuid(lengthFormat, in context, format));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<Guid>(e));
#else
                    result = ValueTask.FromException<Guid>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<TimeSpan> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<TimeSpan>(token));
#else
                result = ValueTask.FromCanceled<TimeSpan>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadTimeSpan(lengthFormat, context, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<TimeSpan>(e));
#else
                    result = ValueTask.FromException<TimeSpan>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, TimeSpanStyles style, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<TimeSpan> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<TimeSpan>(token));
#else
                result = ValueTask.FromCanceled<TimeSpan>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadTimeSpan(lengthFormat, context, formats, style, provider));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<TimeSpan>(e));
#else
                    result = ValueTask.FromException<TimeSpan>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<string> IAsyncBinaryReader.ReadStringAsync(int length, DecodingContext context, CancellationToken token)
        {
            ValueTask<string> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<string>(token));
#else
                result = ValueTask.FromCanceled<string>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadString(length, context));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<string>(e));
#else
                    result = ValueTask.FromException<string>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<string> IAsyncBinaryReader.ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
        {
            ValueTask<string> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<string>(token));
#else
                result = ValueTask.FromCanceled<string>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadString(lengthFormat, context));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<string>(e));
#else
                    result = ValueTask.FromException<string>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(int length, bool littleEndian, CancellationToken token)
        {
            ValueTask<BigInteger> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<BigInteger>(token));
#else
                result = ValueTask.FromCanceled<BigInteger>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadBigInteger(length, littleEndian));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<BigInteger>(e));
#else
                    result = ValueTask.FromException<BigInteger>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(LengthFormat lengthFormat, bool littleEndian, CancellationToken token)
        {
            ValueTask<BigInteger> result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled<BigInteger>(token));
#else
                result = ValueTask.FromCanceled<BigInteger>(token);
#endif
            }
            else
            {
                try
                {
                    result = new(ReadBigInteger(lengthFormat, littleEndian));
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException<BigInteger>(e));
#else
                    result = ValueTask.FromException<BigInteger>(e);
#endif
                }
            }

            return result;
        }

        /// <inheritdoc/>
        Task IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
            => output.WriteAsync(RemainingSequence, token).AsTask();

        /// <inheritdoc/>
        Task IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
            => output.WriteAsync(RemainingSequence, token).AsTask();

        /// <inheritdoc/>
        Task IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, CancellationToken token)
        {
            Task result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled(token);
            }
            else
            {
                result = Task.CompletedTask;
                try
                {
                    writer.Write(RemainingSequence);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        Task IAsyncBinaryReader.CopyToAsync<TArg>(ReadOnlySpanAction<byte, TArg> reader, TArg arg, CancellationToken token)
        {
            Task result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled(token);
            }
            else
            {
                result = Task.CompletedTask;
                try
                {
                    for (ReadOnlyMemory<byte> block; sequence.TryGet(ref position, out block); token.ThrowIfCancellationRequested())
                        reader(block.Span, arg);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        async Task IAsyncBinaryReader.CopyToAsync<TArg>(Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> reader, TArg arg, CancellationToken token)
        {
            foreach (var segment in RemainingSequence)
                await reader(arg, segment, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        async Task IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        {
            foreach (var segment in RemainingSequence)
                await consumer.Invoke(segment, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        bool IAsyncBinaryReader.TryGetSequence(out ReadOnlySequence<byte> bytes)
        {
            bytes = RemainingSequence;
            return true;
        }
    }
}