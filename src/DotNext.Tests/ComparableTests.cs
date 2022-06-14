using System.Diagnostics.CodeAnalysis;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class ComparableTests : Test
    {
        [Fact]
        public static void ClampTest()
        {
            Equal(20M, 10M.Clamp(20M, 30M));
            Equal(25M, 25M.Clamp(20M, 30M));
            Equal(30M, 40M.Clamp(20M, 30M));
        }

        [Fact]
        public static void BetweenTest()
        {
            True(15M.IsBetween(10M, 20M));
            False(10M.IsBetween(10M, 20M, BoundType.Open));
            True(10M.IsBetween(10M, 20M, BoundType.LeftClosed));
            False(15M.IsBetween(10M, 12M));
        }

        [Fact]
        public static void LeftGreaterThanRight()
        {
            False(4L.IsBetween(4L, 3L, BoundType.Closed));
            False(4L.IsBetween(4L, 4L, BoundType.LeftClosed));
        }
    }
}
