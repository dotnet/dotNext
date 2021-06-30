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
            var source = new ValueTaskCompletionSource<int>();
            var task = source.Reset(InfiniteTimeSpan, default);
            False(task.IsCompleted);
            True(source.TrySetResult(42));
            Equal(42, await task);
        }

        [Fact]
        public static async Task CompleteWithError()
        {
            var source = new ValueTaskCompletionSource<int>();
            var task = source.Reset(InfiniteTimeSpan, default);
            True(source.TrySetException(new ArithmeticException()));
            await ThrowsAsync<ArithmeticException>(() => task.AsTask());
        }

        [Fact]
        public static async Task Cancellation()
        {
            var source = new ValueTaskCompletionSource<int>();
            using var cancellation = new CancellationTokenSource();
            var task = source.Reset(InfiniteTimeSpan, cancellation.Token);
            False(task.IsCompleted);
            cancellation.Cancel();
            await ThrowsAsync<OperationCanceledException>(() => task.AsTask());
            False(source.TrySetResult(42));
        }

        [Fact]
        public static async Task ForceTimeout()
        {
            var source = new ValueTaskCompletionSource<int>();
            source.Reset();
            var task = source.CreateTask(TimeSpan.FromMilliseconds(20), default);
            await Task.Delay(100);
            True(task.IsCompleted);
            await ThrowsAsync<TimeoutException>(() => task.AsTask());
            False(source.TrySetResult(42));
        }

        [Fact]
        public static async Task CompleteWithToken()
        {
            var source = new ValueTaskCompletionSource<int>();
            var task = source.Reset(out var completionToken, InfiniteTimeSpan, default);
            False(source.TrySetResult(short.MaxValue, 42));
            False(task.IsCompleted);
            True(source.TrySetResult(completionToken, 42));
            Equal(42, await task);
        }

        [Fact]
        public static async Task Reuse()
        {
            var source = new ValueTaskCompletionSource<int>();
            source.Reset();
            var task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetResult(42));
            Equal(42, await task);

            source.Reset();
            task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetResult(43));
            Equal(43, await task);
        }
    }
}
#endif