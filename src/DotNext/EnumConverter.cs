using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext;

using static Runtime.Intrinsics;

/// <summary>
/// Provides conversion between enum value and primitive types.
/// </summary>
public static class EnumConverter
{
    // Instead of storing pointer to the conversion function, we store the underlying type of the enum.
    // As a result, we can use switch expression and utilize full power of JIT that will eliminate
    // branches of switch expression and replaces the method body with appropriate conversion code
    private static class EnumTypeCode<TEnum>
        where TEnum : struct, Enum
    {
        internal static readonly TypeCode Value = Type.GetTypeCode(typeof(TEnum));
    }

    /// <summary>
    /// Gets underlying type of the enum.
    /// </summary>
    /// <remarks>
    /// The call to this method can be effectively replaced with a constant by JIT.
    /// </remarks>
    /// <typeparam name="TEnum">The type of the enum.</typeparam>
    /// <returns>The underlying type of <typeparamref name="TEnum"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeCode GetTypeCode<TEnum>()
        where TEnum : struct, Enum => EnumTypeCode<TEnum>.Value;

    /// <summary>
    /// Converts a value of type <typeparamref name="TValue"/> to enum of type <typeparamref name="TEnum"/>.
    /// </summary>
    /// <typeparam name="TEnum">The type of the enum.</typeparam>
    /// <typeparam name="TValue">The numeric type representing enum value.</typeparam>
    /// <param name="value">The value to be converted.</param>
    /// <returns>The enum value that is equivalent to <typeparamref name="TEnum"/>.</returns>
    [CLSCompliant(false)]
    public static TEnum ToEnum<TEnum, TValue>(TValue value)
        where TValue : unmanaged, INumberBase<TValue>, IConvertible
        where TEnum : struct, Enum
    {
        if (AreCompatible<TEnum, TValue>())
            return Unsafe.BitCast<TValue, TEnum>(value);

        return GetTypeCode<TEnum>() switch
        {
            TypeCode.Byte => Convert<byte>(value),
            TypeCode.SByte => Convert<sbyte>(value),
            TypeCode.Int16 => Convert<short>(value),
            TypeCode.UInt16 => Convert<ushort>(value),
            TypeCode.Int32 => Convert<int>(value),
            TypeCode.UInt32 => Convert<uint>(value),
            TypeCode.Int64 => Convert<long>(value),
            TypeCode.UInt64 => Convert<ulong>(value),
            _ => BasicExtensions.ChangeType<TValue, TEnum>(value),
        };

        static TEnum Convert<TOther>(TValue value)
            where TOther : unmanaged, INumberBase<TOther>
            => Unsafe.BitCast<TOther, TEnum>(TOther.CreateChecked(value));
    }

    /// <summary>
    /// Converts enum value to numeric value.
    /// </summary>
    /// <typeparam name="TEnum">The type of the enum.</typeparam>
    /// <typeparam name="TValue">The type of numeric value.</typeparam>
    /// <param name="value">The enum value to convert.</param>
    /// <returns>The numeric equivalent of <paramref name="value"/>.</returns>
    [CLSCompliant(false)]
    public static TValue FromEnum<TEnum, TValue>(TEnum value)
        where TValue : unmanaged, INumberBase<TValue>, IConvertible
        where TEnum : struct, Enum
    {
        if (AreCompatible<TEnum, TValue>())
            return Unsafe.BitCast<TEnum, TValue>(value);

        return GetTypeCode<TEnum>() switch
        {
            TypeCode.Byte => Convert<byte>(value),
            TypeCode.SByte => Convert<sbyte>(value),
            TypeCode.Int16 => Convert<short>(value),
            TypeCode.UInt16 => Convert<ushort>(value),
            TypeCode.Int32 => Convert<int>(value),
            TypeCode.UInt32 => Convert<uint>(value),
            TypeCode.Int64 => Convert<long>(value),
            TypeCode.UInt64 => Convert<ulong>(value),
            _ => BasicExtensions.ChangeType<TEnum, TValue>(value),
        };

        static TValue Convert<TOther>(TEnum value)
            where TOther : unmanaged, INumberBase<TOther>
            => TValue.CreateChecked(Unsafe.BitCast<TEnum, TOther>(value));
    }
}