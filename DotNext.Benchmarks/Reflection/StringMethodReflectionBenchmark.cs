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

		private static readonly MethodInfo IndexOfReflected = typeof(string).GetMethod(nameof(string.IndexOf), new[]{ typeof(char), typeof(int) }, Array.Empty<ParameterModifier>());

		private static readonly MemberInvoker<(string instance, char ch, int index, int result)> FastInvoker = IndexOfReflected.AsInvoker<(string instance, char ch, int index, int result)>();

		private static readonly MemberInvoker<(object instance, object ch, object index, object result)> UntypedFastInvoker = IndexOfReflected.AsInvoker<(object instance, object ch, object index, object result)>();

		[Params("", "abccdahehkgbe387jwgr", "wfjwkhwfhwjgfkwjggwhjvfkwhwkgwjgbwjbwjbvbwvjwbvwjbvw")]
		public string StringValue;

		[Benchmark]
		public void WithoutReflection()
		{
			StringValue.IndexOf('7', 0);
		}

		[Benchmark]
		public void WithTypedReflection()
		{
			IndexOf(StringValue, '7', 0);
		}

		[Benchmark]
		public void WithTypedReflectionSpecial()
		{
			IndexOfSpecial(StringValue, ('7', 0));
		}

		[Benchmark]
		public void WithReflection()
		{
			IndexOfReflected.Invoke(StringValue, new object[]{ '7', 0 });
		}

		[Benchmark]
		public void WithFastInvoker()
		{
			var args = (StringValue, '7', 0, -1);
			FastInvoker(in args);
		}

		[Benchmark]
		public void WithUntypedFastInvoker()
		{
			(object, object, object, object) args = (StringValue, '7', 0, null);
			UntypedFastInvoker(in args);
		}
	}
}
