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

    public static TheoryData<BitArray, IReadOnlyList<int>> SetBitsData => new()
    {
        { BitArray.Create<ushort>(0B_0000_0000_0000_0000), [] },
        { BitArray.Create<ushort>(0B_0000_0000_0000_0101), [0, 2] },
        { BitArray.Create<ushort>(0B_0000_0000_0000_0111), [0, 1, 2] },
        { BitArray.Create<ushort>(0B_1000_0100_0010_0001), [0, 5, 10, 15] },
    };

    [Theory]
    [MemberData(nameof(SetBitsData))]
    public static void CheckSetBits(BitArray array, IReadOnlyList<int> indices)
    {
        Equal(indices, array.SetBits);
    }

    [Fact]
    public static void SetBitsAcrossWordsAndTail()
    {
        // exercise word-level scanning (multiple machine words) as well as
        // the trailing bytes that don't fill a whole word
        const int length = 100;
        ReadOnlySpan<int> indices = [0, 1, 7, 8, 31, 32, 33, 63, 64, 65, 95, 99];

        var array = new BitArray(length);
        foreach (var index in indices)
        {
            array[index] = true;
        }

        Equal(indices, [.. array.SetBits]);
    }

    [Fact]
    public static void SetBitsEmpty()
    {
        Empty(new BitArray(length: 128).SetBits);
        Empty(new BitArray(length: 0).SetBits);
    }
}