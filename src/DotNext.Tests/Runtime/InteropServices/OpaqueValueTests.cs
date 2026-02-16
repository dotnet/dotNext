namespace DotNext.Runtime.InteropServices;

public sealed class OpaqueValueTests : Test
{
    [Fact]
    public static void PrimitiveTypes()
    {
        using var opaque = new OpaqueValue<int>(42);
        Equal(42, opaque.Value);
        
        opaque.Value = 56;
        Equal(56, opaque.Value);
    }

    [Fact]
    public static void BlittableTypes()
    {
        var g = Guid.NewGuid();
        using var opaque = new OpaqueValue<Guid>(g);
        Equal(g, opaque.Value);
        
        opaque.Value = Guid.Empty;
        Equal(Guid.Empty, opaque.Value);
    }

    [Fact]
    public static void ReferenceTypes()
    {
        const string expected = "Hello, world!";
        var opaque = new OpaqueValue<string>(expected);
        Equal(expected, opaque.Value);

        opaque.Value = string.Empty;
        Same(string.Empty, opaque.Value);
        opaque.Dispose();
    }

    [Fact]
    public static void EmptyValue()
    {
        using var opaque = new OpaqueValue<string>();
        Null(opaque.Value);
    }

    [Fact]
    public static void Equality()
    {
        using var expected = new OpaqueValue<int>(42);
        var actual = expected;

        Equal(expected, actual);

        actual = default;
        NotEqual(expected, actual);
    }

    [Fact]
    public static void Marshalling()
    {
        using var expected = new OpaqueValue<int>(42);
        var handle = OpaqueValueMarshaller<int>.ConvertToUnmanaged(expected);
        var actual = OpaqueValueMarshaller<int>.ConvertToManaged(handle);
        Equal(expected.Value, actual.Value);
    }

    [Fact]
    public static unsafe void PointerType()
    {
        var i = 42;
        Pointer<int> ptr = &i;
        using var value = new OpaqueValue<Pointer<int>>(ptr);
        Equal(i, value.Value);

        value.Value = 56;
        Equal(i, value.Value);
    }

    [Fact]
    public static void OnStackReference()
    {
        var i = 42;
        using var value = new OpaqueValue<OnStackReference<int>>(new(ref i));
        Equal(i, value.Value);

        value.Value = 56;
        Equal(i, value.Value);
    }
}