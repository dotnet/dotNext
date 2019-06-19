using Xunit;

namespace DotNext.Runtime.InteropServices
{
    public sealed class MemoryTests : Assert
    {
        [Fact]
        public static void SwapValues()
        {
            var x = 10;
            var y = 20;
            Memory.Swap(ref x, ref y);
            Equal(20, x);
            Equal(10, y);
        }

        [Fact]
        public unsafe static void SwapValuesByPointer()
        {
            var x = 10;
            var y = 20;
            Memory.Swap(&x, &y);
            Equal(20, x);
            Equal(10, y);
        }
    }
}