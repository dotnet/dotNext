using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Buffers;

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
    public static unsafe bool TryRead<T>(this scoped ref SpanReader<byte> reader, out T result)
        where T : unmanaged
    {
        if (MemoryMarshal.TryRead(reader.RemainingSpan, out result))
        {
            reader.Advance(sizeof(T));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads the value of blittable type from the raw bytes
    /// represents by memory block.
    /// </summary>
    /// <param name="reader">The memory reader.</param>
    /// <typeparam name="T">The blittable type.</typeparam>
    /// <returns>The value deserialized from bytes.</returns>
    /// <exception cref="InternalBufferOverflowException">The end of memory block is reached.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T Read<T>(this ref SpanReader<byte> reader)
        where T : unmanaged
        => Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference(reader.Read(sizeof(T))));

    /// <summary>
    /// Decodes 16-bit signed integer.
    /// </summary>
    /// <param name="reader">The memory reader.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InternalBufferOverflowException">The end of memory block is reached.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadInt16(this ref SpanReader<byte> reader, bool isLittleEndian)
    {
        var result = reader.Read<short>();
        if (isLittleEndian != BitConverter.IsLittleEndian)
            result = ReverseEndianness(result);
        return result;
    }

    /// <summary>
    /// Decodes 16-bit unsigned integer.
    /// </summary>
    /// <param name="reader">The memory reader.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InternalBufferOverflowException">The end of memory block is reached.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ushort ReadUInt16(this ref SpanReader<byte> reader, bool isLittleEndian)
    {
        var result = reader.Read<ushort>();
        if (isLittleEndian != BitConverter.IsLittleEndian)
            result = ReverseEndianness(result);
        return result;
    }

    /// <summary>
    /// Decodes 32-bit signed integer.
    /// </summary>
    /// <param name="reader">The memory reader.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InternalBufferOverflowException">The end of memory block is reached.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt32(this ref SpanReader<byte> reader, bool isLittleEndian)
    {
        var result = reader.Read<int>();
        if (isLittleEndian != BitConverter.IsLittleEndian)
            result = ReverseEndianness(result);
        return result;
    }

    /// <summary>
    /// Decodes 32-bit unsigned integer.
    /// </summary>
    /// <param name="reader">The memory reader.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InternalBufferOverflowException">The end of memory block is reached.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static uint ReadUInt32(this ref SpanReader<byte> reader, bool isLittleEndian)
    {
        var result = reader.Read<uint>();
        if (isLittleEndian != BitConverter.IsLittleEndian)
            result = ReverseEndianness(result);
        return result;
    }

    /// <summary>
    /// Decodes 64-bit signed integer.
    /// </summary>
    /// <param name="reader">The memory reader.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InternalBufferOverflowException">The end of memory block is reached.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ReadInt64(this ref SpanReader<byte> reader, bool isLittleEndian)
    {
        var result = reader.Read<long>();
        if (isLittleEndian != BitConverter.IsLittleEndian)
            result = ReverseEndianness(result);
        return result;
    }

    /// <summary>
    /// Decodes 64-bit unsigned integer.
    /// </summary>
    /// <param name="reader">The memory reader.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InternalBufferOverflowException">The end of memory block is reached.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong ReadUInt64(this ref SpanReader<byte> reader, bool isLittleEndian)
    {
        var result = reader.Read<ulong>();
        if (isLittleEndian != BitConverter.IsLittleEndian)
            result = ReverseEndianness(result);
        return result;
    }

    /// <summary>
    /// Decodes single-precision floating-point number.
    /// </summary>
    /// <param name="reader">The memory reader.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InternalBufferOverflowException">The end of memory block is reached.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadSingle(this ref SpanReader<byte> reader, bool isLittleEndian)
        => BitConverter.Int32BitsToSingle(reader.ReadInt32(isLittleEndian));

    /// <summary>
    /// Decodes double-precision floating-point number.
    /// </summary>
    /// <param name="reader">The memory reader.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InternalBufferOverflowException">The end of memory block is reached.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ReadDouble(this ref SpanReader<byte> reader, bool isLittleEndian)
        => BitConverter.Int64BitsToDouble(reader.ReadInt64(isLittleEndian));

    /// <summary>
    /// Decodes half-precision floating-point number.
    /// </summary>
    /// <param name="reader">The memory reader.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="InternalBufferOverflowException">The end of memory block is reached.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Half ReadHalf(this ref SpanReader<byte> reader, bool isLittleEndian)
        => BitConverter.Int16BitsToHalf(reader.ReadInt16(isLittleEndian));
}