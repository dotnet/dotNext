using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;

namespace DotNext;

using Intrinsics = Runtime.Intrinsics;

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

    private static T ToEnum<T>(int value)
        where T : unmanaged, Enum
    {
        Intrinsics.Bitcast(value, out T result);
        return result;
    }

    [Benchmark]
    public int ToInt32UsingConstrainedCall() => ToInt32(EnvironmentVariableTarget.Machine);

    [Benchmark]
    public long ToInt64UsingConstrainedCall() => ToInt64(EnvironmentVariableTarget.Machine);

    [Benchmark]
    public int ToInt32UsingGenericConverter() => EnvironmentVariableTarget.Machine.ToInt32();

    [Benchmark]
    public long ToInt64UsingGenericConverter() => EnvironmentVariableTarget.Machine.ToInt64();

    [Benchmark]
    public EnvironmentVariableTarget ToEnumUsingBitcast() => ToEnum<EnvironmentVariableTarget>(2);

    [Benchmark]
    public EnvironmentVariableTarget ToEnumUsingGenericConverter() => 2.ToEnum<EnvironmentVariableTarget>();
}