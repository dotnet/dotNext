using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;

namespace DotNext.Runtime.Caching;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class RandomAccessCacheReadBenchmark
{
    private const string Key = "a";
    private const int Value = 42;
    private const int Capacity = 2;
    
    private Dictionary<string, int> dictionary;
    private ConcurrentDictionary<string, int> concurrentDictionary;
    private RandomAccessCache<string, int> cache;
    private RandomAccessCache<int> unmanagedCache;

    [GlobalSetup(Target = nameof(GetValueFromDictionary))]
    public void SetupDictionary()
    {
        dictionary = new(Capacity)
        {
            {Key, Value},
        };
    }
    
    [GlobalSetup(Target = nameof(GetValueFromConcurrentDictionary))]
    public void SetupConcurrentDictionary()
    {
        concurrentDictionary = new(15, Capacity);
    }

    [GlobalSetup(Target = nameof(GetValueFromCache))]
    public async Task SetupCache()
    {
        cache = new(Capacity);
        using var session = await cache.ChangeAsync(Key);
        session.SetValue(Value);
    }

    [GlobalSetup(Target = nameof(GetValueFromUnmanagedCache))]
    public async Task SetupUnmanagedCache()
    {
        unmanagedCache = new(Capacity);
        using var session = await unmanagedCache.ChangeAsync(RandomAccessCache.AsKey(Key));
        session.SetValue(Value);
    }

    [Benchmark(Baseline = true)]
    public int GetValueFromDictionary()
        => dictionary.GetValueOrDefault(Key, 0);

    [Benchmark]
    public int GetValueFromConcurrentDictionary() => concurrentDictionary.GetValueOrDefault(Key, 0);

    [Benchmark]
    public int GetValueFromCache()
    {
        int result;
        if (cache.TryRead(Key, out var session))
        {
            result = session.Value;
            session.Dispose();
        }
        else
        {
            result = 0;
        }

        return result;
    }

    [Benchmark]
    public int GetValueFromUnmanagedCache()
    {
        int result;
        if (cache.TryRead(Key, out var session))
        {
            result = session.Value;
            session.Dispose();
        }
        else
        {
            result = 0;
        }

        return result;
    }
}