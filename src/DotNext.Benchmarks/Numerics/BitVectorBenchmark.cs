using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;

namespace DotNext.Numerics;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class BitVectorBenchmark
{
    [Benchmark(Description = "8 bits")]
    public void Get8Bits()
    {
        Number.GetBits<byte>(52, stackalloc bool[8]);
    }

    [Benchmark(Description = "16 bits")]
    public void Get16Bits()
    {
        Number.GetBits<ushort>(52, stackalloc bool[16]);
    }

    [Benchmark(Description = "32 bits")]
    public void Get32Bits()
    {
        Number.GetBits(52U, stackalloc bool[32]);
    }

    [Benchmark(Description = "64 bits")]
    public void Get64Bits()
    {
        Number.GetBits(52UL, stackalloc bool[64]);
    }
}