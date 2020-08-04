using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncExchangerTests : Test
    {
        [Fact]
        public static async Task ExchangeInts()
        {
            using var source = new CancellationTokenSource();
            using var exchanger = new AsyncExchanger<int>();
            var task = exchanger.ExchangeAsync(42, source.Token);
            False(task.IsCompleted);
            Equal(42, await exchanger.ExchangeAsync(52, source.Token));
            Equal(52, await task);
        }

        [Fact]
        public static async Task ExchangerGracefulShutdown()
        {
            using var exchanger = new AsyncExchanger<int>();
            var task = exchanger.ExchangeAsync(42);
            False(task.IsCompleted);
            Equal(42, await exchanger.ExchangeAsync(52));
            Equal(52, await task);
            True(exchanger.DisposeAsync().IsCompletedSuccessfully);
        }

        [Fact]
        public static async Task ExchangerGracefulShutdown2()
        {
            using var exchanger = new AsyncExchanger<int>();
            var task = exchanger.ExchangeAsync(42);
            var disposeTask = exchanger.DisposeAsync();
            False(disposeTask.IsCompleted);
            await ThrowsAsync<ObjectDisposedException>(exchanger.ExchangeAsync(52).AsTask);
            await ThrowsAsync<ObjectDisposedException>(task.AsTask);
            True(disposeTask.IsCompletedSuccessfully);
        }
    }
}