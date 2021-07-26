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
            var listener1 = source.WaitAsync(key1);
            var listener2 = source.WaitAsync(key2);

            True(source.TrySignal(key1, 10));
            True(listener1.IsCompletedSuccessfully);
            Equal(10, listener1.Result);

            False(listener2.IsCompleted);

            True(source.TrySignal(key2, 20));
            True(listener2.IsCompletedSuccessfully);
            Equal(20, listener2.Result);
        }

        [Fact]
        public static async Task BroadcastException()
        {
            var key1 = Guid.NewGuid();
            var key2 = Guid.NewGuid();

            var source = new AsyncCorrelationSource<Guid, int>(10);
            var listener1 = source.WaitAsync(key1);
            var listener2 = source.WaitAsync(key2);

            source.PulseAll(new ArithmeticException());

            await ThrowsAsync<ArithmeticException>(async () => await listener1);
            await ThrowsAsync<ArithmeticException>(async () => await listener2);
        }

        [Fact]
        public static async Task BroadcastCancellation()
        {
            var key1 = Guid.NewGuid();
            var key2 = Guid.NewGuid();

            var source = new AsyncCorrelationSource<Guid, int>(10);
            var listener1 = source.WaitAsync(key1);
            var listener2 = source.WaitAsync(key2);

            source.PulseAll(new CancellationToken(true));

            await ThrowsAnyAsync<OperationCanceledException>(async () => await listener1);
            await ThrowsAnyAsync<OperationCanceledException>(async () => await listener2);
        }

        [Fact]
        public static async Task BroadcastResult()
        {
            var key1 = Guid.NewGuid();
            var key2 = Guid.NewGuid();

            var source = new AsyncCorrelationSource<Guid, int>(10);
            var listener1 = source.WaitAsync(key1);
            var listener2 = source.WaitAsync(key2);

            source.PulseAll(42);

            Equal(42, await listener1);
            Equal(42, await listener2);
        }
    }
}