using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Text;

using Buffers;

partial interface IInterpolatedStringHandler
{
    private static int UnsafeWriteEnum<T, TWriter>(T value, TWriter writer, ReadOnlySpan<char> format = default)
        where TWriter : IBufferWriter<char>, allows ref struct
    {
        var span = writer.GetSpan();
        int charsWritten;

        for (int sizeHint;
             !UnsafeTryFormatEnum(value, span, out charsWritten, format);
             span = writer.GetSpan(sizeHint))
        {
            sizeHint = span.Length;
            sizeHint = sizeHint <= MaxBufferSize ? sizeHint << 1 : throw new InsufficientMemoryException();
        }

        writer.Advance(charsWritten);
        return charsWritten;
    }

    private static int UnsafeWriteEnum<T, TWriter>(T value, TWriter writer, ReadOnlySpan<char> format, int alignment, bool leftAlign)
        where TWriter : IBufferWriter<char>, allows ref struct
    {
        for (var bufferSize = alignment;;
             bufferSize = bufferSize <= MaxBufferSize ? bufferSize << 1 : throw new InsufficientMemoryException())
        {
            var span = writer.GetSpan(bufferSize);
            if (UnsafeTryFormatEnum(value, span, out var charsWritten, format))
            {
                alignment = Align(span, WhitespaceUtf16, alignment, charsWritten, leftAlign);
                writer.Advance(alignment);
                return alignment;
            }
        }
    }

    private static bool UnsafeTryFormatEnum<TEnum>(TEnum value,
        Span<char> destination,
        out int charsWritten,
        [StringSyntax(StringSyntaxAttribute.EnumFormat)]
        ReadOnlySpan<char> format = default)
    {
        Debug.Assert(typeof(TEnum).IsEnum);
        return TryFormatUnconstrained(declaringType: null, value, destination, out charsWritten, format);
        
        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = nameof(TryFormatUnconstrained))]
        static extern bool TryFormatUnconstrained(
            Enum? declaringType,
            TEnum value,
            Span<char> destination,
            out int charsWritten,
            [StringSyntax(StringSyntaxAttribute.EnumFormat)]
            ReadOnlySpan<char> format);
    }
    
    private static string UnsafeEnumToString<T>(T value)
    {
        Debug.Assert(value is Enum);

        const int initialBufferSize = 32;
        return UnsafeEnumToStringOnStack(value) ?? UnsafeEnumToStringExtraSpace(value);
        
        static string UnsafeEnumToStringExtraSpace(T value)
        {
            var writer = new BufferWriterSlim<char>(initialBufferSize << 1, MemoryAllocator<char>.Default);
            try
            {
                UnsafeWriteEnum(value, new BufferWriterSlim<char>.Ref(ref writer));
                return new(writer.WrittenSpan);
            }
            finally
            {
                writer.Dispose();
            }
        }

        static string? UnsafeEnumToStringOnStack(T value)
        {
            Span<char> buffer = stackalloc char[initialBufferSize];
            return UnsafeTryFormatEnum(value, buffer, out var charsWritten)
                ? new(buffer.Slice(0, charsWritten))
                : null;
        }
    }
}