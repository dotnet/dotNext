using System.Diagnostics.CodeAnalysis;

namespace DotNext.Buffers.Text
{
    [ExcludeFromCodeCoverage]
    public sealed class HexTests : Test
    {
        [Theory]
        [InlineData(0, true)]
        [InlineData(7, true)]
        [InlineData(10, true)]
        [InlineData(128, true)]
        [InlineData(2048, true)]
        [InlineData(0, false)]
        [InlineData(7, false)]
        [InlineData(10, false)]
        [InlineData(128, false)]
        [InlineData(2048, false)]
        public static void ToHexConversion(int arraySize, bool lowercased)
        {
            var data = RandomBytes(arraySize);
            Equal(ToHexSlow(data, lowercased), Hex.EncodeToUtf16(data, lowercased));
        }

        [Fact]
        public static void ToHexConversionVarLength()
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
        public static void FromHexConversionVarLength()
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
        public static void FromHexConversion(int arraySize, bool lowercased)
        {
            var data = RandomBytes(arraySize);
            ReadOnlySpan<char> hex = ToHexSlow(data, lowercased);
            Equal(data, hex.FromHex());
        }

        private static string ToHexSlow(byte[] data, bool lowercased)
            => string.Join(string.Empty, Array.ConvertAll(data, i => i.ToString(lowercased ? "x2" : "X2", null)));
    }
}