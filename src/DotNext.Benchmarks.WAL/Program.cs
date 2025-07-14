// See https://aka.ms/new-console-template for more information

using DotNext.Benchmarks.WAL;
using DotNext.Diagnostics;
using DotNext.Hosting;
using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.StateMachine;
using FASTER.core;

const int count = 2000;
const int entrySize = 1024;
using var cts = new ConsoleLifetimeTokenSource();

Console.WriteLine("Starting DotNext WAL Performance Test...");
await DotNextWalPerformanceTest(cts.Token).ConfigureAwait(false);

Console.WriteLine("Starting FASTER WAL Performance Test...");
await FasterLogPerformanceTest(cts.Token).ConfigureAwait(false);

static async Task DotNextWalPerformanceTest(CancellationToken token)
{
    var options = new WriteAheadLog.Options
    {
        Location = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
        ChunkSize = 512 * 1024, // page size is 512 KB
        MemoryManagement = WriteAheadLog.MemoryManagementStrategy.PrivateMemory,
    };

    var wal = new WriteAheadLog(options, new NoOpStateMachine());
    try
    {
        Memory<byte> buffer = new byte[entrySize];
        Random.Shared.NextBytes(buffer.Span);
        
        var ts = new Timestamp();
        for (var i = 0; i < count; i++)
        {
            var index = await wal.AppendAsync(buffer, token: token).ConfigureAwait(false);
            await wal.CommitAsync(index, token).ConfigureAwait(false);
        }

        Console.WriteLine($"Finished. Elapsed time: {ts.Elapsed}");
        await wal.FlushAsync(token).ConfigureAwait(false);
    }
    finally
    {
        await wal.DisposeAsync().ConfigureAwait(false);
    }
}

static async Task FasterLogPerformanceTest(CancellationToken token)
{
    using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token);
    var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    
    // file size is 512 KB
    using var fasterLog = new FasterLog(new FasterLogSettings(root) { SegmentSize = 1L << 22 });
    var committer = Task.Run(async () =>
    {
        while (await fasterLog.WaitUncommittedAsync(fasterLog.TailAddress, linkedToken.Token).ConfigureAwait(false))
        {
            await fasterLog.CommitAsync(linkedToken.Token).ConfigureAwait(false);
        }
    }, linkedToken.Token);

    Memory<byte> buffer = new byte[entrySize];
    Random.Shared.NextBytes(buffer.Span);

    var ts = new Timestamp();
    for (var i = 0; i < count; i++)
    {
        await fasterLog.EnqueueAsync(buffer, token).ConfigureAwait(false);
    }

    await fasterLog.CommitAsync(token).ConfigureAwait(false);
    Console.WriteLine($"Finished. Elapsed time: {ts.Elapsed}");
    linkedToken.Cancel();

    await committer.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
}