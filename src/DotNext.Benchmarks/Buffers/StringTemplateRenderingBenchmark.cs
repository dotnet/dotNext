using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;

namespace DotNext.Buffers;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class StringTemplateRenderingBenchmark
{
    private const string Template1 = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit, {0}
        sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, 
        quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor 
        in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. {1}
        Excepteur sint occaecat cupidatat non proident, {2} sunt in culpa qui officia deserunt mollit anim id est 
        laborum. {3}";

    private const string Template2 = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit, $$$
        sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, 
        quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor 
        in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. $$$
        Excepteur sint occaecat cupidatat non proident, $$$ sunt in culpa qui officia deserunt mollit anim id est 
        laborum. $$$";

    private readonly MemoryTemplate<char> precompiledTemplate = Template2.AsTemplate("$$$");

    [Benchmark]
    public string FormatUsingString() => string.Format(Template1, "1", "2", "3", "4");

    [Benchmark]
    public string FormatUsingTemplate() => precompiledTemplate.Render("1", "2", "3", "4");
}