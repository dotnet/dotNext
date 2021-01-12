using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Security.Cryptography;

namespace DotNext
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class RandomStringBenchmark
    {
        private const string AllowedChars = "1234567890abcdef";
        private static readonly Random rnd = new Random();
        private static readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();

        [Benchmark]
        public void GuidString() => Guid.NewGuid().ToString();

        [Benchmark]
        public void RandomString() => rnd.NextString(AllowedChars, 36);

        [Benchmark]
        public void CryptoRngString() => rng.NextString(AllowedChars, 36);
    }
}
