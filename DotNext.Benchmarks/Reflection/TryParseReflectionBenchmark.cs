using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Reflection;

namespace DotNext.Reflection
{   
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
	[Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class TryParseReflectionBenchmark
    {
        private delegate bool TryParseDelegate(string text, out decimal result);

        private static readonly MethodInfo ReflectedMethod = typeof(decimal).GetMethod(nameof(decimal.TryParse), new[]{typeof(string), typeof(decimal).MakeByRefType()});
        private static readonly TryParseDelegate StronglyTyped = Type<decimal>.Method.Get<TryParseDelegate>(nameof(decimal.TryParse), MethodLookup.Static);

        private static readonly Function<(string text, Ref<decimal> result), bool> StronglyTypedSpecial = Type<decimal>.GetStaticMethod<(string, Ref<decimal>), bool>(nameof(decimal.TryParse));

        private static readonly Function<(string text, decimal result), bool> StronglyTypedSpecialUnreflected = ReflectedMethod.Unreflect<Function<(string, decimal), bool>>();

        private static readonly Function<(object text, object result), object> UntypedSpecialUnreflected = ReflectedMethod.Unreflect<Function<(object, object), object>>();


        [Benchmark]
        public void NoReflection()
        {
            decimal.TryParse("748383565500", out var result);
        }

        [Benchmark]
        public void UseReflection()
        {
            ReflectedMethod.Invoke(null, new object[]{"748383565500", decimal.Zero});
        }

        [Benchmark]
        public void UseStronglyTypedReflection()
        {
            StronglyTyped("748383565500", out var result);
        }

        [Benchmark]
        public void UseStronglyTypedSpecialReflection()
        {
            StronglyTypedSpecial(("748383565500", decimal.Zero));
        }

        [Benchmark]
        public void UseStronglyTypedSpecialUnreflected()
        {
            StronglyTypedSpecialUnreflected(("748383565500", decimal.Zero));
        }

        [Benchmark]
        public void UseUntypedSpecialReflection()
        {
            UntypedSpecialUnreflected(("748383565500", decimal.Zero));
        }
    }
}