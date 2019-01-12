using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using FastMember;
using System;
using System.Reflection;

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
        private static readonly MethodInfo ReflectedGetter = IndexOfCalc.GetType().GetProperty(nameof(IndexOfCalculator.IndexOf)).GetMethod;
        private static readonly MemberInvoker<(IndexOfCalculator instance, int result)> Invoker = ReflectedGetter.AsInvoker<(IndexOfCalculator, int)>();
        private static readonly MemberInvoker<(object instance, object result)> UntypedInvoker = ReflectedGetter.AsInvoker<(object, object)>();
        private static readonly MemberGetter<IndexOfCalculator, int> StaticallyReflected = Type<IndexOfCalculator>.Property<int>.RequireGetter(nameof(IndexOfCalculator.IndexOf));
        private static readonly object ExpectedIndex = 11;

        private static void DummyReceiver(object first)
        {
        }

        private static void DummyReceiver(int i)
        {

        }

        [Benchmark]
        public void NoReflection()
        {
            DummyReceiver(IndexOfCalc.IndexOf);
        }

        [Benchmark]
        public void UseObjectAccessor()
        {
            DummyReceiver(Accessor["IndexOf"]);
        }

        [Benchmark]
        public void UseTypedInvoker()
        {
            (IndexOfCalculator instance, int result) args = (IndexOfCalc, 0);
            Invoker(args);
            DummyReceiver(args.result);
        }

        [Benchmark]
        public void UseUntypedInvoker()
        {
            (object instance, object result) args = (IndexOfCalc, 0);
            UntypedInvoker(args);
            DummyReceiver(args.result);
        }

        [Benchmark]
        public void UseStaticReflection()
        {
            DummyReceiver(StaticallyReflected(IndexOfCalc));
        }

        [Benchmark]
        public void UseReflection()
        {
            DummyReceiver(ReflectedGetter.Invoke(IndexOfCalc, Array.Empty<object>()));
        }
    }
}
