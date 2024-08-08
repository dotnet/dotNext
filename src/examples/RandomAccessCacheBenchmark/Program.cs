using System.Diagnostics.Metrics;
using System.IO.Hashing;
using DotNext;
using DotNext.Buffers;
using DotNext.Diagnostics;
using DotNext.Runtime.Caching;

// Usage: program <number-of-entries> <cache-size> <duration-in-seconds> <parallel-requests>

switch (args)
{
    case [var numberOfEntries, var cacheSize, var durationInSeconds, var parallelRequests]:
        await RunBenchmark(
            int.Parse(numberOfEntries),
            int.Parse(cacheSize),
            TimeSpan.FromSeconds(int.Parse(durationInSeconds)),
            int.Parse(parallelRequests)).ConfigureAwait(false);
        break;
    default:
        Console.WriteLine("Usage: program <number-of-entries> <cache-size> <duration-in-seconds> <parallel-requests> <mode:cache|nocache>");
        break;
}

static async Task RunBenchmark(int numberOfEntries, int cacheSize, TimeSpan duration, int parallelRequests)
{
    // setup files to be accessed by using cache
    var files = MakeRandomFiles(numberOfEntries);
    var cache = new RandomAccessCache<string, MemoryOwner<byte>>(cacheSize) { OnEviction = Evict };
    var state = new BenchmarkState();
    var timeTracker = Task.Delay(duration);

    var requests = new List<Task>(parallelRequests);

    for (var i = 0; i < parallelRequests; i++)
    {
        requests.Add(state.ReadOrAddAsync(files, cache, timeTracker));
    }

    await Task.WhenAll(requests).ConfigureAwait(false);
}

static void Evict(string fileName, MemoryOwner<byte> content)
{
    content.Dispose();
}

static IReadOnlyList<string> MakeRandomFiles(int numberOfEntries)
{
    var result = new List<string>();
    var directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
    directory.Create();

    Span<byte> buffer = stackalloc byte[BenchmarkState.CacheFileSize];
    for (var i = 0; i < numberOfEntries; i++)
    {
        var fileName = Path.Combine(directory.FullName, i.ToString());
        result.Add(fileName);
        using var handle = File.OpenHandle(fileName, FileMode.CreateNew, FileAccess.Write, FileShare.None, FileOptions.WriteThrough, numberOfEntries);
        Random.Shared.NextBytes(buffer);
        RandomAccess.Write(handle, buffer, fileOffset: 0L);
    }

    return result;
}

sealed class BenchmarkState
{
    internal const int CacheFileSize = 4096;

    private readonly Histogram<double> accessDuration;

    public BenchmarkState()
    {
        var meter = new Meter("RandomAccessCache");
        accessDuration = meter.CreateHistogram<double>("AccessDuration", "ms");
    }

    internal async Task ReadOrAddAsync(IReadOnlyList<string> files, RandomAccessCache<string, MemoryOwner<byte>> cache, Task timeTracker)
    {
        while (!timeTracker.IsCompleted)
        {
            var ts = new Timestamp();
            await ReadOrAddAsync(files, cache).ConfigureAwait(false);
            accessDuration.Record(ts.ElapsedMilliseconds);
        }
    }

    internal Task ReadOrAddAsync(IReadOnlyList<string> files, RandomAccessCache<string, MemoryOwner<byte>> cache)
        => ReadOrAddAsync(Random.Shared.Peek(files).Value, cache);
    
    private Task ReadOrAddAsync(string fileName, RandomAccessCache<string, MemoryOwner<byte>> cache)
    {
        Task task;
        if (cache.TryGet(fileName, out var handle))
        {
            using (handle)
            {
                Crc32.HashToUInt32(handle.Value.Span);
            }
            
            task = Task.CompletedTask;
        }
        else
        {
            task = AddAsync(fileName, cache);
        }

        return task;
    }

    private async Task AddAsync(string fileName, RandomAccessCache<string, MemoryOwner<byte>> cache)
    {
        using var handle = await cache.ChangeAsync(fileName).ConfigureAwait(false);
        if (handle.TryGetValue(out var buffer))
        {
            Crc32.HashToUInt32(buffer.Span);
        }
        else
        {
            using var fileHandle = File.OpenHandle(fileName, options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            buffer = Memory.AllocateExactly<byte>(CacheFileSize);
            await RandomAccess.ReadAsync(fileHandle, buffer.Memory, fileOffset: 0L).ConfigureAwait(false);
            handle.SetValue(buffer);
        }
    }
}