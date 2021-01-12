using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Reflection;
using System.Security.Cryptography;

namespace DotNext.Reflection
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.Method)]
    public class HashMethodReflectionBenchmark
    {
        private static readonly Func<MD5, byte[], int, int, byte[]> ComputeHash = Type<MD5>.Method<byte[], int, int>.Require<byte[]>(nameof(MD5.ComputeHash));
        private static readonly Function<MD5, (byte[], int, int), byte[]> ComputeHashSpecial = Type<MD5>.RequireMethod<(byte[], int, int), byte[]>(nameof(MD5.ComputeHash));
        private static readonly MethodInfo ComputeHashReflected = typeof(MD5).GetMethod(nameof(MD5.ComputeHash), new[] { typeof(byte[]), typeof(int), typeof(int) }, Array.Empty<ParameterModifier>());

        private readonly byte[] data = new byte[1024 * 1024];    //1KB
        private readonly MD5 hash = MD5.Create();

        public HashMethodReflectionBenchmark()
        {
            //generate random data
            new Random(0xEBD8320).NextBytes(data);
        }

        [Benchmark]
        public void WithoutReflection()
        {
            hash.ComputeHash(data, 0, data.Length);
        }

        [Benchmark]
        public void WithTypedReflection()
        {
            ComputeHash(hash, data, 0, data.Length);
        }

        [Benchmark]
        public void WithTypedReflectionSpecial()
        {
            ComputeHashSpecial(hash, (data, 0, data.Length));
        }

        [Benchmark]
        public void WithReflection()
        {
            ComputeHashReflected.Invoke(hash, new object[] { data, 0, data.Length });
        }
    }
}