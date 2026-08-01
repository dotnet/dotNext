using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Collections.Generic;

[Collection(TestCollections.AsyncPrimitives)]
public class AsyncMulticastSequenceTests : Test
{
    [Fact]
    public static async Task CompleteSequence()
    {
        var sequence = new AsyncMulticastSequence<int>();
        False(sequence.IsCompleted);
        
        await using var listener = sequence
            .As<IAsyncEnumerable<int>>()
            .GetAsyncEnumerator(TestToken);
        var task = listener.MoveNextAsync().AsTask();
        False(task.IsCompleted);
        
        True(sequence.TryComplete());
        True(sequence.IsCompleted);
        False(await task);
    }

    [Fact]
    public static async Task DestroyedEnumeratorDoesntConsume()
    {
        var sequence = new AsyncMulticastSequence<int>();
        var listener = sequence
            .As<IAsyncEnumerable<int>>()
            .GetAsyncEnumerator(TestToken);

        var consumerTask = listener.MoveNextAsync().AsTask();
        var producerTask = sequence.ProduceAsync(42, TestToken).AsTask();
        True(await consumerTask);
        Equal(42, listener.Current);
        await producerTask;

        await listener.DisposeAsync();
        consumerTask = listener.MoveNextAsync().AsTask();
        await sequence.ProduceAsync(43, TestToken);
        False(await consumerTask);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static async Task ListenAsync(bool isSequential)
    {
        var bag1 = new List<int>();
        var bag2 = new List<int>();
        var sequence = new AsyncMulticastSequence<int> { IsSequential = isSequential };
        
        await using var listener1 = sequence.Listen((item, _) =>
        {
            bag1.Add(item);
            return ValueTask.CompletedTask;
        }, TestToken);
        
        await using var listener2 = sequence.Listen((item, _) =>
        {
            bag2.Add(item);
            return ValueTask.CompletedTask;
        }, TestToken);

        await sequence.ProduceAsync(42, TestToken);
        await sequence.ProduceAsync(43, TestToken);

        Equal<int>(bag1, bag2);
        Equal<int>([42, 43], bag1);
    }
}