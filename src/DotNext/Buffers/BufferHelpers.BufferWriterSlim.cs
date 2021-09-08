using System.Runtime.CompilerServices;
using System.Text;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Buffers;

public static partial class BufferHelpers
{
    private static unsafe void Write<T>(ref BufferWriterSlim<byte> builder, delegate*<Span<byte>, T, void> encoder, T value)
        where T : unmanaged
    {
        var memory = Span.AsBytes(ref value);
        encoder(memory, value);
        builder.Write(memory);
    }

    /// <summary>
    /// Encodes 16-bit signed integer as bytes.
    /// </summary>
    /// <param name="builder">The buffer writer.</param>
    /// <param name="value">The value to be encoded.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteInt16(this ref BufferWriterSlim<byte> builder, short value, bool isLittleEndian)
        => Write<short>(ref builder, isLittleEndian ? &WriteInt16LittleEndian : &WriteInt16BigEndian, value);

    /// <summary>
    /// Encodes 16-bit unsigned integer as bytes.
    /// </summary>
    /// <param name="builder">The buffer writer.</param>
    /// <param name="value">The value to be encoded.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe void WriteUInt16(this ref BufferWriterSlim<byte> builder, ushort value, bool isLittleEndian)
        => Write<ushort>(ref builder, isLittleEndian ? &WriteUInt16LittleEndian : &WriteUInt16BigEndian, value);

    /// <summary>
    /// Encodes 32-bit signed integer as bytes.
    /// </summary>
    /// <param name="builder">The buffer writer.</param>
    /// <param name="value">The value to be encoded.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteInt32(this ref BufferWriterSlim<byte> builder, int value, bool isLittleEndian)
        => Write<int>(ref builder, isLittleEndian ? &WriteInt32LittleEndian : &WriteInt32BigEndian, value);

    /// <summary>
    /// Encodes 32-bit unsigned integer as bytes.
    /// </summary>
    /// <param name="builder">The buffer writer.</param>
    /// <param name="value">The value to be encoded.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe void WriteUInt32(this ref BufferWriterSlim<byte> builder, uint value, bool isLittleEndian)
        => Write<uint>(ref builder, isLittleEndian ? &WriteUInt32LittleEndian : &WriteUInt32BigEndian, value);

    /// <summary>
    /// Encodes 64-bit signed integer as bytes.
    /// </summary>
    /// <param name="builder">The buffer writer.</param>
    /// <param name="value">The value to be encoded.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void WriteInt64(this ref BufferWriterSlim<byte> builder, long value, bool isLittleEndian)
        => Write<long>(ref builder, isLittleEndian ? &WriteInt64LittleEndian : &WriteInt64BigEndian, value);

    /// <summary>
    /// Encodes 64-bit unsigned integer as bytes.
    /// </summary>
    /// <param name="builder">The buffer writer.</param>
    /// <param name="value">The value to be encoded.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe void WriteUInt64(this ref BufferWriterSlim<byte> builder, ulong value, bool isLittleEndian)
        => Write<ulong>(ref builder, isLittleEndian ? &WriteUInt64LittleEndian : &WriteUInt64BigEndian, value);

    /// <summary>
    /// Encodes single-precision floating-point number as bytes.
    /// </summary>
    /// <param name="builder">The buffer writer.</param>
    /// <param name="value">The value to be encoded.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    public static unsafe void WriteSingle(this ref BufferWriterSlim<byte> builder, float value, bool isLittleEndian)
        => Write<float>(ref builder, isLittleEndian ? &WriteSingleLittleEndian : &WriteSingleBigEndian, value);

    /// <summary>
    /// Encodes doubke-precision floating-point number as bytes.
    /// </summary>
    /// <param name="builder">The buffer writer.</param>
    /// <param name="value">The value to be encoded.</param>
    /// <param name="isLittleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
    public static unsafe void WriteDouble(this ref BufferWriterSlim<byte> builder, double value, bool isLittleEndian)
        => Write<double>(ref builder, isLittleEndian ? &WriteDoubleLittleEndian : &WriteDoubleBigEndian, value);

    /// <summary>
    /// Writes the contents of string builder to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="input">The string builder.</param>
    public static void Write(this ref BufferWriterSlim<char> writer, StringBuilder input)
    {
        foreach (var chunk in input.GetChunks())
            writer.Write(chunk.Span);
    }

    /// <summary>
    /// Writes interpolated string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="provider">The formatting provider.</param>
    /// <param name="handler">The handler of the interpolated string.</param>
    /// <returns>The number of written characters.</returns>
    public static int WriteString(this ref BufferWriterSlim<char> writer, IFormatProvider? provider, [InterpolatedStringHandlerArgument("writer", "provider")] ref BufferWriterSlimInterpolatedStringHandler handler)
    {
        handler.InstallWriter(out writer);
        return handler.WrittenCount;
    }

    /// <summary>
    /// Writes interpolated string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="handler">The handler of the interpolated string.</param>
    /// <returns>The number of written characters.</returns>
    public static int WriteString(this ref BufferWriterSlim<char> writer, [InterpolatedStringHandlerArgument("writer")] ref BufferWriterSlimInterpolatedStringHandler handler)
        => WriteString(ref writer, null, ref handler);
}