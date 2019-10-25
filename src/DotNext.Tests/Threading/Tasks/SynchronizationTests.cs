using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks
{
    public sealed class SynchronizationTests : Assert
    {
        [Fact]
        public static async Task WaitAsyncWithTimeout()
        {
            var source = new TaskCompletionSource<bool>();
            False(await source.Task.WaitAsync(TimeSpan.FromMilliseconds(10)));
            source.SetResult(true);
            True(await source.Task.WaitAsync(TimeSpan.FromMilliseconds(600)));
        }

        [Fact]
        public static async Task WaitAsyncWithToken()
        {
            using (var source = new CancellationTokenSource(100))
            {
                var task = Task.Delay(500);
                await ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(InfiniteTimeSpan, source.Token));
            }
        }
    }
}