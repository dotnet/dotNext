using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;

namespace DotNext.Numerics;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class BitVectorBenchmark
{
    [Benchmark(Description = "8 bits")]
    public void Get8Bits()
    {
        Span<bool> bits = stackalloc bool[8];
        BitVector.FromByte(52, bits);
    }

    [Benchmark(Description = "16 bits")]
    public void Get16Bits()
    {
        Span<bool> bits = stackalloc bool[16];
        BitVector.FromUInt16(52, bits);
    }

    [Benchmark(Description = "32 bits")]
    public void Get32Bits()
    {
        Span<bool> bits = stackalloc bool[32];
        BitVector.FromUInt32(52U, bits);
    }

    [Benchmark(Description = "64 bits")]
    public void Get64Bits()
    {
        Span<bool> bits = stackalloc bool[64];
        BitVector.FromUInt64(52UL, bits);
    }
}