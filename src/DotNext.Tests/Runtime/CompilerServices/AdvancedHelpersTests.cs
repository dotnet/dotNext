using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices;

public class AdvancedHelpersTests : Test
{
    private Guid field;
    private string str;

    [Fact]
    public void FieldTypedReferenceValueType()
    {
        TypedReference reference = __makeref(field);
        ref Guid g = ref reference.AsRef<Guid>();
        Equal(Guid.Empty, g);
        g = Guid.NewGuid();
        Equal(field, g);
        True(Unsafe.AreSame(in field, in g));
    }

    [Fact]
    public void FieldTypedReferenceClass()
    {
        TypedReference reference = __makeref(str);
        ref string f = ref reference.AsRef<string>();
        Null(f);
        f = "Hello, world!";
        Equal(str, f);
        True(Unsafe.AreSame(in str, in f));
    }

    [Fact]
    public static void SwapValues()
    {
        var x = 10;
        var y = 20;
        RuntimeHelpers.Swap(ref x, ref y);
        Equal(20, x);
        Equal(10, y);
    }

    [Fact]
    public static void AddressOfLocal()
    {
        var i = 20;
        True(Unsafe.AddressOf(in i) != IntPtr.Zero);
    }

    [Fact]
    public static void CopyBlock()
    {
        char[] chars1 = new[] { 'a', 'b', 'c' };
        var chars2 = new char[2];
        Unsafe.Copy(in chars1[1], out chars2[0], (nuint)2);
        Equal('b', chars2[0]);
        Equal('c', chars2[1]);
    }

    [Fact]
    public static void IsNullable()
    {
        True(RuntimeHelpers.IsNullable<string>());
        True(RuntimeHelpers.IsNullable<ValueType>());
        True(RuntimeHelpers.IsNullable<int?>());
        False(RuntimeHelpers.IsNullable<int>());
        False(RuntimeHelpers.IsNullable<IntPtr>());
    }

    [Fact]
    public static void RefTypeDefaultTest()
    {
        True(RuntimeHelpers.IsDefault<string>(null));
        False(RuntimeHelpers.IsDefault(""));
    }

    [Fact]
    public static void StructTypeDefaultTest()
    {
        var value = default(Guid);
        True(RuntimeHelpers.IsDefault(value));
        value = Guid.NewGuid();
        False(RuntimeHelpers.IsDefault(value));
    }

    [Fact]
    public static void SmallStructDefault()
    {
        True(RuntimeHelpers.IsDefault(default(long)));
        False(RuntimeHelpers.IsDefault(42L));
        True(RuntimeHelpers.IsDefault(default(int)));
        False(RuntimeHelpers.IsDefault(42));
        True(RuntimeHelpers.IsDefault(default(byte)));
        False(RuntimeHelpers.IsDefault((byte)42));
        True(RuntimeHelpers.IsDefault(default(short)));
        False(RuntimeHelpers.IsDefault((short)42));
    }

    [Fact]
    public static void LightweightTypeOf()
    {
        var handle = string.TypeId;
        Equal(typeof(string).TypeHandle, handle);
        NotEqual(typeof(StringComparer).TypeHandle, handle);
    }

    [Fact]
    public static void CastObject()
    {
        Throws<InvalidCastException>(() => string.Cast(null));
        Throws<InvalidCastException>(() => string.Cast(20));
        Throws<InvalidCastException>(() => Nullable<int>.Cast(null));
        Equal(42, Nullable<int>.Cast(42));
    }

    [Fact]
    public static void ExactTypeCheck()
    {
        object obj = 12;
        True(int.IsExactTypeOf(obj));
        False(long.IsExactTypeOf(obj));
        obj = string.Empty;
        True(string.IsExactTypeOf(obj));
        False(object.IsExactTypeOf(obj));
        obj = Array.Empty<string>();
        True(BasicExtensions.IsExactTypeOf<string[]>(obj));
        False(BasicExtensions.IsExactTypeOf<object[]>(obj));
    }

    [Fact]
    public static void ArrayLength()
    {
        int[] array = { 42 };
        Equal(1U, Array.GetLength(array));
        array = Array.Empty<int>();
        Equal(0U, Array.GetLength(array));
        Equal(4U, Array.GetLength(new int[2, 2]));
    }

    [Fact]
    public static void HasFinalizer()
    {
        True(RuntimeHelpers.HasFinalizer(new WeakReference(null)));
        False(RuntimeHelpers.HasFinalizer(string.Empty));
        False(RuntimeHelpers.HasFinalizer(new object()));
    }

    [Fact]
    public static void TypeAlignment()
    {
        Equal(sizeof(int), int.Alignment);
        Equal(sizeof(long), long.Alignment);
        Equal(sizeof(long), ValueTuple<byte, long>.Alignment);
        Equal(nint.Size, string.Alignment);
    }
}