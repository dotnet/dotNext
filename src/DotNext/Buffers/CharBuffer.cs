using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotNext.Buffers;

/// <summary>
/// Providers extension methods to work with char buffers.
/// </summary>
public static partial class CharBuffer
{
    private static void Write<TWriter>(TWriter writer, StringBuilder input)
        where TWriter : struct, IBufferWriter<char>, allows ref struct
    {
        foreach (var chunk in input.GetChunks())
            Memory.Write(writer, chunk.Span);
    }

    /// <summary>
    /// Writes the contents of string builder to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="input">The string builder.</param>
    public static void Write(this IBufferWriter<char> writer, StringBuilder input)
        => Write<BufferWriterReference<char>>(new(writer), input);

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
        => AppendFormatted<T, BufferWriterReference<char>>(new(writer), value, format, provider);
    
    private static int Format<TWriter>(TWriter writer, CompositeFormat format, ReadOnlySpan<object?> args, IFormatProvider? provider)
        where TWriter : struct, IBufferWriter<char>, allows ref struct
    {
        const int maxBufferSize = int.MaxValue / 2;

        int bufferSize;
        for (bufferSize = 0; ; bufferSize = bufferSize <= maxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException(ExceptionMessages.NotEnoughMemory))
        {
            var buffer = writer.GetSpan(bufferSize);
            if (buffer.TryWrite(provider, format, out bufferSize, args))
                break;

            bufferSize = buffer.Length;
        }

        writer.Advance(bufferSize);
        return bufferSize;
    }

    /// <summary>
    /// Writes formatted string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="format">Formatting template.</param>
    /// <param name="args">The arguments to be rendered as a part of template.</param>
    /// <param name="provider">A culture-specific formatting information.</param>
    /// <returns>The number of written characters.</returns>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough space to place rendered template.</exception>
    public static int Format(this IBufferWriter<char> writer, CompositeFormat format, ReadOnlySpan<object?> args, IFormatProvider? provider = null)
        => Format<BufferWriterReference<char>>(new(writer), format, args, provider);

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
    
    private static void Concat<TWriter>(TWriter writer, scoped ReadOnlySpan<string?> values)
        where  TWriter : struct, IBufferWriter<char>, allows ref struct
    {
        switch (values.Length)
        {
            case 0:
                break;
            case 1:
                Memory.Write<char, TWriter>(writer, values[0]);
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
    /// Writes concatenated strings.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="values">An array of strings.</param>
    /// <exception cref="OutOfMemoryException">The concatenated string is too large.</exception>
    public static void Concat(this IBufferWriter<char> writer, params ReadOnlySpan<string?> values)
        => Concat<BufferWriterReference<char>>(new(writer), values);

    /// <summary>
    /// Writes the contents of string builder to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="input">The string builder.</param>
    public static void Write(this ref BufferWriterSlim<char> writer, StringBuilder input)
        => Write<BufferWriterSlim<char>.Ref>(new(ref writer), input);

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
        => AppendFormatted<T, BufferWriterSlim<char>.Ref>(new(ref writer), value, format, provider);

    /// <summary>
    /// Writes formatted string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="format">Formatting template.</param>
    /// <param name="args">The arguments to be rendered as a part of template.</param>
    /// <param name="provider">A culture-specific formatting information.</param>
    /// <returns>The number of written characters.</returns>
    /// <exception cref="InsufficientMemoryException"><paramref name="writer"/> has not enough space to place rendered template.</exception>
    public static int Format(this ref BufferWriterSlim<char> writer, CompositeFormat format, scoped ReadOnlySpan<object?> args,
        IFormatProvider? provider = null)
        => Format<BufferWriterSlim<char>.Ref>(new(ref writer), format, args, provider);

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
    public static void Concat(this ref BufferWriterSlim<char> writer, params ReadOnlySpan<string?> values)
        => Concat<BufferWriterSlim<char>.Ref>(new(ref writer), values);

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
        where T : ISpanFormattable
    {
        bool result;
        if (result = value.TryFormat(writer.RemainingSpan, out var writtenCount, format, provider))
            writer.Advance(writtenCount);

        return result;
    }

    /// <summary>
    /// Tries to write formatted string to the buffer.
    /// </summary>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="format">Formatting template.</param>
    /// <param name="args">The arguments to be rendered as a part of template.</param>
    /// <param name="provider">A culture-specific formatting information.</param>
    /// <returns><see langword="true"/> if <paramref name="writer"/> has enough free space to place rendered string; otherwise, <see langword="false"/>.</returns>
    public static bool TryFormat(this ref SpanWriter<char> writer, CompositeFormat format, scoped ReadOnlySpan<object?> args, IFormatProvider? provider = null)
    {
        bool result;
        if (result = writer.RemainingSpan.TryWrite(provider, format, out var charsWritten, args))
            writer.Advance(charsWritten);

        return result;
    }
}