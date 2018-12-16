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
			Equal(sizeof(long), Ref<long>.Size);
			Equal(sizeof(int), Ref<int>.Size);
		}

		[Fact]
		public void BitwiseEqualityTest()
		{
			var value1 = 20L;
			var value2 = 20L;
			var ref1 = value1.AsRef();
			var ref2 = value2.AsRef();
			True(ref1.BitwiseEquals(ref2));
			value2 = 30L;
			False(ref1.BitwiseEquals(ref2));
		}

		[Fact]
		public void BitwiseEqualityTest2()
		{
			var value1 = Guid.NewGuid();
			var value2 = value1;
			var ref1 = value1.AsRef();
			var ref2 = value2.AsRef();
			True(ref1.BitwiseEquals(ref2));
			value2 = default;
			False(ref1.BitwiseEquals(ref2));
		}

		[Fact]
		public void ChangeReferenceTypeTest()
		{
			var value = new MutableStruct() { field = 42 };
			Modify(in value, 50);//defensive copy avoid modification of structure
			Equal(42, value.field);
			var valueRef = (Ref<MutableStruct>)value;
			Equal(42, valueRef.Value.field);
			valueRef.GetPinnableReference().field = 50;
			Equal(50, valueRef.Value.field);
		}

		[Fact]
		public void ValueTypeTest()
		{
			var i = 10;
			var managedRef = i.AsRef();
			Equal(10, managedRef);
			managedRef.Value = 20;
			Equal(20, i);
			Equal(20, managedRef);
		}

		[Fact]
		public void RefTypeTest()
		{
			var str = "Hello, world";
			var managedRef = new Ref<string>(ref str);
			Equal("Hello, world", managedRef);
			managedRef.Value = "";
			Equal("", str);
			Equal("", managedRef.Value);
		}
	}
}
