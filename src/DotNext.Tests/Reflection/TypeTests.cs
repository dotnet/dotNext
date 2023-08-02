namespace DotNext.Reflection;

public sealed class TypeTests : Test
{
    internal static event EventHandler StaticEvent;
    private event EventHandler InstanceEvent;

    private struct Point : IEquatable<Point>
    {
        public int X, Y;

        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public void Zero() => X = Y = 0;

        bool IEquatable<Point>.Equals(Point other)
            => X == other.X && Y == other.Y;
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

        ByRefFunc<string, char, int> indexOf2 = Type<string>.Method.Get<ByRefFunc<string, char, int>>(nameof(string.IndexOf), MethodLookup.Instance);
        NotNull(indexOf2);
        Equal(2, indexOf("abca", 'c'));

        Func<string, char, int, int> indexOf3 = Type<string>.Method<char, int>.Require<int>(nameof(string.IndexOf));
        Equal(1, indexOf3("aba", 'b', 1));

        ByRefAction<Point> zero = Type<Point>.Method.Get<ByRefAction<Point>>(nameof(Point.Zero), MethodLookup.Instance);
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
    public static void ConstructorTests()
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
    public static void SpecialConstructorTests()
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
    public static void ValueTypeTests()
    {
        Func<long> longCtor = Type<long>.Constructor.Get();
        Equal(0L, longCtor());
        Func<int, int, Point> pointCtor = Type<Point>.Constructor<int, int>.Get();
        NotNull(pointCtor);
        var point = pointCtor(10, 20);
        Equal(10, point.X);
        Equal(20, point.Y);
        Function<(int, int), Point> pointCtor2 = Type<Point>.RequireConstructor<(int, int)>();
        point = pointCtor2((30, 40));
        Equal(30, point.X);
        Equal(40, point.Y);
    }

    private sealed class ClassWithProperties
    {

        internal static long StaticProp { get; set; }

        internal int value;
        internal volatile int volatileField;

        public ClassWithProperties() { }

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
    public static void InstanceProperty()
    {
        var instance = new StructWithProperties();
        var rwProperty = Type<StructWithProperties>.Property<string>.Require(nameof(StructWithProperties.ReadWriteProperty));
        True(rwProperty.CanRead);
        True(rwProperty.CanWrite);
        NotNull(rwProperty.GetMethod);
        NotNull(rwProperty.SetMethod);
        rwProperty[instance] = "Hello, world";
        Equal("Hello, world", instance.ReadWriteProperty);
        Equal("Hello, world", rwProperty[instance]);
        True(rwProperty.GetValue(instance, out var str));
        Equal("Hello, world", str);
        var wProperty = Type<StructWithProperties>.Property<int>.Require(nameof(StructWithProperties.WriteOnlyProp));
        True(wProperty.CanWrite);
        False(wProperty.CanRead);
        NotNull(wProperty.SetMethod);
        Null(wProperty.GetMethod);
        wProperty[instance] = 42;
        var rProperty = Type<StructWithProperties>.Property<int>.Require(nameof(StructWithProperties.ReadOnlyProp));
        Equal(42, rProperty[instance]);
    }

    [Fact]
    public static void StructProperty()
    {
        var instance = new StructWithProperties();
        var rwProperty = Type<StructWithProperties>.Property<string>.Require(nameof(StructWithProperties.ReadWriteProperty));
        rwProperty[instance] = "Hello, world";
        Equal("Hello, world", instance.ReadWriteProperty);
        Equal("Hello, world", rwProperty.GetValue(instance));
        var wProperty = Type<StructWithProperties>.Property<int>.Require(nameof(StructWithProperties.WriteOnlyProp));
        True(wProperty.CanWrite);
        False(wProperty.CanRead);
        NotNull(wProperty.SetMethod);
        Null(wProperty.GetMethod);
        wProperty[instance] = 42;
        var rProperty = Type<StructWithProperties>.Property<int>.Require(nameof(StructWithProperties.ReadOnlyProp));
        False(rProperty.CanWrite);
        True(rProperty.CanRead);
        Equal(42, rProperty[instance]);
        //Equal(42, ((MemberAccess.Getter<StructWithProperties, int>)rProperty.GetMethod).Invoke(instance));
    }

