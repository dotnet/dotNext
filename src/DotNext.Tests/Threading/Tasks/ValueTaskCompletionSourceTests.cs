using System.Diagnostics.CodeAnalysis;
using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks
{
    [ExcludeFromCodeCoverage]
    public sealed class ValueTaskCompletionSourceTests : Test
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task SuccessfulCompletion(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
            var task = source.CreateTask(InfiniteTimeSpan, default);
            False(task.IsCompleted);
            True(source.TrySetResult());
            await task;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task CompleteWithError(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
            var task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetException(new ArithmeticException()));
            await ThrowsAsync<ArithmeticException>(task.AsTask);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task Cancellation(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
            using var cancellation = new CancellationTokenSource();
            var task = source.CreateTask(InfiniteTimeSpan, cancellation.Token);
            False(task.IsCompleted);
            cancellation.Cancel();
            await ThrowsAsync<OperationCanceledException>(task.AsTask);
            False(source.TrySetResult());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task ForceTimeout(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
            var task = source.CreateTask(TimeSpan.FromMilliseconds(20), default);
            await ThrowsAsync<TimeoutException>(task.AsTask);
            False(source.TrySetResult());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task CompleteWithToken(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
            var completionToken = source.Reset();
            var task = source.CreateTask(InfiniteTimeSpan, default);
            False(source.TrySetResult(short.MaxValue));
            False(task.IsCompleted);
            True(source.TrySetResult(completionToken));
            await task;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task Reuse(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
            var task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetResult());
            await task;

            source.Reset();
            task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetResult());
            await task;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task AsyncCompletion(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
            var task = source.CreateTask(InfiniteTimeSpan, default);
            var result = Task.Run(task.AsTask);
            await Task.Delay(10);
            True(source.TrySetResult());
            await result;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task AsyncLocalAccess(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task InteropWithTaskCompletionSourceTimeout(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
            var task = source.CreateLinkedTaskCompletionSource("Hello, world!", TimeSpan.FromMilliseconds(20), default).Task;

            Equal("Hello, world!", task.AsyncState);
            await ThrowsAsync<TimeoutException>(Func.Constant(task));
        }

        [Fact]
        public static async Task ConsumeTwice()
        {
            var source = new ValueTaskCompletionSource();
            var task = source.CreateTask(InfiniteTimeSpan, CancellationToken.None);
            source.TrySetResult();

            await task;
            await ThrowsAsync<InvalidOperationException>(task.AsTask);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public static async Task ContextFlow(bool runContinuationsAsynchronously, bool continueOnCapturedContext, bool flowExecutionContext)
        {
            var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
            var dest = new TaskCompletionSource();
            var awaiter = source.CreateTask(InfiniteTimeSpan, CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext)
                .GetAwaiter();

            if (flowExecutionContext)
                awaiter.OnCompleted(dest.SetResult);
            else
                awaiter.UnsafeOnCompleted(dest.SetResult);

            True(source.TrySetResult());
            await dest.Task;
        }

        [Fact]
        public static async Task CanceledToken()
        {
            var source = new ValueTaskCompletionSource();
            var task = source.CreateTask(InfiniteTimeSpan, new(true)).AsTask();
            await task;
        }
    }
}