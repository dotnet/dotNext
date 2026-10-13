using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace DotNext.Text;

using Buffers;

partial interface IInterpolatedStringHandler
{
    private const byte WhitespaceUtf8 = (byte)WhitespaceUtf16;
    
    protected static int AppendFormatted<TWriter>(TWriter writer, Encoder encoder, ReadOnlySpan<char> value)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        int bytesWritten;
        if (value.IsEmpty)
        {
            bytesWritten = 0;
        }
        else
        {
            var output = writer.GetSpan(encoder.GetByteCount(value, flush: true));
            bytesWritten = encoder.GetBytes(value, output, flush: true);
            writer.Advance(bytesWritten);
        }
        
        return bytesWritten;
    }

    protected static int AppendFormatted<T, TWriter>(TWriter writer, Encoder encoder, Span<char> buffer, T value, string? format, IFormatProvider? provider)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        int bufferSize, bytesWritten = 0;
        switch (value)
        {
            case IUtf8SpanFormattable when encoder.IsUtf8:
                for (bufferSize = 0; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
                {
                    var span = writer.GetSpan(bufferSize);

                    // constrained call avoiding boxing for value types
                    if (((IUtf8SpanFormattable)value).TryFormat(span, out bytesWritten, format, provider))
                    {
                        writer.Advance(bytesWritten);
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
                    if (((ISpanFormattable)value).TryFormat(tempBuffer.Span, out bytesWritten, format, provider))
                    {
                        bytesWritten = AppendFormatted(writer, encoder, tempBuffer.Span.Slice(0, bytesWritten));
                        break;
                    }
                }

                break;
            case IFormattable:
                bytesWritten = AppendFormatted(writer, encoder, ((IFormattable)value).ToString(format, provider)); // constrained call avoiding boxing for value types
                break;
            case not null:
                bytesWritten = AppendFormatted(writer, encoder, ToStringNonBoxing(value));
                break;
        }

        return bytesWritten;
    }

    protected static int AppendFormatted<TWriter>(TWriter writer, Encoder encoder, Span<char> buffer, scoped ReadOnlySpan<char> value, int alignment)
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        bool leftAlign;

        if (leftAlign = alignment < 0)
            alignment = -alignment;

        return AppendFormatted(writer, encoder, buffer, value, alignment, leftAlign);
    }

    private static int AppendFormatted<TWriter>(TWriter writer, Encoder encoder, Span<char> buffer, scoped ReadOnlySpan<char> value, int alignment,
        bool leftAlign)
        where TWriter : IBufferWriter<byte>, allows ref struct
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
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        int bufferSize, bytesWritten = 0;
        bool leftAlign;

        if (leftAlign = alignment < 0)
            alignment = -alignment;

        switch (value)
        {
            case IUtf8SpanFormattable when encoder.IsUtf8:
                for (bufferSize = alignment; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
                {
                    var span = writer.GetSpan(bufferSize);
                    if (((IUtf8SpanFormattable)value).TryFormat(span, out bytesWritten, format, provider))
                    {
                        bytesWritten = Align(span, WhitespaceUtf8, alignment, bytesWritten, leftAlign);
                        writer.Advance(bytesWritten);
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

                    if (((ISpanFormattable)value).TryFormat(span, out bytesWritten, format, provider))
                    {
                        bytesWritten = AppendFormatted(writer,
                            encoder,
                            span.Slice(0, Align(span, WhitespaceUtf16, alignment, bytesWritten, leftAlign)));
                        break;
                    }
                }

                break;
            case IFormattable:
                bytesWritten = AppendFormatted(writer, encoder, buffer, ((IFormattable)value).ToString(format, provider).AsSpan(), alignment, leftAlign);
                break;
            case not null:
                bytesWritten = AppendFormatted(writer, encoder, buffer, ToStringNonBoxing(value), alignment, leftAlign);
                break;
        }

        return bytesWritten;
    }

    protected static int EstimateUtf8BufferSize(int literalLength, int formattedCount)
    {
        const long maxBytesPerChar = 4;
        var length = literalLength * maxBytesPerChar + formattedCount * CharsPerPlaceholder * maxBytesPerChar;
        return int.CreateSaturating(length);
    }
}