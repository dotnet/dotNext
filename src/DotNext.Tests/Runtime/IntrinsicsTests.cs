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
        public static void SmallStructDefault()
        {
            var value = default(long);
            True(Intrinsics.IsDefault(value));
            value = 42L;
            False(Intrinsics.IsDefault(value));
        }

        [Fact]
        public static void Bitcast()
        {
            var point = new Point { X = 40, Y = 100 };
            Intrinsics.Bitcast(point, out decimal dec);
            point = default;
            Intrinsics.Bitcast(dec, out point);
            Equal(40, point.X);
            Equal(100, point.Y);
        }

        [Fact]
        public static void BitcastToLargerValueType()
        {
            var point = new Point { X = 40, Y = 100 };
            Intrinsics.Bitcast(point, out Guid g);
            False(g == Guid.Empty);
        }
    }
}