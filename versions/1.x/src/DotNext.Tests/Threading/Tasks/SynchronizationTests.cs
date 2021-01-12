using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks
{
    [ExcludeFromCodeCoverage]
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
                var task = new TaskCompletionSource<bool>().Task;
                await ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(InfiniteTimeSpan, source.Token));
            }
        }

        [Fact]
        public static void GetResult()
        {
            var task = Task.FromResult(42);
            var result = task.GetResult(TimeSpan.Zero);
            Null(result.Error);
            True(result.IsSuccessful);
            Equal(42, result.Value);

            result = task.GetResult(CancellationToken.None);
            Null(result.Error);
            True(result.IsSuccessful);
            Equal(42, result.Value);

            task = Task.FromCanceled<int>(new CancellationToken(true));
            result = task.GetResult(TimeSpan.Zero);
            NotNull(result.Error);
            False(result.IsSuccessful);
            Throws<AggregateException>(() => result.Value);
        }
    }
}