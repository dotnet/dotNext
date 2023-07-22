namespace DotNext.Threading;

public sealed class SchedulerTests : Test
{
    [Fact]
    public static async Task CancelWithoutResult()
    {
        var task = Scheduler.ScheduleAsync(static (args, token) => ValueTask.CompletedTask, 42, DefaultTimeout);
        False(task.Task.IsCompleted);
        task.Cancel();
        await ThrowsAsync<Scheduler.DelayedTaskCanceledException>(Func.Constant<Task>(task.Task));
        True(task.Task.IsCanceled);
    }

    [Fact]
    public static async Task CancelWithResult()
    {
        var task = Scheduler.ScheduleAsync(static (args, token) => ValueTask.FromResult(args), 42, DefaultTimeout);
        False(task.Task.IsCompleted);
        task.Cancel();
        await ThrowsAsync<Scheduler.DelayedTaskCanceledException>(Func.Constant<Task<int>>(task.Task));
        True(task.Task.IsCanceled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public static async Task CompleteWithoutResult(int delay)
    {
        await Scheduler.ScheduleAsync(static (args, token) => ValueTask.CompletedTask, 42, TimeSpan.FromMilliseconds(delay));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public static async Task CompleteWithResult(int delay)
    {
        Equal(42, await Scheduler.ScheduleAsync(static (args, token) => ValueTask.FromResult(42), 42, TimeSpan.FromMilliseconds(delay)));
    }

    [Fact]
    public static void NullCallback()
    {
        Throws<ArgumentNullException>(new Action(static () => Scheduler.ScheduleAsync<int>(null, 42, DefaultTimeout)));
        Throws<ArgumentNullException>(new Action(static () => Scheduler.ScheduleAsync<int>(null, 42, DefaultTimeout)));
    }

    [Fact]
    public static void ScheduleCanceled()
    {
        True(Scheduler.ScheduleAsync(static (args, token) => ValueTask.CompletedTask, 42, DefaultTimeout, new(true)).Task.IsCanceled);
        True(Scheduler.ScheduleAsync(static (args, token) => ValueTask.FromResult(42), 42, DefaultTimeout, new(true)).Task.IsCanceled);
    }
}