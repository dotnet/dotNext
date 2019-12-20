using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class ComparableTests : Assert
    {
        [Fact]
        public static void ClampTest()
        {
            Equal(20M, 10M.Clamp(20M, 30M));
            Equal(25M, 25M.Clamp(20M, 30M));
            Equal(30M, 40M.Clamp(20M, 30M));
        }

        [Fact]
        public static void RestrictionTest()
        {
            Equal(30M, 20M.Max(30M));
            Equal(10M, 10M.Min(30M));
        }

        [Fact]
        public static void BetweenTest()
        {
            True(15M.Between(10M, 20M));
            False(10M.Between(10M, 20M, BoundType.Open));
            True(10M.Between(10M, 20M, BoundType.LeftClosed));
            False(15M.Between(10M, 12M));
        }
    }
}
