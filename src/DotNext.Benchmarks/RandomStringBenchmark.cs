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
        private readonly Random rnd = new();
        private readonly RandomNumberGenerator rng = RandomNumberGenerator.Create();

        [Benchmark]
        public string GuidString() => Guid.NewGuid().ToString();

        [Benchmark]
        public string RandomString() => rnd.NextString(AllowedChars, 36);

        [Benchmark]
        public string CryptoRngString() => rng.NextString(AllowedChars, 36);
    }
}
