using Xunit;

namespace DotNext.Reflection
{
    public sealed class ConversionTest: Assert
    {
        [Fact]
        public void ImplicitOperatorTest()
        {
            Equal(10, Conversion<decimal, int>.Converter(10M));
        }
    }
}
