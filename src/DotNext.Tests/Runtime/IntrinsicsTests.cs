using System;
using System.Drawing;
using Xunit;

namespace DotNext.Runtime
{
    public class IntrinsicsTests : Assert
    {
        [Fact]
        public static void IsNullable()
        {
            True(Intrinsics.IsNullable<string>());
            True(Intrinsics.IsNullable<ValueType>());
            True(Intrinsics.IsNullable<int?>());
            False(Intrinsics.IsNullable<int>());
            False(Intrinsics.IsNullable<IntPtr>());
        }

        [Fact]
        public static void RefTypeDefaultTest()
        {
            True(Intrinsics.IsDefault<string>(default));
            False(Intrinsics.IsDefault(""));
        }

        [Fact]
        public static void StructTypeDefaultTest()
        {
            var value = default(Guid);
            True(Intrinsics.IsDefault(value));
            value = Guid.NewGuid();
            False(Intrinsics.IsDefault(value));
        }

        [Fact]
        public static void BitwiseEqualityCheck()
        {
            var value1 = Guid.NewGuid();
            var value2 = value1;
            True(Intrinsics.BitwiseEquals(value1, value2));
            value2 = default;
            False(Intrinsics.BitwiseEquals(value1, value2));
        }

        [Fact]
        public static void BitwiseEqualityForPrimitive()
        {
            var value1 = 10L;
            var value2 = 20L;
            False(Intrinsics.BitwiseEquals(value1, value2));
            value2 = 10L;
            True(Intrinsics.BitwiseEquals(value1, value2));
        }

        [Fact]
        public static void BitwiseEqualityForDifferentTypesOfTheSameSize()
        {
            var value1 = 1U;
            var value2 = 0;
            False(Intrinsics.BitwiseEquals(value1, value2));
            value2 = 1;
            True(Intrinsics.BitwiseEquals(value1, value2));
        }

        [Fact]
        public static void BitwiseEqualityCheckBigStruct()
        {
            var value1 = (new Point { X = 10, Y = 20 }, new Point { X = 15, Y = 20 });
            var value2 = (new Point { X = 10, Y = 20 }, new Point { X = 15, Y = 30 });
            False(Intrinsics.BitwiseEquals(value1, value2));
            value2.Item2.Y = 20;
            True(Intrinsics.BitwiseEquals(value1, value2));
        }

        [Fact]
        public static void BitwiseComparison()
        {
            var value1 = 40M;
            var value2 = 50M;
            Equal(value1.CompareTo(value2) < 0, Intrinsics.BitwiseCompare(value1, value2) < 0);
            value2 = default;
            Equal(value1.CompareTo(value2) > 0, Intrinsics.BitwiseCompare(value1, value2) > 0);
        }

        [Fact]
        public static void SmallStructDefault()
        {
            var value = default(long);
            True(Intrinsics.IsDefault(value));
            value = 42L;
            False(Intrinsics.IsDefault(value));
        }

        [Fact]
        public static void BitwiseHashCodeForInt()
        {
            var i = 20;
            var hashCode = Intrinsics.BitwiseHashCode(i, false);
            Equal(i, hashCode);
            hashCode = Intrinsics.BitwiseHashCode(i, true);
            NotEqual(i, hashCode);
        }

        [Fact]
        public static void BitwiseHashCodeForLong()
        {
            var i = 20L;
            var hashCode = Intrinsics.BitwiseHashCode(i, false);
            Equal(i, hashCode);
            hashCode = Intrinsics.BitwiseHashCode(i, true);
            NotEqual(i, hashCode);
        }

        [Fact]
        public static void BitwiseHashCodeForGuid()
        {
            var value = Guid.NewGuid();
            Intrinsics.BitwiseHashCode(value, false);
        }

        [Fact]
        public static void BitwiseCompare()
        {
            True(Intrinsics.BitwiseCompare(0, int.MinValue) < 0);
        }
    }
}