using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Numerics;

namespace MissingPieces
{
	[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
	[Orderer(SummaryOrderPolicy.FastestToSlowest)]
	public class BitwiseEqualityBenchmark
	{
		private static readonly long[] Array1 = new[]
		{
			10L, 20L, 50L, 90L, 54L, 90L, 100L, long.MaxValue,
			10L, 20L, 50L, 90L, 54L, 90L, 100L, long.MaxValue,
			10L, 20L, 50L, 90L, 54L, 90L, 100L, long.MaxValue,
			10L, 20L, 50L, 90L, 54L, 90L, 100L, long.MaxValue,
			10L, 20L, 50L, 90L, 54L, 90L, 100L, long.MaxValue,
			10L, 20L, 50L, 90L, 54L, 90L, 100L, long.MaxValue,
		};
		private static readonly long[] Array2 = new[]
		{
			10L, 20L, 50L, 90L, 54L, 90L, 100L, long.MaxValue,
			10L, 20L, 50L, 90L, 54L, 90L, 100L, long.MaxValue,
			10L, 20L, 50L, 90L, 54L, 90L, 100L, long.MaxValue,
			10L, 20L, 50L, 90L, 54L, 90L, 100L, long.MaxValue,
			10L, 20L, 50L, 90L, 54L, 90L, 100L, long.MaxValue,
			10L, 20L, 50L, 90L, 54L, 90L, 100L, 0L,
		};

		public struct BigStructure
		{
			public decimal X, Y, Z, C, A, B;
			public short F;

			public bool Equals(BigStructure other)
				=> X == other.X &&
					Y == other.Y &&
					Z == other.Z &&
					C == other.C &&
					A == other.A &&
					B == other.B &&
					F == other.F;
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
			var first = NonEmptyGuid;
			var second = default(Guid);
			first.BitwiseEquals(second);
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
			var first = new BigStructure { C = 30 };
			var second = default(BigStructure);
			first.BitwiseEquals(second);
		}
	}
}
