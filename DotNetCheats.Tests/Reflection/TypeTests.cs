using System;
using System.IO;
using System.Linq.Expressions;
using Xunit;

namespace MissingPieces.Reflection
{
	public sealed class TypeTests: Assert
	{
		internal static event EventHandler StaticEvent;
		private event EventHandler InstanceEvent;

		private struct Point
		{
			public int X, Y;

			public void Zero() => X = Y = 0;
		}

		private delegate void ByRefAction<T>(in T value);

		private delegate R ByRefFunc<T1, T2, R>(in T1 value, T2 arg);

		[Fact]
		public void NonExistentMethodTest()
		{
			Throws<MissingMethodException>(() => Type<string>.Method<StringComparer>.Require(nameof(string.IndexOf)));
		}

		[Fact]
		public void InstanceMethodTest()
		{
			Func<string, char, int> indexOf = Type<string>.Method<char>.Require<int>(nameof(string.IndexOf));
			var result = indexOf("aba", 'b');
			Equal(1, result);

			ByRefFunc<string, char, int> indexOf2 = Type<string>.Method.Custom<ByRefFunc<string, char, int>>(nameof(string.IndexOf));
			NotNull(indexOf2);
			Equal(2, indexOf("abca", 'c'));

			Func<string, char, int, int> indexOf3 = Type<string>.Method<char, int>.Require<int>(nameof(string.IndexOf));
			Equal(1, indexOf3("aba", 'b', 1));

			ByRefAction<Point> zero = Type<Point>.Method.Custom<ByRefAction<Point>>(nameof(Point.Zero));
			NotNull(zero);
			var point = new Point() { X = 10, Y = 20 };
			zero(point);
			Equal(0, point.X);
			Equal(0, point.Y);
			
			var indexOf4 = Type<string>.RequireMethod<(char, int), int>(nameof(string.IndexOf));
			Equal(1, indexOf4.Invoke("aba", ('b', 1)));
		}

		[Fact]
		public void StaticMethodTest()
		{
			Func<string, string, int> compare = Type<string>.Method<string, string>.RequireStatic<int>(nameof(string.Compare));
			True(compare("a", "b") < 0);

			var compare2 = Type<string>.RequireStaticMethod<(string first, string second), int>(nameof(string.Compare));
			True(compare2.Invoke((first: "a", second: "b")) < 0);
		}

		[Fact]
		public void ConstructorTests()
		{
			Func<char, int, string> stringCtor = Type<string>.Constructor<char, int>.Require();
			var str = stringCtor('a', 3);
			Equal("aaa", str);
			Func<object> objCtor = Type<object>.Constructor.Get();
			NotNull(objCtor());
			Throws<MissingConstructorException>(() => Type<string>.Constructor<int, int, string>.Require());
			Func<int, ClassWithProperties> classCtor = Type<ClassWithProperties>.Constructor<int>.Get(true);
			var obj = classCtor(10);
			Equal(10, obj.ReadOnlyProp);
		}

		[Fact]
		public void SpecialConstructorTests()
		{
			var stringCtor = Type<string>.RequireConstructor<(char, int)>();
			var str = stringCtor.Invoke(('a', 3));
			Equal("aaa", str);

			Null(Type<string>.GetConstructor<(bool, bool)>());

			var ctorWithRef = Type<ClassWithProperties>.RequireConstructor<(int first, Ref<bool> second)>();
			var args = ctorWithRef.ArgList();
			args.first = 20;
			args.second = false;
			NotNull(ctorWithRef.Invoke(args));
			True(args.second);
		}

		[Fact]
		public void ValueTypeTests()
		{
			Func<long> longCtor = Type<long>.Constructor.Get();
			Equal(0L, longCtor());
		}

		private sealed class ClassWithProperties
		{
			
			internal static long StaticProp { get; set; }

			private int value;

			public ClassWithProperties(){}

