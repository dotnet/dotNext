using System.Runtime.InteropServices;

namespace DotNext;

[StructLayout(LayoutKind.Auto)]
internal struct NullableFormattable<T>(in T? value) : ISpanFormattable
    where T : struct, ISpanFormattable
{
    private readonly bool hasValue = value.HasValue;
    private T value = value.GetValueOrDefault();
    
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider)
        => hasValue ? value.ToString(format, formatProvider) : string.Empty;

    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (hasValue)
            return value.TryFormat(destination, out charsWritten, format, provider);

        charsWritten = 0;
        return true;
    }

    public static implicit operator NullableFormattable<T>(in T? value) => new(value);
}