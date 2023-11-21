using System.Numerics;
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
    public static unsafe void Write<T>(this ref SpanWriter<byte> writer, in T value)
        where T : unmanaged
        => Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(writer.Slide(sizeof(T))), value);

    /// <summary>
    /// Writes a number in little-endian byte order.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place the value.</exception>
    public static void WriteLittleEndian<T>(this ref SpanWriter<byte> writer, T value)
        where T : unmanaged, IBinaryInteger<T>
        => writer.Advance(value.WriteLittleEndian(writer.RemainingSpan));

    /// <summary>
    /// Attempts to write a number in little-endian byte order.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <returns><see langword="true"/> if <paramref name="writer"/> has enough space to place the value; otherwise, <see langword="false"/>.</returns>
    public static bool TryWriteLittleEndian<T>(this ref SpanWriter<byte> writer, T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        bool result;
        if (result = value.TryWriteLittleEndian(writer.RemainingSpan, out var writtenBytes))
            writer.Advance(writtenBytes);

        return result;
    }

    /// <summary>
    /// Writes a number in big-endian byte order.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <exception cref="InternalBufferOverflowException">Remaining space in the underlying span is not enough to place the value.</exception>
    public static void WriteBigEndian<T>(this ref SpanWriter<byte> writer, T value)
        where T : unmanaged, IBinaryInteger<T>
        => writer.Advance(value.WriteBigEndian(writer.RemainingSpan));

    /// <summary>
    /// Attempts to write a number in big-endian byte order.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to encode.</param>
    /// <returns><see langword="true"/> if <paramref name="writer"/> has enough space to place the value; otherwise, <see langword="false"/>.</returns>
    public static bool TryWriteBigEndian<T>(this ref SpanWriter<byte> writer, T value)
        where T : unmanaged, IBinaryInteger<T>
    {
        bool result;
        if (result = value.TryWriteLittleEndian(writer.RemainingSpan, out var writtenBytes))
            writer.Advance(writtenBytes);

        return result;
    }

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

    /// <summary>
    /// Converts the value to a set of characters and writes them to the buffer.
    /// </summary>
    /// <typeparam name="T">The formattable type.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be converted to a set of characters.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns><see langword="true"/> if <paramref name="writer"/> has enough space to place the value; otherwise, <see langword="false"/>.</returns>
    public static bool TryWrite<T>(this ref SpanWriter<char> writer, T value, scoped ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : notnull, ISpanFormattable
    {
        bool result;
        if (result = value.TryFormat(writer.RemainingSpan, out var writtenCount, format, provider))
            writer.Advance(writtenCount);

        return result;
    }
}