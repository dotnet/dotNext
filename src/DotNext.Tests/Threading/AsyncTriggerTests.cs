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

        var task1 = trigger.WaitAsync();
        var task2 = trigger.WaitAsync();
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

        var task1 = trigger.WaitAsync();
        var task2 = trigger.WaitAsync();
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

        var task1 = trigger.WaitAsync();
        var task2 = trigger.SignalAndWaitAsync(false, true);

        await task1;
        False(task2.IsCompleted);

        True(trigger.Signal());

        await task2;
    }
    
    [Fact]
    public static async Task SignalAndWaitWithTimeout()
    {
        using var trigger = new AsyncTrigger();

        var task1 = trigger.WaitAsync();
        var task2 = trigger.SignalAndWaitAsync(false, true, DefaultTimeout);

        await task1;
        False(task2.IsCompleted);

        True(trigger.Signal());

        True(await task2);
    }

    [Fact]
    public static async Task SignalEmptyQueue()
    {
        using var trigger = new AsyncTrigger();

        await ThrowsAsync<InvalidOperationException>(trigger.SignalAndWaitAsync(true, true).AsTask);
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
        var task = trigger.SpinWaitAsync(cond).AsTask();
        False(task.IsCompleted);

        trigger.Signal();
        False(task.IsCompleted);

        cond.Value = true;
        Thread.MemoryBarrier();
        trigger.Signal();
        await task;
    }

    [Fact]
    public static async Task SpinWaitAsync2()
    {
        using var trigger = new AsyncTrigger();
        var cond = new Condition();
        var task = trigger.SpinWaitAsync(cond, InfiniteTimeSpan).AsTask();
        False(task.IsCompleted);

        trigger.Signal();
        False(task.IsCompleted);

        cond.Value = true;
        Thread.MemoryBarrier();
        trigger.Signal();
        True(await task);
    }
}