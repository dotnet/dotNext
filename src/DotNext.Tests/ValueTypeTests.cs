using System;
using System.Drawing;
using Xunit;

namespace DotNext
{
    public sealed class ValueTypeTests : Assert
    {
        [Fact]
        public static void IntPtrComparison()
        {
            var first = IntPtr.Zero;
            var second = new IntPtr(10L);
            True(first.LessThan(second));
            False(second.LessThan(first));
            True(second.GreaterThan(first));
            True(second.GreaterThanOrEqual(first));
        }

        [Require64BitProcess]
        public static void IntPtrArithmetic()
        {
            var value = new IntPtr(40);
            Equal(new IntPtr(800), value.Multiply(new IntPtr(20)));
            Equal(new IntPtr(800), value.MultiplyChecked(new IntPtr(20)));
            Equal(int.MaxValue * 2L, new IntPtr(int.MaxValue).Multiply(new IntPtr(2)).ToInt64());
            Equal(new IntPtr(20), value.Divide(new IntPtr(2)));
            Equal(new IntPtr(40 ^ 234), value.Xor(new IntPtr(234)));
            Equal(new IntPtr(-40), value.Negate());
        }

        [Fact]
        public static void BoolToIntConversion()
        {
            Equal(1, true.ToInt32());
            Equal(0, false.ToInt32());
        }

        [Fact]
        public static void BitwiseEqualityCheck()
        {
            var value1 = Guid.NewGuid();
            var value2 = value1;
            True(BitwiseComparer<Guid>.Equals(value1, value2));
            value2 = default;
            False(BitwiseComparer<Guid>.Equals(value1, value2));
        }

        [Fact]
        public static void BitwiseEqualityForPrimitive()
        {
            var value1 = 10L;
            var value2 = 20L;
            False(BitwiseComparer<long>.Equals(value1, value2));
            value2 = 10L;
            True(BitwiseComparer<long>.Equals(value1, value2));
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
            False(BitwiseComparer<Point>.Equals(value1, value2));
            value2.Item2.Y = 20;
            True(BitwiseComparer<Point>.Equals(value1, value2));
        }

        [Fact]
        public static void BitwiseComparison()
        {
            var value1 = 40M;
            var value2 = 50M;
            Equal(value1.CompareTo(value2) < 0, BitwiseComparer<decimal>.Compare(value1, value2) < 0);
            value2 = default;
            Equal(value1.CompareTo(value2) > 0, BitwiseComparer<decimal>.Compare(value1, value2) > 0);
        }

        [Fact]
        public static void BitwiseHashCodeForInt()
        {
            var i = 20;
            var hashCode = BitwiseComparer<int>.GetHashCode(i, false);
            Equal(i, hashCode);
            hashCode = BitwiseComparer<int>.GetHashCode(i, true);
            NotEqual(i, hashCode);
        }

        [Fact]
        public static void BitwiseHashCodeForLong()
        {
            var i = 20L;
            var hashCode = BitwiseComparer<long>.GetHashCode(i, false);
            Equal(i, hashCode);
            hashCode = BitwiseComparer<long>.GetHashCode(i, true);
            NotEqual(i, hashCode);
        }

        [Fact]
        public static void BitwiseHashCodeForGuid()
        {
            var value = Guid.NewGuid();
            BitwiseComparer<Guid>.GetHashCode(value, false);
        }

        [Fact]
        public static void BitwiseCompare()
        {
            True(BitwiseComparer<int>.Compare(0, int.MinValue) < 0);
        }
    }
}
