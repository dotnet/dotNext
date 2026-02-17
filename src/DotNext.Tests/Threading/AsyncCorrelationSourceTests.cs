using static System.Threading.Timeout;

namespace DotNext.Threading;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class AsyncCorrelationSourceTests : Test
{
    [Fact]
    public static async Task RoutingByKey()
    {
        var key1 = Guid.NewGuid();
        var key2 = Guid.NewGuid();

        var source = new AsyncCorrelationSource<Guid, int>(10);
        var listener1 = source.WaitAsync(key1, TestToken);
        var listener2 = source.WaitAsync(key2, TestToken);

        True(source.Pulse(key1, 10, out var userData));
        Null(userData);
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
        var listener1 = source.WaitAsync(key1, TestToken);
        var listener2 = source.WaitAsync(key2, TestToken);

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
        var listener1 = source.WaitAsync(key1, TestToken);
        var listener2 = source.WaitAsync(key2, TestToken);

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
        var listener1 = source.WaitAsync(key1, TestToken);
        var listener2 = source.WaitAsync(key2, TestToken);

        source.PulseAll(42);

        Equal(42, await listener1);
        Equal(42, await listener2);
    }

    [Fact]
    public static async Task PulseWithException()
    {
        var key1 = Guid.NewGuid();

        var source = new AsyncCorrelationSource<Guid, int>(10);
        var listener1 = source.WaitAsync(key1, TestToken);

        True(source.Pulse(key1, new ArithmeticException()));
        await ThrowsAsync<ArithmeticException>(listener1.AsTask);
    }
    
    [Fact]
    public static async Task UserDataPropagation()
    {
        var key1 = Guid.NewGuid();

        var source = new AsyncCorrelationSource<Guid, int>(10);
        var listener1 = source.WaitAsync(key1, string.Empty, InfiniteTimeSpan, TestToken);

        True(source.Pulse(key1, new ArithmeticException(), out var userData));
        Same(string.Empty, userData);
        await ThrowsAsync<ArithmeticException>(listener1.AsTask);
    }
}