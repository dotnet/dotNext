#if !NETCOREAPP3_1
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks
{
    public sealed class ValueTaskCompletionSourceTests : Test
    {
        [Fact]
        public static async Task SuccessfulCompletion()
        {
            var source = new ValueTaskCompletionSource();
            var task = source.CreateTask(InfiniteTimeSpan, default);
            False(task.IsCompleted);
            True(source.TrySetResult());
            await task;
        }

        [Fact]
        public static async Task CompleteWithError()
        {
            var source = new ValueTaskCompletionSource();
            var task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetException(new ArithmeticException()));
            await ThrowsAsync<ArithmeticException>(() => task.AsTask());
        }

        [Fact]
        public static async Task Cancellation()
        {
            var source = new ValueTaskCompletionSource();
            using var cancellation = new CancellationTokenSource();
            var task = source.CreateTask(InfiniteTimeSpan, cancellation.Token);
            False(task.IsCompleted);
            cancellation.Cancel();
            await ThrowsAsync<OperationCanceledException>(() => task.AsTask());
            False(source.TrySetResult());
        }

        [Fact]
        public static async Task ForceTimeout()
        {
            var source = new ValueTaskCompletionSource();
            var task = source.CreateTask(TimeSpan.FromMilliseconds(20), default);
            await Task.Delay(100);
            True(task.IsCompleted);
            await ThrowsAsync<TimeoutException>(() => task.AsTask());
            False(source.TrySetResult());
        }

        [Fact]
        public static async Task CompleteWithToken()
        {
            var source = new ValueTaskCompletionSource();
            var completionToken = source.Reset();
            var task = source.CreateTask(InfiniteTimeSpan, default);
            False(source.TrySetResult(short.MaxValue));
            False(task.IsCompleted);
            True(source.TrySetResult(completionToken));
            await task;
        }

        [Fact]
        public static async Task Reuse()
        {
            var source = new ValueTaskCompletionSource();
            var task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetResult());
            await task;

            source.Reset();
            task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetResult());
            await task;
        }

        [Fact]
        public static async Task AsyncCompletion()
        {
            var source = new ValueTaskCompletionSource();
            var task = source.CreateTask(InfiniteTimeSpan, default);
            var result = Task.Run(async () => await task);
            await Task.Delay(10);
            True(source.TrySetResult());
            await result;
        }

        [Fact]
        public static async Task AsyncLocalAccess()
        {
            var source = new ValueTaskCompletionSource();
            var task = source.CreateTask(InfiniteTimeSpan, default);
            var local = new AsyncLocal<int>() { Value = 56 };
            var result = Task.Run(async () =>
            {
                Equal(56, local.Value);
                await task;
                Equal(56, local.Value);
            });

            await Task.Delay(100);
            True(source.TrySetResult());
            await result;
        }
    }
}
#endif