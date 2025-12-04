namespace DotNext.Runtime.InteropServices;

public sealed class OpaqueValueTests : Test
{
    [Fact]
    public static void PrimitiveTypes()
    {
        using var opaque = new OpaqueValue<int>(42);
        Equal(42, opaque.Unbox());
    }

    [Fact]
    public static void BlittableTypes()
    {
        var g = Guid.NewGuid();
        using var opaque = new OpaqueValue<Guid>(g);
        Equal(g, opaque.Unbox());
    }

    [Fact]
    public static void ReferenceTypes()
    {
        const string expected = "Hello, world!";
        using var opaque = new OpaqueValue<string>(expected);
        Equal(expected, opaque.Value);
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
}