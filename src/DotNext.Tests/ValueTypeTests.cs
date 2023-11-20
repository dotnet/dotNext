using System.Drawing;

namespace DotNext;

public sealed class ValueTypeTests : Test
{
    [Fact]
    public static void BoolToIntConversion()
    {
        Equal(1, true.ToInt32());
        Equal(0, false.ToInt32());
    }

    [Fact]
    public static void BoolToByteConversion()
    {
        Equal(1, true.ToByte());
        Equal(0, false.ToByte());
    }

    [Fact]
    public static void IntToBoolConversion()
    {
        True(1.ToBoolean());
        True(42.ToBoolean());
        False(0.ToBoolean());
    }

    [Fact]
    public static void BitwiseEqualityCheck()
    {
        var value1 = Guid.NewGuid();
        var value2 = value1;
        True(BitwiseComparer<Guid>.Equals(value1, value2));
        Equal(value1, value2, BitwiseComparer<Guid>.Instance);
        value2 = default;
        False(BitwiseComparer<Guid>.Equals(value1, value2));
        NotEqual(value1, value2, BitwiseComparer<Guid>.Instance);
    }

    [Fact]
    public static void BitwiseEqualityForPrimitive()
    {
        var value1 = 10L;
        var value2 = 20L;
        False(BitwiseComparer<long>.Equals(value1, value2));
        NotEqual(value1, value2, BitwiseComparer<long>.Instance);
        value2 = 10L;
        True(BitwiseComparer<long>.Equals(value1, value2));
        Equal(value1, value2, BitwiseComparer<long>.Instance);
    }

    [Fact]
    public static void BitwiseEqualityForDifferentTypesOfTheSameSize()
    {
        var value1 = 1U;
        var value2 = 0;
        False(BitwiseComparer<uint>.Equals(value1, value2));
        value2 = 1;
        True(BitwiseComparer<uint>.Equals(value1, value2));
    }

    [Fact]
    public static void BitwiseEqualityCheckBigStruct()
    {
        var value1 = (new Point { X = 10, Y = 20 }, new Point { X = 15, Y = 20 });
        var value2 = (new Point { X = 10, Y = 20 }, new Point { X = 15, Y = 30 });
        False(BitwiseComparer<(Point, Point)>.Equals(value1, value2));
        value2.Item2.Y = 20;
        True(BitwiseComparer<(Point, Point)>.Equals(value1, value2));
    }

    [Fact]
    public static void BitwiseComparison()
    {
        IComparer<decimal> comparer = BitwiseComparer<decimal>.Instance;
        var value1 = 40M;
        var value2 = 50M;
        Equal(value1.CompareTo(value2) < 0, BitwiseComparer<decimal>.Compare(value1, value2) < 0);
        Equal(value1.CompareTo(value2) < 0, comparer.Compare(value1, value2) < 0);
        value2 = default;
        Equal(value1.CompareTo(value2) > 0, BitwiseComparer<decimal>.Compare(value1, value2) > 0);
        Equal(value1.CompareTo(value2) > 0, comparer.Compare(value1, value2) > 0);
    }

    [Fact]
    public static void BitwiseHashCodeForInt()
    {
        IEqualityComparer<int> comparer = BitwiseComparer<int>.Instance;
        var i = 20;
        var hashCode = BitwiseComparer<int>.GetHashCode(i, false);
        Equal(i, hashCode);
        hashCode = BitwiseComparer<int>.GetHashCode(i, true);
        NotEqual(i, hashCode);
        Equal(hashCode, comparer.GetHashCode(i));
    }

    [Fact]
    public static void BitwiseHashCodeForLong()
    {
        IEqualityComparer<long> comparer = BitwiseComparer<long>.Instance;
        var i = 20L;
        var hashCode = BitwiseComparer<long>.GetHashCode(i, false);
        Equal(i, hashCode);
        hashCode = BitwiseComparer<long>.GetHashCode(i, true);
        NotEqual(i, hashCode);
        Equal(hashCode, comparer.GetHashCode(i));
    }

    [Fact]
    public static void BitwiseHashCodeForGuid()
    {
        IEqualityComparer<Guid> comparer = BitwiseComparer<Guid>.Instance;
        var value = Guid.NewGuid();
        Equal(BitwiseComparer<Guid>.GetHashCode(value, true), comparer.GetHashCode(value));
    }

    [Fact]
    public static void BitwiseCompare()
    {
        True(BitwiseComparer<int>.Compare(0, int.MinValue) < 0);
        IComparer<int> comparer = BitwiseComparer<int>.Instance;
        True(comparer.Compare(0, int.MinValue) < 0);
    }

    [Fact]
    public static void CustomHashCode()
    {
        var result = BitwiseComparer<Guid>.GetHashCode(new Guid(), 0, static (data, hash) => hash + 1, false);
        Equal(4, result);
        result = BitwiseComparer<Guid>.GetHashCode(new Guid(), 0, static (data, hash) => hash + 1, true);
        Equal(5, result);
    }

    [Fact]
    public static void OneOfValues()
    {
        True(2.IsOneOf([2, 5, 7]));
        False(2.IsOneOf([3, 5, 7]));
    }

    [Fact]
    public static void NormalizeToSingle()
    {
        Equal(1F, int.MaxValue.NormalizeToSingle(int.MinValue, int.MaxValue));
        Equal(-1F, int.MinValue.NormalizeToSingle(int.MinValue, int.MaxValue));
    }

    [Fact]
    public static void NormalizeToDouble()
    {
        Equal(1F, int.MaxValue.NormalizeToDouble(int.MinValue, int.MaxValue));
        Equal(-1F, int.MinValue.NormalizeToDouble(int.MinValue, int.MaxValue));
    }

    [Fact]
    public static void WeightOfUInt64()
    {
        var weight = 0UL.Normalize();
        Equal(0D, weight);

        weight = ulong.MaxValue.Normalize();
        Equal(0.9999999999999999D, weight);

        weight = (ulong.MaxValue - 1UL).Normalize();
        Equal(0.9999999999999998D, weight);
    }

    [Fact]
    public static void WeightOfInt64()
    {
        Equal(unchecked((ulong)long.MaxValue).Normalize(), long.MaxValue.Normalize());
    }

    [Fact]
    public static void WeightOfUInt32()
    {
        var weight = 0U.Normalize();
        Equal(0F, weight);

        weight = uint.MaxValue.Normalize();
        Equal(0.99999994F, weight);

        weight = (uint.MaxValue - 1U).Normalize();
        Equal(0.9999999F, weight);
    }

    [Fact]
    public static void WeightOfInt32()
    {
        Equal(unchecked((uint)int.MaxValue).Normalize(), int.MaxValue.Normalize());
    }
}