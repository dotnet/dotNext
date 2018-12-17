using System;
using Xunit;

namespace MissingPieces
{
	public sealed class ByRefTests : Assert
	{
		private struct MutableStruct
		{
			internal int field;

			internal void Modify(int value)
			{
				field = value;
			}
		}

		private static void Modify(in MutableStruct s, int value)
			=> s.Modify(value);

		[Fact]
		public void SizeTest()
		{
			Equal(sizeof(long), StackValue<long>.Size);
			Equal(sizeof(int), StackValue<int>.Size);
		}

		[Fact]
		public void BitwiseEqualityTest()
		{
			StackValue<long> value1 = 20L;
			StackValue<long> value2 = 20L;
			True(value1 == value2);
			value2 = 30L;
			False(value1 == value2);
		}

		[Fact]
		public void BitwiseEqualityTest2()
		{
			StackValue<Guid> value1 = Guid.NewGuid();
			StackValue<Guid> value2 = value1;
			True(value1 == value2);
			value2 = default;
			False(value1 == value2);
		}

		[Fact]
		public void ChangeReferenceTypeTest()
		{
			var value = new MutableStruct() { field = 42 };
			Modify(in value, 50);//defensive copy avoid modification of structure
			Equal(42, value.field);
			var valueRef = Memory.AsRef(in value);
			Equal(42, valueRef.field);
			valueRef.field = 50;
			Equal(50, valueRef.field);
		}
	}
}
