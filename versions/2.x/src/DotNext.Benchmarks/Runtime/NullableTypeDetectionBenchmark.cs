using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System.Text;

namespace DotNext.Runtime
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class NullableTypeDetectionBenchmark
    {
        [Benchmark]
        public bool DetectNullableType()
        {
            var vt = Intrinsics.IsNullable<ValueFunc<int, int>>();
            return vt |= Intrinsics.IsNullable<StringBuilder>();
        }

        [Benchmark]
        public bool DetectNullableUsingReflection()
        {
            var vt = typeof(ValueFunc<int, int>).IsValueType;
            return vt |= typeof(StringBuilder).IsValueType;
        }
    }
}