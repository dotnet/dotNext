using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncBridgeTests : Assert
    {
        [Fact]
        public static async Task WaitForCancellationNoThrow()
        {
            using (var source = new CancellationTokenSource(400))
            {
                await source.Token.WaitAsync();
                True(source.IsCancellationRequested);
            }
        }

        [Fact]
        public static async Task WaitForCancellation()
        {
            using (var source = new CancellationTokenSource(400))
            {
                await ThrowsAsync<OperationCanceledException>(async () => await source.Token.WaitAsync(true));
                True(source.IsCancellationRequested);
            }
        }

        [Fact]
        public static async Task WaitForSignal()
        {
            using (var ev = new ManualResetEvent(false))
            {
                ThreadPool.QueueUserWorkItem(state => ev.Set());
                await ev.WaitAsync();
            }
        }

        [Fact]
        public static void AlreadySignaled()
        {
            using (var ev = new ManualResetEvent(true))
            {
                True(ev.WaitAsync().IsCompleted);
            }
        }
    }
}
