using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class TaskSchedulerTests : Test
{
    [Fact]
    public static async Task CancelWithoutResult()
    {
        var task = System.Threading.Tasks.TaskScheduler.ScheduleAsync(static (_, _) => ValueTask.CompletedTask, 42, InfiniteTimeSpan, TestToken);
        False(task.Task.IsCompleted);
        task.Cancel();
        await ThrowsAsync<DelayedTaskCanceledException>(task.Task);
        True(task.Task.IsCanceled);
    }

    [Fact]
    public static async Task CancelWithResult()
    {
        var task = System.Threading.Tasks.TaskScheduler.ScheduleAsync(static (args, _) => ValueTask.FromResult(args), 42, InfiniteTimeSpan, TestToken);
        False(task.Task.IsCompleted);
        task.Cancel();
        await ThrowsAsync<DelayedTaskCanceledException>(task.Task);
        True(task.Task.IsCanceled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public static async Task CompleteWithoutResult(int delay)
    {
        await System.Threading.Tasks.TaskScheduler.ScheduleAsync(static (_, _) => ValueTask.CompletedTask, 42, TimeSpan.FromMilliseconds(delay), TestToken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public static async Task CompleteWithResult(int delay)
    {
        Equal(42, await System.Threading.Tasks.TaskScheduler.ScheduleAsync(static (_, _) => ValueTask.FromResult(42), 42, TimeSpan.FromMilliseconds(delay), TestToken));
    }

    [Fact]
    public static void NullCallback()
    {
        Throws<ArgumentNullException>(new Action(static () => System.Threading.Tasks.TaskScheduler.ScheduleAsync(null, 42, InfiniteTimeSpan, TestToken)));
        Throws<ArgumentNullException>(new Action(static () => System.Threading.Tasks.TaskScheduler.ScheduleAsync(null, 42, InfiniteTimeSpan, TestToken)));
    }

    [Fact]
    public static void ScheduleCanceled()
    {
        True(System.Threading.Tasks.TaskScheduler.ScheduleAsync(static (_, _) => ValueTask.CompletedTask, 42, InfiniteTimeSpan, new(true)).Task.IsCanceled);
        True(System.Threading.Tasks.TaskScheduler.ScheduleAsync(static (_, _) => ValueTask.FromResult(42), 42, InfiniteTimeSpan, new(true)).Task.IsCanceled);
    }

    [Fact]
    public static void TooLargeTimeout()
    {
        Throws<ArgumentOutOfRangeException>(static () => System.Threading.Tasks.TaskScheduler.ScheduleAsync(static (args, _) => ValueTask.FromResult(args), 42, TimeSpan.FromMilliseconds(Timeout.MaxTimeoutParameterTicks + 1L), TestToken));
    }
}