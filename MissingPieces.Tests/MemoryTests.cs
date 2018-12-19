using System;
using Xunit;

namespace MissingPieces
{
	public sealed class MemoryTests: Assert
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
		public void BitwiseEqualityTest2()
		{
			var value1 = Guid.NewGuid();
			var value2 = value1;
			True(value1.BitwiseEquals(value2));
			value2 = default;
			False(value1.BitwiseEquals(value2));
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

		[Fact]
		public void DefaultTests()
		{
			var value = default(Guid);
            True(value.IsDefault());
            value = Guid.NewGuid();
            False(value.IsDefault());
		}
	}
}
