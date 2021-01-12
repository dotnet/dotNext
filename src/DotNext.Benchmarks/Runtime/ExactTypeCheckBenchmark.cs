using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;

namespace DotNext.Runtime
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class ExactTypeCheckBenchmark
    {
        private static readonly object Obj = 22;

        [Benchmark]
        public bool TypeOfOperator() => Obj.GetType() == typeof(int);

        [Benchmark]
        public bool IntrinsicMethod() => Intrinsics.IsExactTypeOf<int>(Obj);
    }
}