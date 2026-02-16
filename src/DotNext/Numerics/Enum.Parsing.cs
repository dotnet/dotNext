using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext.Numerics;

partial struct Enum<T>
{
    /// <inheritdoc/>
    static Enum<T> IParsable<Enum<T>>.Parse(string s, IFormatProvider? provider)
        => Parse(s, provider);

    /// <inheritdoc/>
    static bool IParsable<Enum<T>>.TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Enum<T> result)
        => TryParse(s, provider, out result);

    /// <inheritdoc/>
    public static Enum<T> Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(s, provider),
            TypeCode.SByte => ConstrainedCall<sbyte>(s, provider),
            TypeCode.UInt16 => ConstrainedCall<ushort>(s, provider),
            TypeCode.Int16 => ConstrainedCall<short>(s, provider),
            TypeCode.UInt32 => ConstrainedCall<uint>(s, provider),
            TypeCode.Int32 => ConstrainedCall<int>(s, provider),
            TypeCode.UInt64 => ConstrainedCall<ulong>(s, provider),
            TypeCode.Int64 => ConstrainedCall<long>(s, provider),
            _ => default,
        };
        
        static Enum<T> ConstrainedCall<TValue>(ReadOnlySpan<char> s, IFormatProvider? provider)
            where TValue : unmanaged, ISpanParsable<TValue>
        {
            AssertUnderlyingType<TValue>();

            return Unsafe.BitCast<TValue, Enum<T>>(TValue.Parse(s, provider));
        }
    }

    /// <inheritdoc/>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Enum<T> result)
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(s, provider, out result),
            TypeCode.SByte => ConstrainedCall<sbyte>(s, provider, out result),
            TypeCode.UInt16 => ConstrainedCall<ushort>(s, provider, out result),
            TypeCode.Int16 => ConstrainedCall<short>(s, provider, out result),
            TypeCode.UInt32 => ConstrainedCall<uint>(s, provider, out result),
            TypeCode.Int32 => ConstrainedCall<int>(s, provider, out result),
            TypeCode.UInt64 => ConstrainedCall<ulong>(s, provider, out result),
            TypeCode.Int64 => ConstrainedCall<long>(s, provider, out result),
            _ => EnumHelpers.Fail(out result),
        };
        
        static bool ConstrainedCall<TValue>(ReadOnlySpan<char> s, IFormatProvider? provider, out Enum<T> result)
            where TValue : unmanaged, ISpanParsable<TValue>
        {
            AssertUnderlyingType<TValue>();
            
            Unsafe.SkipInit(out result);
            return TValue.TryParse(s, provider, out Unsafe.As<Enum<T>, TValue>(ref result));
        }
    }

    /// <inheritdoc/>
    public static Enum<T> Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(utf8Text, provider),
            TypeCode.SByte => ConstrainedCall<sbyte>(utf8Text, provider),
            TypeCode.UInt16 => ConstrainedCall<ushort>(utf8Text, provider),
            TypeCode.Int16 => ConstrainedCall<short>(utf8Text, provider),
            TypeCode.UInt32 => ConstrainedCall<uint>(utf8Text, provider),
            TypeCode.Int32 => ConstrainedCall<int>(utf8Text, provider),
            TypeCode.UInt64 => ConstrainedCall<ulong>(utf8Text, provider),
            TypeCode.Int64 => ConstrainedCall<long>(utf8Text, provider),
            _ => default,
        };

        static Enum<T> ConstrainedCall<TValue>(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider)
            where TValue : unmanaged, IUtf8SpanParsable<TValue>
        {
            AssertUnderlyingType<TValue>();

            return Unsafe.BitCast<TValue, Enum<T>>(TValue.Parse(utf8Text, provider));
        }
    }
    
    /// <inheritdoc/>
    public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Enum<T> result)
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(utf8Text, provider, out result),
            TypeCode.SByte => ConstrainedCall<sbyte>(utf8Text, provider, out result),
            TypeCode.UInt16 => ConstrainedCall<ushort>(utf8Text, provider, out result),
            TypeCode.Int16 => ConstrainedCall<short>(utf8Text, provider, out result),
            TypeCode.UInt32 => ConstrainedCall<uint>(utf8Text, provider, out result),
            TypeCode.Int32 => ConstrainedCall<int>(utf8Text, provider, out result),
            TypeCode.UInt64 => ConstrainedCall<ulong>(utf8Text, provider, out result),
            TypeCode.Int64 => ConstrainedCall<long>(utf8Text, provider, out result),
            _ => EnumHelpers.Fail(out result),
        };
        
        static bool ConstrainedCall<TValue>(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Enum<T> result)
            where TValue : unmanaged, IUtf8SpanParsable<TValue>
        {
            AssertUnderlyingType<TValue>();
            
            Unsafe.SkipInit(out result);
            return TValue.TryParse(utf8Text, provider, out Unsafe.As<Enum<T>, TValue>(ref result));
        }
    }

    /// <inheritdoc/>
    public static bool TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out Enum<T> result)
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(s, style, provider, out result),
            TypeCode.SByte => ConstrainedCall<sbyte>(s, style, provider, out result),
            TypeCode.UInt16 => ConstrainedCall<ushort>(s, style, provider, out result),
            TypeCode.Int16 => ConstrainedCall<short>(s, style, provider, out result),
            TypeCode.UInt32 => ConstrainedCall<uint>(s, style, provider, out result),
            TypeCode.Int32 => ConstrainedCall<int>(s, style, provider, out result),
            TypeCode.UInt64 => ConstrainedCall<ulong>(s, style, provider, out result),
            TypeCode.Int64 => ConstrainedCall<long>(s, style, provider, out result),
            _ => EnumHelpers.Fail(out result),
        };

        static bool ConstrainedCall<TValue>(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out Enum<T> result)
            where TValue : unmanaged, INumberBase<TValue>
        {
            AssertUnderlyingType<TValue>();

            Unsafe.SkipInit(out result);
            return TValue.TryParse(s, style, provider, out Unsafe.As<Enum<T>, TValue>(ref result));
        }
    }

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.TryParse([NotNullWhen(true)] string? s, NumberStyles style, IFormatProvider? provider, out Enum<T> result)
        => TryParse(s, style, provider, out result);

    /// <inheritdoc/>
    static Enum<T> INumberBase<Enum<T>>.Parse(string s, NumberStyles style, IFormatProvider? provider)
        => Parse(s, style, provider);

    /// <inheritdoc/>
    public static Enum<T> Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider)
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(s, style, provider),
            TypeCode.SByte => ConstrainedCall<sbyte>(s, style, provider),
            TypeCode.UInt16 => ConstrainedCall<ushort>(s, style, provider),
            TypeCode.Int16 => ConstrainedCall<short>(s, style, provider),
            TypeCode.UInt32 => ConstrainedCall<uint>(s, style, provider),
            TypeCode.Int32 => ConstrainedCall<int>(s, style, provider),
            TypeCode.UInt64 => ConstrainedCall<ulong>(s, style, provider),
            TypeCode.Int64 => ConstrainedCall<long>(s, style, provider),
            _ => default,
        };
        
        static Enum<T> ConstrainedCall<TValue>(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider)
            where TValue : unmanaged, INumberBase<TValue>
        {
            AssertUnderlyingType<TValue>();

            return Unsafe.BitCast<TValue, Enum<T>>(TValue.Parse(s, style, provider));
        }
    }
}