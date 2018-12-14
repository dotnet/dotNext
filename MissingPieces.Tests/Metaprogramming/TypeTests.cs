using System;
using Xunit;

namespace MissingPieces.Metaprogramming
{
	public sealed class TypeTests: Assert
	{
		[Fact]
		public void ConstructorTests()
		{
			var stringCtor = Type<string>.Constructor<char, int>();
			var str = stringCtor('a', 3);
			Equal("aaa", str);
			var objCtor = Type<object>.Constructor();
			NotNull(objCtor());
		}

		[Fact]
		public void ValueTypeTests()
		{
			var longCtor = Type<long>.Constructor();
			Equal(0L, longCtor());
		}

		private sealed class ClassWithProperties
		{
			public static long StaticProp { get; set; }

			private int value;

			public string ReadWriteProperty { get; set; }

			public int ReadOnlyProp => value;

			public int WriteOnlyProp
			{
				set => this.value = value;
			}
		}

		private struct StructWithProperties
		{
			private int value;

			public string ReadWriteProperty { get; set; }

			public int ReadOnlyProp => value;

			public int WriteOnlyProp
			{
				set => this.value = value;
			}
		}

		[Fact]
		public void InstancePropertyTest()
		{
			var instance = new StructWithProperties();
			var rwProperty = Type<StructWithProperties>.InstanceProperty<string>(nameof(StructWithProperties.ReadWriteProperty));
			True(rwProperty.CanRead);
			True(rwProperty.CanWrite);
			rwProperty[instance] = "Hello, world";
			Equal("Hello, world", instance.ReadWriteProperty);
			Equal("Hello, world", rwProperty[instance]);
			var wProperty = Type<StructWithProperties>.InstanceProperty<int>(nameof(StructWithProperties.WriteOnlyProp));
			True(wProperty.CanWrite);
			False(wProperty.CanRead);
			wProperty[instance] = 42;
			var rProperty = Type<StructWithProperties>.InstanceProperty<int>(nameof(StructWithProperties.ReadOnlyProp));
			False(rProperty.CanWrite);
			True(rProperty.CanRead);
			Equal(42, rProperty[instance]);
		}

		[Fact]
		public void StructPropertyTest()
		{
			var instance = new ClassWithProperties();
			var rwProperty = Type<ClassWithProperties>.InstanceProperty<string>(nameof(ClassWithProperties.ReadWriteProperty));
			True(rwProperty.CanRead);
			True(rwProperty.CanWrite);
			rwProperty[instance] = "Hello, world";
			Equal("Hello, world", instance.ReadWriteProperty);
			Equal("Hello, world", rwProperty[instance]);
			var wProperty = Type<ClassWithProperties>.InstanceProperty<int>(nameof(ClassWithProperties.WriteOnlyProp));
			True(wProperty.CanWrite);
			False(wProperty.CanRead);
			wProperty[instance] = 42;
			var rProperty = Type<ClassWithProperties>.InstanceProperty<int>(nameof(ClassWithProperties.ReadOnlyProp));
			False(rProperty.CanWrite);
			True(rProperty.CanRead);
			Equal(42, rProperty[instance]);
		}

		[Fact]
		public void StaticPropertyTest()
		{
			var property = Type<ClassWithProperties>.StaticProperty<long>(nameof(ClassWithProperties.StaticProp));
			True(property.CanRead);
			True(property.CanWrite);
			property.Value = 42;
			Equal(42L, (long)property);
		}
	}
}
