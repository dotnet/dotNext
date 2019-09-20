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
        public void DetectNullableType()
        {
            var vt = Intrinsics.IsNullable<ValueFunc<int, int>>();
            vt |= Intrinsics.IsNullable<StringBuilder>();
        }

        [Benchmark]
        public void DetectNullableUsingReflection()
        {
            var vt = typeof(ValueFunc<int, int>).IsValueType;
            vt |= typeof(StringBuilder).IsValueType;
        }
    }
}