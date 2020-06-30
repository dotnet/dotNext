using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Buffers
{
    using Text;
    using StringLengthEncoding = IO.StringLengthEncoding;

    /// <summary>
    /// Represents extension methods for writting typed data into buffer.
    /// </summary>
    public static class BufferWriter
    {
        private delegate bool Formatter<T>(in T value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
            where T : struct;

        [StructLayout(LayoutKind.Auto)]
        private struct LengthWriter : SevenBitEncodedInt.IWriter
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

        private static readonly Formatter<byte> UInt8Formatter;
        private static readonly Formatter<sbyte> Int8Formatter;
        private static readonly Formatter<short> Int16Formatter;
        private static readonly Formatter<ushort> UInt16Formatter;
        private static readonly Formatter<int> Int32Formatter;
        private static readonly Formatter<uint> UInt32Formatter;
        private static readonly Formatter<long> Int64Formatter;
        private static readonly Formatter<ulong> UInt64Formatter;
        private static readonly Formatter<Guid> GuidFormatter;
        private static readonly Formatter<DateTime> DateTimeFormatter;
        private static readonly Formatter<DateTimeOffset> DateTimeOffsetFormatter;
        private static readonly Formatter<bool> BooleanFormatter;
        private static readonly Formatter<decimal> DecimalFormatter;
        private static readonly Formatter<float> Float32Formatter;
        private static readonly Formatter<double> Float64Formatter;

        static BufferWriter()
        {
            // TODO: Replace with function pointers in C# 9
            Ldnull();
            Ldftn(Method(Type<byte>(), nameof(byte.TryFormat)));
            Newobj(Constructor(Type<Formatter<byte>>(), Type<object>(), Type<IntPtr>()));
            Pop(out UInt8Formatter);

            Ldnull();
            Ldftn(Method(Type<sbyte>(), nameof(sbyte.TryFormat)));
            Newobj(Constructor(Type<Formatter<sbyte>>(), Type<object>(), Type<IntPtr>()));
            Pop(out Int8Formatter);

            Ldnull();
            Ldftn(Method(Type<short>(), nameof(short.TryFormat)));
            Newobj(Constructor(Type<Formatter<short>>(), Type<object>(), Type<IntPtr>()));
            Pop(out Int16Formatter);

            Ldnull();
            Ldftn(Method(Type<ushort>(), nameof(ushort.TryFormat)));
            Newobj(Constructor(Type<Formatter<ushort>>(), Type<object>(), Type<IntPtr>()));
            Pop(out UInt16Formatter);

            Ldnull();
            Ldftn(Method(Type<int>(), nameof(int.TryFormat)));
            Newobj(Constructor(Type<Formatter<int>>(), Type<object>(), Type<IntPtr>()));
            Pop(out Int32Formatter);

            Ldnull();
            Ldftn(Method(Type<uint>(), nameof(uint.TryFormat)));
            Newobj(Constructor(Type<Formatter<uint>>(), Type<object>(), Type<IntPtr>()));
            Pop(out UInt32Formatter);

            Ldnull();
            Ldftn(Method(Type<long>(), nameof(long.TryFormat)));
            Newobj(Constructor(Type<Formatter<long>>(), Type<object>(), Type<IntPtr>()));
            Pop(out Int64Formatter);

            Ldnull();
            Ldftn(Method(Type<ulong>(), nameof(ulong.TryFormat)));
            Newobj(Constructor(Type<Formatter<ulong>>(), Type<object>(), Type<IntPtr>()));
            Pop(out UInt64Formatter);

            Ldnull();
            Ldftn(Method(Type<DateTime>(), nameof(DateTime.TryFormat)));
            Newobj(Constructor(Type<Formatter<DateTime>>(), Type<object>(), Type<IntPtr>()));
            Pop(out DateTimeFormatter);

            Ldnull();
            Ldftn(Method(Type<DateTimeOffset>(), nameof(DateTimeOffset.TryFormat)));
            Newobj(Constructor(Type<Formatter<DateTimeOffset>>(), Type<object>(), Type<IntPtr>()));
            Pop(out DateTimeOffsetFormatter);

            Ldnull();
            Ldftn(Method(Type<decimal>(), nameof(decimal.TryFormat)));
            Newobj(Constructor(Type<Formatter<decimal>>(), Type<object>(), Type<IntPtr>()));
            Pop(out DecimalFormatter);

            Ldnull();
            Ldftn(Method(Type<float>(), nameof(float.TryFormat)));
            Newobj(Constructor(Type<Formatter<float>>(), Type<object>(), Type<IntPtr>()));
            Pop(out Float32Formatter);

            Ldnull();
            Ldftn(Method(Type<double>(), nameof(double.TryFormat)));
            Newobj(Constructor(Type<Formatter<double>>(), Type<object>(), Type<IntPtr>()));
            Pop(out Float64Formatter);

            BooleanFormatter = TryFormatBoolean;
            GuidFormatter = TryFormatGuid;

            static bool TryFormatGuid(in Guid value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format);

            static bool TryFormatBoolean(in bool value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten);
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
        /// Writes single element to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to add.</param>
        /// <typeparam name="T">The type of elements in the buffer.</typeparam>
        public static void Write<T>(this IBufferWriter<T> writer, T value)
        {
            writer.GetSpan(1)[0] = value;
            writer.Advance(1);
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
            var writer = new LengthWriter(output);
            SevenBitEncodedInt.Encode(ref writer, (uint)value);
            output.Advance(writer.Count);
        }

        internal static void WriteLength(this IBufferWriter<byte> writer, ReadOnlySpan<char> value, Encoding encoding, StringLengthEncoding lengthFormat)
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
                WriteLength(writer, value, context.Encoding, lengthFormat.GetValueOrDefault());

            if (!value.IsEmpty)
                WriteString(writer, value, context.GetEncoder(), context.Encoding.GetMaxByteCount(1), bufferSize);
        }

        /// <summary>
        /// Writes the array to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="buffer">The buffer to write.</param>
        /// <param name="startIndex">Start index in the buffer.</param>
        /// <param name="count">The number of elements in the buffer. to write.</param>
        /// <typeparam name="T">The type of the elements in the buffer.</typeparam>
        public static void Write<T>(this IBufferWriter<T> writer, T[] buffer, int startIndex, int count)
            => writer.Write(buffer.AsSpan(startIndex, count));

        /// <summary>
        /// Writes line termination symbols to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        public static void WriteLine(this IBufferWriter<char> writer)
            => writer.Write(Environment.NewLine);

        private static void Write<T>(IBufferWriter<char> writer, in T value, Formatter<T> formatter, ReadOnlySpan<char> format, IFormatProvider? provider)
            where T : struct
        {
            const int maxBufferSize = int.MaxValue / 2;

            for (int bufferSize = 0; ; )
            {
                var span = writer.GetSpan(bufferSize);
                if (formatter(in value, span, out var charsWritten, format, provider))
                {
                    writer.Advance(charsWritten);
                    break;
                }

                bufferSize = bufferSize <= maxBufferSize ? bufferSize * 2 : throw new OutOfMemoryException();
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
            => Write(writer, in value, UInt8Formatter, format, provider);

        /// <summary>
        /// Writes string representation of 8-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteSByte(this IBufferWriter<char> writer, sbyte value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, in value, Int8Formatter, format, provider);

        /// <summary>
        /// Writes string representation of 16-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt16(this IBufferWriter<char> writer, short value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, in value, Int16Formatter, format, provider);

        /// <summary>
        /// Writes string representation of 16-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt16(this IBufferWriter<char> writer, ushort value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, in value, UInt16Formatter, format, provider);

        /// <summary>
        /// Writes string representation of 32-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt32(this IBufferWriter<char> writer, int value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, in value, Int32Formatter, format, provider);

        /// <summary>
        /// Writes string representation of 32-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt32(this IBufferWriter<char> writer, uint value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, in value, UInt32Formatter, format, provider);

        /// <summary>
        /// Writes string representation of 64-bit signed integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt64(this IBufferWriter<char> writer, long value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, in value, Int64Formatter, format, provider);

        /// <summary>
        /// Writes string representation of 64-bit unsigned integer to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        [CLSCompliant(false)]
        public static void WriteUInt64(this IBufferWriter<char> writer, ulong value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, in value, UInt64Formatter, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="Guid"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        public static unsafe void WriteGuid(this IBufferWriter<char> writer, Guid value, ReadOnlySpan<char> format = default)
            => Write(writer, in value, GuidFormatter, format, null);

        /// <summary>
        /// Writes string representation of <see cref="DateTime"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDateTime(this IBufferWriter<char> writer, DateTime value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, in value, DateTimeFormatter, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="DateTimeOffset"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDateTime(this IBufferWriter<char> writer, DateTimeOffset value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, in value, DateTimeOffsetFormatter, format, provider);

        /// <summary>
        /// Writes boolean value as a string.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        public static void WriteBoolean(this IBufferWriter<char> writer, bool value)
            => Write(writer, in value, BooleanFormatter, default, null);

        /// <summary>
        /// Writes string representation of <see cref="decimal"/> to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDecimal(this IBufferWriter<char> writer, decimal value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, in value, DecimalFormatter, format, provider);

        /// <summary>
        /// Writes string representation of single-precision floating-point number to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteSingle(this IBufferWriter<char> writer, float value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, in value, Float32Formatter, format, provider);

        /// <summary>
        /// Writes string representation of double-precision floating-point number to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteDouble(this IBufferWriter<char> writer, double value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, in value, Float64Formatter, format, provider);

        // TODO: Need writer for StringBuilder but it will be available in .NET Core 5

        /// <summary>
        /// Constructs the string from the buffer.
        /// </summary>
        /// <param name="writer">The buffer of characters.</param>
        /// <returns>The string constructed from the buffer.</returns>
        public static string BuildString(this ArrayBufferWriter<char> writer)
        {
            var span = writer.WrittenSpan;
            return span.IsEmpty ? string.Empty : new string(span);
        }

        /// <summary>
        /// Constructs the string from the buffer.
        /// </summary>
        /// <param name="writer">The buffer of characters.</param>
        /// <returns>The string constructed from the buffer.</returns>
        public static string BuildString(this MemoryWriter<char> writer)
        {
            var span = writer.WrittenMemory.Span;
            return span.IsEmpty ? string.Empty : new string(span);
        }
    }
}