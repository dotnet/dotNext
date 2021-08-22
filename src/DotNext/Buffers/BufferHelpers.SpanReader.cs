using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Buffers
{
    public static partial class BufferHelpers
    {
        /// <summary>
        /// Reads the value of blittable type from the raw bytes
        /// represents by memory block.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <param name="result">The value deserialized from bytes.</param>
        /// <typeparam name="T">The blittable type.</typeparam>
        /// <returns>
        /// <see langword="true"/> if memory block contains enough amount of unread bytes to decode the value;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        public static unsafe bool TryRead<T>(this ref SpanReader<byte> reader, out T result)
            where T : unmanaged
        {
            if (MemoryMarshal.TryRead(reader.RemainingSpan, out result))
            {
                reader.Advance(sizeof(T));
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Reads the value of blittable type from the raw bytes
        /// represents by memory block.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <typeparam name="T">The blittable type.</typeparam>
        /// <returns>The value deserialized from bytes.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The end of memory block is reached.</exception>
        public static unsafe T Read<T>(this ref SpanReader<byte> reader)
            where T : unmanaged
        {
            var result = MemoryMarshal.Read<T>(reader.RemainingSpan);
            reader.Advance(sizeof(T));
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe T Read<T>(this ref SpanReader<byte> reader, delegate*<ReadOnlySpan<byte>, T> parser)
            where T : unmanaged
        {
            var result = parser(reader.RemainingSpan);
            reader.Advance(sizeof(T));
            return result;
        }

        /// <summary>
        /// Decodes 16-bit signed integer.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The end of memory block is reached.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe short ReadInt16(this ref SpanReader<byte> reader, bool isLittleEndian)
            => reader.Read<short>(isLittleEndian ? &ReadInt16LittleEndian : &ReadInt16BigEndian);

        /// <summary>
        /// Decodes 16-bit unsigned integer.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The end of memory block is reached.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe ushort ReadUInt16(this ref SpanReader<byte> reader, bool isLittleEndian)
            => reader.Read<ushort>(isLittleEndian ? &ReadUInt16LittleEndian : &ReadUInt16BigEndian);

        /// <summary>
        /// Decodes 32-bit signed integer.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The end of memory block is reached.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int ReadInt32(this ref SpanReader<byte> reader, bool isLittleEndian)
            => reader.Read<int>(isLittleEndian ? &ReadInt32LittleEndian : &ReadInt32BigEndian);

        /// <summary>
        /// Decodes 32-bit unsigned integer.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The end of memory block is reached.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe uint ReadUInt32(this ref SpanReader<byte> reader, bool isLittleEndian)
            => reader.Read<uint>(isLittleEndian ? &ReadUInt32LittleEndian : &ReadUInt32BigEndian);

        /// <summary>
        /// Decodes 64-bit signed integer.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The end of memory block is reached.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe long ReadInt64(this ref SpanReader<byte> reader, bool isLittleEndian)
            => reader.Read<long>(isLittleEndian ? &ReadInt64LittleEndian : &ReadInt64BigEndian);

        /// <summary>
        /// Decodes 64-bit unsigned integer.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The end of memory block is reached.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe ulong ReadUInt64(this ref SpanReader<byte> reader, bool isLittleEndian)
            => reader.Read<ulong>(isLittleEndian ? &ReadUInt64LittleEndian : &ReadUInt64BigEndian);

#if !NETSTANDARD2_1
        /// <summary>
        /// Decodes single-precision floating-point number.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The end of memory block is reached.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe float ReadSingle(this ref SpanReader<byte> reader, bool isLittleEndian)
            => reader.Read<float>(isLittleEndian ? &ReadSingleLittleEndian : &ReadSingleBigEndian);

        /// <summary>
        /// Decodes double-precision floating-point number.
        /// </summary>
        /// <param name="reader">The memory reader.</param>
        /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The end of memory block is reached.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe double ReadDouble(this ref SpanReader<byte> reader, bool isLittleEndian)
            => reader.Read<double>(isLittleEndian ? &ReadDoubleLittleEndian : &ReadDoubleBigEndian);
#endif
    }
}