			public ClassWithProperties(int val, out bool result)
			{
				value = val;
				result = true;
			}

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
			var rwProperty = Type<StructWithProperties>.InstanceProperty<string>.GetOrThrow(nameof(StructWithProperties.ReadWriteProperty));
			True(rwProperty.CanRead);
			True(rwProperty.CanWrite);
			NotNull(rwProperty.GetMethod);
			NotNull(rwProperty.SetMethod);
			rwProperty[instance] = "Hello, world";
			Equal("Hello, world", instance.ReadWriteProperty);
			Equal("Hello, world", rwProperty[instance]);
			var wProperty = Type<StructWithProperties>.InstanceProperty<int>.GetOrThrow(nameof(StructWithProperties.WriteOnlyProp));
			True(wProperty.CanWrite);
			False(wProperty.CanRead);
			NotNull(wProperty.SetMethod);
			Null(wProperty.GetMethod);
			wProperty[instance] = 42;
			MemberAccess<StructWithProperties, int> rProperty = Type<StructWithProperties>.InstanceProperty<int>.GetOrThrow(nameof(StructWithProperties.ReadOnlyProp));
			Equal(42, rProperty.GetValue(in instance));
		}

		[Fact]
		public void StructPropertyTest()
		{
			var instance = new StructWithProperties();
			MemberAccess<StructWithProperties, string> rwProperty = Type<StructWithProperties>.InstanceProperty<string>.GetOrThrow(nameof(StructWithProperties.ReadWriteProperty));
			rwProperty.SetValue(instance, "Hello, world");
			Equal("Hello, world", instance.ReadWriteProperty);
			Equal("Hello, world", rwProperty.GetValue(instance));
			var wProperty = Type<StructWithProperties>.InstanceProperty<int>.GetOrThrow(nameof(StructWithProperties.WriteOnlyProp));
			True(wProperty.CanWrite);
			False(wProperty.CanRead);
			NotNull(wProperty.SetMethod);
			Null(wProperty.GetMethod);
			wProperty[instance] = 42;
			var rProperty = Type<StructWithProperties>.InstanceProperty<int>.GetOrThrow(nameof(StructWithProperties.ReadOnlyProp));
			False(rProperty.CanWrite);
			True(rProperty.CanRead);
			Equal(42, rProperty[instance]);
			//Equal(42, ((MemberAccess.Getter<StructWithProperties, int>)rProperty.GetMethod).Invoke(instance));
		}

		[Fact]
		public void StaticPropertyTest()
		{
			var property = Type<ClassWithProperties>.StaticProperty<long>.GetOrThrow(nameof(ClassWithProperties.StaticProp), true);
			True(property.CanRead);
			True(property.CanWrite);
			property.Value = 42;
			Equal(42L, property.Value);
		}

		[Fact]
		public void InstanceEventTest()
		{
			var ev = Type<AppDomain>.Event<ResolveEventHandler>.Require(nameof(AppDomain.TypeResolve));
			ResolveEventHandler handler = (sender, args) => null;
			ev.AddEventHandler(AppDomain.CurrentDomain, handler);
			ev.RemoveEventHandler(AppDomain.CurrentDomain, handler);
			var ev2 = Type<TypeTests>.Event<EventHandler>.Require(nameof(InstanceEvent), true);
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
			var ev = Type<TypeTests>.Event<EventHandler>.RequireStatic(nameof(StaticEvent), true);
			EventHandler handler = (sender, args) => { };
			ev.AddEventHandler(handler);
			Equal(StaticEvent, handler);
			ev.RemoveEventHandler(handler);
			Null(StaticEvent);
		}

		private static long Field;

		[Fact]
		public void StaticFieldTest()
		{
			Func<Guid> structField = Type<Guid>.Field<Guid>.RequireStatic(nameof(Guid.Empty));
			Guid.Empty.Equals(structField());
			Func<TextReader> objField = Type<TextReader>.Field<TextReader>.RequireStatic(nameof(TextReader.Null));
			Same(TextReader.Null, objField());
			var statField = Type<TypeTests>.Field<long>.RequireStatic(nameof(Field), true);
			statField.Value = 42L;
			Equal(Field, statField.Value);
		}

		[Fact]
		public void InstanceFieldTest()
		{
			var s = new StructWithProperties();
			var structField = Type<StructWithProperties>.Field<int>.Require("value", true);
			structField[s] = 42;
			Equal(42, s.ReadOnlyProp);
			Equal(42, structField[s]);

			var obj = new ClassWithProperties();
			var classField = Type<ClassWithProperties>.Field<int>.Require("value", true);
			classField[obj] = 42;
			Equal(42, obj.ReadOnlyProp);
			Equal(42, classField[obj]);
		}
	}
}
