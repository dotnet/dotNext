using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;

namespace DotNext;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class UserDataBenchmark
{
    private static readonly UserDataSlot<StringBuilder> Slot = new();
    private ConditionalWeakTable<object, StringBuilder> values;
    private object obj;

    [GlobalSetup]
    public void Initialize()
    {
        obj = new();
        values = new();

        var sb = new StringBuilder();
        values.Add(obj, sb);
        obj.UserData.Set(Slot, sb);
    }

    [Benchmark]
    public StringBuilder ReadFromUserData() => obj.UserData.Get(Slot);

    [Benchmark(Baseline = true)]
    public StringBuilder ReadFromDictionary()
    {
        values.TryGetValue(obj, out var result);
        return result;
    }
}