using System;
using Xunit;

namespace DotNext
{
    public sealed class ValueTypeTests : Assert
    {
        private struct Point
        {
            public long X, Y;
        }

        [Fact]
        public static void Bitcast()
        {
            var point = new Point { X = 40, Y = 100 };
            point.Bitcast(out decimal dec);
            point = default;
            dec.Bitcast(out point);
            Equal(40, point.X);
            Equal(100, point.Y);
        }

        [Fact]
        public static void BitcastToLargerValueType()
        {
            var point = new Point { X = 40, Y = 100 };
            point.Bitcast(out Guid g);
            False(g == Guid.Empty);
        }

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
    }
}
