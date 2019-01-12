using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using FastMember;
using System;

namespace DotNext.Reflection
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class PropertyGetterReflectionBenchmark
    {
        private sealed class IndexOfCalculator
        {
            private readonly char character;
            private readonly int startIndex;
            private readonly string source;

            internal IndexOfCalculator(string source, char ch, int startIndex)
            {
                this.source = source;
                character = ch;
                this.startIndex = startIndex;
            }

            public int IndexOf => source.IndexOf(character, startIndex);
        }

        private static readonly IndexOfCalculator IndexOfCalc = new IndexOfCalculator("Hello, world!", 'd', 0);
        private static readonly ObjectAccessor Accessor = ObjectAccessor.Create(IndexOfCalc);
        private static readonly MemberInvoker<(IndexOfCalculator instance, int result)> Invoker = IndexOfCalc.GetType().GetProperty(nameof(IndexOfCalculator.IndexOf)).GetMethod.AsInvoker<(IndexOfCalculator, int)>();
        private static readonly MemberInvoker<(object instance, object result)> UntypedInvoker = IndexOfCalc.GetType().GetProperty(nameof(IndexOfCalculator.IndexOf)).GetMethod.AsInvoker<(object, object)>();
        private static readonly MemberGetter<IndexOfCalculator, int> StaticallyReflected = Type<IndexOfCalculator>.Property<int>.RequireGetter(nameof(IndexOfCalculator.IndexOf));
        private static readonly object ExpectedIndex = 11;

        private static void AssertEquals(object first, object second)
        {
            if (!Equals(first, second))
                throw new Exception();
        }

        [Benchmark]
        public void NoReflection()
        {
            AssertEquals(IndexOfCalc.IndexOf, ExpectedIndex);
        }

        [Benchmark]
        public void UseObjectAccessor()
        {
            AssertEquals(Accessor["IndexOf"], ExpectedIndex);
        }

        [Benchmark]
        public void UseTypedInvoker()
        {
            (IndexOfCalculator instance, int result) args = (IndexOfCalc, 0);
            Invoker(args);
            AssertEquals(args.result, ExpectedIndex);
        }

        [Benchmark]
        public void UseUntypedInvoker()
        {
            (object instance, object result) args = (IndexOfCalc, 0);
            UntypedInvoker(args);
            AssertEquals(args.result, ExpectedIndex);
        }

        [Benchmark]
        public void UseStaticReflection()
        {
            AssertEquals(StaticallyReflected(IndexOfCalc), ExpectedIndex);
        }
    }
}
