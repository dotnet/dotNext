using System.Buffers;
using System.Numerics;

namespace DotNext.Buffers;

using Binary;
using Numerics;

/// <summary>
/// Providers extension methods to work with byte buffers.
/// </summary>
public static class ByteBuffer
{
    private const int MaxBufferSize = int.MaxValue / 2;

    /// <summary>
    /// Writes a value in little-endian format to the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to be written as a sequence of bytes.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough space to place <paramref name="value"/>.</exception>
    public static void WriteLittleEndian<T>(this IBufferWriter<byte> writer, T value)
        where T : notnull, IBinaryInteger<T>
    {
        var buffer = writer.GetSpan(value.GetByteCount());
        if (!value.TryWriteLittleEndian(buffer, out var bytesWritten))
            throw new InsufficientMemoryException();

        writer.Advance(bytesWritten);
    }

    /// <summary>
    /// Writes a value in big-endian format to the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to be written as a sequence of bytes.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough space to place <paramref name="value"/>.</exception>
    public static void WriteBigEndian<T>(this IBufferWriter<byte> writer, T value)
        where T : notnull, IBinaryInteger<T>
    {
        var buffer = writer.GetSpan(value.GetByteCount());
        if (!value.TryWriteBigEndian(buffer, out var bytesWritten))
            throw new InsufficientMemoryException();

        writer.Advance(bytesWritten);
    }

    /// <summary>
    /// Writes the value as a sequence of bytes to the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to be written as a sequence of bytes.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    public static void Write<T>(this IBufferWriter<byte> writer, T value)
        where T : notnull, IBinaryFormattable<T>
    {
        value.Format(writer.GetSpan(T.Size));
        writer.Advance(T.Size);
    }

    /// <summary>
    /// Writes <see cref="BigInteger"/> value to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <param name="isBigEndian"><see langword="true"/> to use unsigned encoding; otherwise, <see langword="false"/>.</param>
    /// <param name="isUnsigned"><see langword="true"/> to write the bytes in a big-endian byte order; otherwise, <see langword="false"/>.</param>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough space to place <paramref name="value"/>.</exception>
    public static void Write(this IBufferWriter<byte> writer, in BigInteger value, bool isBigEndian = false, bool isUnsigned = false)
    {
        var buffer = writer.GetSpan(value.GetByteCount(isUnsigned));
        if (!value.TryWriteBytes(buffer, out var bytesWritten, isUnsigned, isBigEndian))
            throw new InsufficientMemoryException();

        writer.Advance(bytesWritten);
    }

