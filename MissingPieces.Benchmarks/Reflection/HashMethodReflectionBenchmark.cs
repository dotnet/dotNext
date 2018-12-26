using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Reflection;
using System.Security.Cryptography;

namespace MissingPieces.Reflection
{
    public class HashMethodReflectionBenchmark
    {
        private static readonly Func<MD5, byte[], int, int, byte[]> ComputeHash = Type<MD5>.Method<byte[], int, int>.Require

        private readonly byte[] data = new byte[1024 * 1024 * 1024];    //1MB
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
    }
}