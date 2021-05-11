using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    using TrueTask = Tasks.CompletedTask<bool, Generic.BooleanConst.True>;

    [ExcludeFromCodeCoverage]
    public sealed class AsyncTimerTests : Test
    {
        private sealed class Counter : EventWaitHandle
        {
            internal int Value;

            internal Counter()
                : base(false, EventResetMode.AutoReset)
            {
            }

            internal Task<bool> Run(CancellationToken token)
            {
                Value.IncrementAndGet();
                Set();
                return token.IsCancellationRequested ? Task.FromCanceled<bool>(token) : TrueTask.Task;
            }
        }

        [Fact]
        public static async Task StartStopAsync()
        {
            using var counter = new Counter();
            using var timer = new AsyncTimer(counter.Run);
            True(timer.Start(TimeSpan.FromMilliseconds(10)));
            True(counter.WaitOne(DefaultTimeout));
            True(timer.IsRunning);
            False(await timer.StopAsync());
            False(timer.IsRunning);
            var currentValue = counter.Value;
            True(currentValue > 0);
            //ensure that timer is no more executing
            await Task.Delay(100);
            Equal(currentValue, counter.Value);
        }

        [Fact]
        public static void GracefulShutdown()
        {
            using var counter = new Counter();
            var timer = new AsyncTimer(counter.Run);
            var task = timer.DisposeAsync();
            True(task.IsCompletedSuccessfully);
        }

        [Fact]
        public static async Task GracefulShutdownAsync()
        {
            using var counter = new Counter();
            var timer = new AsyncTimer(counter.Run);
            True(timer.Start(TimeSpan.FromMilliseconds(10)));
            True(counter.WaitOne(DefaultTimeout));
            True(timer.IsRunning);
            await timer.DisposeAsync();
            False(timer.IsRunning);
        }
    }
}
