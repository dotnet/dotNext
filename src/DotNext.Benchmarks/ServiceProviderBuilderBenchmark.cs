using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Generic;

namespace DotNext.Benchmarks
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class ServiceProviderBuilderBenchmark
    {
        private const string Value = "Hello, world!";
        private static readonly IServiceProvider compiledProvider = ServiceProviderBuilder.CreateFactory(
            typeof(ICloneable),
            typeof(IComparable),
            typeof(IComparable<string>),
            typeof(IConvertible),
            typeof(IEquatable<string>),
            typeof(IEnumerable<char>)).Invoke(new[] { Value, Value, Value, Value, Value, Value });

        private static readonly IServiceProvider cachedProvider = new ServiceProviderBuilder()
            .Add<ICloneable>(Value)
            .Add<IComparable>(Value)
            .Add<IComparable<string>>(Value)
            .Add<IConvertible>(Value)
            .Add<IEquatable<string>>(Value)
            .Add<IEnumerable<char>>(Value)
            .Build();

        private static void QueryServices(IServiceProvider provider)
        {
            provider.GetService(typeof(ICloneable));
            provider.GetService(typeof(IComparable));
            provider.GetService(typeof(IComparable<string>));
            provider.GetService(typeof(IConvertible));
            provider.GetService(typeof(IEquatable<string>));
            provider.GetService(typeof(IEnumerable<char>));
        }

        [Benchmark]
        public void UseCompiledProvider() => QueryServices(compiledProvider);

        [Benchmark]
        public void UseCachedProvider() => QueryServices(cachedProvider);
    }
}