using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncBridgeTests : Test
    {
        [Fact]
        public static async Task WaitForCancellationNoThrow()
        {
            using var source = new CancellationTokenSource(400);
            await source.Token.WaitAsync();
            True(source.IsCancellationRequested);
        }

        [Fact]
        public static async Task WaitForCancellation()
        {
            using var source = new CancellationTokenSource(400);
            await ThrowsAsync<OperationCanceledException>(async () => await source.Token.WaitAsync(true));
            True(source.IsCancellationRequested);
        }

        [Fact]
        public static async Task WaitForSignal()
        {
            using var ev = new ManualResetEvent(false);
            ThreadPool.QueueUserWorkItem(static state => state.Set(), ev, false);
            await ev.WaitAsync(DefaultTimeout);
        }

        [Fact]
        public static async Task AlreadySignaled()
        {
            using var ev = new ManualResetEvent(true);
            True(await ev.WaitAsync(DefaultTimeout));
        }
    }
}
