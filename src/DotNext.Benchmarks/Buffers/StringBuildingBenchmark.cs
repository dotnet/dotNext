using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;

namespace DotNext.Buffers
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [MemoryDiagnoser]
    public class StringBuildingBenchmark
    {
        private const string StringValue = "1234567890abcdefghijklmnopqrstuvwxyz";

        [Benchmark]
        public string BuildStringUsingPooledArrayBufferWriter()
        {
            using var writer = new PooledArrayBufferWriter<char>();
            for (var i = 0; i < 100; i++)
            {
                writer.Write(StringValue);
                writer.WriteInt32(int.MaxValue);
                writer.WriteLine();
            }

            return writer.BuildString();
        }

        [Benchmark]
        public string BuildStringUsingSparseBufferWriter()
        {
            using var writer = new SparseBufferWriter<char>();
            for (var i = 0; i < 100; i++)
            {
                writer.Write(StringValue);
                writer.WriteInt32(int.MaxValue);
                writer.WriteLine();
            }

            return writer.BuildString();
        }

        [Benchmark]
        public string BuildStringUsingStringBuilder()
        {
            var writer = new StringBuilder();
            try
            {
                for (var i = 0; i < 100; i++)
                {
                    writer.Append(StringValue).Append(int.MaxValue).AppendLine();
                }

                return writer.ToString();
            }
            finally
            {
                writer.Clear();
            }
        }
    }
}