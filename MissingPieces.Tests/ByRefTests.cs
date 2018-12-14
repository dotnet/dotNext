using Xunit;

namespace MissingPieces.Tests
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
		public void ChangeReferenceTypeTest()
		{
			var value = new MutableStruct() { field = 42 };
			Modify(in value, 50);//defensive copy avoid modification of structure
			Equal(42, value.field);
			var valueRef = (ByRef<MutableStruct>)value;
			Equal(42, valueRef.Value.field);
			valueRef.Reference.field = 50;
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
			var managedRef = new ByRef<string>(ref str);
			Equal("Hello, world", managedRef);
			managedRef.Value = "";
			Equal("", str);
			Equal("", managedRef.Value);
		}
	}
}
