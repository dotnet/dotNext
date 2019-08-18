using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;

namespace DotNext.Benchmarks
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class BitwiseHashCodeBenchmark
    {
        private static readonly Guid NonEmptyGuid = Guid.NewGuid();
        private static readonly BitwiseEqualityBenchmark.LargeStruct NonEmptyLargeStruct = new BitwiseEqualityBenchmark.LargeStruct { X = 10M, C = 42M };

        [Benchmark]
        public void GuidHashCode()
        {
            NonEmptyGuid.GetHashCode();
        }

        [Benchmark]
        public void GuidBitwiseHashCode()
        {
            BitwiseComparer<Guid>.GetHashCode(NonEmptyGuid, false);
        }

        [Benchmark]
        public void LargeStructureHashCode()
        {
            NonEmptyLargeStruct.GetHashCode();
        }

        [Benchmark]
        public void LargeStructureBitwiseHashCode()
        {
            BitwiseComparer<BitwiseEqualityBenchmark.LargeStruct>.GetHashCode(NonEmptyLargeStruct, false);
        }
    }
}