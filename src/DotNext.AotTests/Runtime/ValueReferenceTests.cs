using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime;

[TestClass]
public class ValueReferenceTests
{
    [TestMethod]
    public void MutableFieldRef()
    {
        var obj = new MyClass { AnotherField = string.Empty };
        var reference = new ValueReference<int>(obj, ref obj.Field);

        obj.Field = 20;
        Assert.AreEqual(obj.Field, reference.Value);

        reference.Value = 42;
        Assert.AreEqual(obj.Field, reference.Value);
        Assert.IsTrue(string.IsNullOrEmpty(obj.AnotherField));
    }
    
    [TestMethod]
    public void ImmutableFieldRef()
    {
        var obj = new MyClass { AnotherField = string.Empty };
        var reference = new ReadOnlyValueReference<int>(obj, in obj.Field);

        obj.Field = 20;
        Assert.AreEqual(obj.Field, reference.Value);
        
        Assert.AreEqual(obj.Field, reference.Value);
        Assert.IsTrue(string.IsNullOrEmpty(obj.AnotherField));
    }
    
    [TestMethod]
    public void MutableToImmutableRef()
    {
        var obj = new MyClass { AnotherField = string.Empty };
        var reference = new ValueReference<int>(obj, ref obj.Field);
        ReadOnlyValueReference<int> roReference = reference;

        obj.Field = 20;
        Assert.AreEqual(roReference.Value, reference.Value);

        reference.Value = 42;
        Assert.AreEqual(roReference.Value, reference.Value);
    }
    
    [TestMethod]
    public void MutableRefEquality()
    {
        var obj = new MyClass { AnotherField = string.Empty };
        var reference1 = new ValueReference<int>(obj, ref obj.Field);
        var reference2 = new ValueReference<int>(obj, ref obj.Field);

        Assert.AreEqual(reference1, reference2);
    }

    [TestMethod]
    public void ImmutableRefEquality()
    {
        var obj = new MyClass { AnotherField = string.Empty };
        var reference1 = new ReadOnlyValueReference<int>(obj, in obj.Field);
        var reference2 = new ReadOnlyValueReference<int>(obj, in obj.Field);

        Assert.AreEqual(reference1, reference2);
    }

    [TestMethod]
    public void ReferenceToArray()
    {
        var array = new string[1];
        var reference = new ValueReference<string>(array, 0)
        {
            Value = "Hello, world!"
        };

        Assert.AreSame(array[0], reference.Value);
        Assert.AreSame(array[0], reference.ToString());
    }

    [TestMethod]
    public void MutableEmptyRef()
    {
        var reference = default(ValueReference<string>);
        Assert.IsTrue(reference.IsEmpty);
        Assert.IsNull(reference.ToString());

        Span<string> span = reference;
        Assert.IsTrue(span.IsEmpty);

        Assert.ThrowsException<NullReferenceException>((Func<string>)reference);
        Assert.ThrowsException<NullReferenceException>(((Action<string>)reference).Bind(string.Empty));
    }

    [TestMethod]
    public void ImmutableEmptyRef()
    {
        var reference = default(ReadOnlyValueReference<string>);
        Assert.IsTrue(reference.IsEmpty);
        Assert.IsNull(reference.ToString());
        
        ReadOnlySpan<string> span = reference;
        Assert.IsTrue(span.IsEmpty);
        
        Assert.ThrowsException<NullReferenceException>((Func<string>)reference);
    }

    [TestMethod]
    public void AnonymousValue()
    {
        var reference = new ValueReference<int>(42);
        Assert.AreEqual(42, reference.Value);

        ((Action<int>)reference).Invoke(52);
        Assert.AreEqual(52, ToFunc<ValueReference<int>, int>(reference).Invoke());

        ReadOnlyValueReference<int> roRef = reference;
        Assert.AreEqual(52, roRef.Value);
        Assert.AreEqual(52, ToFunc<ReadOnlyValueReference<int>, int>(reference).Invoke());
    }

    private static Func<T> ToFunc<TSupplier, T>(TSupplier supplier)
        where TSupplier : ISupplier<T>
        => supplier.ToDelegate();

    [TestMethod]
    public void IncorrectReference()
    {
        byte[] empty = [];
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ValueReference<byte>(empty, ref MemoryMarshal.GetArrayDataReference(empty)));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ReadOnlyValueReference<byte>(empty, ref MemoryMarshal.GetArrayDataReference(empty)));
    }

    [TestMethod]
    public void ReferenceSize()
    {
        Assert.AreEqual(Unsafe.SizeOf<ValueReference<float>>(), nint.Size + nint.Size);
    }

    [TestMethod]
    public void BoxedValueInterop()
    {
        var boxedInt = BoxedValue<int>.Box(42);
        ValueReference<int> reference = boxedInt;

        boxedInt.Unbox() = 56;
        Assert.AreEqual(boxedInt, reference.Value);
    }

    [TestMethod]
    public void ArrayCovariance()
    {
        string[] array = ["a", "b"];
        Assert.ThrowsException<ArrayTypeMismatchException>(() => new ValueReference<object>(array, 0));

        var roRef = new ReadOnlyValueReference<object>(array, 1);
        Assert.AreEqual("b", roRef.Value);
    }

    [TestMethod]
    public void SpanInterop()
    {
        var reference = new ValueReference<int>(42);
        Span<int> span = reference;
        Assert.AreEqual(1, span.Length);

        Assert.IsTrue(Unsafe.AreSame(in reference.Value, in span[0]));
    }
    
    [TestMethod]
    public void ReadOnlySpanInterop()
    {
        ReadOnlyValueReference<int> reference = new ValueReference<int>(42);
        ReadOnlySpan<int> span = reference;
        Assert.AreEqual(1, span.Length);

        Assert.IsTrue(Unsafe.AreSame(in reference.Value, in span[0]));
    }

    private record MyClass
    {
        internal int Field;
        internal string? AnotherField;
    }
}