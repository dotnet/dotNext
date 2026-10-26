using System.Runtime.CompilerServices;
using static System.Threading.Timeout;

namespace DotNext.Threading;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class AsyncTriggerTests : Test
{
    [Fact]
    public static async Task UnicastSignal()
    {
        using var trigger = new AsyncTrigger();
        False(trigger.Signal(true));
        False(trigger.Signal(false));

        var task1 = trigger.WaitAsync(TestToken);
        var task2 = trigger.WaitAsync(TestToken);
        False(task1.IsCompleted);
        False(task2.IsCompleted);

        True(trigger.Signal());
        True(trigger.Signal());
        False(trigger.Signal());

        await task1;
        await task2;
    }

    [Fact]
    public static async Task MulticastSignal()
    {
        using var trigger = new AsyncTrigger();

        var task1 = trigger.WaitAsync(TestToken);
        var task2 = trigger.WaitAsync(TestToken);
        False(task1.IsCompleted);
        False(task2.IsCompleted);

        True(trigger.Signal(true));

        await task1;
        await task2;
    }

    [Fact]
    public static async Task SignalAndWait()
    {
        using var trigger = new AsyncTrigger();

        var task1 = trigger.WaitAsync(TestToken);
        var task2 = trigger.SignalAndWaitAsync(false, true, TestToken);

        await task1;
        False(task2.IsCompleted);

        True(trigger.Signal());

        await task2;
    }
    
    [Fact]
    public static async Task SignalAndWaitWithTimeout()
    {
        using var trigger = new AsyncTrigger();

        var task1 = trigger.WaitAsync(TestToken);
        var task2 = trigger.SignalAndWaitAsync(false, true, InfiniteTimeSpan, TestToken);

        await task1;
        False(task2.IsCompleted);

        True(trigger.Signal());

        True(await task2);
    }

    [Fact]
    public static async Task SignalEmptyQueue()
    {
        using var trigger = new AsyncTrigger();

        await ThrowsAnyAsync<InvalidOperationException>(trigger.SignalAndWaitAsync(true, true, TestToken).AsTask);
    }

    private sealed class Condition : StrongBox<bool>, ISupplier<bool>
    {
        bool ISupplier<bool>.Invoke() => Value;
    }

    [Fact]
    public static async Task SpinWaitAsync()
    {
        using var trigger = new AsyncTrigger();
        var cond = new Condition();
        var task = trigger.SpinWaitAsync(cond, TestToken).AsTask();
        False(task.IsCompleted);

        trigger.Signal();
        False(task.IsCompleted);

        cond.Value = true;
        Volatile.WriteBarrier();
        trigger.Signal();
        await task;
    }

    [Fact]
    public static async Task SpinWaitAsync2()
    {
        using var trigger = new AsyncTrigger();
        var cond = new Condition();
        var task = trigger.SpinWaitAsync(cond, InfiniteTimeSpan, TestToken).AsTask();
        False(task.IsCompleted);

        trigger.Signal();
        False(task.IsCompleted);

        cond.Value = true;
        Volatile.WriteBarrier();
        trigger.Signal();
        True(await task);
    }

    [Fact]
    public static async Task SpinWaitWithNonEmptyQueue()
    {
        using var trigger = new AsyncTrigger();
        using var cts = new CancellationTokenSource();

        // live waiter
        var waiter = trigger.WaitAsync(TestToken).AsTask();

        // canceled waiter, its node remains in the queue until the task is consumed
        var canceled = trigger.WaitAsync(cts.Token);
        await cts.CancelAsync();

        // the condition is satisfied, so the caller must not be suspended
        True(await trigger.SpinWaitAsync(new FlipCondition(), DefaultTimeout, TestToken));
        await trigger.SpinWaitAsync(new FlipCondition(), TestToken);

        await ThrowsAnyAsync<OperationCanceledException>(canceled.AsTask);
        True(trigger.Signal());
        await waiter;
    }
}

// returns false on the first check only, as if the condition became true right after it
file sealed class FlipCondition : ISupplier<bool>
{
    private int count;

    bool ISupplier<bool>.Invoke() => count++ > 0;
}