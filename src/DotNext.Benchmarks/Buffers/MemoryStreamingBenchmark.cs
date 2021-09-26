using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using Microsoft.IO;

namespace DotNext.Buffers;

using IO;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class MemoryStreamingBenchmark
{
    private static readonly RecyclableMemoryStreamManager manager = new();
    private readonly byte[] chunk = new byte[2048];

    [Params(100, 1000, 10_000, 100_000, 1000_000)]
    public int TotalCount;

    private void Write(Stream output)
    {
        for (int remaining = TotalCount, taken; remaining > 0; remaining -= taken)
        {
            taken = Math.Min(remaining, chunk.Length);
            output.Write(chunk, 0, taken);
        }
    }

    [Benchmark(Baseline = true)]
    public void WriteToMemoryStream()
    {
        using var ms = new MemoryStream();
        Write(ms);
    }

    [Benchmark]
    public void WriteToRecyclableMemoryStream()
    {
        using var ms = manager.GetStream();
        Write(ms);
    }

    [Benchmark]
    public void WriteToSparseBuffer()
    {
        using var buffer = new SparseBufferWriter<byte>(4096, SparseBufferGrowth.Exponential);
        using var ms = buffer.AsStream(false);
        Write(ms);
    }

    [Benchmark]
    public void WriteToGrowableBuffer()
    {
        using var buffer = new PooledArrayBufferWriter<byte>();
        using var ms = buffer.AsStream();
        Write(ms);
    }

    [Benchmark]
    public void WriteToBufferingWriter()
    {
        using var writer = new FileBufferingWriter(asyncIO: false);
        Write(writer);
    }
}