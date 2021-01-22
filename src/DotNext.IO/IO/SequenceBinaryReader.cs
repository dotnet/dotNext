using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
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
    public struct SequenceBinaryReader : IAsyncBinaryReader
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

        private TResult Read<TResult, TParser>(TParser parser)
            where TParser : struct, IBufferReader<TResult>
        {
            parser.Append<TResult, TParser>(sequence.Slice(position), out position);
            return parser.RemainingBytes == 0 ? parser.Complete() : throw new EndOfStreamException();
        }

        private TResult Read<TResult, TDecoder, TBuffer>(ref TDecoder decoder, in DecodingContext context, TBuffer buffer)
            where TResult : struct
            where TBuffer : struct, IBuffer<char>
            where TDecoder : struct, ISpanDecoder<TResult>
        {
            var parser = new StringReader<TBuffer>(in context, buffer);
            parser.Append<string, StringReader<TBuffer>>(sequence.Slice(position), out position);
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
            if (length > MemoryRental<char>.StackallocThreshold)
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
            where T : unmanaged => Read<T, ValueReader<T>>(new ValueReader<T>());

        /// <summary>
        /// Copies the bytes from the sequence into contiguous block of memory.
        /// </summary>
        /// <param name="output">The block of memory to fill.</param>
        /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
        public void Read(Memory<byte> output) => Read<Missing, MemoryReader>(new MemoryReader(output));

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
            if (length > MemoryRental<char>.StackallocThreshold)
            {
                using var buffer = new ArrayBuffer<char>(length);
                return Read<string, StringReader<ArrayBuffer<char>>>(new StringReader<ArrayBuffer<char>>(in context, buffer));
            }
            else
            {
                var buffer = stackalloc char[length];
                return Read<string, StringReader<UnsafeBuffer<char>>>(new StringReader<UnsafeBuffer<char>>(in context, new UnsafeBuffer<char>(buffer, length)));
            }
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
            Task<T> result;

            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<T>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<T>(Read<T>());
                }
                catch (Exception e)
                {
                    result = Task.FromException<T>(e);
                }
            }

            return new ValueTask<T>(result);
        }

        /// <inheritdoc/>
        ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
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
                    Read(output);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        /// <inheritdoc/>
        ValueTask<MemoryOwner<byte>> IAsyncBinaryReader.ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator, CancellationToken token)
        {
            Task<MemoryOwner<byte>> result;

            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<MemoryOwner<byte>>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<MemoryOwner<byte>>(Read(lengthFormat, allocator));
                }
                catch (Exception e)
                {
                    result = Task.FromException<MemoryOwner<byte>>(e);
                }
            }

            return new ValueTask<MemoryOwner<byte>>(result);
        }

        /// <inheritdoc/>
        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(bool littleEndian, CancellationToken token)
        {
            Task<long> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<long>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<long>(ReadInt64(littleEndian));
                }
                catch (Exception e)
                {
                    result = Task.FromException<long>(e);
                }
            }

            return new ValueTask<long>(result);
        }

        /// <inheritdoc/>
        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            Task<long> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<long>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<long>(ReadInt64(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<long>(e);
                }
            }

            return new ValueTask<long>(result);
        }

        /// <inheritdoc/>
        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(bool littleEndian, CancellationToken token)
        {
            Task<int> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<int>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<int>(ReadInt32(littleEndian));
                }
                catch (Exception e)
                {
                    result = Task.FromException<int>(e);
                }
            }

            return new ValueTask<int>(result);
        }

        /// <inheritdoc/>
        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            Task<int> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<int>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<int>(ReadInt32(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<int>(e);
                }
            }

            return new ValueTask<int>(result);
        }

        /// <inheritdoc/>
        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(bool littleEndian, CancellationToken token)
        {
            Task<short> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<short>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<short>(ReadInt16(littleEndian));
                }
                catch (Exception e)
                {
                    result = Task.FromException<short>(e);
                }
            }

            return new ValueTask<short>(result);
        }

        /// <inheritdoc/>
        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            Task<short> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<short>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<short>(ReadInt16(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<short>(e);
                }
            }

            return new ValueTask<short>(result);
        }

        /// <inheritdoc/>
        ValueTask<byte> IAsyncBinaryReader.ReadByteAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            Task<byte> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<byte>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<byte>(ReadByte(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<byte>(e);
                }
            }

            return new ValueTask<byte>(result);
        }

        /// <inheritdoc/>
        ValueTask<float> IAsyncBinaryReader.ReadSingleAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            Task<float> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<float>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<float>(ReadSingle(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<float>(e);
                }
            }

            return new ValueTask<float>(result);
        }

        /// <inheritdoc/>
        ValueTask<double> IAsyncBinaryReader.ReadDoubleAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            Task<double> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<double>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<double>(ReadDouble(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<double>(e);
                }
            }

            return new ValueTask<double>(result);
        }

        /// <inheritdoc/>
        ValueTask<decimal> IAsyncBinaryReader.ReadDecimalAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            Task<decimal> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<decimal>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<decimal>(ReadDecimal(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<decimal>(e);
                }
            }

            return new ValueTask<decimal>(result);
        }

        /// <inheritdoc/>
        ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(LengthFormat lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
        {
            Task<BigInteger> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<BigInteger>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<BigInteger>(ReadBigInteger(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<BigInteger>(e);
                }
            }

            return new ValueTask<BigInteger>(result);
        }

        /// <inheritdoc/>
        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
        {
            Task<DateTime> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<DateTime>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<DateTime>(ReadDateTime(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<DateTime>(e);
                }
            }

            return new ValueTask<DateTime>(result);
        }

        /// <inheritdoc/>
        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
        {
            Task<DateTime> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<DateTime>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<DateTime>(ReadDateTime(lengthFormat, in context, formats, style, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<DateTime>(e);
                }
            }

            return new ValueTask<DateTime>(result);
        }

        /// <inheritdoc/>
        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(LengthFormat lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
        {
            Task<DateTimeOffset> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<DateTimeOffset>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<DateTimeOffset>(ReadDateTimeOffset(lengthFormat, in context, style, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<DateTimeOffset>(e);
                }
            }

            return new ValueTask<DateTimeOffset>(result);
        }

        /// <inheritdoc/>
        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
        {
            Task<DateTimeOffset> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<DateTimeOffset>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<DateTimeOffset>(ReadDateTimeOffset(lengthFormat, in context, formats, style, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<DateTimeOffset>(e);
                }
            }

            return new ValueTask<DateTimeOffset>(result);
        }

        /// <inheritdoc/>
        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
        {
            Task<Guid> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<Guid>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<Guid>(ReadGuid(lengthFormat, in context));
                }
                catch (Exception e)
                {
                    result = Task.FromException<Guid>(e);
                }
            }

            return new ValueTask<Guid>(result);
        }

        /// <inheritdoc/>
        ValueTask<Guid> IAsyncBinaryReader.ReadGuidAsync(LengthFormat lengthFormat, DecodingContext context, string format, CancellationToken token)
        {
            Task<Guid> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<Guid>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<Guid>(ReadGuid(lengthFormat, in context, format));
                }
                catch (Exception e)
                {
                    result = Task.FromException<Guid>(e);
                }
            }

            return new ValueTask<Guid>(result);
        }

        /// <inheritdoc/>
        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider, CancellationToken token)
        {
            Task<TimeSpan> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<TimeSpan>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<TimeSpan>(ReadTimeSpan(lengthFormat, context, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<TimeSpan>(e);
                }
            }

            return new ValueTask<TimeSpan>(result);
        }

        /// <inheritdoc/>
        ValueTask<TimeSpan> IAsyncBinaryReader.ReadTimeSpanAsync(LengthFormat lengthFormat, DecodingContext context, string[] formats, TimeSpanStyles style, IFormatProvider? provider, CancellationToken token)
        {
            Task<TimeSpan> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<TimeSpan>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<TimeSpan>(ReadTimeSpan(lengthFormat, context, formats, style, provider));
                }
                catch (Exception e)
                {
                    result = Task.FromException<TimeSpan>(e);
                }
            }

            return new ValueTask<TimeSpan>(result);
        }

        /// <inheritdoc/>
        ValueTask<string> IAsyncBinaryReader.ReadStringAsync(int length, DecodingContext context, CancellationToken token)
        {
            Task<string> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<string>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<string>(ReadString(length, context));
                }
                catch (Exception e)
                {
                    result = Task.FromException<string>(e);
                }
            }

            return new ValueTask<string>(result);
        }

        /// <inheritdoc/>
        ValueTask<string> IAsyncBinaryReader.ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
        {
            Task<string> result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled<string>(token);
            }
            else
            {
                try
                {
                    return new ValueTask<string>(ReadString(lengthFormat, context));
                }
                catch (Exception e)
                {
                    result = Task.FromException<string>(e);
                }
            }

            return new ValueTask<string>(result);
        }

        /// <inheritdoc/>
        Task IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
            => output.WriteAsync(sequence.Slice(position), token).AsTask();

        /// <inheritdoc/>
        Task IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
            => output.WriteAsync(sequence.Slice(position), token).AsTask();

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
                    writer.Write(sequence.Slice(position));
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
            foreach (var segment in sequence.Slice(position))
                await reader(arg, segment, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        async Task IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        {
            foreach (var segment in sequence.Slice(position))
                await consumer.Invoke(segment, token).ConfigureAwait(false);
        }
    }
}