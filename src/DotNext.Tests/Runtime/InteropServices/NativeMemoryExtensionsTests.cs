using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

public class NativeMemoryExtensionsTests : Test
{
    [Fact]
    public static unsafe void SwapValuesByPointer()
    {
        var x = 10;
        var y = 20;
        NativeMemory.Swap(&x, &y);
        Equal(20, x);
        Equal(10, y);
    }
    
    [Fact]
    public static unsafe void BitwiseEqualityForByte()
    {
        byte value1 = 10;
        byte value2 = 20;
        False(NativeMemory.Equals(&value1, &value2, sizeof(byte)));
        value2 = 10;
        True(NativeMemory.Equals(&value1, &value2, sizeof(byte)));
    }
    
    [Fact]
    public static unsafe void BitwiseEqualityForLong()
    {
        var value1 = 10L;
        var value2 = 20L;
        False(NativeMemory.Equals(&value1, &value2, (nuint)sizeof(long)));
        value2 = 10;
        True(NativeMemory.Equals(&value1, &value2, (nuint)sizeof(long)));
    }
    
    [Fact]
    public static unsafe void CopyValue()
    {
        int a = 42, b = 0;
        NativeMemory.Copy(&a, &b);
        Equal(a, b);
        Equal(42, b);
    }

    [Fact]
    public static unsafe void CopyValueUnaligned()
    {
        int a = 42, b = 0;
        NativeMemory.CopyUnaligned(&a, &b);
        Equal(a, b);
        Equal(42, b);
    }
    
    [Fact]
    public static unsafe void PtrHashCode()
    {
        nint expected = 10;
        Equal(expected.GetHashCode(), NativeMemory.PointerHashCode(expected.ToPointer()));
    }
}