using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.Buffers;

public static partial class BufferHelpers
{
    /// <summary>
    /// Encodes a number as little-endian format.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="builder">The byte buffer.</param>
    /// <param name="value">The value to encode.</param>
    public static void WriteLittleEndian<T>(this ref BufferWriterSlim<byte> builder, T value)
        where T : unmanaged, IBinaryInteger<T>
        => builder.Advance(value.WriteLittleEndian(builder.GetSpan(value.GetByteCount())));

    /// <summary>
    /// Encodes a number as big-endian format.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="builder">The byte buffer.</param>
    /// <param name="value">The value to encode.</param>
    public static void WriteBigEndian<T>(this ref BufferWriterSlim<byte> builder, T value)
        where T : unmanaged, IBinaryInteger<T>
        => builder.Advance(value.WriteLittleEndian(builder.GetSpan(value.GetByteCount())));

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
    public static int Interpolate(this ref BufferWriterSlim<char> writer, IFormatProvider? provider, [InterpolatedStringHandlerArgument(nameof(writer), nameof(provider))] scoped ref BufferWriterSlimInterpolatedStringHandler handler)
        => handler.WrittenCount;

    /// <summary>
    /// Writes interpolated string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="handler">The handler of the interpolated string.</param>
    /// <returns>The number of written characters.</returns>
    public static int Interpolate(this ref BufferWriterSlim<char> writer, [InterpolatedStringHandlerArgument(nameof(writer))] scoped ref BufferWriterSlimInterpolatedStringHandler handler)
        => Interpolate(ref writer, null, ref handler);

    /// <summary>
    /// Writes the value as a string.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>The number of written characters.</returns>
    public static int Write<T>(this ref BufferWriterSlim<char> writer, T value, string? format = null, IFormatProvider? provider = null)
        => BufferWriterSlimInterpolatedStringHandler.AppendFormatted(ref writer, value, format, provider);

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
    public static void WriteLine(this ref BufferWriterSlim<char> writer, scoped ReadOnlySpan<char> characters)
    {
        writer.Write(characters);
        writer.Write(Environment.NewLine);
    }

    /// <summary>
    /// Writes concatenated strings.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="values">An array of strings.</param>
    /// <exception cref="OutOfMemoryException">The concatenated string is too large.</exception>
    public static void Concat(this ref BufferWriterSlim<char> writer, scoped ReadOnlySpan<string?> values)
    {
        switch (values.Length)
        {
            case 0:
                break;
            case 1:
                writer.Write(values[0]);
                break;
            default:
                var totalLength = 0L;

                foreach (var str in values)
                {
                    if (str is { Length: > 0 })
                    {
                        totalLength += str.Length;
                    }
                }

                switch (totalLength)
                {
                    case 0L:
                        break;
                    case > int.MaxValue:
                        throw new OutOfMemoryException();
                    default:
                        var output = writer.InternalGetSpan((int)totalLength);
                        foreach (var str in values)
                        {
                            if (str is { Length: > 0 })
                            {
                                str.CopyTo(output);
                                output = output.Slice(str.Length);
                                writer.Advance(str.Length);
                            }
                        }

                        break;
                }

                break;
        }
    }

    /// <summary>
    /// Writes the value as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to convert.</param>
    public static void Write<T>(this ref BufferWriterSlim<byte> writer, T value)
        where T : notnull, IBinaryFormattable<T>
    {
        var output = new SpanWriter<byte>(writer.GetSpan(T.Size));
        value.Format(ref output);
        writer.Advance(output.WrittenCount);
    }

    /// <summary>
    /// Writes a sequence of formattable values.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="values">A sequence of values to convert.</param>
    public static void Write<T>(this ref BufferWriterSlim<byte> writer, scoped ReadOnlySpan<T> values)
        where T : notnull, IBinaryFormattable<T>
    {
        if (values.IsEmpty)
            return;

        var output = new SpanWriter<byte>(writer.GetSpan(checked(T.Size * values.Length)));

        foreach (ref readonly var value in values)
            value.Format(ref output);

        writer.Advance(output.WrittenCount);
    }

    /// <summary>
    /// Encodes formatted value as a set of UTF-8 bytes using the specified encoding.
    /// </summary>
    /// <typeparam name="T">The type of formattable value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The type value to be written as string.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>The number of written bytes.</returns>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough free space to place UTF-8 bytes.</exception>
    public static int EncodeAsUtf8<T>(this ref BufferWriterSlim<byte> writer, T value, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : notnull, IUtf8SpanFormattable
    {
        const int maxBufferSize = int.MaxValue / 2;
        int bytesWritten;
        for (var bufferSize = 0; ; bufferSize = bufferSize <= maxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
        {
            var span = writer.InternalGetSpan(bufferSize);

            // constrained call avoiding boxing for value types
            if (((IUtf8SpanFormattable)value).TryFormat(span, out bytesWritten, format, provider))
            {
                writer.Advance(bytesWritten);
                break;
            }
        }

        return bytesWritten;
    }
}