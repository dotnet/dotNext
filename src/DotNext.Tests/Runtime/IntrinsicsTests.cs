using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using BinaryPrimitives = System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Runtime;

public class IntrinsicsTests : Test
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
        Intrinsics.Swap(ref x, ref y);
        Equal(20, x);
        Equal(10, y);
    }

    [Fact]
    public static unsafe void SwapValuesByPointer()
    {
        var x = 10;
        var y = 20;
        Intrinsics.Swap(&x, &y);
        Equal(20, x);
        Equal(10, y);
    }

    [Fact]
    public static void AddressOfLocal()
    {
        var i = 20;
        True(Intrinsics.AddressOf(in i) != IntPtr.Zero);
    }

    [Fact]
    public static unsafe void BitwiseEqualityForByte()
    {
        byte value1 = 10;
        byte value2 = 20;
        False(Intrinsics.Equals(&value1, &value2, (nuint)sizeof(byte)));
        value2 = 10;
        True(Intrinsics.Equals(&value1, &value2, (nuint)sizeof(byte)));
    }

    [Fact]
    public static unsafe void BitwiseEqualityForLong()
    {
        var value1 = 10L;
        var value2 = 20L;
        False(Intrinsics.Equals(&value1, &value2, (nuint)sizeof(long)));
        value2 = 10;
        True(Intrinsics.Equals(&value1, &value2, (nuint)sizeof(long)));
    }

    [Fact]
    public static void CopyBlock()
    {
        char[] chars1 = new[] { 'a', 'b', 'c' };
        var chars2 = new char[2];
        Intrinsics.Copy(in chars1[1], out chars2[0], (nuint)2);
        Equal('b', chars2[0]);
        Equal('c', chars2[1]);
    }

    [Fact]
    public static unsafe void CopyValue()
    {
        int a = 42, b = 0;
        Intrinsics.Copy(&a, &b);
        Equal(a, b);
        Equal(42, b);
    }

    [Fact]
    public static void IsNullable()
    {
        True(Intrinsics.IsNullable<string>());
        True(Intrinsics.IsNullable<ValueType>());
        True(Intrinsics.IsNullable<int?>());
        False(Intrinsics.IsNullable<int>());
        False(Intrinsics.IsNullable<IntPtr>());
    }

    [Fact]
    public static void RefTypeDefaultTest()
    {
        True(Intrinsics.IsDefault<string>(default));
        False(Intrinsics.IsDefault(""));
    }

    [Fact]
    public static void StructTypeDefaultTest()
    {
        var value = default(Guid);
        True(Intrinsics.IsDefault(value));
        value = Guid.NewGuid();
        False(Intrinsics.IsDefault(value));
    }

    [Fact]
    public static void SmallStructDefault()
    {
        True(Intrinsics.IsDefault(default(long)));
        False(Intrinsics.IsDefault(42L));
        True(Intrinsics.IsDefault(default(int)));
        False(Intrinsics.IsDefault(42));
        True(Intrinsics.IsDefault(default(byte)));
        False(Intrinsics.IsDefault((byte)42));
        True(Intrinsics.IsDefault(default(short)));
        False(Intrinsics.IsDefault((short)42));
    }

    [Fact]
    public static void LightweightTypeOf()
    {
        var handle = Intrinsics.TypeOf<string>();
        Equal(typeof(string).TypeHandle, handle);
        NotEqual(typeof(StringComparer).TypeHandle, handle);
    }

    [Flags]
    private enum ByteEnum : byte
    {
        None = 0,
        One = 1,
        Two = 2,
    }

    [Flags]
    private enum ShortEnum : short
    {
        None = 0,
        One = 1,
        Two = 2,
    }

    [Flags]
    private enum LongEnum : long
    {
        None = 0L,
        One = 1L,
        Two = 2L,
    }

    [Fact]
    public static void CastObject()
    {
        Throws<InvalidCastException>(() => Intrinsics.Cast<string>(null));
        Throws<InvalidCastException>(() => Intrinsics.Cast<string>(20));
        Throws<InvalidCastException>(() => Intrinsics.Cast<int?>(null));
        Equal(42, Intrinsics.Cast<int?>(42));
    }

    [Fact]
    public static void ExactTypeCheck()
    {
        object obj = 12;
        True(Intrinsics.IsExactTypeOf<int>(obj));
        False(Intrinsics.IsExactTypeOf<long>(obj));
        obj = string.Empty;
        True(Intrinsics.IsExactTypeOf<string>(obj));
        False(Intrinsics.IsExactTypeOf<object>(obj));
        obj = Array.Empty<string>();
        True(Intrinsics.IsExactTypeOf<string[]>(obj));
        False(Intrinsics.IsExactTypeOf<object[]>(obj));
    }

    [Fact]
    public static void ArrayLength()
    {
        int[] array = { 42 };
        Equal(new IntPtr(1), (IntPtr)Intrinsics.GetLength(array));
        array = Array.Empty<int>();
        Equal(default, Intrinsics.GetLength(array));
        Equal(new IntPtr(4), (IntPtr)Intrinsics.GetLength(new int[2, 2]));
    }

    [Fact]
    public static void HasFinalizer()
    {
        True(Intrinsics.HasFinalizer(new WeakReference(null)));
        False(Intrinsics.HasFinalizer(string.Empty));
        False(Intrinsics.HasFinalizer(new object()));
    }

    [Fact]
    public static void TypeAlignment()
    {
        Equal(4, Intrinsics.AlignOf<int>());
        Equal(8, Intrinsics.AlignOf<long>());
        Equal(8, Intrinsics.AlignOf<ValueTuple<byte, long>>());
        Equal(IntPtr.Size, Intrinsics.AlignOf<string>());
    }
}