namespace DotNext.Runtime;

public sealed class GCNotificationTests : Test
{
    [Fact]
    public static async Task GCHookAsync()
    {
        var task1 = GC.WhenTriggered().WaitAsync(TestToken);
        var task2 = GC.WhenTriggered(2).WaitAsync(TestToken);
        GC.Collect(2, GCCollectionMode.Forced);
        await task1;
        await task2;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task GCHook(bool continueOnCapturedContext)
    {
        var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = GC.WhenTriggered().Register(static (src, _) => src.SetResult(), source, continueOnCapturedContext);
        GC.Collect(0, GCCollectionMode.Forced);
        await source.Task.WaitAsync(TestToken);
    }

    [Fact]
    public static async Task HeapCompactionAsync()
    {
        var task = GC.WhenCompactionOccurred().WaitAsync(TestToken);
        GC.Collect(2, GCCollectionMode.Forced, blocking: false, compacting: true);
        await task;
    }

    [Fact]
    public static async Task OrOperator()
    {
        var task = (GC.WhenHeapFragmented(0.8D) | GC.WhenTriggered()).WaitAsync(TestToken);
        GC.Collect(2, GCCollectionMode.Forced);
        await task;
    }

    [Fact]
    public static async Task XorOperator()
    {
        var task = (GC.WhenHeapFragmented(0.8D) ^ GC.WhenTriggered()).WaitAsync(TestToken);
        GC.Collect(2, GCCollectionMode.Forced);
        await task;
    }

    [Fact]
    public static async Task AndOperator()
    {
        var task = (GC.WhenTriggered() & GC.WhenCompactionOccurred()).WaitAsync(TestToken);
        GC.Collect(2, GCCollectionMode.Forced, blocking: false, compacting: true);
        await task;
    }

    [Fact]
    public static async Task NotOperator()
    {
        var task = (!GC.WhenTriggered().Negate()).WaitAsync(TestToken);
        GC.Collect(2, GCCollectionMode.Forced);
        await task;
    }
}