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
    /// Converts <see cref="long"/> into enum of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target enum type.</typeparam>
    /// <param name="value">The value to be converted.</param>
    /// <returns>Enum value equals to the given <see cref="long"/> value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToEnum<T>(this long value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => default(T),
        TypeCode.Char => ReinterpretCast<char, T>(Convert.ToChar(value)),
        TypeCode.Decimal => ReinterpretCast<decimal, T>(value),
        TypeCode.Boolean => ReinterpretCast<bool, T>(Convert.ToBoolean(value)),
        TypeCode.Byte => ReinterpretCast<byte, T>(Convert.ToByte(value)),
        TypeCode.SByte => ReinterpretCast<sbyte, T>(Convert.ToSByte(value)),
        TypeCode.Int16 => ReinterpretCast<short, T>(Convert.ToInt16(value)),
        TypeCode.UInt16 => ReinterpretCast<ushort, T>(Convert.ToUInt16(value)),
        TypeCode.Int32 => ReinterpretCast<int, T>(Convert.ToInt32(value)),
        TypeCode.UInt32 => ReinterpretCast<uint, T>(Convert.ToUInt32(value)),
        TypeCode.Int64 => ReinterpretCast<long, T>(value),
        TypeCode.UInt64 => ReinterpretCast<ulong, T>(Convert.ToUInt64(value)),
        TypeCode.Single => ReinterpretCast<float, T>(value),
        TypeCode.Double => ReinterpretCast<double, T>(value),
        _ => value.ChangeType<long, T>(),
    };

    /// <summary>
    /// Converts <see cref="int"/> into enum of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target enum type.</typeparam>
    /// <param name="value">The value to be converted.</param>
    /// <returns>Enum value equals to the given <see cref="int"/> value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToEnum<T>(this int value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => default(T),
        TypeCode.Char => ReinterpretCast<char, T>(Convert.ToChar(value)),
        TypeCode.Decimal => ReinterpretCast<decimal, T>(value),
        TypeCode.Boolean => ReinterpretCast<bool, T>(value.ToBoolean()),
        TypeCode.Byte => ReinterpretCast<byte, T>(Convert.ToByte(value)),
        TypeCode.SByte => ReinterpretCast<sbyte, T>(Convert.ToSByte(value)),
        TypeCode.Int16 => ReinterpretCast<short, T>(Convert.ToInt16(value)),
        TypeCode.UInt16 => ReinterpretCast<ushort, T>(Convert.ToUInt16(value)),
        TypeCode.Int32 => ReinterpretCast<int, T>(value),
        TypeCode.UInt32 => ReinterpretCast<uint, T>(Convert.ToUInt32(value)),
        TypeCode.Int64 => ReinterpretCast<long, T>(value),
        TypeCode.UInt64 => ReinterpretCast<ulong, T>(Convert.ToUInt64(value)),
        TypeCode.Single => ReinterpretCast<float, T>(value),
        TypeCode.Double => ReinterpretCast<double, T>(value),
        _ => value.ChangeType<int, T>(),
    };

    /// <summary>
    /// Converts <see cref="short"/> into enum of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target enum type.</typeparam>
    /// <param name="value">The value to be converted.</param>
    /// <returns>Enum value equals to the given <see cref="short"/> value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToEnum<T>(this short value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => default(T),
        TypeCode.Char => ReinterpretCast<char, T>(Convert.ToChar(value)),
        TypeCode.Decimal => ReinterpretCast<decimal, T>(value),
        TypeCode.Boolean => ReinterpretCast<bool, T>(ValueTypeExtensions.ToBoolean(value)),
        TypeCode.Byte => ReinterpretCast<byte, T>(Convert.ToByte(value)),
        TypeCode.SByte => ReinterpretCast<sbyte, T>(Convert.ToSByte(value)),
        TypeCode.Int16 => ReinterpretCast<short, T>(value),
        TypeCode.UInt16 => ReinterpretCast<ushort, T>(Convert.ToUInt16(value)),
        TypeCode.Int32 => ReinterpretCast<int, T>(value),
        TypeCode.UInt32 => ReinterpretCast<uint, T>(Convert.ToUInt32(value)),
        TypeCode.Int64 => ReinterpretCast<long, T>(value),
        TypeCode.UInt64 => ReinterpretCast<ulong, T>(Convert.ToUInt64(value)),
        TypeCode.Single => ReinterpretCast<float, T>(value),
        TypeCode.Double => ReinterpretCast<double, T>(value),
        _ => value.ChangeType<short, T>(),
    };

    /// <summary>
    /// Converts <see cref="byte"/> into enum of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target enum type.</typeparam>
    /// <param name="value">The value to be converted.</param>
    /// <returns>Enum value equals to the given <see cref="byte"/> value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ToEnum<T>(this byte value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => default(T),
        TypeCode.Char => ReinterpretCast<char, T>(Convert.ToChar(value)),
        TypeCode.Decimal => ReinterpretCast<decimal, T>(value),
        TypeCode.Boolean => ReinterpretCast<bool, T>(ValueTypeExtensions.ToBoolean(value)),
        TypeCode.Byte => ReinterpretCast<byte, T>(value),
        TypeCode.SByte => ReinterpretCast<sbyte, T>(Convert.ToSByte(value)),
        TypeCode.Int16 => ReinterpretCast<short, T>(value),
        TypeCode.UInt16 => ReinterpretCast<ushort, T>(value),
        TypeCode.Int32 => ReinterpretCast<int, T>(value),
        TypeCode.UInt32 => ReinterpretCast<uint, T>(value),
        TypeCode.Int64 => ReinterpretCast<long, T>(value),
        TypeCode.UInt64 => ReinterpretCast<ulong, T>(value),
        TypeCode.Single => ReinterpretCast<float, T>(value),
        TypeCode.Double => ReinterpretCast<double, T>(value),
        _ => value.ChangeType<byte, T>(),
    };

    /// <summary>
    /// Converts <see cref="byte"/> into enum of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target enum type.</typeparam>
    /// <param name="value">The value to be converted.</param>
    /// <returns>Enum value equals to the given <see cref="byte"/> value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static T ToEnum<T>(this sbyte value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => default(T),
        TypeCode.Char => ReinterpretCast<char, T>(Convert.ToChar(value)),
        TypeCode.Decimal => ReinterpretCast<decimal, T>(value),
        TypeCode.Boolean => ReinterpretCast<bool, T>(ValueTypeExtensions.ToBoolean(value)),
        TypeCode.Byte => ReinterpretCast<byte, T>(Convert.ToByte(value)),
        TypeCode.SByte => ReinterpretCast<sbyte, T>(value),
        TypeCode.Int16 => ReinterpretCast<short, T>(value),
        TypeCode.UInt16 => ReinterpretCast<ushort, T>(Convert.ToUInt16(value)),
        TypeCode.Int32 => ReinterpretCast<int, T>(value),
        TypeCode.UInt32 => ReinterpretCast<uint, T>(Convert.ToUInt32(value)),
        TypeCode.Int64 => ReinterpretCast<long, T>(value),
        TypeCode.UInt64 => ReinterpretCast<ulong, T>(Convert.ToUInt64(value)),
        TypeCode.Single => ReinterpretCast<float, T>(value),
        TypeCode.Double => ReinterpretCast<double, T>(value),
        _ => value.ChangeType<sbyte, T>(),
    };

    /// <summary>
    /// Converts <see cref="ushort"/> into enum of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target enum type.</typeparam>
    /// <param name="value">The value to be converted.</param>
    /// <returns>Enum value equals to the given <see cref="ushort"/> value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static T ToEnum<T>(this ushort value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => default(T),
        TypeCode.Char => ReinterpretCast<char, T>(Convert.ToChar(value)),
        TypeCode.Decimal => ReinterpretCast<decimal, T>(value),
        TypeCode.Boolean => ReinterpretCast<bool, T>(ValueTypeExtensions.ToBoolean(value)),
        TypeCode.Byte => ReinterpretCast<byte, T>(Convert.ToByte(value)),
        TypeCode.SByte => ReinterpretCast<sbyte, T>(Convert.ToSByte(value)),
        TypeCode.Int16 => ReinterpretCast<short, T>(Convert.ToInt16(value)),
        TypeCode.UInt16 => ReinterpretCast<ushort, T>(value),
        TypeCode.Int32 => ReinterpretCast<int, T>(value),
        TypeCode.UInt32 => ReinterpretCast<uint, T>(value),
        TypeCode.Int64 => ReinterpretCast<long, T>(value),
        TypeCode.UInt64 => ReinterpretCast<ulong, T>(value),
        TypeCode.Single => ReinterpretCast<float, T>(value),
        TypeCode.Double => ReinterpretCast<double, T>(value),
        _ => value.ChangeType<ushort, T>(),
    };

    /// <summary>
    /// Converts <see cref="uint"/> into enum of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target enum type.</typeparam>
    /// <param name="value">The value to be converted.</param>
    /// <returns>Enum value equals to the given <see cref="uint"/> value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static T ToEnum<T>(this uint value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => default(T),
        TypeCode.Char => ReinterpretCast<char, T>(Convert.ToChar(value)),
        TypeCode.Decimal => ReinterpretCast<decimal, T>(value),
        TypeCode.Boolean => ReinterpretCast<bool, T>(Convert.ToBoolean(value)),
        TypeCode.Byte => ReinterpretCast<byte, T>(Convert.ToByte(value)),
        TypeCode.SByte => ReinterpretCast<sbyte, T>(Convert.ToSByte(value)),
        TypeCode.Int16 => ReinterpretCast<short, T>(Convert.ToInt16(value)),
        TypeCode.UInt16 => ReinterpretCast<ushort, T>(Convert.ToUInt16(value)),
        TypeCode.Int32 => ReinterpretCast<int, T>(Convert.ToInt32(value)),
        TypeCode.UInt32 => ReinterpretCast<uint, T>(value),
        TypeCode.Int64 => ReinterpretCast<long, T>(value),
        TypeCode.UInt64 => ReinterpretCast<ulong, T>(value),
        TypeCode.Single => ReinterpretCast<float, T>(value),
        TypeCode.Double => ReinterpretCast<double, T>(value),
        _ => value.ChangeType<uint, T>(),
    };

    /// <summary>
    /// Converts <see cref="ulong"/> into enum of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target enum type.</typeparam>
    /// <param name="value">The value to be converted.</param>
    /// <returns>Enum value equals to the given <see cref="ulong"/> value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static T ToEnum<T>(this ulong value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => default(T),
        TypeCode.Char => ReinterpretCast<char, T>(Convert.ToChar(value)),
        TypeCode.Decimal => ReinterpretCast<decimal, T>(value),
        TypeCode.Boolean => ReinterpretCast<bool, T>(Convert.ToBoolean(value)),
        TypeCode.Byte => ReinterpretCast<byte, T>(Convert.ToByte(value)),
        TypeCode.SByte => ReinterpretCast<sbyte, T>(Convert.ToSByte(value)),
        TypeCode.Int16 => ReinterpretCast<short, T>(Convert.ToInt16(value)),
        TypeCode.UInt16 => ReinterpretCast<ushort, T>(Convert.ToUInt16(value)),
        TypeCode.Int32 => ReinterpretCast<int, T>(Convert.ToInt32(value)),
        TypeCode.UInt32 => ReinterpretCast<uint, T>(Convert.ToUInt32(value)),
        TypeCode.Int64 => ReinterpretCast<long, T>(Convert.ToInt64(value)),
        TypeCode.UInt64 => ReinterpretCast<ulong, T>(value),
        TypeCode.Single => ReinterpretCast<float, T>(value),
        TypeCode.Double => ReinterpretCast<double, T>(value),
        _ => value.ChangeType<ulong, T>(),
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    internal static T ToEnumUnchecked<T>(this ulong value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => default(T),
        TypeCode.Byte or TypeCode.SByte => ReinterpretCast<byte, T>(unchecked((byte)value)),
        TypeCode.Char or TypeCode.Int16 or TypeCode.UInt16 => ReinterpretCast<ushort, T>(unchecked((ushort)value)),
        TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Single => ReinterpretCast<uint, T>(unchecked((uint)value)),
        TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Double => ReinterpretCast<ulong, T>(value),
        _ => value.ChangeType<ulong, T>(),
    };

    /// <summary>
    /// Converts enum value into primitive type <see cref="long"/>.
    /// </summary>
    /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
    /// <param name="value">Enum value to be converted.</param>
    /// <returns>Enum value represented as <see cref="long"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToInt64<T>(this T value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => 0L,
        TypeCode.Char => ReinterpretCast<T, char>(value),
        TypeCode.Decimal => Convert.ToInt64(ReinterpretCast<T, decimal>(value)),
        TypeCode.Boolean => ReinterpretCast<T, bool>(value).ToInt32(),
        TypeCode.Byte => ReinterpretCast<T, byte>(value),
        TypeCode.SByte => ReinterpretCast<T, sbyte>(value),
        TypeCode.Int16 => ReinterpretCast<T, short>(value),
        TypeCode.UInt16 => ReinterpretCast<T, ushort>(value),
        TypeCode.Int32 => ReinterpretCast<T, int>(value),
        TypeCode.UInt32 => ReinterpretCast<T, uint>(value),
        TypeCode.Int64 => ReinterpretCast<T, long>(value),
        TypeCode.UInt64 => Convert.ToInt64(ReinterpretCast<T, ulong>(value)),
        TypeCode.Single => Convert.ToInt64(ReinterpretCast<T, float>(value)),
        TypeCode.Double => Convert.ToInt64(ReinterpretCast<T, double>(value)),
        _ => value.ChangeType<T, long>(),
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong ToUInt64Unchecked<T>(this T value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => 0UL,
        TypeCode.Boolean => ReinterpretCast<T, bool>(value).ToByte(),
        TypeCode.Byte or TypeCode.SByte => ReinterpretCast<T, byte>(value),
        TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Char => ReinterpretCast<T, ushort>(value),
        TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Single => ReinterpretCast<T, uint>(value),
        TypeCode.Int64 or TypeCode.UInt64 or TypeCode.Double => ReinterpretCast<T, ulong>(value),
        _ => throw new NotSupportedException(),
    };

    /// <summary>
    /// Converts enum value into primitive type <see cref="int"/>.
    /// </summary>
    /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
    /// <param name="value">Enum value to be converted.</param>
    /// <returns>Enum value represented as <see cref="int"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToInt32<T>(this T value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => 0,
        TypeCode.Char => ReinterpretCast<T, char>(value),
        TypeCode.Decimal => Convert.ToInt32(ReinterpretCast<T, decimal>(value)),
        TypeCode.Boolean => ReinterpretCast<T, bool>(value).ToInt32(),
        TypeCode.Byte => ReinterpretCast<T, byte>(value),
        TypeCode.SByte => ReinterpretCast<T, sbyte>(value),
        TypeCode.Int16 => ReinterpretCast<T, short>(value),
        TypeCode.UInt16 => ReinterpretCast<T, ushort>(value),
        TypeCode.Int32 => ReinterpretCast<T, int>(value),
        TypeCode.UInt32 => Convert.ToInt32(ReinterpretCast<T, uint>(value)),
        TypeCode.Int64 => Convert.ToInt32(ReinterpretCast<T, long>(value)),
        TypeCode.UInt64 => Convert.ToInt32(ReinterpretCast<T, ulong>(value)),
        TypeCode.Single => Convert.ToInt32(ReinterpretCast<T, float>(value)),
        TypeCode.Double => Convert.ToInt32(ReinterpretCast<T, double>(value)),
        _ => value.ChangeType<T, int>(),
    };

    /// <summary>
    /// Converts enum value into primitive type <see cref="short"/>.
    /// </summary>
    /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
    /// <param name="value">Enum value to be converted.</param>
    /// <returns>Enum value represented as <see cref="short"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ToInt16<T>(this T value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => 0,
        TypeCode.Char => Convert.ToInt16(ReinterpretCast<T, char>(value)),
        TypeCode.Decimal => Convert.ToInt16(ReinterpretCast<T, decimal>(value)),
        TypeCode.Boolean => ReinterpretCast<T, bool>(value).ToByte(),
        TypeCode.Byte => ReinterpretCast<T, byte>(value),
        TypeCode.SByte => ReinterpretCast<T, sbyte>(value),
        TypeCode.Int16 => ReinterpretCast<T, short>(value),
        TypeCode.UInt16 => Convert.ToInt16(ReinterpretCast<T, ushort>(value)),
        TypeCode.Int32 => Convert.ToInt16(ReinterpretCast<T, int>(value)),
        TypeCode.UInt32 => Convert.ToInt16(ReinterpretCast<T, uint>(value)),
        TypeCode.Int64 => Convert.ToInt16(ReinterpretCast<T, long>(value)),
        TypeCode.UInt64 => Convert.ToInt16(ReinterpretCast<T, ulong>(value)),
        TypeCode.Single => Convert.ToInt16(ReinterpretCast<T, float>(value)),
        TypeCode.Double => Convert.ToInt16(ReinterpretCast<T, double>(value)),
        _ => value.ChangeType<T, short>(),
    };

    /// <summary>
    /// Converts enum value into primitive type <see cref="byte"/>.
    /// </summary>
    /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
    /// <param name="value">Enum value to be converted.</param>
    /// <returns>Enum value represented as <see cref="byte"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ToByte<T>(this T value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => 0,
        TypeCode.Char => Convert.ToByte(ReinterpretCast<T, char>(value)),
        TypeCode.Decimal => Convert.ToByte(ReinterpretCast<T, decimal>(value)),
        TypeCode.Boolean => ReinterpretCast<T, bool>(value).ToByte(),
        TypeCode.Byte => ReinterpretCast<T, byte>(value),
        TypeCode.SByte => Convert.ToByte(ReinterpretCast<T, sbyte>(value)),
        TypeCode.Int16 => Convert.ToByte(ReinterpretCast<T, short>(value)),
        TypeCode.UInt16 => Convert.ToByte(ReinterpretCast<T, ushort>(value)),
        TypeCode.Int32 => Convert.ToByte(ReinterpretCast<T, int>(value)),
        TypeCode.UInt32 => Convert.ToByte(ReinterpretCast<T, uint>(value)),
        TypeCode.Int64 => Convert.ToByte(ReinterpretCast<T, long>(value)),
        TypeCode.UInt64 => Convert.ToByte(ReinterpretCast<T, ulong>(value)),
        TypeCode.Single => Convert.ToByte(ReinterpretCast<T, float>(value)),
        TypeCode.Double => Convert.ToByte(ReinterpretCast<T, double>(value)),
        _ => value.ChangeType<T, byte>(),
    };

    /// <summary>
    /// Converts enum value into primitive type <see cref="ulong"/>.
    /// </summary>
    /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
    /// <param name="value">Enum value to be converted.</param>
    /// <returns>Enum value represented as <see cref="ulong"/>.</returns>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ToUInt64<T>(this T value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => 0UL,
        TypeCode.Char => ReinterpretCast<T, char>(value),
        TypeCode.Decimal => Convert.ToUInt64(ReinterpretCast<T, decimal>(value)),
        TypeCode.Boolean => ReinterpretCast<T, bool>(value).ToByte(),
        TypeCode.Byte => ReinterpretCast<T, byte>(value),
        TypeCode.SByte => Convert.ToUInt64(ReinterpretCast<T, sbyte>(value)),
        TypeCode.Int16 => Convert.ToUInt64(ReinterpretCast<T, short>(value)),
        TypeCode.UInt16 => ReinterpretCast<T, ushort>(value),
        TypeCode.Int32 => Convert.ToUInt64(ReinterpretCast<T, int>(value)),
        TypeCode.UInt32 => ReinterpretCast<T, uint>(value),
        TypeCode.Int64 => Convert.ToUInt64(ReinterpretCast<T, long>(value)),
        TypeCode.UInt64 => ReinterpretCast<T, ulong>(value),
        TypeCode.Single => Convert.ToUInt64(ReinterpretCast<T, float>(value)),
        TypeCode.Double => Convert.ToUInt64(ReinterpretCast<T, double>(value)),
        _ => value.ChangeType<T, ulong>(),
    };

    /// <summary>
    /// Converts enum value into primitive type <see cref="uint"/>.
    /// </summary>
    /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
    /// <param name="value">Enum value to be converted.</param>
    /// <returns>Enum value represented as <see cref="uint"/>.</returns>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ToUInt32<T>(this T value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => 0,
        TypeCode.Char => ReinterpretCast<T, char>(value),
        TypeCode.Decimal => Convert.ToUInt32(ReinterpretCast<T, decimal>(value)),
        TypeCode.Boolean => ReinterpretCast<T, bool>(value).ToByte(),
        TypeCode.Byte => ReinterpretCast<T, byte>(value),
        TypeCode.SByte => Convert.ToUInt32(ReinterpretCast<T, sbyte>(value)),
        TypeCode.Int16 => Convert.ToUInt32(ReinterpretCast<T, short>(value)),
        TypeCode.UInt16 => ReinterpretCast<T, ushort>(value),
        TypeCode.Int32 => Convert.ToUInt32(ReinterpretCast<T, int>(value)),
        TypeCode.UInt32 => ReinterpretCast<T, uint>(value),
        TypeCode.Int64 => Convert.ToUInt32(ReinterpretCast<T, long>(value)),
        TypeCode.UInt64 => Convert.ToUInt32(ReinterpretCast<T, ulong>(value)),
        TypeCode.Single => Convert.ToUInt32(ReinterpretCast<T, float>(value)),
        TypeCode.Double => Convert.ToUInt32(ReinterpretCast<T, double>(value)),
        _ => value.ChangeType<T, uint>(),
    };

    /// <summary>
    /// Converts enum value into primitive type <see cref="ushort"/>.
    /// </summary>
    /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
    /// <param name="value">Enum value to be converted.</param>
    /// <returns>Enum value represented as <see cref="ushort"/>.</returns>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ToUInt16<T>(this T value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => 0,
        TypeCode.Char => ReinterpretCast<T, char>(value),
        TypeCode.Decimal => Convert.ToUInt16(ReinterpretCast<T, decimal>(value)),
        TypeCode.Boolean => ReinterpretCast<T, bool>(value).ToByte(),
        TypeCode.Byte => ReinterpretCast<T, byte>(value),
        TypeCode.SByte => Convert.ToUInt16(ReinterpretCast<T, sbyte>(value)),
        TypeCode.Int16 => Convert.ToUInt16(ReinterpretCast<T, short>(value)),
        TypeCode.UInt16 => ReinterpretCast<T, ushort>(value),
        TypeCode.Int32 => Convert.ToUInt16(ReinterpretCast<T, int>(value)),
        TypeCode.UInt32 => Convert.ToUInt16(ReinterpretCast<T, uint>(value)),
        TypeCode.Int64 => Convert.ToUInt16(ReinterpretCast<T, long>(value)),
        TypeCode.UInt64 => Convert.ToUInt16(ReinterpretCast<T, ulong>(value)),
        TypeCode.Single => Convert.ToUInt16(ReinterpretCast<T, float>(value)),
        TypeCode.Double => Convert.ToUInt16(ReinterpretCast<T, double>(value)),
        _ => value.ChangeType<T, ushort>(),
    };

    /// <summary>
    /// Converts enum value into primitive type <see cref="sbyte"/>.
    /// </summary>
    /// <typeparam name="T">Type of the enum value to be converted.</typeparam>
    /// <param name="value">Enum value to be converted.</param>
    /// <returns>Enum value represented as <see cref="sbyte"/>.</returns>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static sbyte ToSByte<T>(this T value)
        where T : struct, Enum => EnumTypeCode<T>.Value switch
    {
        TypeCode.Empty => 0,
        TypeCode.Char => Convert.ToSByte(ReinterpretCast<T, char>(value)),
        TypeCode.Decimal => Convert.ToSByte(ReinterpretCast<T, decimal>(value)),
        TypeCode.Boolean => ReinterpretCast<T, bool>(value).ToSByte(),
        TypeCode.Byte => Convert.ToSByte(ReinterpretCast<T, byte>(value)),
        TypeCode.SByte => ReinterpretCast<T, sbyte>(value),
        TypeCode.Int16 => Convert.ToSByte(ReinterpretCast<T, short>(value)),
        TypeCode.UInt16 => Convert.ToSByte(ReinterpretCast<T, ushort>(value)),
        TypeCode.Int32 => Convert.ToSByte(ReinterpretCast<T, int>(value)),
        TypeCode.UInt32 => Convert.ToSByte(ReinterpretCast<T, uint>(value)),
        TypeCode.Int64 => Convert.ToSByte(ReinterpretCast<T, long>(value)),
        TypeCode.UInt64 => Convert.ToSByte(ReinterpretCast<T, ulong>(value)),
        TypeCode.Single => Convert.ToSByte(ReinterpretCast<T, float>(value)),
        TypeCode.Double => Convert.ToSByte(ReinterpretCast<T, double>(value)),
        _ => value.ChangeType<T, sbyte>(),
    };
}