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
        private static readonly BitwiseEqualityBenchmark.BigStructure NonEmptyBigStruct = new BitwiseEqualityBenchmark.BigStructure { X = 10M, C = 42M };

        [Benchmark]
        public void GuidHashCode()
        {
            NonEmptyGuid.GetHashCode();
        }

        [Benchmark]
        public void GuidBitwiseHashCode()
        {
            ValueType<Guid>.BitwiseHashCode(NonEmptyGuid);
        }

        [Benchmark]
        public void BigStructureHashCode()
        {
            NonEmptyBigStruct.GetHashCode();
        }

        [Benchmark]
        public void BigStructureBitwiseHashCode()
        {
            ValueType<BitwiseEqualityBenchmark.BigStructure>.BitwiseHashCode(NonEmptyBigStruct, false);
        }
    }
}