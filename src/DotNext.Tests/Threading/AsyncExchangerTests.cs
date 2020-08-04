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
        public static void ExchangeInts()
        {
            using var source = new CancellationTokenSource();
            using var exchanger = new AsyncExchanger<int>(false);
            var task = exchanger.ExchangeAsync(42, source.Token);
            False(task.IsCompleted);
            var task2 = exchanger.ExchangeAsync(52, source.Token);
            True(task2.IsCompletedSuccessfully);
            Equal(42, task2.Result);
            True(task.IsCompletedSuccessfully);
            Equal(52, task.Result);
        }

        [Fact]
        public static void ExchangerGracefulShutdown()
        {
            using var exchanger = new AsyncExchanger<int>(false);
            var task = exchanger.ExchangeAsync(42);
            False(task.IsCompleted);
            var task2 = exchanger.ExchangeAsync(52);
            True(task2.IsCompletedSuccessfully);
            Equal(42, task2.Result);
            True(task.IsCompletedSuccessfully);
            Equal(52, task.Result);
            True(exchanger.DisposeAsync().IsCompletedSuccessfully);
        }

        [Fact]
        public static void ExchangerGracefulShutdown2()
        {
            using var exchanger = new AsyncExchanger<int>(false);
            var task = exchanger.ExchangeAsync(42);
            var disposeTask = exchanger.DisposeAsync();
            False(disposeTask.IsCompleted);
            task = exchanger.ExchangeAsync(52);
            True(task.IsFaulted);
            True(disposeTask.IsCompletedSuccessfully);
        }
    }
}