using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;

namespace DotNext
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class BitwiseEqualityBenchmark
    {
        public struct LargeStruct
        {
            public decimal X, Y, Z, C, A, B;
            public short F;

            public bool Equals(LargeStruct other)
                => X == other.X &&
                    Y == other.Y &&
                    Z == other.Z &&
                    C == other.C &&
                    A == other.A &&
                    B == other.B &&
                    F == other.F;

            public override int GetHashCode()
            {
                var hash = unchecked((int)2166136261);
                hash = (hash ^ X.GetHashCode()) * 16777619;
                hash = (hash ^ Y.GetHashCode()) * 16777619;
                hash = (hash ^ Z.GetHashCode()) * 16777619;
                hash = (hash ^ C.GetHashCode()) * 16777619;
                hash = (hash ^ A.GetHashCode()) * 16777619;
                hash = (hash ^ B.GetHashCode()) * 16777619;
                hash = (hash ^ F) * 16777619;
                return hash;
            }
        }

        private static readonly Guid NonEmptyGuid = Guid.NewGuid();
        private static readonly LargeStruct NonEmptyLargeStruct = new LargeStruct { C = 30 };

        [Benchmark]
        public void GuidEqualsMethod()
        {
            NonEmptyGuid.Equals(default);
        }

        [Benchmark]
        public void GuidBitwiseEqualsMethod()
        {
            BitwiseComparer<Guid>.Equals<Guid>(NonEmptyGuid, default);
        }

        [Benchmark]
        public unsafe void GuidBitwiseEqualsUsingSpan()
        {
            var value = NonEmptyGuid;
            var span1 = new ReadOnlySpan<byte>(&value, sizeof(Guid));
            var empty = default(Guid);
            var span2 = new ReadOnlySpan<byte>(&empty, sizeof(Guid));
            span1.SequenceEqual(span2);
        }

        [Benchmark]
        public void LargeStructEqualsMethod()
        {
            NonEmptyLargeStruct.Equals(default);
        }

        [Benchmark]
        public void LargeStructBitwiseEqualsMethod()
        {
            BitwiseComparer<LargeStruct>.Equals<LargeStruct>(NonEmptyLargeStruct, default);
        }

        [Benchmark]
        public unsafe void LargeStructEqualsUsingSpan()
        {
            var value = NonEmptyLargeStruct;
            var span1 = new ReadOnlySpan<byte>(&value, sizeof(LargeStruct));
            var empty = default(LargeStruct);
            var span2 = new ReadOnlySpan<byte>(&empty, sizeof(LargeStruct));
            span1.SequenceEqual(span2);
        }

        [Benchmark]
        public void EnumEqualsUsingDefaultComparer()
        {
            System.Collections.Generic.EqualityComparer<EnvironmentVariableTarget>.Default.Equals(EnvironmentVariableTarget.Machine, EnvironmentVariableTarget.Process);
        }

        [Benchmark]
        public void EnumEqualsUsingBitwiseComparer()
        {
            BitwiseComparer<EnvironmentVariableTarget>.Equals(EnvironmentVariableTarget.Machine, EnvironmentVariableTarget.Process);
        }
    }
}
