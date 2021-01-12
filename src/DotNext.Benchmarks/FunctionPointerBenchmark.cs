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
        private sealed class TestObject
        {
            internal readonly int Value;

            internal TestObject(int value) => Value = value;

            internal int Add(int operand) => Value + operand;
        }

        private static readonly Func<string, int> ParseToIntMethod = int.Parse;
        private static unsafe readonly ValueFunc<string, int> ParseToIntMethodPtr = new ValueFunc<string, int>(&int.Parse);

        private static readonly Func<decimal, decimal> NegateDecimal = Negate;

        private static unsafe readonly ValueFunc<decimal, decimal> NegateDecimalPtr = new ValueFunc<decimal, decimal>(&Negate);

        private static readonly Func<int, int> ClosedDelegate = new Func<int, int>(new TestObject(10).Add);

        private static readonly ValueFunc<int, int> ClosedPtr = new ValueFunc<int, int>(ClosedDelegate);

        private static decimal Negate(decimal value) => -value;

        [Benchmark]
        public int InvokeStaticUsingDelegate() => ParseToIntMethod("123");

        [Benchmark]
        public int InvokeStaticUsingPointer() => ParseToIntMethodPtr.Invoke("123");

        [Benchmark]
        public decimal InvokeStaticUsingDelegateLargeArgs() => NegateDecimal(20M);

        [Benchmark]
        public decimal InvokeStaticUsingPointerLargeArgs() => NegateDecimalPtr.Invoke(20M);

        [Benchmark]
        public int InvokeInstanceUsingDelegate() => ClosedDelegate(42);

        [Benchmark]
        public int InvokeInstanceUsingPointer() => ClosedPtr.Invoke(42);
    }
}
