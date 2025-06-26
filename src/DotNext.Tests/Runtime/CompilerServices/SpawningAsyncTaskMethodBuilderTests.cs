using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class SpawningAsyncTaskMethodBuilderTests : Test
{
    [Fact]
    public static Task ForkAsyncMethodWithResult()
    {
        return Sum(40, 2, Thread.CurrentThread.ManagedThreadId);

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder<>))]
        static async Task<int> Sum(int x, int y, int callerThreadId)
        {
            NotEqual(callerThreadId, Thread.CurrentThread.ManagedThreadId);

            await Task.CompletedTask;
            return x + y;
        }
    }

    [Fact]
    public static Task ForkAsyncMethodWithoutResult()
    {
        return CheckThreadId(Thread.CurrentThread.ManagedThreadId);

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
        static async Task CheckThreadId(int callerThreadId)
        {
            NotEqual(callerThreadId, Thread.CurrentThread.ManagedThreadId);

            await Task.CompletedTask;
        }
    }

    [Fact]
    public static async Task CancellationOfSpawnedMethod()
    {
        var task = CheckThreadId(Thread.CurrentThread.ManagedThreadId, new(true));

        await task.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
        True(task.IsCanceled);

        [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
        static async Task CheckThreadId(int callerThreadId, CancellationToken token)
        {
            NotEqual(callerThreadId, Thread.CurrentThread.ManagedThreadId);

            await Task.Delay(DefaultTimeout, token);
        }
    }
}