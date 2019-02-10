using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;

namespace DotNext
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class BitCastBenchmark
    {
        private struct AltGuid
        {
            internal long A, B;
        }

        private struct Large
        {
            internal long A, B, C;
        }

        private static readonly Guid G = Guid.NewGuid();

        [Benchmark]
        public void TheSameSizeNewCast()
        {
            ValueTypes.BitCast2<Guid, AltGuid>(G);
        }

        [Benchmark]
        public void TheSameSizeOldCast()
        {
            ValueTypes.BitCast<Guid, AltGuid>(G);
        }

        [Benchmark]
        public void BiggerSizeNewCast()
        {
            ValueTypes.BitCast2<Guid, Large>(G);
        }

        [Benchmark]
        public void BiggerSizeOldCast()
        {
            ValueTypes.BitCast<Guid, Large>(G);
        }
    }
}
