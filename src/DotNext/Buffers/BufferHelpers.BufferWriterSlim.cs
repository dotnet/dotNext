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

    /// <summary>
    /// Writes the value as a sequence of characters.
    /// </summary>
    /// <typeparam name="T">The type of the value to convert.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    public static void WriteFormattable<T>(this ref BufferWriterSlim<char> writer, T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : notnull, ISpanFormattable
    {
        const int maxBufferSize = int.MaxValue / 2;

        for (int bufferSize = 0; ;)
        {
            var buffer = writer.GetSpan(bufferSize);
            if (value.TryFormat(buffer, out var charsWritten, format, provider))
            {
                writer.Advance(charsWritten);
                break;
            }

            bufferSize = bufferSize <= maxBufferSize ? buffer.Length * 2 : throw new InsufficientMemoryException();
        }
    }

    /// <summary>
    /// Writes line termination symbols to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    public static void WriteLine(this ref BufferWriterSlim<char> writer)
        => writer.Write(Environment.NewLine);

    /// <summary>
    /// Writes a string to the buffer, followed by a line terminator.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="characters">The characters to write.</param>
    public static void WriteLine(this ref BufferWriterSlim<char> writer, ReadOnlySpan<char> characters)
    {
        writer.Write(characters);
        writer.Write(Environment.NewLine);
    }

    /// <summary>
    /// Writes the value as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to convert.</param>
    public static void WriteFormattable<T>(this ref BufferWriterSlim<byte> writer, T value)
        where T : notnull, IBinaryFormattable<T>
    {
        var output = new SpanWriter<byte>(writer.GetSpan(T.Size));
        value.Format(ref output);
        writer.Advance(output.WrittenCount);
    }

    /// <summary>
    /// Writes a sequence of formattable values.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="values">A sequence of values to convert.</param>
    public static void WriteFormattable<T>(this ref BufferWriterSlim<byte> writer, ReadOnlySpan<T> values)
        where T : notnull, IBinaryFormattable<T>
    {
        if (values.IsEmpty)
            return;

        var output = new SpanWriter<byte>(writer.GetSpan(checked(T.Size * values.Length)));

        foreach (ref readonly var value in values)
            value.Format(ref output);

        writer.Advance(output.WrittenCount);
    }
}