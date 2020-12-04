using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Generic;

namespace DotNext.Benchmarks
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class ServiceProviderFactoryBenchmark
    {
        private const string Value = "Hello, world!";
        private static readonly IServiceProvider compiledProvider = ServiceProviderFactory.CreateFactory(
            typeof(ICloneable),
            typeof(IComparable),
            typeof(IComparable<string>),
            typeof(IConvertible),
            typeof(IEquatable<string>),
            typeof(IEnumerable<char>)).Invoke(new[] { Value, Value, Value, Value, Value, Value });

        private static readonly IServiceProvider cachedProvider = new ServiceProviderFactory.Builder()
            .Add<ICloneable>(Value)
            .Add<IComparable>(Value)
            .Add<IComparable<string>>(Value)
            .Add<IConvertible>(Value)
            .Add<IEquatable<string>>(Value)
            .Add<IEnumerable<char>>(Value)
            .Build();

        private static readonly IServiceProvider tupleProvider = ServiceProviderFactory.FromTuple(new ValueTuple<ICloneable, IComparable, IComparable<string>, IConvertible, IEquatable<string>, IEnumerable<char>>(
            Value, Value, Value, Value, Value, Value));

        [Benchmark]
        public object CompiledProvider() => compiledProvider.GetService(typeof(IConvertible));

        [Benchmark]
        public object CachedProvider() => cachedProvider.GetService(typeof(IConvertible));

        [Benchmark]
        public object TupleProvider() => tupleProvider.GetService(typeof(IConvertible));

        [Benchmark]
        public object CompiledProviderMissingService() => compiledProvider.GetService(typeof(int));

        [Benchmark]
        public object CachedProviderMissingService() => cachedProvider.GetService(typeof(int));

        [Benchmark]
        public object TupleProviderMissingService() => tupleProvider.GetService(typeof(int));
    }
}