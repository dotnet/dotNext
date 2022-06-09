using System.Diagnostics.CodeAnalysis;

namespace DotNext;

[ExcludeFromCodeCoverage]
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
        Equal(zero, ((sbyte)0).ToEnum<TEnum>());
        Equal(zero, ((byte)0).ToEnum<TEnum>());
        Equal(zero, ((short)0).ToEnum<TEnum>());
        Equal(zero, ((ushort)0).ToEnum<TEnum>());
        Equal(zero, 0.ToEnum<TEnum>());
        Equal(zero, 0U.ToEnum<TEnum>());
        Equal(zero, 0L.ToEnum<TEnum>());
        Equal(zero, 0UL.ToEnum<TEnum>());

        Equal(one, ((sbyte)1).ToEnum<TEnum>());
        Equal(one, ((byte)1).ToEnum<TEnum>());
        Equal(one, ((short)1).ToEnum<TEnum>());
        Equal(one, ((ushort)1).ToEnum<TEnum>());
        Equal(one, 1.ToEnum<TEnum>());
        Equal(one, 1U.ToEnum<TEnum>());
        Equal(one, 1L.ToEnum<TEnum>());
        Equal(one, 1UL.ToEnum<TEnum>());

        Equal(two, ((sbyte)2).ToEnum<TEnum>());
        Equal(two, ((byte)2).ToEnum<TEnum>());
        Equal(two, ((short)2).ToEnum<TEnum>());
        Equal(two, ((ushort)2).ToEnum<TEnum>());
        Equal(two, 2.ToEnum<TEnum>());
        Equal(two, 2U.ToEnum<TEnum>());
        Equal(two, 2L.ToEnum<TEnum>());
        Equal(two, 2UL.ToEnum<TEnum>());
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
        Equal(0, zero.ToByte());
        Equal(0, zero.ToSByte());
        Equal(0, zero.ToInt16());
        Equal(0, zero.ToUInt16());
        Equal(0, zero.ToInt32());
        Equal(0U, zero.ToUInt32());
        Equal(0L, zero.ToInt64());
        Equal(0UL, zero.ToUInt64());

        Equal(1, one.ToByte());
        Equal(1, one.ToSByte());
        Equal(1, one.ToInt16());
        Equal(1, one.ToUInt16());
        Equal(1, one.ToInt32());
        Equal(1U, one.ToUInt32());
        Equal(1L, one.ToInt64());
        Equal(1UL, one.ToUInt64());

        Equal(2, two.ToByte());
        Equal(2, two.ToSByte());
        Equal(2, two.ToInt16());
        Equal(2, two.ToUInt16());
        Equal(2, two.ToInt32());
        Equal(2U, two.ToUInt32());
        Equal(2L, two.ToInt64());
        Equal(2UL, two.ToUInt64());
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

        Equal(-1, expected.ToSByte());
        Throws<OverflowException>(() => expected.ToByte());

        Equal(-1, expected.ToInt16());
        Throws<OverflowException>(() => expected.ToUInt16());

        Equal(-1, expected.ToInt32());
        Throws<OverflowException>(() => expected.ToUInt32());

        Equal(-1L, expected.ToInt64());
        Throws<OverflowException>(() => expected.ToUInt64());
    }
}