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
        var hub = new AsyncEventHub(3);
        Equal(3, hub.Count);

        True(hub.Pulse(0));
        True(hub.WaitOneAsync(0, DefaultTimeout).IsCompletedSuccessfully);
        False(hub.WaitOneAsync(1).IsCompleted);
    }

    [Fact]
    public static async Task WaitAny()
    {
        var hub = new AsyncEventHub(3);

        int[] indexes = { 0 };
        bool[] flags = { false };
        hub.Pulse(indexes, flags);
        True(flags[0]);

        Equal(0, await hub.WaitAnyAsync(DefaultTimeout));
        Equal(0, await hub.WaitAnyAsync([0, 1]));
    }

    [Fact]
    public static async Task WaitAny2()
    {
        var hub = new AsyncEventHub(3);

        int[] indexes = { 0 };
        bool[] flags = { false };
        hub.ResetAndPulse(indexes, flags);
        True(flags[0]);

        Equal(0, await hub.WaitAnyAsync());
        Equal(0, await hub.WaitAnyAsync([0, 1], DefaultTimeout));
    }
    
    [Fact]
    public static async Task WaitAny3()
    {
        var hub = new AsyncEventHub(3);

        int[] indexes = { 0 };
        Equal(1, hub.ResetAndPulse(indexes));

        Equal(0, await hub.WaitAnyAsync());
        Equal(0, await hub.WaitAnyAsync([0, 1], DefaultTimeout));
    }

    [Fact]
    public static async Task WaitAny4()
    {
        var hub = new AsyncEventHub(3);

        True(hub.ResetAndPulse(0));

        Equal(0, await hub.WaitAnyAsync());
        Equal(0, await hub.WaitAnyAsync([0, 1], DefaultTimeout));
    }

    [Fact]
    public static async Task WaitAll()
    {
        var hub = new AsyncEventHub(3);

        Equal(3, hub.PulseAll());

        await hub.WaitAllAsync([0, 1], DefaultTimeout);
        await hub.WaitAllAsync();
    }

    [Fact]
    public static async Task WaitAll2()
    {
        var hub = new AsyncEventHub(3);

        bool[] flags = { false, false, false };
        hub.PulseAll(flags);
        True(flags[0]);
        True(flags[1]);
        True(flags[2]);

        await hub.WaitAllAsync([0, 1]);
        await hub.WaitAllAsync(DefaultTimeout);
    }

    [Fact]
    public static void CaptureState()
    {
        var hub = new AsyncEventHub(3);
        Span<bool> state = stackalloc bool[hub.Count];
        state.Clear();

        hub.CaptureState(state);
        True(state.IndexOf(true) < 0);

        True(hub.Pulse(1));
        hub.CaptureState(state);
        Equal(1, state.IndexOf(true));
    }

    [Fact]
    public static async Task CancelPendingTasks()
    {
        var hub = new AsyncEventHub(3);
        var task1 = hub.WaitOneAsync(0);
        var task2 = hub.WaitOneAsync(1);

        hub.CancelSuspendedCallers(new(canceled: true));
        await ThrowsAsync<TaskCanceledException>(Func.Constant(task1));
        await ThrowsAsync<TaskCanceledException>(Func.Constant(task2));
    }
}