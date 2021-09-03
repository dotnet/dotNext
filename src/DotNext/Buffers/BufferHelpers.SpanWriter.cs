using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Buffers
{
    public static partial class BufferHelpers
    {
        /// <summary>
        /// Writes value of blittable type as bytes to the underlying memory block.
        /// </summary>
        /// <param name="writer">The memory writer.</param>
        /// <param name="value">The value of blittable type.</param>
        /// <typeparam name="T">The blittable type.</typeparam>
        /// <returns>
        /// <see langword="true"/> if all bytes are copied successfully;
        /// <see langword="false"/> if remaining space in the underlying span is not enough to place all <paramref name="value"/> bytes.
        /// </returns>
        public static bool TryWrite<T>(this ref SpanWriter<byte> writer, in T value)
            where T : unmanaged
            => writer.TryWrite(Span.AsReadOnlyBytes(in value));

        /// <summary>
        /// Writes value of blittable type as bytes to the underlying memory block.
        /// </summary>
        /// <param name="writer">The memory writer.</param>
        /// <param name="value">The value of blittable type.</param>
        /// <typeparam name="T">The blittable type.</typeparam>
        /// <exception cref="EndOfStreamException">Remaining space in the underlying span is not enough to place all <paramref name="value"/> bytes.</exception>
        public static void Write<T>(this ref SpanWriter<byte> writer, in T value)
            where T : unmanaged
            => writer.Write(Span.AsReadOnlyBytes(in value));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void Write<T>(this ref SpanWriter<byte> writer, delegate*<Span<byte>, T, void> action, T value)
            where T : unmanaged
        {
            action(writer.RemainingSpan, value);
            writer.Advance(sizeof(T));
        }

        /// <summary>
        /// Writes 16-bit signed integer to the block of memory.
        /// </summary>
        /// <param name="writer">Memory writer.</param>
        /// <param name="value">The value to be encoded in the block of memory.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <exception cref="ArgumentOutOfRangeException">Remaining space in the underlying span is not enough to place the value.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteInt16(this ref SpanWriter<byte> writer, short value, bool isLittleEndian)
            => writer.Write(isLittleEndian ? &WriteInt16LittleEndian : &WriteInt16BigEndian, value);

        /// <summary>
        /// Writes 16-bit unsigned integer to the block of memory.
        /// </summary>
        /// <param name="writer">Memory writer.</param>
        /// <param name="value">The value to be encoded in the block of memory.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <exception cref="ArgumentOutOfRangeException">Remaining space in the underlying span is not enough to place the value.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe void WriteUInt16(this ref SpanWriter<byte> writer, ushort value, bool isLittleEndian)
            => writer.Write(isLittleEndian ? &WriteUInt16LittleEndian : &WriteUInt16BigEndian, value);

        /// <summary>
        /// Writes 32-bit signed integer to the block of memory.
        /// </summary>
        /// <param name="writer">Memory writer.</param>
        /// <param name="value">The value to be encoded in the block of memory.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <exception cref="ArgumentOutOfRangeException">Remaining space in the underlying span is not enough to place the value.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteInt32(this ref SpanWriter<byte> writer, int value, bool isLittleEndian)
            => writer.Write(isLittleEndian ? &WriteInt32LittleEndian : &WriteInt32BigEndian, value);

        /// <summary>
        /// Writes 32-bit unsigned integer to the block of memory.
        /// </summary>
        /// <param name="writer">Memory writer.</param>
        /// <param name="value">The value to be encoded in the block of memory.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <exception cref="ArgumentOutOfRangeException">Remaining space in the underlying span is not enough to place the value.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe void WriteUInt32(this ref SpanWriter<byte> writer, uint value, bool isLittleEndian)
            => writer.Write(isLittleEndian ? &WriteUInt32LittleEndian : &WriteUInt32BigEndian, value);

        /// <summary>
        /// Writes 64-bit signed integer to the block of memory.
        /// </summary>
        /// <param name="writer">Memory writer.</param>
        /// <param name="value">The value to be encoded in the block of memory.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <exception cref="ArgumentOutOfRangeException">Remaining space in the underlying span is not enough to place the value.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteInt64(this ref SpanWriter<byte> writer, long value, bool isLittleEndian)
            => writer.Write(isLittleEndian ? &WriteInt64LittleEndian : &WriteInt64BigEndian, value);

        /// <summary>
        /// Writes 64-bit unsigned integer to the block of memory.
        /// </summary>
        /// <param name="writer">Memory writer.</param>
        /// <param name="value">The value to be encoded in the block of memory.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <exception cref="ArgumentOutOfRangeException">Remaining space in the underlying span is not enough to place the value.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe void WriteUInt64(this ref SpanWriter<byte> writer, ulong value, bool isLittleEndian)
            => writer.Write(isLittleEndian ? &WriteUInt64LittleEndian : &WriteUInt64BigEndian, value);

        /// <summary>
        /// Writes single-precision floating-point number to the block of memory.
        /// </summary>
        /// <param name="writer">Memory writer.</param>
        /// <param name="value">The value to be encoded in the block of memory.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <exception cref="ArgumentOutOfRangeException">Remaining space in the underlying span is not enough to place the value.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteSingle(this ref SpanWriter<byte> writer, float value, bool isLittleEndian)
            => writer.Write(isLittleEndian ? &WriteSingleLittleEndian : &WriteSingleBigEndian, value);

        /// <summary>
        /// Writes double-precision floating-point number to the block of memory.
        /// </summary>
        /// <param name="writer">Memory writer.</param>
        /// <param name="value">The value to be encoded in the block of memory.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <exception cref="ArgumentOutOfRangeException">Remaining space in the underlying span is not enough to place the value.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteDouble(this ref SpanWriter<byte> writer, double value, bool isLittleEndian)
            => writer.Write(isLittleEndian ? &WriteDoubleLittleEndian : &WriteDoubleBigEndian, value);

        /// <summary>
        /// Writes the contents of string builder to the buffer.
        /// </summary>
        /// <param name="writer">The buffer writer.</param>
        /// <param name="input">The string builder.</param>
        public static void Write(this ref SpanWriter<char> writer, StringBuilder input)
        {
            foreach (var chunk in input.GetChunks())
                writer.Write(chunk.Span);
        }
    }
}