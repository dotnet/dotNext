using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Generic;

namespace DotNext.Benchmarks
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class BitwiseHashCodeBenchmark
    {
        private static readonly Guid NonEmptyGuid = Guid.NewGuid();
        private static readonly BitwiseEqualityBenchmark.LargeStruct NonEmptyLargeStruct = new() { X = 10M, C = 42M };

        [Benchmark]
        public int GuidHashCode() => NonEmptyGuid.GetHashCode();

        [Benchmark]
        public int GuidHashCodeUsingDefaultEqualityComparer()
            => EqualityComparer<Guid>.Default.GetHashCode(NonEmptyGuid);

        [Benchmark]
        public int GuidBitwiseHashCode() => BitwiseComparer<Guid>.GetHashCode(NonEmptyGuid, false);

        [Benchmark]
        public int LargeStructureHashCode() => NonEmptyLargeStruct.GetHashCode();

        [Benchmark]
        public int LargeStructureHashCodeUsingDefaultEqualityComparer()
            => EqualityComparer<BitwiseEqualityBenchmark.LargeStruct>.Default.GetHashCode(NonEmptyLargeStruct);

        [Benchmark]
        public int LargeStructureBitwiseHashCode()
            => BitwiseComparer<BitwiseEqualityBenchmark.LargeStruct>.GetHashCode(NonEmptyLargeStruct, false);
    }
}