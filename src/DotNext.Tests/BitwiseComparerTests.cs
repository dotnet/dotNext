using System.Drawing;

namespace DotNext;

public sealed class BitwiseComparerTests : Test
{
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
}