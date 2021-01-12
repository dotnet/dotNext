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
        private static readonly MemberGetter<IndexOfCalculator, int> StaticallyReflected = Type<IndexOfCalculator>.Property<int>.RequireGetter(nameof(IndexOfCalculator.IndexOf));

        private static readonly Function<object, ValueTuple, object> UntypedReflected = ReflectedGetter.Unreflect<Function<object, ValueTuple, object>>();

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
        public void UseFastTypedReflection()
        {
            DummyReceiver(StaticallyReflected(IndexOfCalc));
        }

        [Benchmark]
        public void UseFastUntypedReflection()
        {
            DummyReceiver(UntypedReflected(IndexOfCalc, new ValueTuple()));
        }

        [Benchmark]
        public void UseReflection()
        {
            DummyReceiver(ReflectedGetter.Invoke(IndexOfCalc, Array.Empty<object>()));
        }
    }
}
