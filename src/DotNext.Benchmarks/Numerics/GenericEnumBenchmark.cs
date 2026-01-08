using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;

namespace DotNext.Numerics;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class GenericEnumBenchmark
{
    private static int ToInt32<T>(T value)
        where T : struct, IConvertible
        => value.ToInt32(null);

    private static long ToInt64<T>(T value)
        where T : struct, IConvertible
        => value.ToInt64(null);

    [Benchmark]
    public int ToInt32UsingConstrainedCall() => ToInt32(EnvironmentVariableTarget.Machine);

    [Benchmark]
    public long ToInt64UsingConstrainedCall() => ToInt64(EnvironmentVariableTarget.Machine);

    [Benchmark]
    public int ToInt32UsingGenericConverter()
        => new Enum<EnvironmentVariableTarget>(EnvironmentVariableTarget.Machine).ConvertTruncating<int>();

    [Benchmark]
    public long ToInt64UsingGenericConverter()
        => new Enum<EnvironmentVariableTarget>(EnvironmentVariableTarget.Machine).ConvertTruncating<long>();

    [Benchmark]
    public EnvironmentVariableTarget ToEnumUsingGenericConverter()
        => Enum<EnvironmentVariableTarget>.CreateSaturating(2);
}