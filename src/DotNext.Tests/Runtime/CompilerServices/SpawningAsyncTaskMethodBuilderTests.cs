using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class SpawningAsyncTaskMethodBuilderTests : Test
{
    [Fact]
    public static async Task ForkAsyncMethodWithResult()
    {
        var resetEvent = new TaskCompletionSource();
        var task = Sum(40, 2, Thread.CurrentThread.ManagedThreadId);
        await resetEvent.Task.WaitAsync(DefaultTimeout);

        Equal(42, await task);

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
        async Task<int> Sum(int x, int y, int callerThreadId)
        {
            resetEvent.SetResult();
            NotEqual(callerThreadId, Thread.CurrentThread.ManagedThreadId);

            await Task.CompletedTask;
            return x + y;
        }
    }

    [Fact]
    public static async Task ForkAsyncMethodWithoutResult()
    {
        var resetEvent = new TaskCompletionSource();
        var task = CheckThreadId(Thread.CurrentThread.ManagedThreadId);
        await resetEvent.Task.WaitAsync(DefaultTimeout);

        await task;

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
        async Task CheckThreadId(int callerThreadId)
        {
            resetEvent.SetResult();
            NotEqual(callerThreadId, Thread.CurrentThread.ManagedThreadId);

            await Task.CompletedTask;
        }
    }

    [Fact]
    public static async Task CancellationOfSpawnedMethod()
    {
        var resetEvent = new TaskCompletionSource();
        var task = CheckThreadId(Thread.CurrentThread.ManagedThreadId, new(true));
        await resetEvent.Task.WaitAsync(DefaultTimeout);

        await task.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
        True(task.IsCanceled);

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
        async Task CheckThreadId(int callerThreadId, CancellationToken token)
        {
            resetEvent.SetResult();
            NotEqual(callerThreadId, Thread.CurrentThread.ManagedThreadId);

            await Task.Delay(DefaultTimeout, token);
        }
    }
}