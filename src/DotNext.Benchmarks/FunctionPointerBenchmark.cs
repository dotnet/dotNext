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
        private static readonly ValueFunc<string, int> ParseToIntMethodPtr = new ValueFunc<string, int>(ParseToIntMethod);

        private static readonly Func<decimal, decimal> NegateDecimal = DelegateHelpers.CreateOpenDelegate<Func<decimal, decimal>>(arg => -arg);

        private static readonly ValueFunc<decimal, decimal> NegateDecimalPtr = new ValueFunc<decimal, decimal>(NegateDecimal);

        private static readonly Func<int, int> ClosedDelegate = new Func<int, int>(new TestObject(10).Add);

        private static readonly ValueFunc<int, int> ClosedPtr = new ValueFunc<int, int>(ClosedDelegate);

        [Benchmark]
        public void InvokeStaticUsingDelegate()
        {
            ParseToIntMethod("123");
        }

        [Benchmark]
        public void InvokeStaticUsingPointer()
        {
            ParseToIntMethodPtr.Invoke("123");
        }

        [Benchmark]
        public void InvokeStaticUsingDelegateLargeArgs()
        {
            NegateDecimal(20M);
        }

        [Benchmark]
        public void InvokeStaticUsingPointerLargeArgs()
        {
            NegateDecimalPtr.Invoke(20M);
        }

        [Benchmark]
        public void InvokeInstanceUsingDelegate()
        {
            ClosedDelegate(42);
        }

        [Benchmark]
        public void InvokeInstanceUsingPointer()
        {
            ClosedPtr.Invoke(42);
        }
    }
}
