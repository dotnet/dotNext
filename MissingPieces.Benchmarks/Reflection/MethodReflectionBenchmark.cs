using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Reflection;

namespace MissingPieces.Reflection
{
	[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
	[Orderer(SummaryOrderPolicy.Method)]
	[BaselineColumn]
	public class MethodReflectionBenchmark
	{
		private static readonly Func<string, char, int, int> IndexOf = Type<string>.Method<char, int>.Get<int>(nameof(string.IndexOf));
		private static readonly Function<string, (char, int), int> IndexOfSpecial = Type<string>.RequireMethod<(char, int), int>(nameof(string.IndexOf));

		private static readonly MethodInfo IndexOfReflected = typeof(string).GetMethod(nameof(string.IndexOf), new[]{ typeof(char), typeof(int) }, Array.Empty<ParameterModifier>());

		[Params("", "abccdahehkgbe387jwgr", "wfjwkhwfhwjgfkwjggwhjvfkwhwkgwjgbwjbwjbvbwvjwbvwjbvw")]
		public string StringValue;

		[Benchmark]
		public void WithoutReflection()
		{
			StringValue.IndexOf('7', 0);
		}

		[Benchmark]
		public void WithReflection()
		{
			IndexOfReflected.Invoke(StringValue, new object[]{ '7', 0 });
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
	}
}
