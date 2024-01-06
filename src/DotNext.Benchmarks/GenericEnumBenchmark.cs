using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;

namespace DotNext;

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
    public int ToInt32UsingGenericConverter() => EnumConverter.FromEnum<EnvironmentVariableTarget, int>(EnvironmentVariableTarget.Machine);

    [Benchmark]
    public long ToInt64UsingGenericConverter() => EnumConverter.FromEnum<EnvironmentVariableTarget, long>(EnvironmentVariableTarget.Machine);

    [Benchmark]
    public EnvironmentVariableTarget ToEnumUsingGenericConverter() => EnumConverter.ToEnum<EnvironmentVariableTarget, int>(2);
}