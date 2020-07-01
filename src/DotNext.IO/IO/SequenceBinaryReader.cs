using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Missing = System.Reflection.Missing;

namespace DotNext.IO
{
    using Buffers;
    using static Pipelines.PipeExtensions;
    using DecodingContext = Text.DecodingContext;

    /// <summary>
    /// Represents binary reader for the sequence of bytes.
    /// </summary>
    public struct SequenceBinaryReader : IAsyncBinaryReader
    {
        private delegate TResult ValueParser<TResult, TStyle>(ReadOnlySpan<char> value, TStyle style, IFormatProvider? provider)
            where TResult : struct
            where TStyle : struct, Enum;

        // TODO: Should be replaced with function pointer in C# 9
        private static readonly ValueParser<long, NumberStyles> Int64Parser = long.Parse;
        private static readonly ValueParser<ulong, NumberStyles> UInt64Parser = ulong.Parse;
        private static readonly ValueParser<int, NumberStyles> Int32Parser = int.Parse;
        private static readonly ValueParser<uint, NumberStyles> UInt32Parser = uint.Parse;
        private static readonly ValueParser<short, NumberStyles> Int16Parser = short.Parse;
        private static readonly ValueParser<ushort, NumberStyles> UInt16Parser = ushort.Parse;
        private static readonly ValueParser<byte, NumberStyles> UInt8Parser = byte.Parse;
        private static readonly ValueParser<sbyte, NumberStyles> Int8Parser = sbyte.Parse;
        private static readonly ValueParser<float, NumberStyles> Float32Parser = float.Parse;
        private static readonly ValueParser<double, NumberStyles> Float64Parser = double.Parse;
        private static readonly ValueParser<decimal, NumberStyles> DecimalParser = decimal.Parse;
        private static readonly ValueParser<DateTime, DateTimeStyles> DateTimeParser = ParseDateTime;
        private static readonly ValueParser<DateTimeOffset, DateTimeStyles> DateTimeOffsetParser = ParseDateTimeOffset;

        private readonly ReadOnlySequence<byte> sequence;
        private SequencePosition position;

        internal SequenceBinaryReader(ReadOnlySequence<byte> sequence)
        {
            this.sequence = sequence;
            position = sequence.Start;
        }

        private static DateTime ParseDateTime(ReadOnlySpan<char> value, DateTimeStyles style, IFormatProvider? provider)
            => DateTime.Parse(value, provider, style);

        private static DateTimeOffset ParseDateTimeOffset(ReadOnlySpan<char> value, DateTimeStyles style, IFormatProvider? provider)
            => DateTimeOffset.Parse(value, provider, style);

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

        private Span<char> ReadString<TBuffer>(in DecodingContext context, in TBuffer buffer)
            where TBuffer : struct, IBuffer<char>
        {
            var parser = new StringReader<TBuffer>(in context, in buffer);
            parser.Append<string, StringReader<TBuffer>>(sequence.Slice(position), out position);
            return parser.IsCompleted ? parser.Result : throw new EndOfStreamException();
        }

