using System.Collections;
using System.Runtime.InteropServices;

namespace DotNext.Collections;

using Runtime.InteropServices;

public sealed class BitArrayExtensionsTests : Test
{
    [Fact]
    public static void PopCount()
    {
        var bits = new BitArray(length: 30, defaultValue: true);
        Equal(30L, bits.PopCount);

        bits.SetAll(value: false);
        Equal(0L, bits.PopCount);

        bits[0] = true;
        Equal(1L, bits.PopCount);
    }

    [Fact]
    public static void CreateFromBlittableValue()
    {
        var bits = BitArray.Create(uint.MaxValue);
        Equal(uint.PopCount(uint.MaxValue), bits.PopCount);

        bits = BitArray.Create(1U);
        Equal(uint.PopCount(1U), bits.PopCount);
        True(bits[0]);
        
        bits = BitArray.Create(Int128.One);
        Equal(Int128.PopCount(Int128.One), bits.PopCount);
        True(bits[0]);
    }
    
    [Fact]
    public static void CreateFromSpan()
    {
        var value = uint.MaxValue;
        var bits = BitArray.Create(MemoryMarshal.AsReadOnlyBytes(in value));
        Equal(uint.PopCount(value), bits.PopCount);

        value = 1U;
        bits = BitArray.Create(MemoryMarshal.AsReadOnlyBytes(in value));
        Equal(uint.PopCount(value), bits.PopCount);
        True(bits[0]);
    }
}