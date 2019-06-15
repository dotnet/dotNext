using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Reflection;

namespace DotNext.Reflection
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.Method)]
    public class StringMethodReflectionBenchmark
    {
        private static readonly Func<string, char, int, int> IndexOf = Type<string>.Method<char, int>.Require<int>(nameof(string.IndexOf));
        private static readonly Function<string, (char, int), int> IndexOfSpecial = Type<string>.RequireMethod<(char, int), int>(nameof(string.IndexOf));

        private static readonly MethodInfo IndexOfReflected = typeof(string).GetMethod(nameof(string.IndexOf), new[] { typeof(char), typeof(int) }, Array.Empty<ParameterModifier>());

        private static readonly Function<object, (object, object), object> IndexOfSpecialUntyped = IndexOfReflected.Unreflect<Function<object, (object, object), object>>();

        [Params("", "abccdahehkgbe387jwgr", "wfjwkhwfhwjgfkwjggwhjvfkwhwkgwjgbwjbwjbvbwvjwbvwjbvw")]
        public string StringValue;

        [Benchmark]
        public void WithoutReflection()
        {
            StringValue.IndexOf('7', 0);
        }

        [Benchmark]
        public void WithTypedFastReflection()
        {
            IndexOf(StringValue, '7', 0);
        }

        [Benchmark]
        public void WithTypedFastReflectionSpecial()
        {
            IndexOfSpecial(StringValue, ('7', 0));
        }

        [Benchmark]
        public void WithUntypedFastReflectionSpecial()
        {
            IndexOfSpecialUntyped(StringValue, ('7', 0));
        }

        [Benchmark]
        public void WithReflection()
        {
            IndexOfReflected.Invoke(StringValue, new object[] { '7', 0 });
        }
    }
}
