using System.Runtime.CompilerServices;

namespace DotNext;

partial struct Enum<T>
{
    /// <inheritdoc/>
    string IFormattable.ToString(string? format, IFormatProvider? formatProvider)
        => value.ToString(format);

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value, destination, out charsWritten, format, provider),
            TypeCode.SByte => ConstrainedCall<sbyte>(value, destination, out charsWritten, format, provider),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value, destination, out charsWritten, format, provider),
            TypeCode.Int16 => ConstrainedCall<short>(value, destination, out charsWritten, format, provider),
            TypeCode.UInt32 => ConstrainedCall<uint>(value, destination, out charsWritten, format, provider),
            TypeCode.Int32 => ConstrainedCall<int>(value, destination, out charsWritten, format, provider),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value, destination, out charsWritten, format, provider),
            TypeCode.Int64 => ConstrainedCall<long>(value, destination, out charsWritten, format, provider),
            _ => EnumHelpers.Fail(out charsWritten)
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ConstrainedCall<TValue>(T value, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format,
            IFormatProvider? provider)
            where TValue : unmanaged, ISpanFormattable
        {
            AssertUnderlyingType<TValue>();
            
            return Unsafe.BitCast<T, TValue>(value).TryFormat(destination, out charsWritten, format, provider);
        }
    }
}