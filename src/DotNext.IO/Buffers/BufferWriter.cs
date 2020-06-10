using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Buffers
{
    using Text;
    using StringLengthEncoding = IO.StringLengthEncoding;

    /// <summary>
    /// Represents extension methods for writting typed data into buffer.
    /// </summary>
    public static class BufferWriter
    {
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
    }
}