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

        [Benchmark]
        public string OptimizedReverse() => StringValue.Reverse();

        [Benchmark]
        public string ClassicReverse()
        {
            var buffer = StringValue.ToCharArray();
            Array.Reverse(buffer);
            return new string(buffer);
        }
    }
}
