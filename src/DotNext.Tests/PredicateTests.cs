using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class PredicateTests : Assert
    {
        [Fact]
        public static void PredefinedDelegatesTest()
        {
            True(Predicate.True<string>().Invoke(""));
            False(Predicate.False<int>().Invoke(0));
            True(Predicate.IsNull<string>().Invoke(null));
            False(Predicate.IsNull<string>().Invoke(""));
            False(Predicate.IsNotNull<string>().Invoke(null));
            True(Predicate.IsNotNull<string>().Invoke(""));
        }

        [Fact]
        public static void NegateTest()
        {
            False(Predicate.IsNull<string>().Negate().Invoke(null));
            True(Predicate.IsNull<string>().Negate().Invoke(""));
        }

        [Fact]
        public static void ConversionTest()
        {
            True(Predicate.AsConverter<string>(str => str.Length == 0).Invoke(""));
            False(Predicate.AsFunc<string>(str => str.Length > 0).Invoke(""));
        }
    }
}
