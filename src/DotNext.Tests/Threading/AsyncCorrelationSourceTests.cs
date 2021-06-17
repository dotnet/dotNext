using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    public sealed class AsyncCorrelationSourceTests : Test
    {
        [Fact]
        public static void RoutingByKey()
        {
            var key1 = Guid.NewGuid();
            var key2 = Guid.NewGuid();

            var source = new AsyncCorrelationSource<Guid, int>(10);
            using var listener1 = source.Listen(key1);
            using var listener2 = source.Listen(key2);

            True(source.TrySignal(key1, 10));
            True(listener1.WaitAsync(DefaultTimeout).IsCompletedSuccessfully);
            Equal(10, listener1.WaitAsync().Result);

            False(listener2.WaitAsync().IsCompleted);

            True(source.TrySignal(key2, 20));
            True(listener1.WaitAsync(DefaultTimeout).IsCompletedSuccessfully);
            Equal(20, listener2.WaitAsync().Result);
        }

        [Fact]
        public static async Task BroadcastException()
        {
            var key1 = Guid.NewGuid();
            var key2 = Guid.NewGuid();

            var source = new AsyncCorrelationSource<Guid, int>(10);
            using var listener1 = source.Listen(key1);
            using var listener2 = source.Listen(key2);

            source.PulseAll(new ArithmeticException());

            await ThrowsAsync<ArithmeticException>(listener1.WaitAsync);
            await ThrowsAsync<ArithmeticException>(listener2.WaitAsync);
        }

        [Fact]
        public static async Task BroadcastCancellation()
        {
            var key1 = Guid.NewGuid();
            var key2 = Guid.NewGuid();

            var source = new AsyncCorrelationSource<Guid, int>(10);
            using var listener1 = source.Listen(key1);
            using var listener2 = source.Listen(key2);

            source.PulseAll(new CancellationToken(true));

            await ThrowsAnyAsync<OperationCanceledException>(listener1.WaitAsync);
            await ThrowsAnyAsync<OperationCanceledException>(listener2.WaitAsync);
        }

        [Fact]
        public static async Task BroadcastResult()
        {
            var key1 = Guid.NewGuid();
            var key2 = Guid.NewGuid();

            var source = new AsyncCorrelationSource<Guid, int>(10);
            using var listener1 = source.Listen(key1);
            using var listener2 = source.Listen(key2);

            source.PulseAll(42);

            Equal(42, await listener1.WaitAsync());
            Equal(42, await listener2.WaitAsync());
        }
    }
}