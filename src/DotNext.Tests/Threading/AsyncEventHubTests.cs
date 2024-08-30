namespace DotNext.Threading;

public sealed class AsyncEventHubTests : Test
{
    [Fact]
    public static void InvalidCount()
    {
        Throws<ArgumentOutOfRangeException>(static () => new AsyncEventHub(0));
        Throws<ArgumentOutOfRangeException>(static () => new AsyncEventHub(-1));
    }

    [Fact]
    public static void WaitOne()
    {
        using var hub = new AsyncEventHub(3);
        Equal(3, hub.Count);

        True(hub.Pulse(0));
        True(hub.WaitOneAsync(0, DefaultTimeout).IsCompletedSuccessfully);
        False(hub.WaitOneAsync(1).IsCompleted);
    }

    [Fact]
    public static async Task WaitAny()
    {
        using var hub = new AsyncEventHub(3);

        var flags = hub.Pulse(new AsyncEventHub.EventGroup([0]));
        True(flags.Contains(0));

        var set = new HashSet<int>();
        await hub.WaitAnyAsync(set, DefaultTimeout);
        Equal(0, Single(set));

        set.Clear();
        await hub.WaitAnyAsync(new AsyncEventHub.EventGroup([0, 1]), set);
        Equal(0, Single(set));
    }

    [Fact]
    public static async Task WaitAny2()
    {
        using var hub = new AsyncEventHub(3);
        
        var flags = hub.ResetAndPulse(new AsyncEventHub.EventGroup([0]));
        True(flags.Contains(0));

        await hub.WaitAnyAsync(DefaultTimeout);
        await hub.WaitAnyAsync();
    }
    
    [Fact]
    public static async Task WaitAny3()
    {
        using var hub = new AsyncEventHub(3);

        True(hub.ResetAndPulse(0));

        await hub.WaitAnyAsync(DefaultTimeout);
        
        var set = new HashSet<int>();
        await hub.WaitAnyAsync(set);
        Equal(0, Single(set));
    }

    [Fact]
    public static async Task WaitAll()
    {
        using var hub = new AsyncEventHub(3);

        var flags = hub.PulseAll();
        Equal(3, flags.Count);
        Contains(0, flags);
        Contains(1, flags);
        Contains(2, flags);

        await hub.WaitAllAsync(new([0, 1]), DefaultTimeout);
        await hub.WaitAllAsync();
        await hub.WaitAllAsync(DefaultTimeout);
    }

    [Fact]
    public static void CaptureState()
    {
        using var hub = new AsyncEventHub(3);
        Span<bool> state = stackalloc bool[hub.Count];
        state.Clear();

        var flags = hub.CaptureState();
        Empty(flags);

        True(hub.Pulse(1));
        flags = hub.CaptureState();
        Equal(1, Single(flags));
    }

    [Fact]
    public static async Task CancelPendingTasks()
    {
        using var hub = new AsyncEventHub(3);
        var task1 = hub.WaitOneAsync(0).AsTask();
        var task2 = hub.WaitOneAsync(1).AsTask();

        hub.CancelSuspendedCallers(new(canceled: true));
        await ThrowsAsync<OperationCanceledException>(Func.Constant(task1));
        await ThrowsAsync<OperationCanceledException>(Func.Constant(task2));
    }

    [Fact]
    public static void ResetAndPulse()
    {
        using var hub = new AsyncEventHub(3);

        True(hub.Pulse(1));
        False(hub.ResetAndPulse(1));
        Empty(hub.ResetAndPulse(new AsyncEventHub.EventGroup([1])));

        var flags = hub.ResetAndPulse(new AsyncEventHub.EventGroup([0, 2]));
        Equal(2, flags.Count);
        True(flags.Contains(0));
        True(flags.Contains(2));
        False(flags.Contains(1));
    }

    [Fact]
    public static void Pulse()
    {
        using var hub = new AsyncEventHub(3);
        Equal(1, Single(hub.Pulse(new AsyncEventHub.EventGroup([1]))));
        Empty(hub.Pulse(new AsyncEventHub.EventGroup([1])));
    }

    [Fact]
    public static async Task IncorrectGroup()
    {
        using var hub = new AsyncEventHub(3);
        var group = new AsyncEventHub.EventGroup([3]);
        Throws<ArgumentOutOfRangeException>(() => hub.ResetAndPulse(group));
        Throws<ArgumentOutOfRangeException>(() => hub.Pulse(group));
        await ThrowsAsync<ArgumentOutOfRangeException>(hub.WaitAllAsync(group).AsTask);
        await ThrowsAsync<ArgumentOutOfRangeException>(hub.WaitAnyAsync(group).AsTask);
        await ThrowsAsync<ArgumentOutOfRangeException>(hub.WaitAllAsync(group, DefaultTimeout).AsTask);
        await ThrowsAsync<ArgumentOutOfRangeException>(hub.WaitAnyAsync(group, DefaultTimeout).AsTask);
    }

    [Fact]
    public static async Task OutOfOrderWaitQueueProcessing()
    {
        using var hub = new AsyncEventHub(3);
        var task = hub.WaitOneAsync(0);

        True(hub.Pulse(1));
        False(task.IsCompleted);
        
        True(hub.Pulse(0));
        True(task.IsCompleted);

        await task;
    }
}