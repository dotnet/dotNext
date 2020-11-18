using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using System;

namespace DotNext.Benchmarks
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [MemoryDiagnoser]
    public class ServiceProviderFactoryMemoryBenchmark
    {
        private const string Value = "Value";
        private static readonly Func<IConvertible, ICloneable, IComparable, IServiceProvider> compiledFactory = ServiceProviderFactory.CreateFactory<IConvertible, ICloneable, IComparable>();

        [Benchmark]
        public object CompiledProvider() => compiledFactory.Invoke(Value, Value, Value);

        [Benchmark]
        public object CachedProvider() => ServiceProviderFactory.Create<IConvertible, ICloneable, IComparable>(Value, Value, Value);

        [Benchmark]
        public object FromTuple() => ServiceProviderFactory.FromTuple(new ValueTuple<IConvertible, ICloneable, IComparable>(Value, Value, Value));
    }
}