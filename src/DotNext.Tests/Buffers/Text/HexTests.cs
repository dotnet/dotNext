using System.Text;

namespace DotNext.Buffers.Text;

public sealed class HexTests : Test
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(10, true)]
    [InlineData(24, true)]
    [InlineData(25, true)]
    [InlineData(128, true)]
    [InlineData(2048, true)]
    [InlineData(0, false)]
    [InlineData(7, false)]
    [InlineData(10, false)]
    [InlineData(24, false)]
    [InlineData(25, false)]
    [InlineData(128, false)]
    [InlineData(2048, false)]
    public static void ToUtf16(int arraySize, bool lowercased)
    {
        var data = RandomBytes(arraySize);
        Equal(ToHexSlow(data, lowercased), Hex.EncodeToUtf16(data, lowercased));
    }

    [Fact]
    public static void ToUtf16ConversionVarLength()
    {
        ReadOnlySpan<byte> data = new byte[] { 1, 2 };
        char[] encoded = new char[1];
        Equal(0, Hex.EncodeToUtf16(data, encoded));
        encoded = new char[2];
        Equal(2, Hex.EncodeToUtf16(data, encoded));
        Equal('0', encoded[0]);
        Equal('1', encoded[1]);
    }

    [Fact]
    public static void FromUtf16ConversionVarLength()
    {
        ReadOnlySpan<char> data = new char[] { 'F', 'F', 'A' };
        var decoded = new byte[1];
        Equal(1, Hex.DecodeFromUtf16(data, decoded));
        Equal(byte.MaxValue, decoded[0]);
        data = "ABBA".AsSpan();
        decoded = new byte[2];
        Equal(2, Hex.DecodeFromUtf16(data, decoded));
        Equal(0xAB, decoded[0]);
        Equal(0xBA, decoded[1]);
        data = "abba".AsSpan();
        Equal(2, Hex.DecodeFromUtf16(data, decoded));
        Equal(0xAB, decoded[0]);
        Equal(0xBA, decoded[1]);
        data = default;
        Equal(0, Hex.DecodeFromUtf16(data, decoded));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(128, true)]
    [InlineData(2048, true)]
    [InlineData(0, false)]
    [InlineData(128, false)]
    [InlineData(2048, false)]
    [Obsolete]
    public static void FromUtf16(int arraySize, bool lowercased)
    {
        var expected = RandomBytes(arraySize);
        ReadOnlySpan<char> hex = ToHexSlow(expected, lowercased);

        var actual = new byte[expected.Length];
        Equal(actual.Length, Hex.DecodeFromUtf16(hex, actual));
        Equal(expected, actual);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(10, true)]
    [InlineData(24, true)]
    [InlineData(25, true)]
    [InlineData(128, true)]
    [InlineData(2048, true)]
    [InlineData(0, false)]
    [InlineData(7, false)]
    [InlineData(10, false)]
    [InlineData(24, false)]
    [InlineData(25, false)]
    [InlineData(128, false)]
    [InlineData(2048, false)]
    public static void ToUtf8(int arraySize, bool lowercased)
    {
        var data = RandomBytes(arraySize);
        Equal(ToHexSlow(data, lowercased), Encoding.UTF8.GetString(Hex.EncodeToUtf8(data, lowercased)));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(128, true)]
    [InlineData(2048, true)]
    [InlineData(0, false)]
    [InlineData(128, false)]
    [InlineData(2048, false)]
    [Obsolete]
    public static void FromUtf8(int arraySize, bool lowercased)
    {
        var expected = RandomBytes(arraySize);
        ReadOnlySpan<byte> hex = Encoding.UTF8.GetBytes(ToHexSlow(expected, lowercased));

        var actual = new byte[expected.Length];
        Equal(actual.Length, Hex.DecodeFromUtf8(hex, actual));
        Equal(expected, actual);
    }

    private static string ToHexSlow(byte[] data, bool lowercased)
    {
        var str = Convert.ToHexString(data);
        if (lowercased)
            str = str.ToLowerInvariant();

        return str;
    }
}