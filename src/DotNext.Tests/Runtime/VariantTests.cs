using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using DotNext.Reflection;

namespace DotNext.Runtime;

public sealed class VariantTests : Test
{
    [Fact]
    public static void EmptyValue()
    {
        True(Variant.Empty.IsEmpty);
        False(Variant.Empty.IsMutable);
        True(Variant.Empty == default);
        False(Variant.Empty != default);
        False(Variant.Empty.IsTypeOf<bool>());
        
        Same(typeof(void), Variant.Empty.TargetType);
    }

    [Fact]
    public static void BoxedValue()
    {
        ValueType value = 42;
        var variant = Variant.Mutable(value);
        True(variant.IsMutable);
        Same(typeof(int), variant.TargetType);
        True(variant.IsTypeOf<int>());
        
        variant.Mutable<int>() = 43;
        Equal(43, value);
    }

    [Fact]
    public static void ToObject()
    {
        Null(Variant.Empty.ToObject());

        var i = 42;
        Equal(42, Variant.Immutable(in i).ToObject());

        var str = "Hello, world!";
        Same(str, Variant.Immutable(in str).ToObject());
    }

    [Fact]
    public static void Immutability()
    {
        var i = 42;
        False(Variant.Immutable(in i).IsMutable);
        True(Variant.Mutable(ref i).IsMutable);
    }

    [Fact]
    public static void Equality()
    {
        var i = 42;
        var x = Variant.Immutable(in i);
        var y = Variant.Immutable(in i);
        
        True(x == y);
    }

    [Fact]
    public static void ImmutableToMutable()
    {
        var i = 42;
        var v = Variant.Immutable(in i);
        Equal(i, v.Immutable<int>());

        var thrown = false;
        try
        {
            v.Mutable<int>() = 42;
        }
        catch (InvalidCastException)
        {
            thrown = true;
        }
        
        True(thrown);
    }

    [Fact]
    public static void FromLocalReference()
    {
        var i = 42;
        var reference = new LocalReference<int>(ref i);
        Variant v = reference;
        True(v.IsMutable);
        i = 56;
        Equal(56, v.Mutable<int>());

        ReadOnlyLocalReference<int> roRef = reference;
        v = roRef;
        False(v.IsMutable);
        Equal(56, v.Immutable<int>());
    }

    [Fact]
    public static void FromTypedReference()
    {
        var i = 42;
        Variant v = __makeref(i);
        v.Mutable<int>() = 56;
        Equal(56, i);

        v = default(TypedReference);
        True(v.IsEmpty);
    }

    [Fact]
    [DynamicDependency(DynamicallyAccessedMemberTypes.AllFields, typeof(StrongBox<int>))]
    public static void FromInstanceField()
    {
        var obj = new StrongBox<int> { Value = 42 };
        var field = typeof(StrongBox<int>).GetField(nameof(StrongBox<>.Value));
        NotNull(field);
        var value = Variant.FromField(obj, field);
        True(value.IsMutable);
        True(Unsafe.AreSame(in obj.Value, in value.Mutable<int>()));
    }

    [Fact]
    [DynamicDependency(DynamicallyAccessedMemberTypes.AllFields, typeof(string))]
    public static void FromStaticField()
    {
        var field = typeof(string).GetField(nameof(string.Empty));
        NotNull(field);
        Throws<ArgumentException>(() => Variant.FromField("", field));
    }

    [Fact]
    public static void MutableToImmutable()
    {
        var i = 42;
        var mutable = Variant.Mutable(ref i);
        True(mutable.IsMutable);

        var immutable = Variant.Immutable(mutable);
        False(immutable.IsMutable);
        True(mutable.Equals(immutable));

        immutable = Variant.Immutable(immutable);
        False(immutable.IsMutable);
        True(mutable.Equals(immutable));

        immutable = Variant.Empty;
        False(immutable.IsMutable);
        immutable = Variant.Immutable(immutable);
        Equal(typeof(void), immutable.TargetType);
    }

