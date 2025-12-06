namespace DotNext.Runtime;

public sealed class GCNotificationTests : Test
{
    [Fact]
    public static async Task GCHookAsync()
    {
        var task1 = GCNotification.GCTriggered().WaitAsync(TestToken);
        var task2 = GCNotification.GCTriggered(2).WaitAsync(TestToken);
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
        using var registration = GCNotification.GCTriggered().Register(static (src, _) => src.SetResult(), source, continueOnCapturedContext);
        GC.Collect(0, GCCollectionMode.Forced);
        await source.Task.WaitAsync(TestToken);
    }

    [Fact]
    public static async Task HeapCompactionAsync()
    {
        var task = GCNotification.HeapCompaction().WaitAsync(TestToken);
        GC.Collect(2, GCCollectionMode.Forced, blocking: false, compacting: true);
        await task;
    }

    [Fact]
    public static async Task OrOperator()
    {
        var task = (GCNotification.HeapFragmentation(0.8D) | GCNotification.GCTriggered()).WaitAsync(TestToken);
        GC.Collect(2, GCCollectionMode.Forced);
        await task;
    }

    [Fact]
    public static async Task XorOperator()
    {
        var task = (GCNotification.HeapFragmentation(0.8D) ^ GCNotification.GCTriggered()).WaitAsync(TestToken);
        GC.Collect(2, GCCollectionMode.Forced);
        await task;
    }

    [Fact]
    public static async Task AndOperator()
    {
        var task = (GCNotification.GCTriggered() & GCNotification.HeapCompaction()).WaitAsync(TestToken);
        GC.Collect(2, GCCollectionMode.Forced, blocking: false, compacting: true);
        await task;
    }

    [Fact]
    public static async Task NotOperator()
    {
        var task = (!GCNotification.GCTriggered().Negate()).WaitAsync(TestToken);
        GC.Collect(2, GCCollectionMode.Forced);
        await task;
    }
}