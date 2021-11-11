using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.CompilerServices
{
    [ExcludeFromCodeCoverage]
    public sealed class BoxTests : Test
    {
        [Fact]
        public static void BoxValue()
        {
            Box<int> box = 10;
            Equal(10, box.Value);
        }

        [Fact]
        public static void BoxNullableValue()
        {
            int? i = null;
            Box<int> box = i;
            Null(box);

            i = 10;
            box = i;
            Equal(10, box.Value);
        }
    }
}