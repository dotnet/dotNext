using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Buffers
{
    using System.Threading;
    using Text;
    using StringLengthEncoding = IO.StringLengthEncoding;

    /// <summary>
    /// Represents extension methods for writting typed data into buffer.
    /// </summary>
    public static class BufferWriter
    {
        private const int MaxBufferSize = int.MaxValue / 2;
        internal const MemoryAllocator<char>? DefaultAllocator = null;

        // TODO: Replace with function pointers in C# 9
        internal interface ISpanFormattable
        {
            bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct SByteFormatter : ISpanFormattable
        {
            private readonly sbyte value;

            internal SByteFormatter(sbyte value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator SByteFormatter(sbyte value) => new SByteFormatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct ByteFormatter : ISpanFormattable
        {
            private readonly byte value;

            internal ByteFormatter(byte value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator ByteFormatter(byte value) => new ByteFormatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct Int16Formatter : ISpanFormattable
        {
            private readonly short value;

            internal Int16Formatter(short value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator Int16Formatter(short value) => new Int16Formatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct UInt16Formatter : ISpanFormattable
        {
            private readonly ushort value;

            internal UInt16Formatter(ushort value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator UInt16Formatter(ushort value) => new UInt16Formatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct Int32Formatter : ISpanFormattable
        {
            private readonly int value;

            internal Int32Formatter(int value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator Int32Formatter(int value) => new Int32Formatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct UInt32Formatter : ISpanFormattable
        {
            private readonly uint value;

            internal UInt32Formatter(uint value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator UInt32Formatter(uint value) => new UInt32Formatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct Int64Formatter : ISpanFormattable
        {
            private readonly long value;

            internal Int64Formatter(long value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator Int64Formatter(long value) => new Int64Formatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct UInt64Formatter : ISpanFormattable
        {
            private readonly ulong value;

            internal UInt64Formatter(ulong value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator UInt64Formatter(ulong value) => new UInt64Formatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct DecimalFormatter : ISpanFormattable
        {
            private readonly decimal value;

            internal DecimalFormatter(decimal value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator DecimalFormatter(decimal value) => new DecimalFormatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct SingleFormatter : ISpanFormattable
        {
            private readonly float value;

            internal SingleFormatter(float value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator SingleFormatter(float value) => new SingleFormatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct DoubleFormatter : ISpanFormattable
        {
            private readonly double value;

            internal DoubleFormatter(double value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator DoubleFormatter(double value) => new DoubleFormatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct GuidFormatter : ISpanFormattable
        {
            private readonly Guid value;

            internal GuidFormatter(Guid value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format);

            public static implicit operator GuidFormatter(Guid value) => new GuidFormatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct DateTimeFormatter : ISpanFormattable
        {
            private readonly DateTime value;

            internal DateTimeFormatter(DateTime value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator DateTimeFormatter(DateTime value) => new DateTimeFormatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct DateTimeOffsetFormatter : ISpanFormattable
        {
            private readonly DateTimeOffset value;

            internal DateTimeOffsetFormatter(DateTimeOffset value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator DateTimeOffsetFormatter(DateTimeOffset value) => new DateTimeOffsetFormatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal readonly struct TimeSpanFormatter : ISpanFormattable
        {
            private readonly TimeSpan value;

            internal TimeSpanFormatter(TimeSpan value) => this.value = value;

            bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format, provider);

            public static implicit operator TimeSpanFormatter(TimeSpan value) => new TimeSpanFormatter(value);
        }

        [StructLayout(LayoutKind.Auto)]
        internal struct LengthWriter : SevenBitEncodedInt.IWriter
        {
            private readonly Memory<byte> writer;
            private int offset;

            internal LengthWriter(IBufferWriter<byte> output)
            {
                writer = output.GetMemory(5);
                offset = 0;
            }

            internal readonly int Count => offset;

            void SevenBitEncodedInt.IWriter.WriteByte(byte value)
            {
                writer.Span[offset++] = value;
            }
        }

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
        /// Writes the sequence of elements to the buffer.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The sequence of elements to be written.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The number of written elements.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static long Write<T>(this IBufferWriter<T> writer, ReadOnlySequence<T> value, CancellationToken token = default)
        {
            var count = 0L;

            for (var position = value.Start; value.TryGet(ref position, out var block); count += block.Length, token.ThrowIfCancellationRequested())
                writer.Write(block.Span);

            return count;
        }

        /// <summary>
        /// Writes single element to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to add.</param>
        /// <typeparam name="T">The type of elements in the buffer.</typeparam>
        [Obsolete("Use BufferHelpers.Write extension method instead", true)]
        public static void Write<T>(IBufferWriter<T> writer, T value)
            => BufferHelpers.Write(writer, value);

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
            var writer = new LengthWriter(output);
            SevenBitEncodedInt.Encode(ref writer, (uint)value);
            output.Advance(writer.Count);
        }

        internal static void WriteLength(this IBufferWriter<byte> writer, ReadOnlySpan<char> value, StringLengthEncoding lengthFormat, Encoding encoding)
        {
            var length = encoding.GetByteCount(value);
            switch (lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case StringLengthEncoding.PlainLittleEndian:
                    length.ReverseIfNeeded(true);
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.PlainBigEndian:
                    length.ReverseIfNeeded(false);
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.Plain:
                    Write(writer, length);
                    break;
                case StringLengthEncoding.Compressed:
                    Write7BitEncodedInt(writer, length);
                    break;
            }
        }

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
        public static void WriteString(this IBufferWriter<byte> writer, ReadOnlySpan<char> value, in EncodingContext context, int bufferSize = 0, StringLengthEncoding? lengthFormat = null)
        {
            if (lengthFormat.HasValue)
                WriteLength(writer, value, lengthFormat.GetValueOrDefault(), context.Encoding);

            if (!value.IsEmpty)
                WriteString(writer, value, context.GetEncoder(), context.Encoding.GetMaxByteCount(1), bufferSize);
        }

        private static bool WriteString<T>(IBufferWriter<byte> writer, T value, Span<char> buffer, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format, IFormatProvider? provider, int bufferSize)
            where T : struct, ISpanFormattable
        {
            if (!value.TryFormat(buffer, out var charsWritten, format, provider))
                return false;

            ReadOnlySpan<char> result = buffer.Slice(0, charsWritten);
            WriteLength(writer, result, lengthFormat, context.Encoding);
            WriteString(writer, result, context.GetEncoder(), context.Encoding.GetMaxByteCount(1), bufferSize);
            return true;
        }

        private static void Write<T>(IBufferWriter<byte> writer, T value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format, IFormatProvider? provider, int bufferSize)
            where T : struct, ISpanFormattable
        {
            const int initialCharBufferSize = 128;

            // attempt to allocate char buffer on the stack
            Span<char> charBuffer = stackalloc char[initialCharBufferSize];
            if (!WriteString(writer, value, charBuffer, lengthFormat, in context, format, provider, bufferSize))
            {
                for (var charBufferSize = initialCharBufferSize * 2; ; charBufferSize = charBufferSize <= MaxBufferSize ? charBufferSize * 2 : throw new InsufficientMemoryException())
                {
                    using var owner = DefaultAllocator.Invoke(charBufferSize, false);
                    if (WriteString(writer, value, charBuffer, lengthFormat, in context, format, provider, bufferSize))
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
        public static void WriteInt64(this IBufferWriter<byte> writer, long value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<Int64Formatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

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
        public static void WriteUInt64(this IBufferWriter<byte> writer, ulong value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<UInt64Formatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

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
        public static void WriteInt32(this IBufferWriter<byte> writer, int value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<Int32Formatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

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
        public static void WriteUInt32(this IBufferWriter<byte> writer, uint value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<UInt32Formatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

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
        public static void WriteInt16(this IBufferWriter<byte> writer, short value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<Int16Formatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

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
        public static void WriteUInt16(this IBufferWriter<byte> writer, ushort value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<UInt16Formatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

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
        public static void WriteSByte(this IBufferWriter<byte> writer, sbyte value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<SByteFormatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

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
        public static void WriteByte(this IBufferWriter<byte> writer, byte value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<ByteFormatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

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
        public static void WriteDecimal(this IBufferWriter<byte> writer, decimal value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<DecimalFormatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

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
        public static void WriteSingle(this IBufferWriter<byte> writer, float value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<SingleFormatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

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
        public static void WriteDouble(this IBufferWriter<byte> writer, double value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<DoubleFormatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Encodes <see cref="Guid"/> as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The encoding context.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="bufferSize">The buffer size (in bytes) used for encoding.</param>
        public static void WriteGuid(this IBufferWriter<byte> writer, Guid value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, int bufferSize = 0)
            => Write<GuidFormatter>(writer, value, lengthFormat, in context, format, null, bufferSize);

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
        public static void WriteDateTime(this IBufferWriter<byte> writer, DateTime value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<DateTimeFormatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

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
        public static void WriteDateTimeOffset(this IBufferWriter<byte> writer, DateTimeOffset value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<DateTimeOffsetFormatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

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
        public static void WriteTimeSpan(this IBufferWriter<byte> writer, TimeSpan value, StringLengthEncoding lengthFormat, in EncodingContext context, ReadOnlySpan<char> format = default, IFormatProvider? provider = null, int bufferSize = 0)
            => Write<TimeSpanFormatter>(writer, value, lengthFormat, in context, format, provider, bufferSize);

        /// <summary>
        /// Writes the array to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="buffer">The buffer to write.</param>
        /// <param name="startIndex">Start index in the buffer.</param>
        /// <param name="count">The number of elements in the buffer. to write.</param>
        /// <typeparam name="T">The type of the elements in the buffer.</typeparam>
        [Obsolete("Use BuffersExtensions.Write extension method instead")]
        public static void Write<T>(this IBufferWriter<T> writer, T[] buffer, int startIndex, int count)
            => writer.Write(buffer.AsSpan(startIndex, count));

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

        private static void Write<T>(IBufferWriter<char> writer, T value, ReadOnlySpan<char> format, IFormatProvider? provider)
            where T : struct, ISpanFormattable
        {
            for (int bufferSize = 0; ; )
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
            => Write<ByteFormatter>(writer, value, format, provider);

        /// <summary>
        /// Writes string representation of 8-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteSByte(this IBufferWriter<char> writer, sbyte value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<SByteFormatter>(writer, value, format, provider);

        /// <summary>
        /// Writes string representation of 16-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt16(this IBufferWriter<char> writer, short value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<Int16Formatter>(writer, value, format, provider);

        /// <summary>
        /// Writes string representation of 16-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt16(this IBufferWriter<char> writer, ushort value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<UInt16Formatter>(writer, value, format, provider);

        /// <summary>
        /// Writes string representation of 32-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt32(this IBufferWriter<char> writer, int value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<Int32Formatter>(writer, value, format, provider);

        /// <summary>
        /// Writes string representation of 32-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt32(this IBufferWriter<char> writer, uint value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<UInt32Formatter>(writer, value, format, provider);

        /// <summary>
        /// Writes string representation of 64-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt64(this IBufferWriter<char> writer, long value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<Int64Formatter>(writer, value, format, provider);

        /// <summary>
        /// Writes string representation of 64-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt64(this IBufferWriter<char> writer, ulong value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<UInt64Formatter>(writer, value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="Guid"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        public static unsafe void WriteGuid(this IBufferWriter<char> writer, Guid value, ReadOnlySpan<char> format = default)
            => Write<GuidFormatter>(writer, value, format, null);

        /// <summary>
        /// Writes string representation of <see cref="DateTime"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDateTime(this IBufferWriter<char> writer, DateTime value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<DateTimeFormatter>(writer, value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="DateTimeOffset"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDateTimeOffset(this IBufferWriter<char> writer, DateTimeOffset value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<DateTimeOffsetFormatter>(writer, value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="decimal"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDecimal(this IBufferWriter<char> writer, decimal value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<DecimalFormatter>(writer, value, format, provider);

        /// <summary>
        /// Writes string representation of single-precision floating-point number to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteSingle(this IBufferWriter<char> writer, float value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<SingleFormatter>(writer, value, format, provider);

        /// <summary>
        /// Writes string representation of double-precision floating-point number to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDouble(this IBufferWriter<char> writer, double value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<DoubleFormatter>(writer, value, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="TimeSpan"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteTimeSpan(this IBufferWriter<char> writer, TimeSpan value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write<TimeSpanFormatter>(writer, value, format, provider);

        // TODO: Need writer for StringBuilder but it will be available in .NET Core 5

        /// <summary>
        /// Constructs the string from the buffer.
        /// </summary>
        /// <param name="writer">The buffer of characters.</param>
        /// <returns>The string constructed from the buffer.</returns>
        [Obsolete("Use BufferHelpers class instead", true)]
        public static string BuildString(ArrayBufferWriter<char> writer)
            => BufferHelpers.BuildString(writer);

        /// <summary>
        /// Constructs the string from the buffer.
        /// </summary>
        /// <param name="writer">The buffer of characters.</param>
        /// <returns>The string constructed from the buffer.</returns>
        [Obsolete("Use BufferHelpers class instead", true)]
        public static string BuildString(MemoryWriter<char> writer)
            => BufferHelpers.BuildString(writer);
    }
}