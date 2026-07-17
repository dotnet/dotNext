using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System.Threading;

namespace DotNext.Threading;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class AsyncReaderWriterLockBenchmark
{
    private ReaderWriterLockSlim rwLock;
    private AsyncReaderWriterLock asyncRwLock;

    [GlobalSetup]
    public void Initialize()
    {
        rwLock = new(LockRecursionPolicy.NoRecursion);
        asyncRwLock = new();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        rwLock.Dispose();
        asyncRwLock.Dispose();
    }

    [Benchmark(Description = "ReaderWriterLockSlim acquire/release", Baseline = true)]
    public void AcquireReleaseRWLockSlim()
    {
        rwLock.EnterWriteLock();
        rwLock.ExitWriteLock();
    }

    [Benchmark(Description = "AsyncReaderWriterLock synchronous acquire/release")]
    public void AcquireReleaseAsyncRWLockSynchronously()
    {
        asyncRwLock.TryEnterWriteLock();
        asyncRwLock.Release();
    }

    [Benchmark(Description = "AsyncReaderWriterLock asynchronous acquire/release")]
    public void AcquireReleaseAsyncRWLockAsynchronously()
    {
        asyncRwLock.EnterWriteLockAsync().GetAwaiter().GetResult();
        asyncRwLock.Release();
    }
}