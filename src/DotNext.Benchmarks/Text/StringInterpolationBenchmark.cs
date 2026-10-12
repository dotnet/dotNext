using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;

namespace DotNext.Text;

using Buffers;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class StringInterpolationBenchmark
{
    private const int InitialBufferSize = 128;
    private const EnvironmentVariableTarget EnumValue = EnvironmentVariableTarget.User;

    [Benchmark(Baseline = true)]
    public void InterpolateUsingBufferWriterSlim()
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[InitialBufferSize]);
        writer.Interpolate(provider: null, $"Integer value of {EnumValue} is {(int)EnumValue}");
        writer.Dispose();
    }

    [Benchmark]
    public void InterpolateUsingOnStack()
    {
        Span<char> buffer = stackalloc char[InitialBufferSize];
        string.Interpolate(buffer, provider: null, $"Integer value of {EnumValue} is {(int)EnumValue}").Dispose();
    }

    [Benchmark]
    public void InterpolateUsingRentedMemory()
    {
        string.Interpolate(allocator: null, provider: null, $"Integer value of {EnumValue} is {(int)EnumValue}").Dispose();
    }
}