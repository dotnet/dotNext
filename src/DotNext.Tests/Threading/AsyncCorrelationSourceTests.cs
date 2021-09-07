using Xunit;

namespace DotNext.Threading
{
    public sealed class AsyncCorrelationSourceTests : Test
    {
        [Fact]
        public static async Task RoutingByKey()
        {
            var key1 = Guid.NewGuid();
            var key2 = Guid.NewGuid();

            var source = new AsyncCorrelationSource<Guid, int>(10);
            var listener1 = source.WaitAsync(key1);
            var listener2 = source.WaitAsync(key2);

            True(source.Pulse(key1, 10));
            Equal(10, await listener1);

            False(listener2.IsCompleted);

            True(source.Pulse(key2, 20));
            Equal(20, await listener2);
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

            await ThrowsAsync<ArithmeticException>(listener1.AsTask);
            await ThrowsAsync<ArithmeticException>(listener2.AsTask);
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

            await ThrowsAnyAsync<OperationCanceledException>(listener1.AsTask);
            await ThrowsAnyAsync<OperationCanceledException>(listener2.AsTask);
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