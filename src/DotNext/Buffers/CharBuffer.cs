using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.Buffers;

/// <summary>
/// Providers extension methods to work with char buffers.
/// </summary>
public static class CharBuffer
{
    /// <summary>
    /// Writes the contents of string builder to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="input">The string builder.</param>
    public static void Write(this IBufferWriter<char> writer, StringBuilder input)
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
    public static int Interpolate(this IBufferWriter<char> writer, IFormatProvider? provider, [InterpolatedStringHandlerArgument(nameof(writer), nameof(provider))] in BufferWriterInterpolatedStringHandler handler)
        => handler.WrittenCount;

    /// <summary>
    /// Writes interpolated string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="handler">The handler of the interpolated string.</param>
    /// <returns>The number of written characters.</returns>
    public static int Interpolate(this IBufferWriter<char> writer, [InterpolatedStringHandlerArgument(nameof(writer))] in BufferWriterInterpolatedStringHandler handler)
        => Interpolate(writer, null, in handler);

    /// <summary>
    /// Writes the value as a string.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>The number of written characters.</returns>
    public static int Format<T>(this IBufferWriter<char> writer, T value, string? format = null, IFormatProvider? provider = null)
        => BufferWriterInterpolatedStringHandler.AppendFormatted(writer, value, format, provider);

    /// <summary>
    /// Writes line termination symbols to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    public static void WriteLine(this IBufferWriter<char> writer)
        => writer.Write(Environment.NewLine);

    /// <summary>
    /// Writes a string to the buffer, followed by a line terminator.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="characters">The characters to write.</param>
    public static void WriteLine(this IBufferWriter<char> writer, scoped ReadOnlySpan<char> characters)
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
    public static void Concat(this IBufferWriter<char> writer, scoped ReadOnlySpan<string?> values)
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
                        var output = writer.GetSpan((int)totalLength);
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
    public static int Interpolate(this ref BufferWriterSlim<char> writer, IFormatProvider? provider, [InterpolatedStringHandlerArgument(nameof(writer), nameof(provider))] scoped in BufferWriterSlimInterpolatedStringHandler handler)
        => handler.WrittenCount;

    /// <summary>
    /// Writes interpolated string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="handler">The handler of the interpolated string.</param>
    /// <returns>The number of written characters.</returns>
    public static int Interpolate(this ref BufferWriterSlim<char> writer, [InterpolatedStringHandlerArgument(nameof(writer))] scoped in BufferWriterSlimInterpolatedStringHandler handler)
        => Interpolate(ref writer, null, in handler);

    /// <summary>
    /// Writes the value as a string.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="format">The format of the value.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>The number of written characters.</returns>
    public static int Format<T>(this ref BufferWriterSlim<char> writer, T value, string? format = null, IFormatProvider? provider = null)
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
    /// <returns><see langword="true"/> if <paramref name="writer"/> has enough space to place the value; otherwise, <see langword="false"/>.</returns>
    public static bool TryFormat<T>(this ref SpanWriter<char> writer, T value, scoped ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        where T : notnull, ISpanFormattable
    {
        bool result;
        if (result = value.TryFormat(writer.RemainingSpan, out var writtenCount, format, provider))
            writer.Advance(writtenCount);

        return result;
    }
}