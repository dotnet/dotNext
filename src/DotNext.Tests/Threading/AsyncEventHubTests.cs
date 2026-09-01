using static System.Threading.Timeout;

namespace DotNext.Threading;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class AsyncEventHubTests : Test
{
    [Fact]
    public static void InvalidCount()
    {
        Throws<ArgumentOutOfRangeException>(static () => new AsyncEventHub(0));
        Throws<ArgumentOutOfRangeException>(static () => new AsyncEventHub(-1));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(512)]
    public static void WaitOne(int count)
    {
        using var hub = new AsyncEventHub(count);
        Equal(count, hub.Count);

        True(hub.Pulse(0));
        True(hub.WaitOneAsync(0, TestToken).IsCompletedSuccessfully);
        False(hub.WaitOneAsync(1, TestToken).IsCompleted);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(512)]
    public static async Task WaitAny(int count)
    {
        using var hub = new AsyncEventHub(count);

        var flags = hub.Pulse(new AsyncEventHub.EventGroup([0]));
        True(flags.Contains(0));

        var set = new HashSet<int>();
        await hub.WaitAnyAsync(set, InfiniteTimeSpan, TestToken);
        Equal(0, Single(set));

        set.Clear();
        await hub.WaitAnyAsync(new AsyncEventHub.EventGroup([0, 1]), set, TestToken);
        Equal(0, Single(set));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(512)]
    public static async Task WaitAny2(int count)
    {
        using var hub = new AsyncEventHub(count);
        
        var flags = hub.ResetAndPulse(new AsyncEventHub.EventGroup([0]));
        True(flags.Contains(0));

        await hub.WaitAnyAsync(InfiniteTimeSpan, TestToken);
        await hub.WaitAnyAsync(TestToken);
    }
    
    [Theory]
    [InlineData(3)]
    [InlineData(512)]
    public static async Task WaitAny3(int count)
    {
        using var hub = new AsyncEventHub(count);

        True(hub.ResetAndPulse(0));

        await hub.WaitAnyAsync(InfiniteTimeSpan, TestToken);
        
        var set = new HashSet<int>();
        await hub.WaitAnyAsync(set, TestToken);
        Equal(0, Single(set));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(512)]
    public static async Task WaitAll(int count)
    {
        using var hub = new AsyncEventHub(count);

        var flags = hub.PulseAll();
        Equal(count, flags.Count);
        Contains(0, flags);
        Contains(1, flags);
        Contains(2, flags);

        await hub.WaitAllAsync(new([0, 1]), InfiniteTimeSpan, TestToken);
        await hub.WaitAllAsync(TestToken);
        await hub.WaitAllAsync(InfiniteTimeSpan, TestToken);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(512)]
    public static void CaptureState(int count)
    {
        using var hub = new AsyncEventHub(count);

        var flags = hub.CaptureState();
        Empty(flags);

        True(hub.Pulse(1));
        flags = hub.CaptureState();
        Equal(1, Single(flags));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(512)]
    public static async Task CancelPendingTasks(int count)
    {
        using var hub = new AsyncEventHub(count);
        var task1 = hub.WaitOneAsync(0, TestToken).AsTask();
        var task2 = hub.WaitOneAsync(1, TestToken).AsTask();

        hub.CancelSuspendedCallers(new(canceled: true));
        await ThrowsAsync<OperationCanceledException>(task1);
        await ThrowsAsync<OperationCanceledException>(task2);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(512)]
    public static void ResetAndPulse(int count)
    {
        using var hub = new AsyncEventHub(count);

        True(hub.Pulse(1));
        False(hub.ResetAndPulse(1));
        Empty(hub.ResetAndPulse(new AsyncEventHub.EventGroup([1])));

        var flags = hub.ResetAndPulse(new AsyncEventHub.EventGroup([0, 2]));
        Equal(2, flags.Count);
        True(flags.Contains(0));
        True(flags.Contains(2));
        False(flags.Contains(1));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(512)]
    public static void Pulse(int count)
    {
        using var hub = new AsyncEventHub(count);
        Equal(1, Single(hub.Pulse(new AsyncEventHub.EventGroup([1]))));
        Empty(hub.Pulse(new AsyncEventHub.EventGroup([1])));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(512)]
    public static async Task IncorrectGroup(int count)
    {
        using var hub = new AsyncEventHub(count);
        var group = new AsyncEventHub.EventGroup([count]);
        Throws<ArgumentOutOfRangeException>(() => hub.ResetAndPulse(group));
        Throws<ArgumentOutOfRangeException>(() => hub.Pulse(group));
        await ThrowsAsync<ArgumentOutOfRangeException>(hub.WaitAllAsync(group, TestToken).AsTask);
        await ThrowsAsync<ArgumentOutOfRangeException>(hub.WaitAnyAsync(group, TestToken).AsTask);
        await ThrowsAsync<ArgumentOutOfRangeException>(hub.WaitAllAsync(group, InfiniteTimeSpan, TestToken).AsTask);
        await ThrowsAsync<ArgumentOutOfRangeException>(hub.WaitAnyAsync(group, InfiniteTimeSpan, TestToken).AsTask);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(512)]
    public static async Task OutOfOrderWaitQueueProcessing(int count)
    {
        using var hub = new AsyncEventHub(count);
        var task = hub.WaitOneAsync(0, TestToken);

        True(hub.Pulse(1));
        False(task.IsCompleted);
        
        True(hub.Pulse(0));
        True(task.IsCompleted);

        await task;
    }

    [Fact]
    public static void LargeEventGroup()
    {
        var group = new AsyncEventHub.EventGroup([1, 512, 514]);
        Contains(512, group);
        Contains(514, group);
        Contains(1, group);
        Equal(3, group.Count);
    }
}