using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
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
    }
}