    /// <summary>
    /// Formats the value as UTF-8 into the provided buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to be written as UTF-8.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <param name="format">A standard or custom format string.</param>
    /// <param name="provider">Culture-specific formatting information.</param>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough space to place <paramref name="value"/>.</exception>
    public static void Format<T>(this IBufferWriter<byte> writer, T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : notnull, IUtf8SpanFormattable
    {
        var buffer = writer.GetSpan();
        int bytesWritten;
        for (int sizeHint; !value.TryFormat(buffer, out bytesWritten, format, provider); buffer = writer.GetSpan(sizeHint))
        {
            sizeHint = buffer.Length;
            sizeHint = sizeHint <= MaxBufferSize ? sizeHint << 1 : throw new InsufficientMemoryException();
        }

        writer.Advance(bytesWritten);
    }

    /// <summary>
    /// Writes a value in little-endian format to the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to be written as a sequence of bytes.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough space to place <paramref name="value"/>.</exception>
    public static void WriteLittleEndian<T>(this ref BufferWriterSlim<byte> writer, T value)
        where T : notnull, IBinaryInteger<T>
    {
        var buffer = writer.GetSpan(value.GetByteCount());
        if (!value.TryWriteLittleEndian(buffer, out var bytesWritten))
            throw new InsufficientMemoryException();

        writer.Advance(bytesWritten);
    }

    /// <summary>
    /// Writes a value in big-endian format to the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to be written as a sequence of bytes.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough space to place <paramref name="value"/>.</exception>
    public static void WriteBigEndian<T>(this ref BufferWriterSlim<byte> writer, T value)
        where T : notnull, IBinaryInteger<T>
    {
        var buffer = writer.GetSpan(value.GetByteCount());
        if (!value.TryWriteBigEndian(buffer, out var bytesWritten))
            throw new InsufficientMemoryException();

        writer.Advance(bytesWritten);
    }

    /// <summary>
    /// Writes the value as a sequence of bytes to the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to be written as a sequence of bytes.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    public static void Write<T>(this ref BufferWriterSlim<byte> writer, T value)
        where T : notnull, IBinaryFormattable<T>
    {
        value.Format(writer.GetSpan(T.Size));
        writer.Advance(T.Size);
    }

    /// <summary>
    /// Writes <see cref="BigInteger"/> value to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <param name="isBigEndian"><see langword="true"/> to use unsigned encoding; otherwise, <see langword="false"/>.</param>
    /// <param name="isUnsigned"><see langword="true"/> to write the bytes in a big-endian byte order; otherwise, <see langword="false"/>.</param>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough space to place <paramref name="value"/>.</exception>
    public static void Write(this ref BufferWriterSlim<byte> writer, in BigInteger value, bool isBigEndian = false, bool isUnsigned = false)
    {
        var buffer = writer.InternalGetSpan(value.GetByteCount(isUnsigned));
        if (!value.TryWriteBytes(buffer, out var bytesWritten, isUnsigned, isBigEndian))
            throw new InsufficientMemoryException();

        writer.Advance(bytesWritten);
    }

    /// <summary>
    /// Formats the value as UTF-8 into the provided buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to be written as UTF-8.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <param name="format">A standard or custom format string.</param>
    /// <param name="provider">Culture-specific formatting information.</param>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough space to place <paramref name="value"/>.</exception>
    public static void Format<T>(this ref BufferWriterSlim<byte> writer, T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : notnull, IUtf8SpanFormattable
    {
        var buffer = writer.InternalGetSpan(sizeHint: 0);
        int bytesWritten;
        for (int sizeHint; !value.TryFormat(buffer, out bytesWritten, format, provider); buffer = writer.InternalGetSpan(sizeHint))
        {
            sizeHint = buffer.Length;
            sizeHint = sizeHint <= MaxBufferSize ? sizeHint << 1 : throw new InsufficientMemoryException();
        }

        writer.Advance(bytesWritten);
    }

    /// <summary>
    /// Writes a value in little-endian format to the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to be written as a sequence of bytes.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough space to place <paramref name="value"/>.</exception>
    public static void WriteLittleEndian<T>(this ref SpanWriter<byte> writer, T value)
        where T : notnull, IBinaryInteger<T>
    {
        if (!value.TryWriteLittleEndian(writer.RemainingSpan, out var bytesWritten))
            throw new InsufficientMemoryException();

        writer.Advance(bytesWritten);
    }

    /// <summary>
    /// Writes a value in big-endian format to the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to be written as a sequence of bytes.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough space to place <paramref name="value"/>.</exception>
    public static void WriteBigEndian<T>(this ref SpanWriter<byte> writer, T value)
        where T : notnull, IBinaryInteger<T>
    {
        if (!value.TryWriteBigEndian(writer.RemainingSpan, out var bytesWritten))
            throw new InsufficientMemoryException();

        writer.Advance(bytesWritten);
    }

    /// <summary>
    /// Writes the value as a sequence of bytes to the buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to be written as a sequence of bytes.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    public static void Write<T>(this ref SpanWriter<byte> writer, T value)
        where T : notnull, IBinaryFormattable<T>
        => value.Format(writer.Slide(T.Size));

    /// <summary>
    /// Writes <see cref="BigInteger"/> value to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <param name="isBigEndian"><see langword="true"/> to use unsigned encoding; otherwise, <see langword="false"/>.</param>
    /// <param name="isUnsigned"><see langword="true"/> to write the bytes in a big-endian byte order; otherwise, <see langword="false"/>.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="writer"/> has enough space to place formatted value;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryWrite(this ref SpanWriter<byte> writer, in BigInteger value, bool isBigEndian = false, bool isUnsigned = false)
    {
        bool result;
        if (result = value.TryWriteBytes(writer.RemainingSpan, out var bytesWritten, isUnsigned, isBigEndian))
            writer.Advance(bytesWritten);

        return result;
    }

    /// <summary>
    /// Tries to format the value as UTF-8 into the provided buffer.
    /// </summary>
    /// <typeparam name="T">The type of the value to be written as UTF-8.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to be written as a sequence of bytes.</param>
    /// <param name="format">A standard or custom format string.</param>
    /// <param name="provider">Culture-specific formatting information.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="writer"/> has enough space to place formatted value;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryFormat<T>(this ref SpanWriter<byte> writer, T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : notnull, IUtf8SpanFormattable
    {
        bool result;
        if (result = value.TryFormat(writer.RemainingSpan, out var bytesWritten, format, provider))
            writer.Advance(bytesWritten);

        return result;
    }

    /// <summary>
    /// Reads a value encoded in little-endian format.
    /// </summary>
    /// <typeparam name="T">The type of the value to read.</typeparam>
    /// <param name="reader">The reader.</param>
    /// <returns>The value read from <paramref name="reader"/>.</returns>
    public static T ReadLittleEndian<T>(this ref SpanReader<byte> reader)
        where T : struct, IBinaryInteger<T>
        => T.ReadLittleEndian(reader.Read(Number.GetMaxByteCount<T>()), Number.IsSigned<T>() is false);

    /// <summary>
    /// Reads a value encoded in big-endian format.
    /// </summary>
    /// <typeparam name="T">The type of the value to read.</typeparam>
    /// <param name="reader">The reader.</param>
    /// <returns>The value read from <paramref name="reader"/>.</returns>
    [CLSCompliant(false)]
    public static T ReadBigEndian<T>(this ref SpanReader<byte> reader)
        where T : struct, IBinaryInteger<T>, IUnsignedNumber<T>
        => T.ReadBigEndian(reader.Read(Number.GetMaxByteCount<T>()), Number.IsSigned<T>() is false);
}