using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext;

partial struct Enum<T>
{
    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.TryConvertFromChecked<TOther>(TOther value, out Enum<T> result)
        => TryConvertFrom<TOther, CheckedConversion<TOther>>(value, out result);

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.TryConvertFromSaturating<TOther>(TOther value, out Enum<T> result)
        => TryConvertFrom<TOther, SaturatingConversion<TOther>>(value, out result);

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.TryConvertFromTruncating<TOther>(TOther value, out Enum<T> result)
        => TryConvertFrom<TOther, TruncatingConversion<TOther>>(value, out result);

    private static bool TryConvertFrom<TOther, TConverter>(TOther value, out Enum<T> result)
        where TOther : INumberBase<TOther>
        where TConverter : EnumHelpers.IConverter<TOther>, allows ref struct
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value, out result),
            TypeCode.SByte => ConstrainedCall<sbyte>(value, out result),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value, out result),
            TypeCode.Int16 => ConstrainedCall<short>(value, out result),
            TypeCode.UInt32 => ConstrainedCall<uint>(value, out result),
            TypeCode.Int32 => ConstrainedCall<int>(value, out result),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value, out result),
            TypeCode.Int64 => ConstrainedCall<long>(value, out result),
            _ => EnumHelpers.Fail(out result),
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ConstrainedCall<TValue>(TOther value, out Enum<T> result)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            Unsafe.SkipInit(out result);
            return TConverter.TryConvertFrom(value, out Unsafe.As<Enum<T>, TValue>(ref result));
        }
    }

    /// <summary>
    /// Converts the underlying value to the specified type, or throws if the conversion is not available.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <returns>The conversion result.</returns>
    /// <exception cref="OverflowException">The underlying value cannot be converted to <typeparamref name="TResult"/> without overflow.</exception>
    public TResult ConvertChecked<TResult>()
        where TResult : INumberBase<TResult>
        => ConvertTo<TResult, CheckedConversion<TResult>>();

    /// <summary>
    /// Converts the underlying value to the specified type, or returns the maximum/minimum possible value
    /// for <typeparamref name="TResult"/> if it overflows.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <returns>The conversion result.</returns>
    public TResult ConvertSaturating<TResult>()
        where TResult : INumberBase<TResult>
        => ConvertTo<TResult, SaturatingConversion<TResult>>();

    /// <summary>
    /// Converts the underlying value to the specified type, or returns the truncated value
    /// that fits to <typeparamref name="TResult"/> type.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <returns>The conversion result.</returns>
    public TResult ConvertTruncating<TResult>()
        where TResult : INumberBase<TResult>
        => ConvertTo<TResult, TruncatingConversion<TResult>>();

    private TResult ConvertTo<TResult, TConverter>()
        where TResult : INumberBase<TResult>
        where TConverter : EnumHelpers.IConverter<TResult>, allows ref struct
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value),
            TypeCode.SByte => ConstrainedCall<sbyte>(value),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value),
            TypeCode.Int16 => ConstrainedCall<short>(value),
            TypeCode.UInt32 => ConstrainedCall<uint>(value),
            TypeCode.Int32 => ConstrainedCall<int>(value),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value),
            TypeCode.Int64 => ConstrainedCall<long>(value),
            _ => TResult.Zero,
        };
        
        static TResult ConstrainedCall<TValue>(T value)
            where TValue : unmanaged, INumberBase<TValue>
        {
            AssertUnderlyingType<TValue>();

            return TConverter.ConvertTo(Unsafe.BitCast<T, TValue>(value));
        }
    }

    /// <inheritdoc/>
    public static Enum<T> CreateChecked<TOther>(TOther value)
        where TOther : INumberBase<TOther>
        => ConvertFrom<TOther, CheckedConversion<TOther>>(value);

    /// <inheritdoc/>
    public static Enum<T> CreateTruncating<TOther>(TOther value)
        where TOther : INumberBase<TOther>
        => ConvertFrom<TOther, TruncatingConversion<TOther>>(value);

    /// <inheritdoc/>
    public static Enum<T> CreateSaturating<TOther>(TOther value)
        where TOther : INumberBase<TOther>
        => ConvertFrom<TOther, SaturatingConversion<TOther>>(value);

    private static Enum<T> ConvertFrom<TOther, TConverter>(TOther value)
        where TOther : INumberBase<TOther>
        where TConverter : EnumHelpers.IConverter<TOther>, allows ref struct
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value),
            TypeCode.SByte => ConstrainedCall<sbyte>(value),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value),
            TypeCode.Int16 => ConstrainedCall<short>(value),
            TypeCode.UInt32 => ConstrainedCall<uint>(value),
            TypeCode.Int32 => ConstrainedCall<int>(value),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value),
            TypeCode.Int64 => ConstrainedCall<long>(value),
            _ => default,
        };

        static Enum<T> ConstrainedCall<TValue>(TOther value)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            return Unsafe.BitCast<TValue, Enum<T>>(TConverter.ConvertFrom<TValue>(value));
        }
    }

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.TryConvertToChecked<TOther>(Enum<T> value, [MaybeNullWhen(false)] out TOther result)
        => value.TryConvertTo<TOther, CheckedConversion<TOther>>(out result);

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.TryConvertToSaturating<TOther>(Enum<T> value, [MaybeNullWhen(false)] out TOther result)
        => value.TryConvertTo<TOther, SaturatingConversion<TOther>>(out result);

    /// <inheritdoc/>
    static bool INumberBase<Enum<T>>.TryConvertToTruncating<TOther>(Enum<T> value, [MaybeNullWhen(false)] out TOther result)
        => value.TryConvertTo<TOther, TruncatingConversion<TOther>>(out result);

    private bool TryConvertTo<TOther, TConverter>([MaybeNullWhen(false)] out TOther result)
        where TOther : INumberBase<TOther>
        where TConverter : EnumHelpers.IConverter<TOther>, allows ref struct
    {
        return UnderlyingType switch
        {
            TypeCode.Byte => ConstrainedCall<byte>(value, out result),
            TypeCode.SByte => ConstrainedCall<sbyte>(value, out result),
            TypeCode.UInt16 => ConstrainedCall<ushort>(value, out result),
            TypeCode.Int16 => ConstrainedCall<short>(value, out result),
            TypeCode.UInt32 => ConstrainedCall<uint>(value, out result),
            TypeCode.Int32 => ConstrainedCall<int>(value, out result),
            TypeCode.UInt64 => ConstrainedCall<ulong>(value, out result),
            TypeCode.Int64 => ConstrainedCall<long>(value, out result),
            _ => EnumHelpers.Fail(out result),
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ConstrainedCall<TValue>(T value, [MaybeNullWhen(false)] out TOther result)
            where TValue : unmanaged, IBinaryInteger<TValue>
        {
            AssertUnderlyingType<TValue>();

            return TConverter.TryConvertTo(Unsafe.BitCast<T, TValue>(value), out result);
        }
    }

    private readonly ref struct SaturatingConversion<TOther> : EnumHelpers.IConverter<TOther>
        where TOther : INumberBase<TOther>
    {
        static bool EnumHelpers.IConverter<TOther>.TryConvertFrom<TResult>(TOther value, out TResult result)
            => TResult.TryConvertFromSaturating(value, out result);

        static TResult EnumHelpers.IConverter<TOther>.ConvertFrom<TResult>(TOther value)
            => TResult.CreateSaturating(value);

        static bool EnumHelpers.IConverter<TOther>.TryConvertTo<TValue>(TValue value, [MaybeNullWhen(false)] out TOther result)
            => TValue.TryConvertToSaturating(value, out result);

        static TOther EnumHelpers.IConverter<TOther>.ConvertTo<TValue>(TValue value)
            => TOther.CreateSaturating(value);
    }
    
    private readonly ref struct CheckedConversion<TOther> : EnumHelpers.IConverter<TOther>
        where TOther : INumberBase<TOther>
    {
        static bool EnumHelpers.IConverter<TOther>.TryConvertFrom<TResult>(TOther value, out TResult result)
            => TResult.TryConvertFromChecked(value, out result);
        
        static TResult EnumHelpers.IConverter<TOther>.ConvertFrom<TResult>(TOther value)
            => TResult.CreateChecked(value);
        
        static bool EnumHelpers.IConverter<TOther>.TryConvertTo<TValue>(TValue value, [MaybeNullWhen(false)] out TOther result)
            => TValue.TryConvertToChecked(value, out result);
        
        static TOther EnumHelpers.IConverter<TOther>.ConvertTo<TValue>(TValue value)
            => TOther.CreateChecked(value);
    }
    
    private readonly ref struct TruncatingConversion<TOther> : EnumHelpers.IConverter<TOther>
        where TOther : INumberBase<TOther>
    {
        static bool EnumHelpers.IConverter<TOther>.TryConvertFrom<TResult>(TOther value, out TResult result)
            => TResult.TryConvertFromTruncating(value, out result);
        
        static TResult EnumHelpers.IConverter<TOther>.ConvertFrom<TResult>(TOther value)
            => TResult.CreateTruncating(value);
        
        static bool EnumHelpers.IConverter<TOther>.TryConvertTo<TValue>(TValue value, [MaybeNullWhen(false)] out TOther result)
            => TValue.TryConvertToTruncating(value, out result);
        
        static TOther EnumHelpers.IConverter<TOther>.ConvertTo<TValue>(TValue value)
            => TOther.CreateTruncating(value);
    }
}

partial class EnumHelpers
{
    internal interface IConverter<TOther>
        where TOther : INumberBase<TOther>
    {
        static abstract bool TryConvertFrom<TResult>(TOther value, out TResult result)
            where TResult : unmanaged, INumberBase<TResult>;

        static abstract TResult ConvertFrom<TResult>(TOther value)
            where TResult : unmanaged, INumberBase<TResult>;

        static abstract bool TryConvertTo<TValue>(TValue value, [MaybeNullWhen(false)] out TOther result)
            where TValue : INumberBase<TValue>;

        static abstract TOther ConvertTo<TValue>(TValue value)
            where TValue : unmanaged, INumberBase<TValue>;
    }
}