    [Fact]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(int?))]
    public static void VariantToString()
    {
        Null(Variant.Empty.ToString());
        
        var span = ReadOnlySpan<byte>.Empty;
        var immutable = Variant.Immutable(in span);
        Equal(typeof(ReadOnlySpan<byte>).ToString(), immutable.ToString());

        int? nullable = null;
        immutable = Variant.Immutable(in nullable);
        Null(immutable.ToString());

        nullable = 42;
        Equal(nullable.ToString(), immutable.ToString());

        immutable = Variant.Immutable(in string.Empty);
        Equal(string.Empty, immutable.ToString());
    }

    [Fact]
    public static void ClearVariant()
    {
        Throws<InvalidOperationException>(static () => Variant.Empty.Clear());

        var i = 42;
        var v = Variant.Mutable(ref i);
        v.Clear();
        Equal(0, i);

        var str = string.Empty;
        v = Variant.Mutable(ref str);
        v.Clear();
        Null(str);

        var nullable = new int?(42);
        v = Variant.Mutable(ref nullable);
        v.Clear();
        Null(nullable);
    }

    [Fact]
    public static void VariantCopyTo()
    {
        True(Variant.Empty.TryCopyTo(Variant.Empty));

        int sourceInt = 42, destInt = 0;
        True(Variant.Immutable(in sourceInt).TryCopyTo(Variant.Mutable(ref destInt)));
        Equal(sourceInt, destInt);

        int? sourceNullable = 42, destNullable = null;
        True(Variant.Immutable(in sourceNullable).TryCopyTo(Variant.Mutable(ref destNullable)));
        Equal(sourceNullable, destNullable);

        string sourceObj = string.Empty, destObj = null;
        True(Variant.Immutable(in sourceObj).TryCopyTo(Variant.Mutable(ref destObj)));
        Equal(sourceObj, destObj);
    }

    [Fact]
    public static void VariantCopyToNullable()
    {
        int sourceInt = 42;
        int? destInt = null;
        True(Variant.Immutable(in sourceInt).TryCopyTo(Variant.Mutable(ref destInt)));
        Equal(sourceInt, destInt);

        sourceInt = 43;
        True(Variant.Immutable(in sourceInt).TryCopyTo(Variant.Mutable(ref destInt)));
        Equal(sourceInt, destInt);

        // contains references, the value is located at non-zero offset
        var sourcePair = new KeyValuePair<string, int>("a", 1);
        KeyValuePair<string, int>? destPair = null;
        True(Variant.Immutable(in sourcePair).TryCopyTo(Variant.Mutable(ref destPair)));
        Equal(sourcePair, destPair);

        int? sourceNullable = 1;
        int destPlain = 0;
        False(Variant.Immutable(in sourceNullable).TryCopyTo(Variant.Mutable(ref destPlain)));
        Equal(0, destPlain);

        False(Variant.Immutable(in sourceInt).TryCopyTo(Variant.Immutable(in destInt)));
    }

    [Fact]
    public static void VariantSetValue()
    {
        var i = 0;
        True(Variant.Mutable(ref i).TrySetValue(42));
        Equal(42, i);
        False(Variant.Mutable(ref i).TrySetValue(null));
        False(Variant.Mutable(ref i).TrySetValue(42L));
        False(Variant.Mutable(ref i).TrySetValue(DayOfWeek.Monday));
        False(Variant.Immutable(in i).TrySetValue(43));
        Equal(42, i);
        False(Variant.Empty.TrySetValue(null));

        int? ni = null;
        True(Variant.Mutable(ref ni).TrySetValue(42));
        Equal(42, ni);
        True(Variant.Mutable(ref ni).TrySetValue(null));
        Null(ni);

        KeyValuePair<string, int>? pair = null;
        True(Variant.Mutable(ref pair).TrySetValue(new KeyValuePair<string, int>("a", 1)));
        Equal(new KeyValuePair<string, int>("a", 1), pair);

        var s = "a";
        True(Variant.Mutable(ref s).TrySetValue("b"));
        Equal("b", s);
        False(Variant.Mutable(ref s).TrySetValue(42));
        True(Variant.Mutable(ref s).TrySetValue(null));
        Null(s);

        // boxed value is stored as-is
        object boxed = 42;
        ValueType vt = 0;
        True(Variant.Mutable(ref vt).TrySetValue(boxed));
        Same(boxed, vt);
    }

    [Fact]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicFields, typeof(PointerHolder))]
    public static unsafe void PointerField()
    {
        var value = 42;
        var holder = new PointerHolder { Pointer1 = &value, Pointer2 = null };
        var source = typeof(PointerHolder).GetField(nameof(PointerHolder.Pointer1));
        var destination = typeof(PointerHolder).GetField(nameof(PointerHolder.Pointer2));
        NotNull(source);
        NotNull(destination);

        Equal(typeof(int*), Variant.FromField(holder, source).TargetType);
        True(Pointer.Unbox(Variant.FromField(holder, source).ToObject()!) == &value);

        True(Variant.FromField(holder, source).TryCopyTo(Variant.FromField(holder, destination)));
        True(holder.Pointer2 == &value);

        Variant.FromField(holder, destination).Clear();
        True(holder.Pointer2 is null);

        True(Variant.FromField(holder, destination).TrySetValue(Variant.FromField(holder, source).ToObject()));
        True(holder.Pointer2 == &value);
        True(Variant.FromField(holder, destination).TrySetValue(Pointer.Box(null, typeof(int*))));
        True(holder.Pointer2 is null);
        False(Variant.FromField(holder, destination).TrySetValue(null));
        False(Variant.FromField(holder, destination).TrySetValue((nint)42));
    }

    [Fact]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicFields, typeof(PointerHolder))]
    public static unsafe void FunctionPointerField()
    {
        var holder = new PointerHolder { FunctionPointer1 = &PointerHolder.Target, FunctionPointer2 = null };
        var source = typeof(PointerHolder).GetField(nameof(PointerHolder.FunctionPointer1));
        var destination = typeof(PointerHolder).GetField(nameof(PointerHolder.FunctionPointer2));
        NotNull(source);
        NotNull(destination);

        True(Variant.FromField(holder, source).TargetType.IsFunctionPointer);
        Equal((nint)holder.FunctionPointer1, Variant.FromField(holder, source).ToObject());

        True(Variant.FromField(holder, source).TryCopyTo(Variant.FromField(holder, destination)));
        Equal((nint)holder.FunctionPointer1, (nint)holder.FunctionPointer2);

        Variant.FromField(holder, destination).Clear();
        True(holder.FunctionPointer2 is null);

        True(Variant.FromField(holder, destination).TrySetValue(Variant.FromField(holder, source).ToObject()));
        Equal((nint)holder.FunctionPointer1, (nint)holder.FunctionPointer2);
        True(Variant.FromField(holder, destination).TrySetValue(nint.Zero));
        True(holder.FunctionPointer2 is null);
        False(Variant.FromField(holder, destination).TrySetValue(null));
    }
}

file sealed unsafe class PointerHolder
{
    public int* Pointer1, Pointer2;
    public delegate*<void> FunctionPointer1, FunctionPointer2;

    public static void Target()
    {
    }
}