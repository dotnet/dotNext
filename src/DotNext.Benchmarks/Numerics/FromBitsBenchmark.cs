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
    public byte ToUInt8() => bits.FromBits<byte>();
    
    [Benchmark(Description = "16 bits")]
    public short ToInt16() => bits.FromBits<short>();
    
    [Benchmark(Description = "32 bits")]
    public int ToInt32() => bits.FromBits<int>();
    
    [Benchmark(Description = "64 bits")]
    public long ToInt64() => bits.FromBits<long>();
}