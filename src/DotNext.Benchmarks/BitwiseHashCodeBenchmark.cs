using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Generic;

namespace DotNext.Benchmarks;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.Declared)]
public class BitwiseHashCodeBenchmark
{
    private static readonly Guid NonEmptyGuid = Guid.NewGuid();
    private static readonly BitwiseEqualityBenchmark.LargeStruct NonEmptyLargeStruct = new() { X = 10M, C = 42M };

    [Benchmark(Description = "Guid.GetHashCode")]
    public int GuidHashCode() => NonEmptyGuid.GetHashCode();

    [Benchmark(Description = "BitwiseComparer<Guid>.GetHashCode")]
    public int GuidBitwiseHashCode() => BitwiseComparer<Guid>.GetHashCode(NonEmptyGuid, false);

    [Benchmark(Description = "BitwiseComparer<LargeStructure>.GetHashCode")]
    public int LargeStructureHashCode() => NonEmptyLargeStruct.GetHashCode();

    [Benchmark(Description = "LargeStructure.GetHashCode")]
    public int LargeStructureBitwiseHashCode()
        => BitwiseComparer<BitwiseEqualityBenchmark.LargeStruct>.GetHashCode(NonEmptyLargeStruct, false);
}