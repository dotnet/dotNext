using System.Runtime.CompilerServices;

namespace DotNext.Runtime;

public sealed class ValueReferenceTests : Test
{
    [Fact]
    public static void MutableFieldRef()
    {
        var obj = new MyClass() { AnotherField = string.Empty };
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
        var obj = new MyClass() { AnotherField = string.Empty };
        var reference = new ReadOnlyValueReference<int>(obj, in obj.Field);

        obj.Field = 20;
        Equal(obj.Field, reference.Value);
        
        Equal(obj.Field, reference.Value);
        Empty(obj.AnotherField);
    }
    
    [Fact]
    public static void MutableToImmutableRef()
    {
        var obj = new MyClass() { AnotherField = string.Empty };
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
        var obj = new MyClass() { AnotherField = string.Empty };
        var reference1 = new ValueReference<int>(obj, ref obj.Field);
        var reference2 = new ValueReference<int>(obj, ref obj.Field);

        Equal(reference1, reference2);
    }

    [Fact]
    public static void ImmutableRefEquality()
    {
        var obj = new MyClass() { AnotherField = string.Empty };
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
        var reference = default(ValueReference<float>);
        True(reference.IsEmpty);
        Null(reference.ToString());
    }
    
    [Fact]
    public static void ImmutableEmptyRef()
    {
        var reference = default(ReadOnlyValueReference<float>);
        True(reference.IsEmpty);
        Null(reference.ToString());
    }

    private record class MyClass : IResettable
    {
        internal int Field;
        internal string AnotherField;

        public virtual void Reset()
        {
            
        }
    }
}