using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.CompilerServices
{
    [ExcludeFromCodeCoverage]
    public sealed class SharedContainerTests : Test
    {
        [Fact]
        public static void BoxValue()
        {
            Shared<int> box = 10;
            Equal(10, box.Value);
        }

        [Fact]
        public static void BoxNullableValue()
        {
            int? i = null;
            Shared<int> box = i;
            Null(box);

            i = 10;
            box = i;
            Equal(10, box.Value);
        }
    }
}