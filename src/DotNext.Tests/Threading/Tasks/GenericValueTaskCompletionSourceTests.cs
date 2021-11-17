using System.Diagnostics.CodeAnalysis;
using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks
{
    [ExcludeFromCodeCoverage]
    public sealed class GenericValueTaskCompletionSourceTests : Test
    {
        [Fact]
        public static async Task SuccessfulCompletion()
        {
            var source = new ValueTaskCompletionSource<int>();
            var task = source.CreateTask(InfiniteTimeSpan, default);
            False(task.IsCompleted);
            True(source.TrySetResult(42));
            Equal(42, await task);
        }

        [Fact]
        public static async Task CompleteWithError()
        {
            var source = new ValueTaskCompletionSource<int>();
            var task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetException(new ArithmeticException()));
            await ThrowsAsync<ArithmeticException>(() => task.AsTask());
        }

        [Fact]
        public static async Task Cancellation()
        {
            var source = new ValueTaskCompletionSource<int>();
            using var cancellation = new CancellationTokenSource();
            var task = source.CreateTask(InfiniteTimeSpan, cancellation.Token);
            False(task.IsCompleted);
            cancellation.Cancel();
            await ThrowsAsync<OperationCanceledException>(task.AsTask);
            False(source.TrySetResult(42));
        }

        [Fact]
        public static async Task ForceTimeout()
        {
            var source = new ValueTaskCompletionSource<int>();
            var task = source.CreateTask(TimeSpan.FromMilliseconds(20), default);
            await ThrowsAsync<TimeoutException>(task.AsTask);
            False(source.TrySetResult(42));
        }

        [Fact]
        public static async Task CompleteWithToken()
        {
            var source = new ValueTaskCompletionSource<int>();
            var completionToken = source.Reset();
            var task = source.CreateTask(InfiniteTimeSpan, default);
            False(source.TrySetResult(short.MaxValue, 42));
            False(task.IsCompleted);
            True(source.TrySetResult(completionToken, 42));
            Equal(42, await task);
        }

        [Fact]
        public static async Task Reuse()
        {
            var source = new ValueTaskCompletionSource<int>();

            var task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetResult(42));
            Equal(42, await task);

            source.Reset();
            task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetResult(43));
            Equal(43, await task);
        }

        [Fact]
        public static async Task AsyncCompletion()
        {
            var source = new ValueTaskCompletionSource<int>();

            var task = source.CreateTask(InfiniteTimeSpan, default);
            var result = Task.Run(async () => await task);
            await Task.Delay(10);
            True(source.TrySetResult(42));
            Equal(42, await result);
        }

        [Fact]
        public static async Task AsyncLocalAccess()
        {
            var source = new ValueTaskCompletionSource<int>();

            var task = source.CreateTask(InfiniteTimeSpan, default);
            var local = new AsyncLocal<int>() { Value = 56 };
            var result = Task.Run(async () =>
            {
                Equal(56, local.Value);
                var result = await task;
                Equal(56, local.Value);
                return result;
            });

            await Task.Delay(100);
            True(source.TrySetResult(42));
            Equal(42, await result);
        }

        [Fact]
        public static async Task InteropWithTaskCompletionSourceTimeout()
        {
            var source = new ValueTaskCompletionSource<int>();
            var task = source.CreateLinkedTaskCompletionSource("Hello, world!", TimeSpan.FromMilliseconds(20), default).Task;

            Equal("Hello, world!", task.AsyncState);
            await ThrowsAsync<TimeoutException>(Func.Constant(task));
        }
    }
}