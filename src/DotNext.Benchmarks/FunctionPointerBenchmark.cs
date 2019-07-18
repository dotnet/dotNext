using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;

namespace DotNext
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class FunctionPointerBenchmark
    {
        private static readonly Func<string, int> ParseMethod = int.Parse;
        private static readonly FunctionPointer<string, int> ParseMethodPtr = new FunctionPointer<string, int>(ParseMethod);

        [Benchmark]
        public void InvokeDelegate()
        {
            ParseMethod("123");
        }

        [Benchmark]
        public void InvokeFunctionPtr()
        {
            ParseMethodPtr.Invoke("123");
        }
    }
}
