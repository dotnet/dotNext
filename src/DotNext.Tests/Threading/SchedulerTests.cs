using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
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

        [Fact]
        public static async Task CompleteWithoutResult()
        {
            await Scheduler.ScheduleAsync(static (args, token) => ValueTask.CompletedTask, 42, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public static async Task CompleteWithResult()
        {
            Equal(42, await Scheduler.ScheduleAsync(static (args, token) => ValueTask.FromResult(42), 42, TimeSpan.FromMilliseconds(1)));
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
}