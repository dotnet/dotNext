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

    [Fact]
    public static async Task ListenAsync()
    {
        var bag1 = new List<int>();
        var bag2 = new List<int>();
        var sequence = new AsyncMulticastSequence<int> { IsSequential = true };
        
        await using var listener1 = sequence.ForEach((item, _) =>
        {
            bag1.Add(item);
            return ValueTask.CompletedTask;
        }, TestToken);
        
        await using var listener2 = sequence.ForEach((item, _) =>
        {
            bag2.Add(item);
            return ValueTask.CompletedTask;
        }, TestToken);

        await sequence.ProduceAsync(42, TestToken);
        await sequence.ProduceAsync(43, TestToken);

        Equal<int>(bag1, bag2);
        Equal<int>([42, 43], bag1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static async Task ProduceAndComplete(bool isSequential)
    {
        var sequence = new AsyncMulticastSequence<int>() { IsSequential = isSequential };
        var bag = new List<int>();

        await using var listener1 = sequence.ForEach((item, _) =>
            {
                bag.Add(item);
                return ValueTask.CompletedTask;
            },
            out var completion,
            TestToken);

        await sequence.ProduceAsync(42, TestToken);
        True(sequence.TryComplete());
        await completion.WaitAsync(TestToken);
        Equal<int>([42], bag);
    }
}