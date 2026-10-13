using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;

namespace DotNext.Text;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class CompositeFormatInterningBenchmark
{
    private const string Template = "This string emulates log message {0} with some parameters {1:x} and {2:o} for formatting";

    [Benchmark(Description = "Cached", Baseline = true)]
    public CompositeFormat GetCachedFormat() => CompositeFormat.Of(Template);

    [Benchmark(Description = "IsInterned")]
    public CompositeFormat GetInternedFormat() => CompositeFormat.IsInterned(Template);

    [Benchmark(Description = "Parsed")]
    public CompositeFormat ParseFormat() => CompositeFormat.Parse(Template);
}