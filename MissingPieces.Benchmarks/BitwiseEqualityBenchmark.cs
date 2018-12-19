using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;

namespace MissingPieces
{
	[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
	[Orderer(SummaryOrderPolicy.FastestToSlowest)]
	public class BitwiseEqualityBenchmark
	{
		public struct BigStructure
		{
			public decimal X, Y, Z, C, A, B;

			public bool Equals(BigStructure other)
				=> X == other.X &&
					Y == other.Y &&
					Z == other.Z &&
					C == other.C &&
					A == other.A &&
					B == other.B;
		}

		private static readonly Guid NonEmptyGuid = Guid.NewGuid();

		[Benchmark]
		public void GuidEqualsMethod()
		{
			var first = NonEmptyGuid;
			var second = default(Guid);
			first.Equals(second);
		}

		[Benchmark]
		public void GuidBitwiseEqualsMethod()
		{
			StackValue<Guid> first = NonEmptyGuid;
			StackValue<Guid> second = default;
			first.Equals(in second);
		}

		[Benchmark]
		public void BigStructEqualsMethod()
		{
			var first = new BigStructure { C = 30 };
			var second = default(BigStructure);
			first.Equals(second);
		}

		[Benchmark]
		public void BigStructBitwiseEqualsMethod()
		{
			StackValue<BigStructure> first = new BigStructure { C = 30 };
			StackValue<BigStructure> second = default;
			first.Equals(second);
		}
	}
}
