using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading;

[ExcludeFromCodeCoverage]
public sealed class AsyncBatchedExecutorTests : Test
{
    private static int _callsCount;
    
    [Theory]
    [InlineData(10, 5)]
    [InlineData(10, 100)]
    public static async Task CheckExecute(int batchSize, int collectionSize)
    {
        _callsCount = 0;
        var collection = Enumerable.Range(1, collectionSize);
        await collection.ExecuteBatched(batchSize, MockedCall);
        
        True(_callsCount == collectionSize);
    }
    
    [Theory]
    [InlineData(10, 5)]
    [InlineData(10, 100)]
    public static async Task CheckExecuteWithResult(int batchSize, int collectionSize)
    {
        var collection = Enumerable.Range(1, collectionSize);

        var results = new List<string>();
        await foreach (var result in collection.ExecuteBatchedWithResult(batchSize, MockedCallWithResult))
            results.Add(result);

        True(results.Count == collectionSize);
        True(results.All(z => z == "Test"));
    }

    private static async Task MockedCall(int number)
    {
        _callsCount++;
        await Task.Delay(number);
    }
    
    private static async Task<string> MockedCallWithResult(int number)
    {
        await Task.Delay(number);

        return "Test";
    }
}