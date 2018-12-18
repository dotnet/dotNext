using System;
using System.IO;
using Xunit;

namespace MissingPieces.Metaprogramming
{
	public sealed class TypeTests: Assert
	{
		internal static event EventHandler StaticEvent;
		private event EventHandler InstanceEvent;

		[Fact]
		public void MethodTest()
		{
			Func<string, char, int> indexOf = Type<string>.Method<Func<string, char, int>>.Instance.GetOrNull(nameof(string.IndexOf));
			NotNull(indexOf);
			var result = indexOf("aba", 'b');
		}

		[Fact]
		public void ConstructorTests()
		{
			var stringCtor = Type<string>.Constructor.Get<char, int>();
			var str = stringCtor('a', 3);
			Equal("aaa", str);
			var objCtor = Type<object>.Constructor.Get();
			stringCtor = Type<string>.Constructor<Func<char, int, string>>.GetOrNull();
			str = stringCtor('a', 3);
			Equal("aaa", str);
			var invalidCtor = Type<string>.Constructor<Func<int, int, string>>.GetOrNull();
			Null(invalidCtor);
			var classCtor = Type<ClassWithProperties>.Constructor.Get<int>(true);
			var obj = classCtor(10);
			Equal(10, obj.ReadOnlyProp);
		}

		[Fact]
		public void ValueTypeTests()
		{
			var longCtor = Type<long>.Constructor.Get();
			Equal(0L, longCtor());
		}

		private sealed class ClassWithProperties
		{
			
			internal static long StaticProp { get; set; }

			private int value;

			public ClassWithProperties(){}

			internal ClassWithProperties(int val) => value = val;

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
			var rwProperty = Type<StructWithProperties>.Property<string>.Instance.Get(nameof(StructWithProperties.ReadWriteProperty));
			True(rwProperty.CanRead);
			True(rwProperty.CanWrite);
			rwProperty[instance] = "Hello, world";
			Equal("Hello, world", instance.ReadWriteProperty);
			Equal("Hello, world", rwProperty[instance]);
			var wProperty = Type<StructWithProperties>.Property<int>.Instance.Get(nameof(StructWithProperties.WriteOnlyProp));
			True(wProperty.CanWrite);
			False(wProperty.CanRead);
			wProperty[instance] = 42;
			MemberAccess<StructWithProperties, int> rProperty = Type<StructWithProperties>.Property<int>.Instance.Get(nameof(StructWithProperties.ReadOnlyProp));
			Equal(42, rProperty.GetValue(in instance));
		}

		[Fact]
		public void StructPropertyTest()
		{
			var instance = new ClassWithProperties();
			MemberAccess<ClassWithProperties, string> rwProperty = Type<ClassWithProperties>.Property<string>.Instance.Get(nameof(ClassWithProperties.ReadWriteProperty));
			rwProperty.SetValue(instance, "Hello, world");
			Equal("Hello, world", instance.ReadWriteProperty);
			Equal("Hello, world", rwProperty.GetValue(instance));
			var wProperty = Type<ClassWithProperties>.Property<int>.Instance.Get(nameof(ClassWithProperties.WriteOnlyProp));
			True(wProperty.CanWrite);
			False(wProperty.CanRead);
			wProperty[instance] = 42;
			var rProperty = Type<ClassWithProperties>.Property<int>.Instance.Get(nameof(ClassWithProperties.ReadOnlyProp));
			False(rProperty.CanWrite);
			True(rProperty.CanRead);
			Equal(42, rProperty[instance]);
		}

		[Fact]
		public void StaticPropertyTest()
		{
			var property = Type<ClassWithProperties>.Property<long>.Static.Get(nameof(ClassWithProperties.StaticProp), true);
			True(property.CanRead);
			True(property.CanWrite);
			property.Value = 42;
			Equal(42L, (long)property);
		}

		[Fact]
		public void InstanceEventTest()
		{
			EventAccess<AppDomain, ResolveEventHandler> ev = Type<AppDomain>.Event<ResolveEventHandler>.Instance.Get(nameof(AppDomain.TypeResolve));
			ResolveEventHandler handler = (sender, args) => null;
			ev.AddEventHandler(AppDomain.CurrentDomain, handler);
			ev.RemoveEventHandler(AppDomain.CurrentDomain, handler);
			var ev2 = Type<TypeTests>.Event<EventHandler>.Instance.Get(nameof(InstanceEvent), true);
			Null(InstanceEvent);
			EventHandler handler2 = (sender, args) => { };
			ev2.AddEventHandler(this, handler2);
			Equal(InstanceEvent, handler2);
			ev2.RemoveEventHandler(this, handler2);
			Null(InstanceEvent);
		}

		[Fact]
		public void StaticEventTest()
		{
			var ev = Type<TypeTests>.Event<EventHandler>.Static.Get(nameof(StaticEvent), true);
			EventHandler handler = (sender, args) => { };
			ev.AddEventHandler(handler);
			Equal(StaticEvent, handler);
			ev.RemoveEventHandler(handler);
			Null(StaticEvent);
		}

		[Fact]
		public void StaticFieldTest()
		{
			var structField = Type<Guid>.Field<Guid>.Static.Get(nameof(Guid.Empty));
			StackValue<Guid>.BitwiseEquals(default, structField.Value);
			var objField = Type<TextReader>.Field<TextReader>.Static.Get(nameof(TextReader.Null));
			Same(TextReader.Null, objField.Value);
		}
	}
}
