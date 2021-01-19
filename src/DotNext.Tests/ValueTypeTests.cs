using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class ValueTypeTests : Test
    {
        [Fact]
        [Obsolete("This test is for checking obsolete member")]
        public static void IntPtrComparison()
        {
            var first = IntPtr.Zero;
            var second = new IntPtr(10L);
            True(first.LessThan(second));
            False(second.LessThan(first));
            True(second.GreaterThan(first));
            True(second.GreaterThanOrEqual(first));
            True(first.LessThanOrEqual(second));
            False(second.LessThanOrEqual(first));
        }

        [Require64BitProcess]
        [Obsolete("This test is for checking obsolete member")]
        public static void IntPtrArithmetic()
        {
            var value = new IntPtr(40);
            Equal(new IntPtr(800), value.Multiply(new IntPtr(20)));
            Equal(new IntPtr(800), value.MultiplyChecked(new IntPtr(20)));
            Equal(int.MaxValue * 2L, new IntPtr(int.MaxValue).Multiply(new IntPtr(2)).ToInt64());
            Equal(new IntPtr(20), value.Divide(new IntPtr(2)));
            Equal(new IntPtr(40 ^ 234), value.Xor(new IntPtr(234)));
            Equal(new IntPtr(-40), value.Negate());
            Equal(new IntPtr(60), value.Add(new IntPtr(20)));
            Equal(new IntPtr(39), value.Decrement());
            Equal(new IntPtr(41), value.Increment());
            Equal(new IntPtr(1), value.Remainder(new IntPtr(3)));
        }

        [Require64BitProcess]
        [Obsolete("This test is for checking obsolete member")]
        public static void UIntPtrArithmetic()
        {
            var value = new UIntPtr(40U);
            Equal(new UIntPtr(800U), value.Multiply(new UIntPtr(20U)));
            Equal(new UIntPtr(800U), value.MultiplyChecked(new UIntPtr(20U)));
            Equal(uint.MaxValue * 2UL, new UIntPtr(uint.MaxValue).Multiply(new UIntPtr(2U)).ToUInt64());
            Equal(new UIntPtr(20U), value.Divide(new UIntPtr(2L)));
            Equal(new UIntPtr(40U ^ 234U), value.Xor(new UIntPtr(234U)));
            Equal(new UIntPtr(60U), value.Add(new UIntPtr(20U)));
            Equal(new UIntPtr(39U), value.Decrement());
            Equal(new UIntPtr(41U), value.Increment());
            Equal(new UIntPtr(1), value.Remainder(new UIntPtr(3)));
        }

        [Require64BitProcess]
        [Obsolete("This test is for checking obsolete member")]
        public static void IntPtrArithmeticOverflow()
        {
            var value = new IntPtr(long.MaxValue);
            Throws<OverflowException>(() => value.AddChecked(new IntPtr(1)));
            value = new IntPtr(long.MinValue);
            Throws<OverflowException>(() => value.SubtractChecked(new IntPtr(1)));
            Equal(new IntPtr(long.MaxValue), value.Subtract(new IntPtr(1)));
            Throws<OverflowException>(() => value.ToUIntPtrChecked());
        }

        [Require64BitProcess]
        [Obsolete("This test is for checking obsolete member")]
        public static void UIntPtrArithmeticOverflow()
        {
            var value = new UIntPtr(ulong.MaxValue);
            Throws<OverflowException>(() => value.AddChecked(new UIntPtr(1U)));
            Throws<OverflowException>(() => value.ToIntPtrChecked());
            value = new UIntPtr(0U);
            Throws<OverflowException>(() => value.SubtractChecked(new UIntPtr(1U)));
            Equal(new UIntPtr(ulong.MaxValue), value.Subtract(new UIntPtr(1U)));
        }

        [Fact]
        [Obsolete("This test is for checking obsolete member")]
        public static void IntPtrBitwiseOperations()
        {
            Equal(default, new IntPtr(1).And(default));
            Equal(new IntPtr(1), new IntPtr(1).Or(default));
            Equal(default, new IntPtr().Or(default));
            Equal(new IntPtr(1), new IntPtr(1).Xor(default));
            Equal(default, new IntPtr().Xor(default));
            Equal(default, new IntPtr(1).Xor(new IntPtr(1)));
            Equal(new IntPtr(4), new IntPtr(2).LeftShift(1));
            Equal(new IntPtr(2), new IntPtr(4).RightShift(1));
        }

        [Fact]
        [Obsolete("This test is for checking obsolete member")]
        public static void UIntPtrBitwiseOperations()
        {
            Equal(default, new UIntPtr(1U).And(default));
            Equal(new UIntPtr(1U), new UIntPtr(1U).Or(default));
            Equal(default, new UIntPtr().Or(default));
            Equal(new UIntPtr(1U), new UIntPtr(1U).Xor(default));
            Equal(default, new UIntPtr().Xor(default));
            Equal(default, new UIntPtr(1U).Xor(new UIntPtr(1U)));
            Equal(new UIntPtr(4), new UIntPtr(2).LeftShift(1));
            Equal(new UIntPtr(2), new UIntPtr(4).RightShift(1));
        }

        [Fact]
        [Obsolete("This test is for checking obsolete member")]
        public static void UIntPtrComparison()
        {
            True(new UIntPtr(10).GreaterThan(new UIntPtr(9)));
            False(new UIntPtr(10).GreaterThan(new UIntPtr(10)));
            True(new UIntPtr(10).GreaterThanOrEqual(new UIntPtr(10)));
            False(new UIntPtr(10).GreaterThanOrEqual(new UIntPtr(11)));
            True(new UIntPtr(10).LessThan(new UIntPtr(11)));
            False(new UIntPtr(10).LessThan(new UIntPtr(10)));
            True(new UIntPtr(10).LessThanOrEqual(new UIntPtr(10)));
        }

        [Require64BitProcess]
        [Obsolete("This test is for checking obsolete member")]
        public static void OnesComplement()
        {
            Equal(new UIntPtr(ulong.MaxValue), new UIntPtr().OnesComplement());
            Equal(new IntPtr(-1L), new IntPtr().OnesComplement());
        }

        [Fact]
        [Obsolete("This test is for checking obsolete member")]
        public static void IntPtrConversion()
        {
            Equal(new UIntPtr(42U), new IntPtr(42).ToUIntPtr());
            Equal(new IntPtr(42), new UIntPtr(42U).ToIntPtr());
        }

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
            var result = BitwiseComparer<Guid>.GetHashCode(new Guid(), 0, (hash, data) => hash + 1, false);
            Equal(4, result);
            result = BitwiseComparer<Guid>.GetHashCode(new Guid(), 0, (hash, data) => hash + 1, true);
            Equal(5, result);
        }

        [Fact]
        public static void OneOfValues()
        {
            True(2.IsOneOf(2, 5, 7));
            False(2.IsOneOf(3, 5, 7));

            True(2.IsOneOf(new List<int> { 2, 5, 7 }));
            False(2.IsOneOf(new List<int> { 3, 5, 7 }));
        }
    }
}
