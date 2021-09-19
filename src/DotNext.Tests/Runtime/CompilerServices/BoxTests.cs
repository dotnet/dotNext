using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.CompilerServices
{
    [ExcludeFromCodeCoverage]
    public sealed class BoxTests : Test
    {
        [Fact]
        public static void TypeCheck()
        {
            Throws<ArgumentException>(() => new Box<int>(decimal.Zero));
            object value = 42;
            var box = new Box<int>(value);
            Equal(42, box.Value);
            Equal(42, (int)box);
        }

        [Fact]
        public static void IsEmpty()
        {
            True(default(Box<int>).IsEmpty);
            False(new Box<int>(42).IsEmpty);
        }

        [Fact]
        public static void Operators()
        {
            object value = 42;
            var box1 = new Box<int>(value);
            var box2 = new Box<int>(value);
            True(box1 == box2);
            False(box1 != box2);
            Equal(box1, box2);
            Equal(box1.GetHashCode(), box2.GetHashCode());
            box2 = default;
            False(box1 == box2);
            True(box1 != box2);
            NotEqual(box1, box2);
        }

        [Fact]
        public static unsafe void MemoryPinning()
        {
            var box = new Box<int>(42);
            fixed (int* ptr = box)
            {
                Equal(42, *ptr);
            }
        }
    }
}