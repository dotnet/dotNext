using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices;

[Collection("tst")]
public sealed class SpawningAsyncTaskMethodBuilderTests : Test
{
    [Fact]
    public static async Task ForkAsyncMethodWithResult()
    {
        using var resetEvent = new ManualResetEventSlim(initialState: false);
        var task = Sum(40, 2, Thread.CurrentThread.ManagedThreadId);
        True(resetEvent.Wait(DefaultTimeout));

        Equal(42, await task);

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
        async Task<int> Sum(int x, int y, int callerThreadId)
        {
            resetEvent.Set();
            NotEqual(callerThreadId, Thread.CurrentThread.ManagedThreadId);

            await Task.Yield();
            return x + y;
        }
    }

    [Fact]
    public static async Task ForkAsyncMethodWithoutResult()
    {
        using var resetEvent = new ManualResetEventSlim(initialState: false);
        var task = CheckThreadId(Thread.CurrentThread.ManagedThreadId);
        True(resetEvent.Wait(DefaultTimeout));

        await task;

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
        async Task CheckThreadId(int callerThreadId)
        {
            resetEvent.Set();
            NotEqual(callerThreadId, Thread.CurrentThread.ManagedThreadId);

            await Task.Yield();
        }
    }

    [Fact]
    public static async Task CancellationOfSpawnedMethod()
    {
        using var resetEvent = new ManualResetEventSlim(initialState: false);
        var task = CheckThreadId(Thread.CurrentThread.ManagedThreadId, new(true));
        True(resetEvent.Wait(DefaultTimeout));

        await Task.WhenAny(task);
        True(task.IsCanceled);

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
        async Task CheckThreadId(int callerThreadId, CancellationToken token)
        {
            resetEvent.Set();
            NotEqual(callerThreadId, Thread.CurrentThread.ManagedThreadId);

            await Task.Delay(DefaultTimeout, token);
        }
    }
}