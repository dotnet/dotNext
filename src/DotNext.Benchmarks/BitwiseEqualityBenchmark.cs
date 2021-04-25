using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Generic;

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
        private static readonly LargeStruct NonEmptyLargeStruct = new (){ C = 30 };

        [Benchmark]
        public bool GuidEqualsMethod() => NonEmptyGuid.Equals(default);

        [Benchmark]
        public bool GuidEqualsUsingDefaultEqualityComparer()
            => EqualityComparer<Guid>.Default.Equals(NonEmptyGuid, default);

        [Benchmark]
        public bool GuidBitwiseEqualsMethod()
            => BitwiseComparer<Guid>.Equals<Guid>(NonEmptyGuid, default);

        [Benchmark]
        public unsafe bool GuidBitwiseEqualsUsingSpan()
        {
            var value = NonEmptyGuid;
            var span1 = new ReadOnlySpan<byte>(&value, sizeof(Guid));
            var empty = default(Guid);
            var span2 = new ReadOnlySpan<byte>(&empty, sizeof(Guid));
            return span1.SequenceEqual(span2);
        }

        [Benchmark]
        public bool LargeStructEqualsMethod() => NonEmptyLargeStruct.Equals(default);

        [Benchmark]
        public bool LargeStructEqualsUsingDefaultEqualityComparer()
            => EqualityComparer<LargeStruct>.Default.Equals(NonEmptyLargeStruct, default);

        [Benchmark]
        public bool LargeStructBitwiseEqualsMethod()
            => BitwiseComparer<LargeStruct>.Equals<LargeStruct>(NonEmptyLargeStruct, default);

        [Benchmark]
        public unsafe bool LargeStructEqualsUsingSpan()
        {
            var value = NonEmptyLargeStruct;
            var span1 = new ReadOnlySpan<byte>(&value, sizeof(LargeStruct));
            var empty = default(LargeStruct);
            var span2 = new ReadOnlySpan<byte>(&empty, sizeof(LargeStruct));
            return span1.SequenceEqual(span2);
        }

        [Benchmark]
        public bool EnumEqualsUsingDefaultComparer()
            => System.Collections.Generic.EqualityComparer<EnvironmentVariableTarget>.Default.Equals(EnvironmentVariableTarget.Machine, EnvironmentVariableTarget.Process);

        [Benchmark]
        public bool EnumEqualsUsingBitwiseComparer()
            => BitwiseComparer<EnvironmentVariableTarget>.Equals(EnvironmentVariableTarget.Machine, EnvironmentVariableTarget.Process);
    }
}
