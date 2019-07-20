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
        private static readonly Func<string, int> ParseToIntMethod = int.Parse;
        private static readonly FunctionPointer<string, int> ParseToIntMethodPtr = new FunctionPointer<string, int>(ParseToIntMethod);

        private static readonly Func<string, Guid> ParseToGuidMethod = Guid.Parse;

        private static readonly FunctionPointer<string, Guid> ParseToGuidMethodPtr = new FunctionPointer<string, Guid>(ParseToGuidMethod);

        [Benchmark]
        public void InvokeDelegate()
        {
            ParseToIntMethod("123");
        }

        [Benchmark]
        public void InvokeFunctionPtr()
        {
            ParseToIntMethodPtr.Invoke("123");
        }

        [Benchmark]
        public void InvokeDelegateLargeReturn()
        {
            ParseToGuidMethod("{28a507b5-79b4-44d2-9fe9-969313d76361}");
        }

        [Benchmark]
        public void InvokeFunctionPtrLargeReturn()
        {
            ParseToGuidMethodPtr.Invoke("{28a507b5-79b4-44d2-9fe9-969313d76361}");
        }
    }
}
