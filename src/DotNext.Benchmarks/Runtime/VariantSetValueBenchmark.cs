using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using DotNext.Reflection;

namespace DotNext.Runtime;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.Declared)]
[MemoryDiagnoser]
public class VariantSetValueBenchmark
{
    private static readonly FieldInfo IntField = typeof(Holder).GetField(nameof(Holder.IntValue))!;
    private static readonly FieldInfo StringField = typeof(Holder).GetField(nameof(Holder.StringValue))!;

    private readonly Holder holder = new();
    private readonly object boxedInt = 42;
    private readonly object stringValue = "Hello, world!";

    [Benchmark(Description = "Variant.TrySetValue, int")]
    public bool VariantSetInt() => Variant.Mutable(ref holder.IntValue).TrySetValue(boxedInt);

    [Benchmark(Description = "Variant.FromField + TrySetValue, int")]
    public bool VariantFromFieldSetInt() => Variant.FromField(holder, IntField).TrySetValue(boxedInt);

    [Benchmark(Description = "FieldInfo.SetValue, int")]
    public void FieldInfoSetInt() => IntField.SetValue(holder, boxedInt);

    [Benchmark(Description = "FieldInfo.SetValueDirect, int")]
    public void FieldInfoSetDirectInt()
    {
        var target = holder;
        IntField.SetValueDirect(__makeref(target), boxedInt);
    }

    [Benchmark(Description = "Variant.TrySetValue, string")]
    public bool VariantSetString() => Variant.Mutable(ref holder.StringValue).TrySetValue(stringValue);

    [Benchmark(Description = "Variant.FromField + TrySetValue, string")]
    public bool VariantFromFieldSetString() => Variant.FromField(holder, StringField).TrySetValue(stringValue);

    [Benchmark(Description = "FieldInfo.SetValue, string")]
    public void FieldInfoSetString() => StringField.SetValue(holder, stringValue);

    [Benchmark(Description = "FieldInfo.SetValueDirect, string")]
    public void FieldInfoSetDirectString()
    {
        var target = holder;
        StringField.SetValueDirect(__makeref(target), stringValue);
    }

    public sealed class Holder
    {
        public int IntValue;
        public string StringValue;
    }
}
