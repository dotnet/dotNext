using System.Numerics;

namespace DotNext;

public sealed class EnumTests : Test
{
    private enum ByteEnum : byte
    {
        Zero = 0,
        One,
        Two,
    }

    private enum SByteEnum : sbyte
    {
        Zero = 0,
        One,
        Two,
    }

    private enum Int16Enum : short
    {
        Zero = 0,
        One,
        Two,
    }

    private enum UInt16Enum : ushort
    {
        Zero = 0,
        One,
        Two,
    }

    private enum Int32Enum : int
    {
        Zero = 0,
        One,
        Two,
    }

    private enum UInt32Enum : uint
    {
        Zero = 0,
        One,
        Two,
    }

    private enum Int64Enum : long
    {
        Zero = 0,
        One,
        Two,
    }

    private enum UInt64Enum : ulong
    {
        Zero = 0,
        One,
        Two,
    }

    private static void FromPrimitive<TEnum>(TEnum zero, TEnum one, TEnum two)
        where TEnum : struct, Enum
    {
        Equal(zero, EnumConverter.ToEnum<TEnum, sbyte>(0));
        Equal(zero, EnumConverter.ToEnum<TEnum, byte>(0));
        Equal(zero, EnumConverter.ToEnum<TEnum, short>(0));
        Equal(zero, EnumConverter.ToEnum<TEnum, ushort>(0));
        Equal(zero,EnumConverter.ToEnum<TEnum, int>(0));
        Equal(zero, EnumConverter.ToEnum<TEnum, uint>(0U));
        Equal(zero, EnumConverter.ToEnum<TEnum, long>(0L));
        Equal(zero, EnumConverter.ToEnum<TEnum, ulong>(0UL));

        Equal(one, EnumConverter.ToEnum<TEnum, sbyte>(1));
        Equal(one, EnumConverter.ToEnum<TEnum, byte>(1));
        Equal(one, EnumConverter.ToEnum<TEnum, short>(1));
        Equal(one, EnumConverter.ToEnum<TEnum, ushort>(1));
        Equal(one,EnumConverter.ToEnum<TEnum, int>(1));
        Equal(one, EnumConverter.ToEnum<TEnum, uint>(1U));
        Equal(one, EnumConverter.ToEnum<TEnum, long>(1L));
        Equal(one, EnumConverter.ToEnum<TEnum, ulong>(1UL));

        Equal(two, EnumConverter.ToEnum<TEnum, sbyte>(2));
        Equal(two, EnumConverter.ToEnum<TEnum, byte>(2));
        Equal(two, EnumConverter.ToEnum<TEnum, short>(2));
        Equal(two, EnumConverter.ToEnum<TEnum, ushort>(2));
        Equal(two,EnumConverter.ToEnum<TEnum, int>(2));
        Equal(two, EnumConverter.ToEnum<TEnum, uint>(2U));
        Equal(two, EnumConverter.ToEnum<TEnum, long>(2L));
        Equal(two, EnumConverter.ToEnum<TEnum, ulong>(2UL));
    }

    [Fact]
    public static void ConversionFromPrimitive()
    {
        FromPrimitive(ByteEnum.Zero, ByteEnum.One, ByteEnum.Two);
        FromPrimitive(SByteEnum.Zero, SByteEnum.One, SByteEnum.Two);
        FromPrimitive(Int16Enum.Zero, Int16Enum.One, Int16Enum.Two);
        FromPrimitive(UInt16Enum.Zero, UInt16Enum.One, UInt16Enum.Two);
        FromPrimitive(Int32Enum.Zero, Int32Enum.One, Int32Enum.Two);
        FromPrimitive(UInt32Enum.Zero, UInt32Enum.One, UInt32Enum.Two);
        FromPrimitive(Int64Enum.Zero, Int64Enum.One, Int64Enum.Two);
        FromPrimitive(UInt64Enum.Zero, UInt64Enum.One, UInt64Enum.Two);
    }

    private static void ToPrimitive<TEnum>(TEnum zero, TEnum one, TEnum two)
        where TEnum : struct, Enum
    {
        Equal(0, EnumConverter.FromEnum<TEnum, byte>(zero));
        Equal(0, EnumConverter.FromEnum<TEnum, sbyte>(zero));
        Equal(0, EnumConverter.FromEnum<TEnum, short>(zero));
        Equal(0, EnumConverter.FromEnum<TEnum, ushort>(zero));
        Equal(0, EnumConverter.FromEnum<TEnum, int>(zero));
        Equal(0U, EnumConverter.FromEnum<TEnum, uint>(zero));
        Equal(0L, EnumConverter.FromEnum<TEnum, long>(zero));
        Equal(0UL, EnumConverter.FromEnum<TEnum, ulong>(zero));

        Equal(1, EnumConverter.FromEnum<TEnum, byte>(one));
        Equal(1, EnumConverter.FromEnum<TEnum, sbyte>(one));
        Equal(1, EnumConverter.FromEnum<TEnum, short>(one));
        Equal(1, EnumConverter.FromEnum<TEnum, ushort>(one));
        Equal(1, EnumConverter.FromEnum<TEnum, int>(one));
        Equal(1U, EnumConverter.FromEnum<TEnum, uint>(one));
        Equal(1L, EnumConverter.FromEnum<TEnum, long>(one));
        Equal(1UL, EnumConverter.FromEnum<TEnum, ulong>(one));

        Equal(2, EnumConverter.FromEnum<TEnum, byte>(two));
        Equal(2, EnumConverter.FromEnum<TEnum, sbyte>(two));
        Equal(2, EnumConverter.FromEnum<TEnum, short>(two));
        Equal(2, EnumConverter.FromEnum<TEnum, ushort>(two));
        Equal(2, EnumConverter.FromEnum<TEnum, int>(two));
        Equal(2U, EnumConverter.FromEnum<TEnum, uint>(two));
        Equal(2L, EnumConverter.FromEnum<TEnum, long>(two));
        Equal(2UL, EnumConverter.FromEnum<TEnum, ulong>(two));
    }

    [Fact]
    public static void ConvertionToPrimitive()
    {
        ToPrimitive(ByteEnum.Zero, ByteEnum.One, ByteEnum.Two);
        ToPrimitive(SByteEnum.Zero, SByteEnum.One, SByteEnum.Two);
        ToPrimitive(Int16Enum.Zero, Int16Enum.One, Int16Enum.Two);
        ToPrimitive(UInt16Enum.Zero, UInt16Enum.One, UInt16Enum.Two);
        ToPrimitive(Int32Enum.Zero, Int32Enum.One, Int32Enum.Two);
        ToPrimitive(UInt32Enum.Zero, UInt32Enum.One, UInt32Enum.Two);
        ToPrimitive(Int64Enum.Zero, Int64Enum.One, Int64Enum.Two);
        ToPrimitive(UInt64Enum.Zero, UInt64Enum.One, UInt64Enum.Two);
    }

    [Fact]
    public static void NegativeValueConversion()
    {
        var expected = (EnvironmentVariableTarget)(-1);

        Equal(-1, EnumConverter.FromEnum<EnvironmentVariableTarget, sbyte>(expected));
        Throws<OverflowException>(() => EnumConverter.FromEnum<EnvironmentVariableTarget, byte>(expected));

        Equal(-1, EnumConverter.FromEnum<EnvironmentVariableTarget, short>(expected));
        Throws<OverflowException>(() => EnumConverter.FromEnum<EnvironmentVariableTarget, ushort>(expected));
    }
}