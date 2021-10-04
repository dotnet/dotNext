using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Reflection;

namespace DotNext.Reflection;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.Method)]
public class StringMethodReflectionBenchmark
{
    private static readonly Func<string, char, int, int> IndexOf = Type<string>.Method<char, int>.Require<int>(nameof(string.IndexOf));
    private static readonly Function<string, (char, int), int> IndexOfSpecial = Type<string>.RequireMethod<(char, int), int>(nameof(string.IndexOf));

    private static readonly MethodInfo IndexOfReflected = typeof(string).GetMethod(nameof(string.IndexOf), new[] { typeof(char), typeof(int) }, Array.Empty<ParameterModifier>());

    private static readonly Function<object, (object, object), object> IndexOfSpecialUntyped = IndexOfReflected.Unreflect<Function<object, (object, object), object>>();

    private static readonly DynamicInvoker IndexOfDynamicInvoker = IndexOfReflected.Unreflect();

    [Params("", "abccdahehkgbe387jwgr", "wfjwkhwfhwjgfkwjggwhjvfkwhwkgwjgbwjbwjbvbwvjwbvwjbvw")]
    public string StringValue;

    [Benchmark(Description = "Direct call", Baseline = true)]
    public int WithoutReflection() => StringValue.IndexOf('7', 0);

    [Benchmark(Description = "Reflection with DotNext using delegate type Func<string, char, int, int>")]
    public int WithTypedFastReflection() => IndexOf(StringValue, '7', 0);

    [Benchmark(Description = "Reflection with DotNext using delegate type Function<string, (char, int), int>")]
    public int WithTypedFastReflectionSpecial() => IndexOfSpecial(StringValue, ('7', 0));

    [Benchmark(Description = "Reflection with DotNext using delegate type Function<object, (object, object), object>")]
    public object WithUntypedFastReflectionSpecial() => IndexOfSpecialUntyped(StringValue, ('7', 0));

    [Benchmark(Description = "Reflection with DotNext using DynamicInvoker")]
    public object WithDynamicInvoker() => IndexOfDynamicInvoker.Invoke(StringValue, '7', 0);

    [Benchmark(Description = ".NET reflection")]
    public object WithReflection() => IndexOfReflected.Invoke(StringValue, new object[] { '7', 0 });
}