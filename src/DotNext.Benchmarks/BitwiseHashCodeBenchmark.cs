using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;

namespace DotNext.Benchmarks
{
    using static Runtime.Intrinsics;

    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class BitwiseHashCodeBenchmark
    {
        private static readonly Guid NonEmptyGuid = Guid.NewGuid();
        private static readonly BitwiseEqualityBenchmark.LargeStruct NonEmptyBigStruct = new BitwiseEqualityBenchmark.LargeStruct { X = 10M, C = 42M };

        [Benchmark]
        public void GuidHashCode()
        {
            NonEmptyGuid.GetHashCode();
        }

        [Benchmark]
        public void GuidBitwiseHashCode()
        {
            BitwiseHashCode(NonEmptyGuid, false);
        }

        [Benchmark]
        public void BigStructureHashCode()
        {
            NonEmptyBigStruct.GetHashCode();
        }

        [Benchmark]
        public void BigStructureBitwiseHashCode()
        {
            BitwiseHashCode(NonEmptyBigStruct, false);
        }
    }
}