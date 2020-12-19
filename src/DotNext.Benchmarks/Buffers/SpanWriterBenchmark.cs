using System.Buffers;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;

namespace DotNext.Buffers
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class SpanWriterBenchmark
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
        public void AddRangeToSpanWriter()
        {
            var builder = new SpanWriter<int>(stackalloc int[10]);
            builder.Write(inputArray);
        }

        [Benchmark]
        public void AddRangeToBufferWriterSlim()
        {
            using var builder = new BufferWriterSlim<int>(stackalloc int[10]);
            builder.Write(inputArray);
        }

        [Benchmark]
        public void AddRangeToArrayWriter()
        {
            using var writer = new PooledArrayBufferWriter<int>(10);
            writer.Write(inputArray);
        }

        [Benchmark]
        public void AddRangeToSparseBuffer()
        {
            using var writer = new SparseBufferWriter<int>();
            writer.Write(inputArray);
        }
    }
}