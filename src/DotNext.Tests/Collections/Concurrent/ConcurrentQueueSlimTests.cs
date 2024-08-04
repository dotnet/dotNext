namespace DotNext.Collections.Concurrent;

public sealed class ConcurrentQueueSlimTests : Test
{
    [Fact]
    public static void DequeueEmpty()
    {
        var queue = new ConcurrentQueueSlim<int>();
        queue.Enqueue(42);
        
        True(queue.TryDequeue(out var item));
        Equal(42, item);

        False(queue.TryDequeue(out item));

        queue.Enqueue(43);
        True(queue.TryDequeue(out item));
        Equal(43, item);
    }
    
    [Fact]
    public static async Task SingleWriteMultipleReaders()
    {
        const int writeCount = 100;
        const int readCount = writeCount / 2;

        var queue = new ConcurrentQueueSlim<int>();
        var output1 = new int[readCount];
        var output2 = new int[readCount];
        
        var writeTask = Task.Run(() => WriteJob(queue));
        var readTask1 = Task.Run(() => ReadJob(queue, output1));
        var readTask2 = Task.Run(() => ReadJob(queue, output2));

        await Task.WhenAll(writeTask, readTask1, readTask2).WaitAsync(DefaultTimeout);

        Empty(output1.Intersect(output2));

        var set = new HashSet<int>(output1.Concat(output2));
        Equal(writeCount, set.Count);

        for (var i = 0; i < writeCount; i++)
        {
            Contains(i, set);
        }

        static void WriteJob(ConcurrentQueueSlim<int> queue)
        {
            for (var i = 0; i < writeCount; i++)
            {
                queue.Enqueue(i);
            }
        }

        static void ReadJob(ConcurrentQueueSlim<int> queue, int[] output)
        {
            for (var i = 0; i < readCount;)
            {
                if (queue.TryDequeue(out var item))
                {
                    output[i++] = item;
                }
            }
        }
    }

    [Fact]
    public static void ConsumeQueue()
    {
        var queue = new ConcurrentQueueSlim<int>();
        queue.Enqueue(10);
        queue.Enqueue(20);

        Equal(new[] { 10, 20 }, queue.Consume());
        False(queue.TryDequeue(out _));
    }
}