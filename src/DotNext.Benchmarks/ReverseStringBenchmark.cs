using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;

namespace DotNext
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [MemoryDiagnoser]
    public class ReverseStringBenchmark
    {
        [Params("", "abccdahehkgbe387jwgr", "wfjwkhwfhwjgfkwjggwhjvfkwhwkgwjgbwjbwjbvbwvjwbvwjbvw57383thgewjugteg")]
        public string StringValue;

        private static void DevNull(string value)
        {

        }

        [Benchmark]
        public void OptimizedReverse()
        {
            DevNull(StringValue.Reverse());
        }

        [Benchmark]
        public void ClassicReverse()
        {
            var buffer = StringValue.ToCharArray();
            Array.Reverse(buffer);
            DevNull(new string(buffer));
        }
    }
}