        private unsafe TResult Read<TResult, TStyle>(ValueParser<TResult, TStyle> parser, StringLengthEncoding lengthFormat, in DecodingContext context, TStyle style, IFormatProvider? provider)
            where TResult : struct
            where TStyle : struct, Enum
        {
            var length = ReadLength(lengthFormat);
            if (length > MemoryRental<char>.StackallocThreshold)
            {
                using var buffer = new ArrayBuffer<char>(length);
                return parser(ReadString(in context, in buffer), style, provider);
            }
            else
            {
                var buffer = stackalloc char[length];
                return parser(ReadString(in context, new UnsafeBuffer<char>(buffer, length)), style, provider);
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
        /// Parses 64-bit signed integer from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The numner is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public long ReadInt64(StringLengthEncoding lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read(Int64Parser, lengthFormat, in context, style, provider);

        /// <summary>
        /// Parses 64-bit unsigned integer from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The numner is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public ulong ReadUInt64(StringLengthEncoding lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read(UInt64Parser, lengthFormat, in context, style, provider);

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
        /// <exception cref="FormatException">The numner is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public int ReadInt32(StringLengthEncoding lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read(Int32Parser, lengthFormat, in context, style, provider);

        /// <summary>
        /// Parses 32-bit unsigned integer from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The numner is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public uint ReadUInt32(StringLengthEncoding lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read(UInt32Parser, lengthFormat, in context, style, provider);

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
        /// <exception cref="FormatException">The numner is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public short ReadInt16(StringLengthEncoding lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read(Int16Parser, lengthFormat, in context, style, provider);

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
        /// <exception cref="FormatException">The numner is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public ushort ReadUInt16(StringLengthEncoding lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read(UInt16Parser, lengthFormat, in context, style, provider);

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
        /// <exception cref="FormatException">The numner is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public byte ReadByte(StringLengthEncoding lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read(UInt8Parser, lengthFormat, in context, style, provider);

        /// <summary>
        /// Parses 8-bit unsigned integer from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The numner is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public sbyte ReadSByte(StringLengthEncoding lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
            => Read(Int8Parser, lengthFormat, in context, style, provider);

        /// <summary>
        /// Parses single-precision floating-point number from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The numner is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public float ReadSingle(StringLengthEncoding lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null)
            => Read(Float32Parser, lengthFormat, in context, style, provider);

        /// <summary>
        /// Parses double-precision floating-point number from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The numner is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public double ReadDouble(StringLengthEncoding lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.AllowThousands | NumberStyles.Float, IFormatProvider? provider = null)
            => Read(Float64Parser, lengthFormat, in context, style, provider);

        /// <summary>
        /// Parses <see cref="decimal"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The numner is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public decimal ReadDecimal(StringLengthEncoding lengthFormat, in DecodingContext context, NumberStyles style = NumberStyles.Number, IFormatProvider? provider = null)
            => Read(DecimalParser, lengthFormat, in context, style, provider);

        /// <summary>
        /// Parses <see cref="DateTime"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The numner is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public DateTime ReadDateTime(StringLengthEncoding lengthFormat, in DecodingContext context, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
            => Read(DateTimeParser, lengthFormat, in context, style, provider);

        /// <summary>
        /// Parses <see cref="DateTimeOffset"/> from its string representation encoded in the underlying stream.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="style">A bitwise combination of the enumeration values that indicates the style elements.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="FormatException">The numner is in incorrect format.</exception>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public DateTimeOffset ReadDateTimeOffset(StringLengthEncoding lengthFormat, in DecodingContext context, DateTimeStyles style = DateTimeStyles.None, IFormatProvider? provider = null)
            => Read(DateTimeOffsetParser, lengthFormat, in context, style, provider);

        /// <summary>
        /// Decodes the string.
        /// </summary>
        /// <param name="length">The length of the encoded string, in bytes.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <returns>The decoded string.</returns>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
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

        private int ReadLength(StringLengthEncoding lengthFormat)
        {
            int length;
            var littleEndian = BitConverter.IsLittleEndian;
            switch (lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case StringLengthEncoding.Plain:
                    length = Read<int>();
                    break;
                case StringLengthEncoding.PlainLittleEndian:
                    littleEndian = true;
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.PlainBigEndian:
                    littleEndian = false;
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.Compressed:
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
        public string ReadString(StringLengthEncoding lengthFormat, in DecodingContext context)
            => ReadString(ReadLength(lengthFormat), in context);

        /// <inheritdoc/>
        ValueTask<T> IAsyncBinaryReader.ReadAsync<T>(CancellationToken token)
            => token.IsCancellationRequested ?
                new ValueTask<T>(Task.FromCanceled<T>(token)) :
                new ValueTask<T>(Read<T>());

        /// <inheritdoc/>
        ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return new ValueTask(Task.FromCanceled(token));
            Read(output);
            return new ValueTask();
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
        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
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
        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
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
        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
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
        ValueTask<byte> IAsyncBinaryReader.ReadByteAsync(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
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
        ValueTask<float> IAsyncBinaryReader.ReadSingleAsync(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
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
        ValueTask<double> IAsyncBinaryReader.ReadDoubleAsync(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
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
        ValueTask<decimal> IAsyncBinaryReader.ReadDecimalAsync(StringLengthEncoding lengthFormat, DecodingContext context, NumberStyles style, IFormatProvider? provider, CancellationToken token)
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
        ValueTask<DateTime> IAsyncBinaryReader.ReadDateTimeAsync(StringLengthEncoding lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
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
        ValueTask<DateTimeOffset> IAsyncBinaryReader.ReadDateTimeOffsetAsync(StringLengthEncoding lengthFormat, DecodingContext context, DateTimeStyles style, IFormatProvider? provider, CancellationToken token)
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
        ValueTask<string> IAsyncBinaryReader.ReadStringAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token)
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
    }
}