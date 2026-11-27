using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices;

public class ExecutionStackTests : Test
{
    [Fact]
    public static void IsOnStack()
    {
        True(RuntimeHelpers.IsStackDetectionSupported);
        var i = 42;
        True(RuntimeHelpers.IsOnExecutionStack(in i));
        False(RuntimeHelpers.IsOnExecutionStack(in new StrongBox<int>().Value));
    }

    [Fact]
    public static void ReadOnlySpanLocation()
    {
        Span<byte> span = stackalloc byte[8];
        True(span.IsStackAllocated);

        span = new byte[8];
        False(span.IsStackAllocated);
        
        False(ReadOnlySpan<byte>.Empty.IsStackAllocated);
    }

    [Fact]
    public static void CheckExecutionStackSize()
    {
        const int frameSize = 64 * 1024;

        // The runtime doesn't always honor the requested stack size (it may fall back to the OS default),
        // so the test checks invariants instead of the exact size
        var stackSize = RuntimeHelpers.ExecutionStackSize;
        Equal(0U, stackSize % (uint)Environment.SystemPageSize);

        var remaining = RuntimeHelpers.RemainingExecutionStack;
        InRange(remaining, 1U, stackSize - 1U);

        InRange(GetRemainingStackInDeeperFrame(), 1U, remaining - frameSize);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static nuint GetRemainingStackInDeeperFrame()
        {
            Span<byte> frame = stackalloc byte[frameSize];
            frame[0] = 1;
            return RuntimeHelpers.RemainingExecutionStack;
        }
    }
}