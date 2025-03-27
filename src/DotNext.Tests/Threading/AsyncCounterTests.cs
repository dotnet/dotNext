namespace DotNext.Threading;

public sealed class AsyncCounterTests : Test
{
    [Fact]
    public static async Task SignalAndWait()
    {
        using (var counter = new AsyncCounter())
        {
            Equal(0, counter.Value);
            counter.Increment(2L);
            counter.Increment(0L);
            Equal(2, counter.Value);
            True(await counter.WaitAsync(TimeSpan.Zero));
            True(await counter.WaitAsync(TimeSpan.Zero));
            False(await counter.WaitAsync(TimeSpan.Zero));
            Equal(0, counter.Value);
            False(counter.As<IAsyncEvent>().Reset());
        }
        using (IAsyncEvent counter = new AsyncCounter())
        {
            False(counter.IsSet);
            True(counter.Signal());
            True(counter.Signal());
            True(counter.IsSet);
            True(await counter.WaitAsync(TimeSpan.Zero));
            True(await counter.WaitAsync(TimeSpan.Zero));
            False(await counter.WaitAsync(TimeSpan.Zero));
            False(counter.IsSet);
        }
    }

    [Fact]
    public static void InvalidDeltaValue()
    {
        using var counter = new AsyncCounter();

        Throws<ArgumentOutOfRangeException>(() => counter.Increment(-1L));
    }

    [Fact]
    public static void CounterOverflow()
    {
        using var counter = new AsyncCounter(initialValue: long.MaxValue);

        Throws<OverflowException>(counter.Increment);
    }

    [Fact]
    public static void DecrementSynchronously()
    {
        using (var counter = new AsyncCounter())
        {
            False(counter.TryDecrement());
            
            counter.Increment();
            True(counter.TryDecrement());
        }
    }

    [Fact]
    public static async Task DecrementTwice()
    {
        using var counter = new AsyncCounter(0);
        var task1 = counter.WaitAsync().AsTask();
        var task2 = counter.WaitAsync().AsTask();

        counter.Increment();
        var t = await Task.WhenAny(task1, task2);
        True(task1.IsCompleted ^ task2.IsCompleted);

        Equal(0L, counter.Value);
    }
}