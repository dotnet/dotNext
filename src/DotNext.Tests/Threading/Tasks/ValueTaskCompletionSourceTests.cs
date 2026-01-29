using System.Runtime.CompilerServices;
using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class ValueTaskCompletionSourceTests : Test
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task SuccessfulCompletion(bool runContinuationsAsynchronously)
    {
        var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
        var task = source.CreateTask(InfiniteTimeSpan, TestToken);
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
        var task = source.CreateTask(InfiniteTimeSpan, TestToken);
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
        await cancellation.CancelAsync();
        await ThrowsAsync<OperationCanceledException>(task.AsTask);
        False(source.TrySetResult());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task ForceTimeout(bool runContinuationsAsynchronously)
    {
        var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
        var task = source.CreateTask(TimeSpan.FromMilliseconds(20), TestToken);
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
        var task = source.CreateTask(InfiniteTimeSpan, TestToken);
        False(source.TrySetResult(completionData: null, short.MaxValue));
        False(task.IsCompleted);
        True(source.TrySetResult(completionData: null, completionToken));
        await task;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task Reuse(bool runContinuationsAsynchronously)
    {
        var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
        var task = source.CreateTask(InfiniteTimeSpan, TestToken);
        True(source.TrySetResult());
        await task;

        source.Reset();
        task = source.CreateTask(InfiniteTimeSpan, TestToken);
        True(source.TrySetResult());
        await task;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task AsyncCompletion(bool runContinuationsAsynchronously)
    {
        var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
        var task = source.CreateTask(InfiniteTimeSpan, TestToken);
        var result = Task.Run(task.AsTask, TestToken);
        await Task.Delay(10, TestToken);
        True(source.TrySetResult());
        await result;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task AsyncLocalAccess(bool runContinuationsAsynchronously)
    {
        var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
        var task = source.CreateTask(InfiniteTimeSpan, TestToken);
        var local = new AsyncLocal<int> { Value = 56 };
        var result = Task.Run(async () =>
        {
            Equal(56, local.Value);
            await task;
            Equal(56, local.Value);
        }, TestToken);

        await Task.Delay(100, TestToken);
        True(source.TrySetResult());
        await result;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static async Task InteropWithTaskCompletionSourceTimeout(bool runContinuationsAsynchronously)
    {
        var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
        var task = source.CreateLinkedTaskCompletionSource("Hello, world!", TimeSpan.FromMilliseconds(20), TestToken).Task;

        Equal("Hello, world!", task.AsyncState);
        await ThrowsAsync<TimeoutException>(task);
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
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public static async Task ContextFlow(bool runContinuationsAsynchronously, bool flowExecutionContext)
    {
        var source = new ValueTaskCompletionSource(runContinuationsAsynchronously);
        var dest = new TaskCompletionSource();
        var awaiter = source.CreateTask(InfiniteTimeSpan, CancellationToken.None)
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
        await ThrowsAsync<OperationCanceledException>(task);
    }

    [Fact]
    public static async Task LazyCompletion()
    {
        var source = new ValueTaskCompletionSource();
        var task = source.CreateTask(InfiniteTimeSpan, CancellationToken.None).AsTask();
        
        True(TrySetResult(source, string.Empty, completionToken: null, e: null, out var resumable));
        True(resumable);
        Same(string.Empty, source.CompletionData);
        False(task.IsCompleted);

        NotifyConsumer(source);
        await task;
        source.Reset();

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "NotifyConsumer")]
        static extern void NotifyConsumer(ManualResetCompletionSource source);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "TrySetResult")]
        static extern bool TrySetResult(ValueTaskCompletionSource source,
            object completionData,
            short? completionToken,
            Exception e,
            out bool resumable);
    }

    [Fact]
    public static async Task AttachContinuationToCompletedSource()
    {
        var source = new ValueTaskCompletionSource();
        True(source.TrySetResult());

        var task = new TaskCompletionSource();
        var awaiter = source.CreateTask(InfiniteTimeSpan, CancellationToken.None).GetAwaiter();
        awaiter.UnsafeOnCompleted(() =>
        {
            try
            {
                awaiter.GetResult();
                task.SetResult();
            }
            catch (Exception e)
            {
                task.SetException(e);
            }
        });

        await task.Task;
    }
}