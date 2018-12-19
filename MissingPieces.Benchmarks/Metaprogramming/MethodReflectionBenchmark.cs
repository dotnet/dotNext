using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;

namespace MissingPieces.Metaprogramming
{
	[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
	[Orderer(SummaryOrderPolicy.FastestToSlowest)]
	public class MethodReflectionBenchmark
	{
		private static readonly Func<string, char, int> IndexOf = Type<string>.Method.Instance<int>.Get<char>(nameof(string.IndexOf));

		[Params("", "abccdaa387jwgr", "aaaaaaaaaaaaaaa")]
		public string StringValue;

		[Benchmark]
		public void WithReflection()
		{
			IndexOf(StringValue, '7');
		}

		[Benchmark]
		public void WithoutReflection()
		{
			StringValue.IndexOf('7');
		}
	}
}
