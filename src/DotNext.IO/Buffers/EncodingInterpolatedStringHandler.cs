using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Buffers;

using EncodingContext = DotNext.Text.EncodingContext;

/// <summary>
/// Represents converter of interpolated string directly to a sequence of bytes
/// using the specified encoding.
/// </summary>
[InterpolatedStringHandler]
[EditorBrowsable(EditorBrowsableState.Never)]
[StructLayout(LayoutKind.Auto)]
public ref struct EncodingInterpolatedStringHandler
{
    private const int MaxBufferSize = int.MaxValue / 2;
    private const char Whitespace = ' ';

    private readonly IBufferWriter<byte> buffer;
    private readonly IFormatProvider? provider;
    private readonly Encoding encoding;
    private readonly Encoder encoder;
    private readonly Span<char> charBuffer;
    private int count;

    /// <summary>
    /// Initializes a new interpolated string handler.
    /// </summary>
    /// <param name="literalLength">The total number of characters in known at compile-time.</param>
    /// <param name="formattedCount">The number of placeholders.</param>
    /// <param name="buffer">The output buffer.</param>
    /// <param name="context">The encoding context.</param>
    /// <param name="charBuffer">The preallocated temporary buffer for characters.</param>
    /// <param name="provider">The format provider.</param>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is <see langword="null"/>.</exception>
    public EncodingInterpolatedStringHandler(int literalLength, int formattedCount, IBufferWriter<byte> buffer, in EncodingContext context, Span<char> charBuffer = default, IFormatProvider? provider = null)
    {
        this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        this.provider = provider;
        encoding = context.Encoding;
        encoder = context.GetEncoder();
        this.charBuffer = charBuffer;

        // assume that every placeholder will be converted to substring no longer than X chars
        const int charsPerPlaceholder = 10;
        buffer.GetSpan(context.Encoding.GetMaxByteCount((charsPerPlaceholder * formattedCount) + literalLength));
        count = 0;
    }

    /// <summary>
    /// Gets number of written characters.
    /// </summary>
    public readonly int WrittenCount => count;

    /// <summary>
    /// Writes the specified string to the handler.
    /// </summary>
    /// <param name="value">The string to write.</param>
    public void AppendLiteral(string? value)
        => AppendFormatted(value.AsSpan());

    /// <summary>
    /// Writes the specified character span to the handler.
    /// </summary>
    /// <param name="value">The span to write.</param>
    public void AppendFormatted(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
            return;

        var output = buffer.GetSpan(encoding.GetByteCount(value));
        var writtenCount = encoder.GetBytes(value, output, true);
        buffer.Advance(writtenCount);
        count += writtenCount;
    }

    /// <summary>
    /// Writes the specified value to the handler.
    /// </summary>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <param name="format">The format string.</param>
    public void AppendFormatted<T>(T value, string? format = null)
    {
        switch (value)
        {
            case ISpanFormattable:
                for (int bufferSize = 0; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize * 2 : throw new InsufficientMemoryException())
                {
                    using var tempBuffer = bufferSize <= charBuffer.Length ? charBuffer : new MemoryRental<char>(bufferSize, false);

                    // constrained call avoiding boxing for value types
                    if (((ISpanFormattable)value).TryFormat(tempBuffer.Span, out var charsWritten, format, provider))
                    {
                        AppendFormatted(tempBuffer.Span.Slice(0, charsWritten));
                        break;
                    }
                }

                break;
            case IFormattable:
                AppendLiteral(((IFormattable)value).ToString(format, provider)); // constrained call avoiding boxing for value types
                break;
            case not null:
                AppendLiteral(value.ToString());
                break;
        }
    }

    private void AppendFormatted(ReadOnlySpan<char> value, int alignment, bool leftAlign)
    {
        Debug.Assert(alignment >= 0);

        var padding = alignment - value.Length;
        if (padding <= 0)
        {
            AppendFormatted(value);
            return;
        }

        using var tempBuffer = alignment <= charBuffer.Length ? charBuffer.Slice(0, alignment) : new MemoryRental<char>(alignment);
        var span = tempBuffer.Span;
        var filler = leftAlign
            ? span.Slice(value.Length, padding)
            : span.TrimLength(padding, out span);

        filler.Fill(Whitespace);
        value.CopyTo(span);

        AppendFormatted(span);
    }

    /// <summary>
    /// Writes the specified string of chars to the handler.
    /// </summary>
    /// <param name="value">The span to write.</param>
    /// <param name="alignment">
    /// Minimum number of characters that should be written for this value. If the value is negative,
    /// it indicates left-aligned and the required minimum is the absolute value.
    /// </param>
    public void AppendFormatted(ReadOnlySpan<char> value, int alignment)
    {
        var leftAlign = false;

        if (alignment < 0)
        {
            leftAlign = true;
            alignment = -alignment;
        }

        AppendFormatted(value, alignment, leftAlign);
    }

    /// <summary>
    /// Writes the specified value to the handler.
    /// </summary>
    /// <typeparam name="T">The type of the value to write.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <param name="alignment">
    /// Minimum number of characters that should be written for this value. If the value is negative,
    /// it indicates left-aligned and the required minimum is the absolute value.
    /// </param>
    /// <param name="format">The format string.</param>
    public void AppendFormatted<T>(T value, int alignment, string? format = null)
    {
        var leftAlign = false;

        if (alignment < 0)
        {
            leftAlign = true;
            alignment = -alignment;
        }

        switch (value)
        {
            case ISpanFormattable:
                for (int bufferSize = alignment; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize * 2 : throw new InsufficientMemoryException())
                {
                    using var tempBuffer = bufferSize <= charBuffer.Length ? charBuffer : new MemoryRental<char>(bufferSize, false);
                    Span<char> span = tempBuffer.Span, filler;

                    if (((ISpanFormattable)value).TryFormat(span, out var charsWritten, format, provider))
                    {
                        var padding = alignment - charsWritten;

                        if (padding <= 0)
                        {
                            alignment = charsWritten;
                        }
                        else if (leftAlign)
                        {
                            filler = span.Slice(charsWritten, padding);
                            filler.Fill(Whitespace);
                        }
                        else
                        {
                            filler = span.TrimLength(padding, out var rest);
                            span.Slice(0, charsWritten).CopyTo(rest);
                            filler.Fill(Whitespace);
                        }

                        AppendFormatted(span.Slice(0, alignment));
                        break;
                    }
                }

                break;
            case IFormattable:
                AppendFormatted(((IFormattable)value).ToString(format, provider).AsSpan(), alignment, leftAlign);
                break;
            case not null:
                AppendFormatted(value.ToString().AsSpan(), alignment, leftAlign);
                break;
        }
    }
}