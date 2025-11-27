using System.Buffers;
using System.Diagnostics;

namespace DotNext.Buffers;

partial class CharBuffer
{
    private const int MaxBufferSize = int.MaxValue / 2;
    private const char Whitespace = ' ';
    
    internal static int AppendFormatted<T, TWriter>(TWriter buffer, T value, string? format, IFormatProvider? provider)
        where TWriter : struct, IBufferWriter<char>, allows ref struct
    {
        int charsWritten;

        switch (value)
        {
            case ISpanFormattable:
                Span<char> span = buffer.GetSpan();

                // constrained call avoiding boxing for value types
                for (int sizeHint; !((ISpanFormattable)value).TryFormat(span, out charsWritten, format, provider); span = buffer.GetSpan(sizeHint))
                {
                    sizeHint = span.Length;
                    sizeHint = sizeHint <= MaxBufferSize ? sizeHint << 1 : throw new InsufficientMemoryException();
                }

                buffer.Advance(charsWritten);
                break;
            case IFormattable:
                // constrained call avoiding boxing for value types
                charsWritten = Write(buffer, ((IFormattable)value).ToString(format, provider));

                break;
            case not null:
                charsWritten = Write(buffer, value.ToString());
                break;
            default:
                charsWritten = 0;
                break;
        }

        return charsWritten;

        static int Write(TWriter writer, scoped ReadOnlySpan<char> chars)
        {
            Memory.Write(writer, chars);
            return chars.Length;
        }
    }

    internal static int AppendFormatted<TWriter>(TWriter writer, scoped ReadOnlySpan<char> value, int alignment)
        where TWriter : struct, IBufferWriter<char>, allows ref struct
    {
        bool leftAlign;

        if (leftAlign = alignment < 0)
            alignment = -alignment;

        return AppendFormatted(writer, value, alignment, leftAlign);
    }

    private static int AppendFormatted<TWriter>(TWriter writer, scoped ReadOnlySpan<char> value, int alignment, bool leftAlign)
        where TWriter : struct, IBufferWriter<char>, allows ref struct
    {
        Debug.Assert(alignment >= 0);

        var padding = alignment - value.Length;
        int writtenCount;
        if (padding <= 0)
        {
            Memory.Write(writer, value);
            writtenCount = value.Length;
        }
        else
        {
            var span = writer.GetSpan(alignment);
            var filler = leftAlign
                ? span.Slice(value.Length, padding)
                : span.TrimLength(padding, out span);

            filler.Fill(Whitespace);
            value.CopyTo(span);

            writer.Advance(alignment);
            writtenCount = alignment;
        }

        return writtenCount;
    }
    
    internal static int AppendFormatted<T, TWriter>(TWriter writer, T value, int alignment, string? format, IFormatProvider? provider)
        where TWriter : struct, IBufferWriter<char>, allows ref struct
    {
        bool leftAlign;

        if (leftAlign = alignment < 0)
            alignment = -alignment;

        var writtenCount = 0;
        switch (value)
        {
            case ISpanFormattable:
                for (int bufferSize = alignment; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
                {
                    Span<char> span = writer.GetSpan(bufferSize), filler;
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

                        writer.Advance(alignment);
                        writtenCount += alignment;
                        break;
                    }
                }

                break;
            case IFormattable:
                writtenCount = AppendFormatted(writer, ((IFormattable)value).ToString(format, provider), alignment, leftAlign);
                break;
            case not null:
                writtenCount = AppendFormatted(writer, value.ToString(), alignment, leftAlign);
                break;
        }

        return writtenCount;
    }
}