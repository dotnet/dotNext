using static System.Threading.Timeout;

namespace DotNext.Threading;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class SchedulerTests : Test
{
    [Fact]
    public static async Task CancelWithoutResult()
    {
        var task = Scheduler.ScheduleAsync(static (_, _) => ValueTask.CompletedTask, 42, InfiniteTimeSpan, TestToken);
        False(task.Task.IsCompleted);
        task.Cancel();
        await ThrowsAsync<Scheduler.DelayedTaskCanceledException>(task.Task);
        True(task.Task.IsCanceled);
    }

    [Fact]
    public static async Task CancelWithResult()
    {
        var task = Scheduler.ScheduleAsync(static (args, _) => ValueTask.FromResult(args), 42, InfiniteTimeSpan, TestToken);
        False(task.Task.IsCompleted);
        task.Cancel();
        await ThrowsAsync<Scheduler.DelayedTaskCanceledException>(task.Task);
        True(task.Task.IsCanceled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public static async Task CompleteWithoutResult(int delay)
    {
        await Scheduler.ScheduleAsync(static (_, _) => ValueTask.CompletedTask, 42, TimeSpan.FromMilliseconds(delay), TestToken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public static async Task CompleteWithResult(int delay)
    {
        Equal(42, await Scheduler.ScheduleAsync(static (_, _) => ValueTask.FromResult(42), 42, TimeSpan.FromMilliseconds(delay), TestToken));
    }

    [Fact]
    public static void NullCallback()
    {
        Throws<ArgumentNullException>(new Action(static () => Scheduler.ScheduleAsync(null, 42, InfiniteTimeSpan, TestToken)));
        Throws<ArgumentNullException>(new Action(static () => Scheduler.ScheduleAsync(null, 42, InfiniteTimeSpan, TestToken)));
    }

    [Fact]
    public static void ScheduleCanceled()
    {
        True(Scheduler.ScheduleAsync(static (_, _) => ValueTask.CompletedTask, 42, InfiniteTimeSpan, new(true)).Task.IsCanceled);
        True(Scheduler.ScheduleAsync(static (_, _) => ValueTask.FromResult(42), 42, InfiniteTimeSpan, new(true)).Task.IsCanceled);
    }

    [Fact]
    public static void TooLargeTimeout()
    {
        Throws<ArgumentOutOfRangeException>(static () => Scheduler.ScheduleAsync(static (args, _) => ValueTask.FromResult(args), 42, TimeSpan.FromMilliseconds(Timeout.MaxTimeoutParameterTicks + 1L), TestToken));
    }
}