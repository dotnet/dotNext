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
    private ReaderWriterSpinLock spinLock;

    [GlobalSetup]
    public void Initialize()
    {
        rwLock = new(LockRecursionPolicy.NoRecursion);
        asyncRwLock = new();
        spinLock = new();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        rwLock.Dispose();
        asyncRwLock.Dispose();
        spinLock = default;
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

    [Benchmark(Description = "AsyncReaderWriterLock synchronous acquire/release")]
    public void AcquireReleaseAsyncRWLockAsynchronously()
    {
        asyncRwLock.EnterWriteLockAsync().GetAwaiter().GetResult();
        asyncRwLock.Release();
    }

    [Benchmark(Description = "ReaderWriterSpinLock acquire/release")]
    public void AcquireReleaseRWLockSpin()
    {
        spinLock.EnterWriteLock();
        spinLock.ExitWriteLock();
    }
}