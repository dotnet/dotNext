using System.Diagnostics.CodeAnalysis;
using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks
{
    [ExcludeFromCodeCoverage]
    public sealed class GenericValueTaskCompletionSourceTests : Test
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task SuccessfulCompletion(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource<int>(runContinuationsAsynchronously);
            Equal(ManualResetCompletionSourceStatus.WaitForActivation, source.Status);

            var task = source.CreateTask(InfiniteTimeSpan, default);
            Equal(ManualResetCompletionSourceStatus.Activated, source.Status);

            False(task.IsCompleted);
            True(source.TrySetResult(42));
            Equal(ManualResetCompletionSourceStatus.WaitForConsumption, source.Status);

            Equal(42, await task);
            Equal(ManualResetCompletionSourceStatus.Consumed, source.Status);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task CompleteWithError(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource<int>(runContinuationsAsynchronously);
            var task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetException(new ArithmeticException()));
            await ThrowsAsync<ArithmeticException>(() => task.AsTask());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task Cancellation(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource<int>(runContinuationsAsynchronously);
            using var cancellation = new CancellationTokenSource();
            var task = source.CreateTask(InfiniteTimeSpan, cancellation.Token);
            False(task.IsCompleted);
            cancellation.Cancel();
            await ThrowsAsync<OperationCanceledException>(task.AsTask);
            False(source.TrySetResult(42));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task ForceTimeout(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource<int>(runContinuationsAsynchronously);
            var task = source.CreateTask(TimeSpan.FromMilliseconds(20), default);
            await ThrowsAsync<TimeoutException>(task.AsTask);
            False(source.TrySetResult(42));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task CompleteWithToken(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource<int>(runContinuationsAsynchronously);
            var completionToken = source.Reset();
            var task = source.CreateTask(InfiniteTimeSpan, default);
            False(source.TrySetResult(short.MaxValue, 42));
            False(task.IsCompleted);
            True(source.TrySetResult(completionToken, 42));
            Equal(42, await task);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task Reuse(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource<int>(runContinuationsAsynchronously);

            var task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetResult(42));
            Equal(42, await task);
            Equal(ManualResetCompletionSourceStatus.Consumed, source.Status);

            source.Reset();
            Equal(ManualResetCompletionSourceStatus.WaitForActivation, source.Status);
            task = source.CreateTask(InfiniteTimeSpan, default);
            True(source.TrySetResult(43));
            Equal(43, await task);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task AsyncCompletion(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource<int>(runContinuationsAsynchronously);

            var task = source.CreateTask(InfiniteTimeSpan, default);
            var result = Task.Run(task.AsTask);
            await Task.Delay(10);
            True(source.TrySetResult(42));
            Equal(42, await result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task AsyncLocalAccess(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource<int>(runContinuationsAsynchronously);

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task InteropWithTaskCompletionSourceTimeout(bool runContinuationsAsynchronously)
        {
            var source = new ValueTaskCompletionSource<int>(runContinuationsAsynchronously);
            var task = source.CreateLinkedTaskCompletionSource("Hello, world!", TimeSpan.FromMilliseconds(20), default).Task;

            Equal("Hello, world!", task.AsyncState);
            await ThrowsAsync<TimeoutException>(Func.Constant(task));
        }

        [Fact]
        public static async Task ConsumeTwice()
        {
            var source = new ValueTaskCompletionSource<int>();
            var task = source.CreateTask(InfiniteTimeSpan, CancellationToken.None);
            source.TrySetResult(42);

            Equal(42, await task);
            await ThrowsAsync<InvalidOperationException>(task.AsTask);
        }
    }
}