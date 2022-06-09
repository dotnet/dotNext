using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Threading;

[ExcludeFromCodeCoverage]
public sealed class AsyncTriggerTests : Test
{
    private sealed class State : StrongBox<int>
    {

    }

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
    public static async Task SignalEmptyQueue()
    {
        using var trigger = new AsyncTrigger();

        await ThrowsAsync<InvalidOperationException>(trigger.SignalAndWaitAsync(true, true).AsTask);
    }

    private sealed class TestTransition : AsyncTrigger<StrongBox<int>>.ITransition
    {
        bool AsyncTrigger<StrongBox<int>>.ITransition.Test(StrongBox<int> state)
            => state.Value == 42;

        void AsyncTrigger<StrongBox<int>>.ITransition.Transit(StrongBox<int> state)
            => state.Value = 56;
    }

    [Fact]
    public static async Task Transitions()
    {
        using var trigger = new AsyncTrigger<StrongBox<int>>(new());

        trigger.State.Value = 64;
        var task1 = trigger.WaitAsync(new TestTransition());
        False(task1.IsCompleted);

        trigger.Signal(static state => state.Value = 42);

        var task2 = trigger.WaitAsync(new TestTransition());
        False(task2.IsCompleted);

        await task1;

        trigger.CancelSuspendedCallers(new(true));

        await ThrowsAsync<OperationCanceledException>(task2.AsTask);
    }
}