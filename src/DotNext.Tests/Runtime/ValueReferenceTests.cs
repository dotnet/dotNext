using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime;

public sealed class ValueReferenceTests : Test
{
    [Fact]
    public static void MutableFieldRef()
    {
        var obj = new MyClass { AnotherField = string.Empty };
        var reference = new ValueReference<int>(obj, ref obj.Field);

        obj.Field = 20;
        Equal(obj.Field, reference.Value);

        reference.Value = 42;
        Equal(obj.Field, reference.Value);
        Empty(obj.AnotherField);
    }
    
    [Fact]
    public static void ImmutableFieldRef()
    {
        var obj = new MyClass { AnotherField = string.Empty };
        var reference = new ReadOnlyValueReference<int>(obj, in obj.Field);

        obj.Field = 20;
        Equal(obj.Field, reference.Value);
        
        Equal(obj.Field, reference.Value);
        Empty(obj.AnotherField);
    }
    
    [Fact]
    public static void MutableToImmutableRef()
    {
        var obj = new MyClass { AnotherField = string.Empty };
        var reference = new ValueReference<int>(obj, ref obj.Field);
        ReadOnlyValueReference<int> roReference = reference;

        obj.Field = 20;
        Equal(roReference.Value, reference.Value);

        reference.Value = 42;
        Equal(roReference.Value, reference.Value);
    }
    
    [Fact]
    public static void MutableRefEquality()
    {
        var obj = new MyClass { AnotherField = string.Empty };
        var reference1 = new ValueReference<int>(obj, ref obj.Field);
        var reference2 = new ValueReference<int>(obj, ref obj.Field);

        Equal(reference1, reference2);
    }

    [Fact]
    public static void ImmutableRefEquality()
    {
        var obj = new MyClass { AnotherField = string.Empty };
        var reference1 = new ReadOnlyValueReference<int>(obj, in obj.Field);
        var reference2 = new ReadOnlyValueReference<int>(obj, in obj.Field);

        Equal(reference1, reference2);
    }

    [Fact]
    public static void ReferenceToArray()
    {
        var array = new string[1];
        var reference = new ValueReference<string>(array, 0)
        {
            Value = "Hello, world!"
        };

        Same(array[0], reference.Value);
        Same(array[0], reference.ToString());
    }

    [Fact]
    public static void MutableEmptyRef()
    {
        var reference = default(ValueReference<string>);
        True(reference.IsEmpty);
        Null(reference.ToString());
        Throws<NullReferenceException>(() => reference.Value);

        Span<string> span = reference;
        True(span.IsEmpty);

        Throws<NullReferenceException>((Func<string>)reference);
        Throws<NullReferenceException>(((Action<string>)reference).Bind(string.Empty));
    }

    [Fact]
    public static void ImmutableEmptyRef()
    {
        var reference = default(ReadOnlyValueReference<string>);
        True(reference.IsEmpty);
        Null(reference.ToString());
        Throws<NullReferenceException>(() => reference.Value);
        
        ReadOnlySpan<string> span = reference;
        True(span.IsEmpty);
        
        Throws<NullReferenceException>((Func<string>)reference);
    }

    [Fact]
    public static void AnonymousValue()
    {
        var reference = new ValueReference<int>(42);
        Equal(42, reference.Value);

        ((Action<int>)reference).Invoke(52);
        Equal(52, ToFunc<ValueReference<int>, int>(reference).Invoke());

        ReadOnlyValueReference<int> roRef = reference;
        Equal(52, roRef.Value);
        Equal(52, ToFunc<ReadOnlyValueReference<int>, int>(reference).Invoke());
    }

    private static Func<T> ToFunc<TSupplier, T>(TSupplier supplier)
        where TSupplier : ISupplier<T>
        => supplier.ToDelegate();

    [Fact]
    public static void StaticObjectAccess()
    {
        var reference = new ValueReference<string>(ref MyClass.StaticObject)
        {
            Value = "Hello, world",
        };

        GC.Collect(3, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        
        True(reference == new ValueReference<string>(ref MyClass.StaticObject));
        Same(MyClass.StaticObject, reference.Value);
        Same(MyClass.StaticObject, ToFunc<ValueReference<string>, string>(reference).Invoke());
    }
    
    [Fact]
    public static void StaticValueTypeAccess()
    {
        var reference = new ReadOnlyValueReference<int>(in MyClass.StaticValueType);
        MyClass.StaticValueType = 42;

        GC.Collect(3, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        
        True(reference == new ReadOnlyValueReference<int>(in MyClass.StaticValueType));
        Equal(MyClass.StaticValueType, reference.Value);
    }

    [Fact]
    public static void IncorrectReference()
    {
        byte[] empty = [];
        Throws<ArgumentOutOfRangeException>(() => new ValueReference<byte>(empty, ref MemoryMarshal.GetArrayDataReference(empty)));
        Throws<ArgumentOutOfRangeException>(() => new ReadOnlyValueReference<byte>(empty, ref MemoryMarshal.GetArrayDataReference(empty)));
    }

    [Fact]
    public static void ReferenceSize()
    {
        Equal(Unsafe.SizeOf<ValueReference<float>>(), nint.Size + nint.Size);
    }

    [Fact]
    public static void BoxedValueInterop()
    {
        var boxedInt = BoxedValue<int>.Box(42);
        ValueReference<int> reference = boxedInt;

        boxedInt.Unbox() = 56;
        Equal(boxedInt, reference.Value);
    }

    [Fact]
    public static void ArrayCovariance()
    {
        string[] array = ["a", "b"];
        Throws<ArrayTypeMismatchException>(() => new ValueReference<object>(array, 0));

        var roRef = new ReadOnlyValueReference<object>(array, 1);
        Equal("b", roRef.Value);
    }

    [Fact]
    public static void SpanInterop()
    {
        var reference = new ValueReference<int>(42);
        Span<int> span = reference;
        Equal(1, span.Length);

        True(Unsafe.AreSame(in reference.Value, in span[0]));
    }
    
    [Fact]
    public static void ReadOnlySpanInterop()
    {
        ReadOnlyValueReference<int> reference = new ValueReference<int>(42);
        ReadOnlySpan<int> span = reference;
        Equal(1, span.Length);

        True(Unsafe.AreSame(in reference.Value, in span[0]));
    }

    private record MyClass : IResettable
    {
        internal static string StaticObject;
        
        [FixedAddressValueType]
        internal static int StaticValueType;
        
        internal int Field;
        internal string AnotherField;

        public virtual void Reset()
        {
            
        }
    }

    [Fact]
    public static unsafe void PinAnonymousValue()
    {
        ValueReference<int> valueRef = new(42);
        fixed (int* ptr = valueRef)
        {
            Equal(42, *ptr);
        }

        fixed (int* ptr = (ReadOnlyValueReference<int>)valueRef)
        {
            Equal(42, *ptr);
        }
    }
}