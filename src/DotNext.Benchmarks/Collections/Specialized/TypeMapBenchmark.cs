using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DotNext.Collections.Specialized;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class TypeMapBenchmark
{
    private sealed class DictionaryBasedLookup<TValue> : Dictionary<Type, TValue>
    {
        public void Set<TKey>(TValue value) => this[typeof(TKey)] = value;

        public bool TryGetValue<TKey>(out TValue value) => TryGetValue(typeof(TKey), out value);
    }

    private sealed class ConcurrentDictionaryBasedLookup<TValue> : ConcurrentDictionary<Type, TValue>
    {
        public void Set<TKey>(TValue value) => this[typeof(TKey)] = value;

        public bool TryGetValue<TKey>(out TValue value) => TryGetValue(typeof(TKey), out value);
    }

    private readonly TypeMap<int> threadUnsafeMap = new();
    private readonly ConcurrentTypeMap<int> threadSafeMap = new();
    private readonly DictionaryBasedLookup<int> dictionaryLookup = new();
    private readonly ConcurrentDictionaryBasedLookup<int> concurrentLookup = new();

    [Benchmark]
    public int TypeMapLookup()
    {
        threadUnsafeMap.Set<string>(42);
        threadUnsafeMap.TryGetValue<string>(out var result);
        return result;
    }

    [Benchmark]
    public int ConcurrentTypeMapLookup()
    {
        threadSafeMap.Set<string>(42);
        threadSafeMap.TryGetValue<string>(out var result);
        return result;
    }

    [Benchmark]
    public int DictionaryLookup()
    {
        dictionaryLookup.Set<string>(42);
        dictionaryLookup.TryGetValue<string>(out var result);
        return result;
    }

    [Benchmark]
    public int ConcurrentDictionaryLookup()
    {
        concurrentLookup.Set<string>(42);
        concurrentLookup.TryGetValue<string>(out var result);
        return result;
    }
}