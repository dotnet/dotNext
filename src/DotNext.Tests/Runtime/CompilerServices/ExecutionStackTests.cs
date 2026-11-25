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
        // 2 MB is not used: the runtime ignores exactly that value on Linux and gives the thread the OS default stack size
        const int stackSize = 1024 * 1024 * 4;
        nuint actualSize = 0U, remaining = 0U;

        // asserts can't run on the raw thread: an unhandled exception there aborts the test host
        var t = new Thread(() =>
        {
            actualSize = RuntimeHelpers.ExecutionStackSize;
            remaining = RuntimeHelpers.RemainingExecutionStack;
        }, stackSize);

        t.Start();
        t.Join();

        Equal((nuint)stackSize, actualSize);
        InRange(remaining, 1U, actualSize - 1U);
    }
}