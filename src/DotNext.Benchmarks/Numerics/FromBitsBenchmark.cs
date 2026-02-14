using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;

namespace DotNext.Numerics;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class FromBitsBenchmark
{
    private readonly bool[] bits;

    public FromBitsBenchmark()
    {
        bits = new bool[128];
        Random.Shared.GetItems([true, false], bits);
    }

    [Benchmark(Description = "8 bits")]
    public byte ToUInt8() => byte.FromBits(bits);
    
    [Benchmark(Description = "16 bits")]
    public short ToInt16() => short.FromBits(bits);
    
    [Benchmark(Description = "32 bits")]
    public int ToInt32() => int.FromBits(bits);
    
    [Benchmark(Description = "64 bits")]
    public long ToInt64() => long.FromBits(bits);
}