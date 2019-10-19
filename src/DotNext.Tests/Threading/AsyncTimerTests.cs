using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    using TrueTask = Tasks.CompletedTask<bool, Generic.BooleanConst.True>;

    public sealed class AsyncTimerTests : Assert
    {
        private sealed class Counter
        {
            internal int Value;

            internal Task<bool> Run(CancellationToken token)
            {
                Value.IncrementAndGet();
                return TrueTask.Task;
            }
        }

        [Fact]
        public static async Task StartStopAsync()
        {
            var counter = new Counter();
            using (var timer = new AsyncTimer(counter.Run))
            {
                True(timer.Start(TimeSpan.FromMilliseconds(10)));
                await Task.Delay(100);
                True(timer.IsRunning);
                False(await timer.StopAsync());
                False(timer.IsRunning);
                var currentValue = counter.Value;
                True(currentValue > 0);
                //ensure that timer is no more executing
                await Task.Delay(100);
                Equal(currentValue, counter.Value);
            }
        }
    }
}
