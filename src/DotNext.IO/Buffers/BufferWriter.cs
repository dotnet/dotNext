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
            where T : struct, IFormattable;

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

        private static readonly Formatter<int> Int32Formatter;
        private static readonly Formatter<Guid> GuidFormatter;

        static BufferWriter()
        {
            Ldnull();
            Ldftn(Method(Type<int>(), nameof(int.TryFormat)));
            Newobj(Constructor(Type<Formatter<int>>(), Type<object>(), Type<IntPtr>()));
            Pop(out Int32Formatter);

            GuidFormatter = TryFormat;

            static bool TryFormat(in Guid value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
                => value.TryFormat(destination, out charsWritten, format);
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
            => writer.Write(MemoryMarshal.CreateReadOnlySpan(ref value, 1));

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

        private static void Write<T>(IBufferWriter<char> writer, in T value, int bufferSize, Formatter<T> formatter, ReadOnlySpan<char> format, IFormatProvider? provider)
            where T : struct, IFormattable
        {
            for (int growBy = 0; ; )
            {
                var span = writer.GetSpan(bufferSize);
                if (formatter(in value, span, out var charsWritten, format, provider))
                {
                    writer.Advance(charsWritten);
                    break;
                }
                else if (growBy == 0)
                {
                    growBy = bufferSize / 2;
                }

                bufferSize = checked(bufferSize + growBy);
            }
        }

        /// <summary>
        /// Writes string representation of 32-bit signed integer to the buffer. 
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        /// <param name="provider">An optional object that supplies culture-specific formatting information.</param>
        public static void WriteInt32(this IBufferWriter<char> writer, int value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => Write(writer, in value, sizeof(int) * 8, Int32Formatter, format, provider);

        /// <summary>
        /// Writes string representation of <see cref="Guid"/> to the buffer. 
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="format">A span containing the characters that represent a standard or custom format string.</param>
        public static unsafe void WriteGuid(this IBufferWriter<char> writer, Guid value, ReadOnlySpan<char> format = default)
            => Write(writer, in value, sizeof(Guid) * 2 + 6, GuidFormatter, format, null);

        /// <summary>
        /// Constructs the string from the buffer.
        /// </summary>
        /// <param name="writer">The buffer of characters.</param>
        /// <returns>The string constructed from the buffer.</returns>
        public static string BuildString(this ArrayBufferWriter<char> writer)
            => new string(writer.WrittenSpan);

        /// <summary>
        /// Constructs the string from the buffer.
        /// </summary>
        /// <param name="writer">The buffer of characters.</param>
        /// <returns>The string constructed from the buffer.</returns>
        public static string BuildString(this MemoryWriter<char> writer)
            => new string(writer.WrittenMemory.Span);
    }
}