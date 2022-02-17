using System;
using System.Collections.Concurrent;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;

namespace DotNext.Runtime.Caching;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.Declared)]
[MemoryDiagnoser]
public class ConcurrentCacheBenchmark
{
    private const int Capacity = 10;

    private Thread[] threads;

    [Params(2, 3, 4, 5)]
    public int threadCount;

    [Params(CacheEvictionPolicy.LRU, CacheEvictionPolicy.LFU)]
    public CacheEvictionPolicy evictionPolicy;

    private ConcurrentCache<int, string> cache;
    private ConcurrentDictionary<int, string> dictionary;

    [IterationSetup(Target = nameof(ConcurrentCacheRead))]
    public void InitializeConcurrentCacheAccess()
    {
        cache = new(Capacity, Environment.ProcessorCount, evictionPolicy);

        // fill cache
        for (var i = 0; i < Capacity; i++)
            cache[i] = i.ToString();

        // read from cache
        threads = new Thread[threadCount];

        foreach (ref var thread in threads.AsSpan())
            thread = new Thread(Run);

        void Run()
        {
            var rnd = new Random();

            for (var i = 0; i < 100; i++)
                TouchCache(rnd);
        }

        void TouchCache(Random random)
        {
            var index = random.Next(Capacity);
            cache.TryGetValue(index, out _);
        }
    }

    [IterationCleanup(Target = nameof(ConcurrentCacheRead))]
    public void CleanupCache()
    {
        cache.Clear();
    }

    [IterationSetup(Target = nameof(ConcurrentDictionaryRead))]
    public void InitializeConcurrentDictionaryAccess()
    {
        dictionary = new(Environment.ProcessorCount, Capacity);

        // fill cache
        for (var i = 0; i < Capacity; i++)
            dictionary[i] = i.ToString();

        // read from cache
        threads = new Thread[threadCount];

        foreach (ref var thread in threads.AsSpan())
            thread = new Thread(Run);

        void Run()
        {
            var rnd = new Random();

            for (var i = 0; i < 100; i++)
                TouchDictionary(rnd);
        }

        void TouchDictionary(Random random)
        {
            var index = random.Next(Capacity);
            dictionary.TryGetValue(index, out _);
        }
    }

    [IterationCleanup(Target = nameof(ConcurrentDictionaryRead))]
    public void CleanupDictionary()
    {
        dictionary.Clear();
    }

    [Benchmark(Baseline = true)]
    public void ConcurrentDictionaryRead()
    {
        foreach (var thread in threads)
            thread.Start();

        foreach (var thread in threads)
            thread.Join();
    }

    [Benchmark]
    public void ConcurrentCacheRead()
    {
        foreach (var thread in threads)
            thread.Start();

        foreach (var thread in threads)
            thread.Join();
    }
}