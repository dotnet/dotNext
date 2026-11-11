using System.Diagnostics.CodeAnalysis;
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
        
        Same(typeof(void), Variant.Empty.TargetType);
    }

    [Fact]
    public static void BoxedValue()
    {
        ValueType value = 42;
        var variant = Variant.Mutable(value);
        True(variant.IsMutable);
        Same(typeof(int), variant.TargetType);
        
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
        v.Immutable<int>();

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
        var field = obj.GetType().GetField(nameof(StrongBox<>.Value));
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
}