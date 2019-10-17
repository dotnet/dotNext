using Xunit;

namespace DotNext.Buffers
{
    public sealed class UnmanagedMemoryPoolTests : Assert
    {
        [Fact]
        public static void Allocation()
        {
            using (var manager = UnmanagedMemoryPool<long>.Allocate(2))
            {
                Equal(2, manager.Length);

                Equal(sizeof(long) * 2, manager.Size);
                Equal(0, manager.Span[0]);
                Equal(0, manager.Span[1]);
                manager.Pointer[0] = 10L;
                manager.Pointer[1] = 20L;
                Equal(10L, manager.Span[0]);
                Equal(20L, manager.Span[1]);
                Equal(10L, manager.Memory.Span[0]);
                Equal(20L, manager.Memory.Span[1]);
            }
        }

        [Fact]
        public static void Pooling()
        {
            using (var pool = new UnmanagedMemoryPool<long>(10, trackAllocations: true))
            using (var manager = pool.Rent(2))
            {
                Equal(2, manager.Memory.Length);

                Equal(0, manager.Memory.Span[0]);
                Equal(0, manager.Memory.Span[1]);
                manager.Memory.Span[0] = 10L;
                manager.Memory.Span[1] = 20L;
                Equal(10L, manager.Memory.Span[0]);
                Equal(20L, manager.Memory.Span[1]);
            }
        }
    }
}
