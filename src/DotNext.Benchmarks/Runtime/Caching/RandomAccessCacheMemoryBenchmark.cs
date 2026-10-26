using System;
using System.Buffers.Binary;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;

namespace DotNext.Runtime.Caching;

// Compares the managed and unmanaged caches with the same number of entries:
// - Populate: the managed heap allocations needed to fill the cache (see Allocated column)
// - FullGC: the duration of a full blocking GC while the filled cache is alive, which reflects the GC mark work
// The memory retained by the filled cache is printed to the log of the benchmark process
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 2, iterationCount: 15)]
[InvocationCount(1)]
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class RandomAccessCacheMemoryBenchmark
{
    private const string PopulateCategory = "Populate";
    private const string FullGCCategory = "FullGC";
    private const string UnmanagedMeterName = "DotNext.Runtime.Caching.RandomAccessCache.Unmanaged";

    // reused for every key, because the unmanaged cache copies the key to the entry
    private readonly byte[] keyBuffer = new byte[sizeof(long)];
    private RandomAccessCache<long, long> managedCache;
    private RandomAccessCache<long> unmanagedCache;

    [Params(100_000, 1_000_000)]
    public int Count;

    private async Task PopulateManagedAsync()
    {
        for (var key = 0L; key < Count; key++)
        {
            using var session = await managedCache.ChangeAsync(key);
            session.SetValue(key);
        }

        // the entries are promoted in order, so the last one completes the promotion of all of them
        await using (var session = await managedCache.ChangeAsync(-1L))
        {
            session.SetValue(-1L);
        }
    }

    private async Task PopulateUnmanagedAsync()
    {
        for (var key = 0L; key < Count; key++)
        {
            BinaryPrimitives.WriteInt64LittleEndian(keyBuffer, key);
            using var session = await unmanagedCache.ChangeAsync(RandomAccessCache.AsKey(keyBuffer));
            session.SetValue(key);
        }

        BinaryPrimitives.WriteInt64LittleEndian(keyBuffer, -1L);
        await using (var session = await unmanagedCache.ChangeAsync(RandomAccessCache.AsKey(keyBuffer)))
        {
            session.SetValue(-1L);
        }
    }

    // the capacity covers all the entries, so nothing is evicted
    private int Capacity => Count + 1;

    [IterationSetup(Target = nameof(PopulateManagedCache))]
    public void CreateManagedCache() => managedCache = new(Capacity);

    [IterationCleanup(Target = nameof(PopulateManagedCache))]
    public void DisposeManagedCache() => managedCache.DisposeAsync().AsTask().Wait();

    [IterationSetup(Target = nameof(PopulateUnmanagedCache))]
    public void CreateUnmanagedCache() => unmanagedCache = new(Capacity);

    [IterationCleanup(Target = nameof(PopulateUnmanagedCache))]
    public void DisposeUnmanagedCache() => unmanagedCache.DisposeAsync().AsTask().Wait();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(PopulateCategory)]
    public Task PopulateManagedCache() => PopulateManagedAsync();

    [Benchmark]
    [BenchmarkCategory(PopulateCategory)]
    public Task PopulateUnmanagedCache() => PopulateUnmanagedAsync();

    [GlobalSetup(Target = nameof(FullGCWithManagedCache))]
    public void SetupFullGCWithManagedCache()
    {
        var before = GetManagedHeapSize();
        managedCache = new(Capacity);
        PopulateManagedAsync().Wait();
        Console.WriteLine($"// Managed cache with {Count} entries retains {GetManagedHeapSize() - before} bytes of managed heap");
    }

    [GlobalCleanup(Target = nameof(FullGCWithManagedCache))]
    public void CleanupFullGCWithManagedCache() => DisposeManagedCache();

    [GlobalSetup(Target = nameof(FullGCWithUnmanagedCache))]
    public void SetupFullGCWithUnmanagedCache()
    {
        var before = GetManagedHeapSize();
        long unmanagedMemory = 0L;
        using (var listener = new MeterListener())
        {
            listener.InstrumentPublished = static (instrument, listener) =>
            {
                if (instrument is { Meter.Name: UnmanagedMeterName, Name: "allocated-memory" })
                    listener.EnableMeasurementEvents(instrument);
            };

            listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => Interlocked.Add(ref unmanagedMemory, measurement));
            listener.Start();

            unmanagedCache = new(Capacity);
            PopulateUnmanagedAsync().Wait();
        }

        Console.WriteLine($"// Unmanaged cache with {Count} entries retains {GetManagedHeapSize() - before} bytes of managed heap and {unmanagedMemory} bytes of unmanaged memory");
    }

    [GlobalCleanup(Target = nameof(FullGCWithUnmanagedCache))]
    public void CleanupFullGCWithUnmanagedCache() => DisposeUnmanagedCache();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory(FullGCCategory)]
    public void FullGCWithoutCache() => GC.Collect();

    [Benchmark]
    [BenchmarkCategory(FullGCCategory)]
    public void FullGCWithManagedCache() => GC.Collect();

    [Benchmark]
    [BenchmarkCategory(FullGCCategory)]
    public void FullGCWithUnmanagedCache() => GC.Collect();

    private static long GetManagedHeapSize()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        return GC.GetTotalMemory(forceFullCollection: true);
    }
}
