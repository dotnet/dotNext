using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace DotNext.Text;

using Buffers;

partial interface IInterpolatedStringHandler
{
    private const byte WhitespaceUtf8 = (byte)' ';
    
    protected static int AppendFormatted<TWriter>(TWriter writer, Encoder encoder, ReadOnlySpan<char> value)
        where TWriter : struct, IBufferWriter<byte>, allows ref struct
    {
        if (value.IsEmpty)
            return 0;

        var output = writer.GetSpan(encoder.GetByteCount(value, flush: true));
        var writtenCount = encoder.GetBytes(value, output, flush: true);
        writer.Advance(writtenCount);
        return writtenCount;
    }

    protected static int AppendFormatted<T, TWriter>(TWriter writer, Encoder encoder, Span<char> buffer, T value, string? format, IFormatProvider? provider)
        where TWriter : struct, IBufferWriter<byte>, allows ref struct
    {
        int bufferSize, charsWritten = 0;
        switch (value)
        {
            case IUtf8SpanFormattable when IsUtf8(encoder):
                for (bufferSize = 0; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
                {
                    var span = writer.GetSpan(bufferSize);

                    // constrained call avoiding boxing for value types
                    if (((IUtf8SpanFormattable)value).TryFormat(span, out charsWritten, format, provider))
                    {
                        writer.Advance(charsWritten);
                        break;
                    }
                }

                break;
            case ISpanFormattable:
                for (bufferSize = 0; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
                {
                    using var tempBuffer = bufferSize <= buffer.Length
                        ? buffer
                        : new SpanOwner<char>(bufferSize, exactSize: false);

                    // constrained call avoiding boxing for value types
                    if (((ISpanFormattable)value).TryFormat(tempBuffer.Span, out charsWritten, format, provider))
                    {
                        charsWritten = AppendFormatted(writer, encoder, tempBuffer.Span.Slice(0, charsWritten));
                        break;
                    }
                }

                break;
            case IFormattable:
                charsWritten = AppendFormatted(writer, encoder, ((IFormattable)value).ToString(format, provider)); // constrained call avoiding boxing for value types
                break;
            case not null:
                charsWritten = AppendFormatted(writer, encoder, value.ToString());
                break;
        }

        return charsWritten;
    }

    protected static int AppendFormatted<TWriter>(TWriter writer, Encoder encoder, Span<char> buffer, scoped ReadOnlySpan<char> value, int alignment)
        where TWriter : struct, IBufferWriter<byte>, allows ref struct
    {
        bool leftAlign;

        if (leftAlign = alignment < 0)
            alignment = -alignment;

        return AppendFormatted(writer, encoder, buffer, value, alignment, leftAlign);
    }

    private static int AppendFormatted<TWriter>(TWriter writer, Encoder encoder, Span<char> buffer, scoped ReadOnlySpan<char> value, int alignment,
        bool leftAlign)
        where TWriter : struct, IBufferWriter<byte>, allows ref struct
    {
        Debug.Assert(alignment >= 0);

        var padding = alignment - value.Length;
        if (padding <= 0)
        {
            return AppendFormatted(writer, encoder, value);
        }

        using var tempBuffer = alignment <= buffer.Length
            ? buffer.Slice(0, alignment)
            : new SpanOwner<char>(alignment);

        var span = tempBuffer.Span;
        var filler = leftAlign
            ? span.Slice(value.Length, padding)
            : span.TrimLength(padding, out span);

        filler.Fill(WhitespaceUtf16);
        value.CopyTo(span);

        return AppendFormatted(writer, encoder, span);
    }

    protected static int AppendFormatted<T, TWriter>(TWriter writer, Encoder encoder, Span<char> buffer, T value, int alignment, string? format, IFormatProvider? provider)
        where TWriter : struct, IBufferWriter<byte>, allows ref struct
    {
        int bufferSize, charsWritten = 0;
        bool leftAlign;

        if (leftAlign = alignment < 0)
            alignment = -alignment;

        switch (value)
        {
            case IUtf8SpanFormattable when IsUtf8(encoder):
                for (bufferSize = alignment; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
                {
                    var span = writer.GetSpan(bufferSize);
                    if (((IUtf8SpanFormattable)value).TryFormat(span, out charsWritten, format, provider))
                    {
                        charsWritten = Align(span, WhitespaceUtf8, alignment, charsWritten, leftAlign);
                        writer.Advance(charsWritten);
                        break;
                    }
                }

                break;
            case ISpanFormattable:
                for (bufferSize = alignment; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize * 2 : throw new InsufficientMemoryException())
                {
                    using var tempBuffer = bufferSize <= buffer.Length
                        ? buffer
                        : new SpanOwner<char>(bufferSize, false);
                    
                    var span = tempBuffer.Span;

                    if (((ISpanFormattable)value).TryFormat(span, out charsWritten, format, provider))
                    {
                        charsWritten = AppendFormatted(writer,
                            encoder,
                            span.Slice(0, Align(span, WhitespaceUtf16, alignment, charsWritten, leftAlign)));
                        break;
                    }
                }

                break;
            case IFormattable:
                charsWritten = AppendFormatted(writer, encoder, buffer, ((IFormattable)value).ToString(format, provider).AsSpan(), alignment, leftAlign);
                break;
            case not null:
                charsWritten = AppendFormatted(writer, encoder, buffer, value.ToString().AsSpan(), alignment, leftAlign);
                break;
        }

        return charsWritten;
    }
    
    private static int Align<T>(Span<T> buffer, T whitespace, int alignment, int bytesWritten, bool leftAlign)
        where T : struct, IBinaryNumber<T>
    {
        Span<T> filler;
        var padding = alignment - bytesWritten;

        if (padding <= 0)
        {
            alignment = bytesWritten;
        }
        else if (leftAlign)
        {
            filler = buffer.Slice(bytesWritten, padding);
            filler.Fill(whitespace);
        }
        else
        {
            filler = buffer.TrimLength(padding, out var rest);
            buffer.Slice(0, bytesWritten).CopyTo(rest);
            filler.Fill(whitespace);
        }

        return alignment;
    }

    protected static int EstimateUtf8BufferSize(int literalLength, int formattedCount)
    {
        const long maxBytesPerChar = 4;
        var length = literalLength * maxBytesPerChar + formattedCount * CharsPerPlaceholder * maxBytesPerChar;
        return int.CreateSaturating(length);
    }
}