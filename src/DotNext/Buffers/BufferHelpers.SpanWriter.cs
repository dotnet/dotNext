using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Buffers;

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
    public static bool TryWrite<T>(this ref SpanWriter<byte> writer, scoped in T value)
        where T : unmanaged
        => writer.TryWrite(Span.AsReadOnlyBytes(in value));

    /// <summary>
    /// Writes value of blittable type as bytes to the underlying memory block.
    /// </summary>
    /// <param name="writer">The memory writer.</param>
    /// <param name="value">The value of blittable type.</param>
    /// <typeparam name="T">The blittable type.</typeparam>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place all <paramref name="value"/> bytes.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void Write<T>(this ref SpanWriter<byte> writer, scoped in T value)
        where T : unmanaged
        => Unsafe.WriteUnaligned<T>(ref MemoryMarshal.GetReference(writer.Slide(sizeof(T))), value);

    /// <summary>
    /// Writes 16-bit signed integer to the block of memory.
    /// </summary>
    /// <param name="writer">Memory writer.</param>
    /// <param name="value">The value to be encoded in the block of memory.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place the value.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt16(this ref SpanWriter<byte> writer, short value, bool isLittleEndian)
        => writer.Write(isLittleEndian == BitConverter.IsLittleEndian ? value : ReverseEndianness(value)); // TODO: Replace with generic numbers in .NET 8

    /// <summary>
    /// Writes 16-bit unsigned integer to the block of memory.
    /// </summary>
    /// <param name="writer">Memory writer.</param>
    /// <param name="value">The value to be encoded in the block of memory.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place the value.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static void WriteUInt16(this ref SpanWriter<byte> writer, ushort value, bool isLittleEndian)
        => writer.Write(isLittleEndian == BitConverter.IsLittleEndian ? value : ReverseEndianness(value));

    /// <summary>
    /// Writes 32-bit signed integer to the block of memory.
    /// </summary>
    /// <param name="writer">Memory writer.</param>
    /// <param name="value">The value to be encoded in the block of memory.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place the value.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt32(this ref SpanWriter<byte> writer, int value, bool isLittleEndian)
        => writer.Write(isLittleEndian == BitConverter.IsLittleEndian ? value : ReverseEndianness(value));

    /// <summary>
    /// Writes 32-bit unsigned integer to the block of memory.
    /// </summary>
    /// <param name="writer">Memory writer.</param>
    /// <param name="value">The value to be encoded in the block of memory.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place the value.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static void WriteUInt32(this ref SpanWriter<byte> writer, uint value, bool isLittleEndian)
        => writer.Write(isLittleEndian == BitConverter.IsLittleEndian ? value : ReverseEndianness(value));

    /// <summary>
    /// Writes 64-bit signed integer to the block of memory.
    /// </summary>
    /// <param name="writer">Memory writer.</param>
    /// <param name="value">The value to be encoded in the block of memory.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place the value.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteInt64(this ref SpanWriter<byte> writer, long value, bool isLittleEndian)
        => writer.Write(isLittleEndian == BitConverter.IsLittleEndian ? value : ReverseEndianness(value));

    /// <summary>
    /// Writes 64-bit unsigned integer to the block of memory.
    /// </summary>
    /// <param name="writer">Memory writer.</param>
    /// <param name="value">The value to be encoded in the block of memory.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place the value.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static void WriteUInt64(this ref SpanWriter<byte> writer, ulong value, bool isLittleEndian)
        => writer.Write(isLittleEndian == BitConverter.IsLittleEndian ? value : ReverseEndianness(value));

    /// <summary>
    /// Writes single-precision floating-point number to the block of memory.
    /// </summary>
    /// <param name="writer">Memory writer.</param>
    /// <param name="value">The value to be encoded in the block of memory.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place the value.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteSingle(this ref SpanWriter<byte> writer, float value, bool isLittleEndian)
        => writer.WriteInt32(BitConverter.SingleToInt32Bits(value), isLittleEndian);

    /// <summary>
    /// Writes double-precision floating-point number to the block of memory.
    /// </summary>
    /// <param name="writer">Memory writer.</param>
    /// <param name="value">The value to be encoded in the block of memory.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place the value.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteDouble(this ref SpanWriter<byte> writer, double value, bool isLittleEndian)
        => writer.WriteInt64(BitConverter.DoubleToInt64Bits(value), isLittleEndian);

    /// <summary>
    /// Writes half-precision floating-point number to the block of memory.
    /// </summary>
    /// <param name="writer">Memory writer.</param>
    /// <param name="value">The value to be encoded in the block of memory.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place the value.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteHalf(this ref SpanWriter<byte> writer, Half value, bool isLittleEndian)
        => writer.WriteInt16(BitConverter.HalfToInt16Bits(value), isLittleEndian);

    /// <summary>
    /// Writes the contents of a string builder to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="input">The string builder.</param>
    public static void Write(this ref SpanWriter<char> writer, StringBuilder input)
    {
        foreach (var chunk in input.GetChunks())
            writer.Write(chunk.Span);
    }

    /// <summary>
    /// Converts the value to a set of characters and writes them to the buffer.
    /// </summary>
    /// <typeparam name="T">The formattable type.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be converted to a set of characters.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="writer"/> is not large enough to place the characters.</exception>
    public static void Write<T>(this ref SpanWriter<char> writer, T value, scoped ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : notnull, ISpanFormattable
    {
        if (!value.TryFormat(writer.RemainingSpan, out var writtenCount, format, provider))
            throw new ArgumentOutOfRangeException(nameof(writer));

        writer.Advance(writtenCount);
    }
}