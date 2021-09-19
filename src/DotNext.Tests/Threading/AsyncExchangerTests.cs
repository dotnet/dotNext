using System.Diagnostics.CodeAnalysis;

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
            var task = exchanger.ExchangeAsync(42, DefaultTimeout, source.Token);
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
            await disposeTask;
        }

        [Fact]
        public static async Task CheckCancellation()
        {
            await using var exchanger = new AsyncExchanger<int>();
            var task = exchanger.ExchangeAsync(42, new CancellationToken(true));
            await ThrowsAsync<OperationCanceledException>(task.AsTask);
            task = exchanger.ExchangeAsync(42);
            False(task.IsCompleted);
            Equal(42, await exchanger.ExchangeAsync(56));
            Equal(56, await task);
        }

        [Fact]
        public static async Task Termination()
        {
            await using var exchanger = new AsyncExchanger<int>();
            var task = exchanger.ExchangeAsync(42);
            exchanger.Terminate();
            await ThrowsAsync<ExchangeTerminatedException>(task.AsTask);
            True(exchanger.IsTerminated);
            task = exchanger.ExchangeAsync(56);
            True(task.IsFaulted);
            await ThrowsAsync<ExchangeTerminatedException>(task.AsTask);
        }

        [Fact]
        public static async Task SynchronousExchange()
        {
            using var exchanger = new AsyncExchanger<int>();
            var value = 56;
            False(exchanger.TryExchange(ref value));
            Equal(56, value);

            var task = exchanger.ExchangeAsync(42);
            True(exchanger.TryExchange(ref value));
            Equal(42, value);
            Equal(56, await task);

            exchanger.Terminate();
            Throws<ExchangeTerminatedException>(() =>
            {
                var tmp = 0;
                exchanger.TryExchange(ref tmp);
            });
        }

        [Fact]
        public static async Task StressTest()
        {
            await using var exchanger = new AsyncExchanger<int>();

            var task1 = Task.Run(async () =>
            {
                for (var i = 0; i < 200; i++)
                    Equal(52, await exchanger.ExchangeAsync(42));
            });

            var task2 = Task.Run(async () =>
            {
                for (var i = 0; i < 200; i++)
                    Equal(42, await exchanger.ExchangeAsync(52));
            });

            await Task.WhenAll(task1, task2);
        }
    }
}