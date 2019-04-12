using System;
using Xunit;

namespace DotNext
{
	public sealed class ValueTypeTests: Assert
	{
		private struct Point
		{
			public long X, Y;
		}

		[Fact]
		public static void BitcastTest()
		{
			var point = new Point{X = 40, Y = 100};
			point.BitCast(out decimal dec);
			dec.BitCast(out point);
			Equal(40, point.X);
			Equal(100, point.Y);
		}

		[Fact]
		public static void AsBinaryTest()
		{
			var g = Guid.NewGuid();
			var bytes = ValueType<Guid>.AsBinary(g);
			True(g.ToByteArray().SequenceEqual(bytes));
		}

		[Fact]
		public static void BitwiseEqualityTest2()
		{
			var value1 = Guid.NewGuid();
			var value2 = value1;
			True(ValueType<Guid>.BitwiseEquals(value1, value2));
			value2 = default;
			False(ValueType<Guid>.BitwiseEquals(value1, value2));
		}

		[Fact]
		public static void BitwiseComparisonTest()
		{
			var value1 = 40M;
			var value2 = 50M;
			Equal(value1.CompareTo(value2) < 0, ValueType<decimal>.BitwiseCompare(value1, value2) < 0);
			value2 = default;
			Equal(value1.CompareTo(value2) > 0, ValueType<decimal>.BitwiseCompare(value1, value2) > 0);
		}

		[Fact]
		public static void DefaultTests()
		{
			var value = default(Guid);
            True(ValueType<Guid>.IsDefault(value));
            value = Guid.NewGuid();
            False(ValueType<Guid>.IsDefault(value));
		}
	}
}
