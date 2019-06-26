using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System.Reflection;

namespace DotNext.Reflection
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class TryParseReflectionBenchmark
    {
        private delegate bool TryParseDelegate(string text, out decimal result);

        private static readonly MethodInfo ReflectedMethod = typeof(decimal).GetMethod(nameof(decimal.TryParse), new[] { typeof(string), typeof(decimal).MakeByRefType() });
        private static readonly TryParseDelegate StronglyTyped = Type<decimal>.Method.Get<TryParseDelegate>(nameof(decimal.TryParse), MethodLookup.Static);

        private static readonly Function<(string text, Ref<decimal> result), bool> StronglyTypedSpecial = Type<decimal>.GetStaticMethod<(string, Ref<decimal>), bool>(nameof(decimal.TryParse));

        private static readonly Function<(string text, decimal result), bool> StronglyTypedSpecialUnreflected = ReflectedMethod.Unreflect<Function<(string, decimal), bool>>();

        private static readonly Function<(object text, object result), object> UntypedSpecialUnreflected = ReflectedMethod.Unreflect<Function<(object, object), object>>();

        private const string StringValue = "748383565500";

        [Benchmark]
        public void NoReflection()
        {
            decimal.TryParse(StringValue, out var _);
        }

        [Benchmark]
        public void UseReflection()
        {
            ReflectedMethod.Invoke(null, new object[] { StringValue, decimal.Zero });
        }

        [Benchmark]
        public void UseStronglyTypedReflection()
        {
            StronglyTyped(StringValue, out var result);
        }

        [Benchmark]
        public void UseStronglyTypedSpecialReflection()
        {
            StronglyTypedSpecial((StringValue, decimal.Zero));
        }

        [Benchmark]
        public void UseStronglyTypedSpecialUnreflected()
        {
            StronglyTypedSpecialUnreflected((StringValue, decimal.Zero));
        }

        [Benchmark]
        public void UseUntypedSpecialReflection()
        {
            UntypedSpecialUnreflected((StringValue, decimal.Zero));
        }
    }
}