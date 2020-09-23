using System.Buffers;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;

namespace DotNext.Buffers
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class SpanBuilderBenchmark
    {
        private readonly int[] inputArray = new int[] { 10, 20, 30, 40, 50, 60, 70, 80 };

        [Benchmark]
        public void AddRangeToList()
        {
            var list = new List<int>(10);
            list.AddRange(inputArray);
            list.Clear();
        }

        [Benchmark]
        public void AddRangeToSpanBuilderFixedSize()
        {
            using var builder = new SpanBuilder<int>(stackalloc int[10]);
            builder.Write(inputArray);
        }

        [Benchmark]
        public void AddRangeToSpanBuilderGrowable()
        {
            using var builder = new SpanBuilder<int>(stackalloc int[10], false);
            builder.Write(inputArray);
        }

        [Benchmark]
        public void AddRangeToArrayWriter()
        {
            using var writer = new PooledArrayBufferWriter<int>(10);
            writer.Write(inputArray);
        }
    }
}