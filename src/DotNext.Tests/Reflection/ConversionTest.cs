using Xunit;

namespace DotNext.Reflection
{
    public sealed class ConversionTest: Assert
    {
        [Fact]
        public static void ImplicitCast()
        {
            Equal(10, Conversion<decimal, int>.Converter(10M));
        }

        [Fact]
        public static void BuiltinConversion()
        {
            Equal(42, Conversion<byte, int>.Converter(42));
        }
    }
}
