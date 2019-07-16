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
            dec.Bitcast(out point);
            Equal(40, point.X);
            Equal(100, point.Y);
        }

        [Fact]
        public static void ConvertToByteArray()
        {
            var g = Guid.NewGuid();
            var bytes = ValueType<Guid>.AsBinary(g);
            True(g.ToByteArray().SequenceEqual(bytes));
        }

        [Fact]
        public static void BitwiseEqualityCheck()
        {
            var value1 = Guid.NewGuid();
            var value2 = value1;
            True(ValueType<Guid>.BitwiseEquals(value1, value2));
            value2 = default;
            False(ValueType<Guid>.BitwiseEquals(value1, value2));
        }

        [Fact]
        public static void BitwiseEqualityForPrimitive()
        {
            var value1 = 10L;
            var value2 = 20L;
            False(ValueType<long>.BitwiseEquals(value1, value2));
            value2 = 10L;
            True(ValueType<long>.BitwiseEquals(value1, value2));
        }

        [Fact]
        public static void BitwiseEqualityForDifferentTypesOfTheSameSize()
        {
            var value1 = 1U;
            var value2 = 0;
            False(ValueType<uint>.BitwiseEquals(value1, value2));
            value2 = 1;
            True(ValueType<uint>.BitwiseEquals(value1, value2));
        }

        [Fact]
        public static void BitwiseEqualityCheckBigStruct()
        {
            var value1 = (new Point { X = 10, Y = 20 }, new Point { X = 15, Y = 20 });
            var value2 = (new Point { X = 10, Y = 20 }, new Point { X = 15, Y = 30 });
            False(ValueType<(Point, Point)>.BitwiseEquals(value1, value2));
            value2.Item2.Y = 20;
            True(ValueType<(Point, Point)>.BitwiseEquals(value1, value2));
        }

        [Fact]
        public static void BitwiseComparison()
        {
            var value1 = 40M;
            var value2 = 50M;
            Equal(value1.CompareTo(value2) < 0, ValueType<decimal>.BitwiseCompare(value1, value2) < 0);
            value2 = default;
            Equal(value1.CompareTo(value2) > 0, ValueType<decimal>.BitwiseCompare(value1, value2) > 0);
        }

        [Fact]
        public static void LargeStructDefault()
        {
            var value = default(Guid);
            True(ValueType<Guid>.IsDefault(value));
            value = Guid.NewGuid();
            False(ValueType<Guid>.IsDefault(value));
        }

        [Fact]
        public static void SmallStructDefault()
        {
            var value = default(long);
            True(ValueType<long>.IsDefault(value));
            value = 42L;
            False(ValueType<long>.IsDefault(value));
        }
    }
}
