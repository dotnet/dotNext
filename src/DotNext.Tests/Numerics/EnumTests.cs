namespace DotNext.Numerics;

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
        Equal(zero, Enum<TEnum>.CreateChecked<sbyte>(0));
        Equal(zero, Enum<TEnum>.CreateChecked<byte>(0));
        Equal(zero, Enum<TEnum>.CreateChecked<short>(0));
        Equal(zero, Enum<TEnum>.CreateChecked< ushort>(0));
        Equal(zero, Enum<TEnum>.CreateChecked(0));
        Equal(zero, Enum<TEnum>.CreateChecked(0U));
        Equal(zero, Enum<TEnum>.CreateChecked(0L));
        Equal(zero, Enum<TEnum>.CreateChecked(0UL));

        Equal(one, Enum<TEnum>.CreateChecked<sbyte>(1));
        Equal(one, Enum<TEnum>.CreateChecked<byte>(1));
        Equal(one, Enum<TEnum>.CreateChecked<short>(1));
        Equal(one, Enum<TEnum>.CreateChecked<ushort>(1));
        Equal(one, Enum<TEnum>.CreateChecked(1));
        Equal(one, Enum<TEnum>.CreateChecked(1U));
        Equal(one, Enum<TEnum>.CreateChecked(1L));
        Equal(one, Enum<TEnum>.CreateChecked(1UL));

        Equal(two, Enum<TEnum>.CreateChecked<sbyte>(2));
        Equal(two, Enum<TEnum>.CreateChecked<byte>(2));
        Equal(two, Enum<TEnum>.CreateChecked<short>(2));
        Equal(two, Enum<TEnum>.CreateChecked<ushort>(2));
        Equal(two, Enum<TEnum>.CreateChecked(2));
        Equal(two, Enum<TEnum>.CreateChecked(2U));
        Equal(two, Enum<TEnum>.CreateChecked(2L));
        Equal(two, Enum<TEnum>.CreateChecked(2UL));
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
        Equal(0, new Enum<TEnum>(zero).ConvertChecked<byte>());
        Equal(0, new Enum<TEnum>(zero).ConvertChecked<sbyte>());
        Equal(0, new Enum<TEnum>(zero).ConvertChecked<short>());
        Equal(0, new Enum<TEnum>(zero).ConvertChecked<ushort>());
        Equal(0, new Enum<TEnum>(zero).ConvertChecked<int>());
        Equal(0U, new Enum<TEnum>(zero).ConvertChecked<uint>());
        Equal(0L, new Enum<TEnum>(zero).ConvertChecked<long>());
        Equal(0UL, new Enum<TEnum>(zero).ConvertChecked<ulong>());

        Equal(1, new Enum<TEnum>(one).ConvertChecked<byte>());
        Equal(1, new Enum<TEnum>(one).ConvertChecked<sbyte>());
        Equal(1, new Enum<TEnum>(one).ConvertChecked<short>());
        Equal(1, new Enum<TEnum>(one).ConvertChecked<ushort>());
        Equal(1, new Enum<TEnum>(one).ConvertChecked<int>());
        Equal(1U, new Enum<TEnum>(one).ConvertChecked<uint>());
        Equal(1L, new Enum<TEnum>(one).ConvertChecked<long>());
        Equal(1UL, new Enum<TEnum>(one).ConvertChecked<ulong>());

        Equal(2, new Enum<TEnum>(two).ConvertChecked<byte>());
        Equal(2, new Enum<TEnum>(two).ConvertChecked<sbyte>());
        Equal(2, new Enum<TEnum>(two).ConvertChecked<short>());
        Equal(2, new Enum<TEnum>(two).ConvertChecked<ushort>());
        Equal(2, new Enum<TEnum>(two).ConvertChecked<int>());
        Equal(2U, new Enum<TEnum>(two).ConvertChecked<uint>());
        Equal(2L, new Enum<TEnum>(two).ConvertChecked<long>());
        Equal(2UL, new Enum<TEnum>(two).ConvertChecked<ulong>());
    }

    [Fact]
    public static void ConversionToPrimitive()
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
        const EnvironmentVariableTarget expected = (EnvironmentVariableTarget)(-1);

        Equal(-1, new Enum<EnvironmentVariableTarget>(expected).ConvertChecked<sbyte>());
        Throws<OverflowException>(() => new Enum<EnvironmentVariableTarget>(expected).ConvertChecked<byte>());
    }

    [Fact]
    public static void ConvertEnumToEnum()
    {
        const EnvironmentVariableTarget expected = EnvironmentVariableTarget.Machine;
        Int32Enum actual = new Enum<EnvironmentVariableTarget>(expected).ConvertTruncating<Enum<Int32Enum>>();

        Equal((int)expected, (int)actual);
    }

    [Fact]
    public static void CheckSign()
    {
        True(Enum<Int32Enum>.IsSigned);
        False(Enum<UInt32Enum>.IsSigned);
    }

    [Fact]
    public static void ByteCount()
    {
        Equal(int.MaxByteCount, Enum<Int32Enum>.MaxByteCount);
        Equal(byte.MaxByteCount, Enum<ByteEnum>.MaxByteCount);
    }

    [Fact]
    public static void PositiveNegativeValues()
    {
        var value = (Enum<Int32Enum>)Int32Enum.One;
        True(value.IsPositive);
        False(value.IsNegative);
        True(value.IsNotZero);

        value = default;
        False(value.IsPositive);
        False(value.IsNegative);
        False(value.IsNotZero);
    }

    [Fact]
    public static void EvenOddValues()
    {
        var value = (Enum<Int32Enum>)Int32Enum.One;
        False(value.IsEven);
        True(value.IsOdd);

        value = (Enum<Int32Enum>)Int32Enum.Two;
        True(value.IsEven);
        False(value.IsOdd);
    }

    [Fact]
    public static void AddSubtractValues()
    {
        var x = (Enum<Int64Enum>)Int64Enum.One;
        var y = (Enum<Int64Enum>)Int64Enum.One;
        Equal(Int64Enum.Two, x + y);
        Equal(Int64Enum.Zero, x - y);
    }
}