    [Fact]
    public static void StaticProperty()
    {
        var property = Type<ClassWithProperties>.Property<long>.RequireStatic(nameof(ClassWithProperties.StaticProp), true);
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
    public static void StaticEventTest()
    {
        var ev = Type<TypeTests>.Event<EventHandler>.RequireStatic(nameof(StaticEvent), true);
        EventHandler handler = (sender, args) => { };
        ev.AddEventHandler(handler);
        Equal(StaticEvent, handler);
        ev.RemoveEventHandler(handler);
        Null(StaticEvent);
    }

    private static long Field = 0;
    private static volatile int VolatileField = 0;

    [Fact]
    public static void StaticFieldTest()
    {
        var statField = Type<TypeTests>.Field<long>.RequireStatic(nameof(Field), true);
        statField.Value = 42L;
        Equal(Field, statField.Value);
        MemberGetter<long> getter = statField;
        Equal(42L, getter());
    }

    [Fact]
    public static void StaticVolatileField()
    {
        var statField = Type<TypeTests>.Field<int>.RequireStatic(nameof(VolatileField), true);
        True(statField.GetValue(null, out int value));
        Equal(VolatileField, value);
        True(statField.SetValue(null, 42));
        True(statField.GetValue(null, out value));
        Equal(42, value);
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
        var classField = Type<ClassWithProperties>.Field<int>.Require(nameof(ClassWithProperties.value), true);
        classField[obj] = 42;
        Equal(42, obj.ReadOnlyProp);
        Equal(42, classField[obj]);
    }

    [Fact]
    public void InstanceVolatileFieldTest()
    {
        var obj = new ClassWithProperties();
        var classField = Type<ClassWithProperties>.Field<int>.Require(nameof(ClassWithProperties.volatileField), true);
        True(classField.SetValue(obj, 42));
        True(classField.GetValue(obj, out int value));
        Equal(42, value);
    }

    [Fact]
    public static void InvalidConversionTest()
    {
        False(Type<string>.TryConvert(23, out _));
        False(Type<string>.TryConvert(new object(), out _));
        False(Type<int>.TryConvert(new object(), out _));
        True(Type<IConvertible>.TryConvert(42, out _));
    }

    [Fact]
    public static void GetHashCodeTest()
    {
        Equal("Hello".GetHashCode(), Type<string>.GetHashCode("Hello"));
        NotEqual(new object().GetHashCode(), Type<object>.GetHashCode(new object()));
        var guid = Guid.NewGuid();
        Equal(guid.GetHashCode(), Type<Guid>.GetHashCode(guid));
        NotEqual(BitwiseComparer<Guid>.GetHashCode(guid), Type<Guid>.GetHashCode(guid));
        var point = new Point { X = 10, Y = 20 };
        NotEqual(point.GetHashCode(), Type<Point>.GetHashCode(point));
        Equal(BitwiseComparer<Point>.GetHashCode(point), Type<Point>.GetHashCode(point));
    }

    [Fact]
    public static void BitwiseEqualityTest()
    {
        var guid = Guid.NewGuid();
        True(Type<Guid>.Equals(in guid, in guid));
        var point = new Point { X = 10, Y = 20 };
        True(Type<Point>.Equals(point, point));
        True(Type<string>.Equals(new string("Hello"), "Hello"));
        False(Type<object>.Equals(new object(), new object()));
        True(Type<StructWithProperties>.Equals(new StructWithProperties() { WriteOnlyProp = 20 }, new StructWithProperties { WriteOnlyProp = 20 }));
        False(Type<StructWithProperties>.Equals(new StructWithProperties() { WriteOnlyProp = 10 }, new StructWithProperties { WriteOnlyProp = 20 }));
    }

    public class ClassA
    {
        public int PropertyName { get; set; }
    }

    public class ClassB : ClassA
    {
        public new int PropertyName { get; set; }
    }

    [Fact]
    public void PropertyOverloadingTest()
    {
        MemberGetter<ClassB, int> property = Type<ClassB>.Property<int>.Require(nameof(ClassB.PropertyName));
        var obj = new ClassB() { PropertyName = 42 };
        Equal(42, property(obj));
        Equal(0, ((ClassA)obj).PropertyName);
    }

    [Fact]
    public void StaticIndexerTest()
    {
        var property = Type<TypeWithStaticIndexer>.Indexer<Ref<int>, string>.RequireStatic("MyIndexer");
        True(property.CanRead);
        True(property.CanWrite);
        TypeWithStaticIndexer.BackedArray[1] = "Hello, world";
        Equal("Hello, world", property[1]);
        property[1] = "Barry Burton";
        Equal("Barry Burton", property[1]);
    }

    [Fact]
    public void InstanceIndexerTest()
    {
        var list = new List<long>() { 10, 40, 100 };
        var property = Type<List<long>>.Indexer<Ref<int>, long>.Require();
        True(property.CanRead);
        True(property.CanWrite);
        Equal(40, property[list, 1]);
        Equal(100, property[list, 2]);
        property[list, 1] = 120;
        Equal(120, list[1]);
        Equal(120, property[list, 1]);
    }
}