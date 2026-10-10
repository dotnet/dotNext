using System.Buffers;
using System.Diagnostics;

namespace DotNext.Text;

using Buffers;

partial interface IInterpolatedStringHandler
{
    private const char WhitespaceUtf16 = ' ';
    
    internal static int AppendFormatted<T, TWriter>(TWriter writer, T value, string? format, IFormatProvider? provider)
        where TWriter : IBufferWriter<char>, allows ref struct
    {
        int charsWritten;

        switch (value)
        {
            case Enum:
                charsWritten = UnsafeWriteEnum(value, writer, format);
                break;
            case ISpanFormattable:
                var span = writer.GetSpan();

                // constrained call avoiding boxing for value types
                for (int sizeHint; !((ISpanFormattable)value).TryFormat(span, out charsWritten, format, provider); span = writer.GetSpan(sizeHint))
                {
                    sizeHint = span.Length;
                    sizeHint = sizeHint <= MaxBufferSize ? sizeHint << 1 : throw new InsufficientMemoryException();
                }

                writer.Advance(charsWritten);
                break;
            case IFormattable:
                // constrained call avoiding boxing for value types
                charsWritten = Write(writer, ((IFormattable)value).ToString(format, provider));
                break;
            case not null:
                charsWritten = Write(writer, ToStringNonBoxing(value));
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

    protected static int AppendFormatted<TWriter>(TWriter writer, scoped ReadOnlySpan<char> value, int alignment)
        where TWriter : IBufferWriter<char>, allows ref struct
    {
        bool leftAlign;

        if (leftAlign = alignment < 0)
            alignment = -alignment;

        return AppendFormatted(writer, value, alignment, leftAlign);
    }

    private static int AppendFormatted<TWriter>(TWriter writer, scoped ReadOnlySpan<char> value, int alignment, bool leftAlign)
        where TWriter : IBufferWriter<char>, allows ref struct
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

            filler.Fill(WhitespaceUtf16);
            value.CopyTo(span);

            writer.Advance(alignment);
            writtenCount = alignment;
        }

        return writtenCount;
    }
    
    internal static int AppendFormatted<T, TWriter>(TWriter writer, T value, int alignment, string? format, IFormatProvider? provider)
        where TWriter : IBufferWriter<char>, allows ref struct
    {
        bool leftAlign;

        if (leftAlign = alignment < 0)
            alignment = -alignment;

        var writtenCount = 0;
        switch (value)
        {
            case Enum:
                writtenCount = UnsafeWriteEnum(value, writer, format, alignment, leftAlign);
                break;
            case ISpanFormattable:
                for (var bufferSize = alignment; ; bufferSize = bufferSize <= MaxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
                {
                    var span = writer.GetSpan(bufferSize);
                    if (((ISpanFormattable)value).TryFormat(span, out var charsWritten, format, provider))
                    {
                        alignment = Align(span, WhitespaceUtf16, alignment, charsWritten, leftAlign);
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
                writtenCount = AppendFormatted(writer, ToStringNonBoxing(value), alignment, leftAlign);
                break;
        }

        return writtenCount;
    }

    protected static int EstimateUtf16BufferSize(int literalLength, int formattedCount)
    {
        var length = literalLength + formattedCount * CharsPerPlaceholder;
        return int.CreateSaturating(length);
    }
}