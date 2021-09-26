using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Threading.Tasks;

namespace DotNext.Metaprogramming;

using Linq.Expressions;
using static CodeGenerator;

/// <summary>
/// This benchmark aimed to compare overhead of in-memory compiled async lambda with
/// async method in C#.
/// </summary>
[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[MemoryDiagnoser]
public class AsyncMethodOverheadBenchmark
{
    private Func<Task<int>> generatedAsyncFunc;

    [GlobalSetup]
    public void CompileAsyncFuncInMemory()
    {
        generatedAsyncFunc = AsyncLambda<Func<Task<int>>>(static fun =>
        {
            var result = DeclareVariable("result", "42".Const());
            Await(typeof(Task).CallStatic(nameof(Task.Delay), 0.Const()));
            Assign(result, result.Concat("3".Const()));
            Await(typeof(Task).CallStatic(nameof(Task.Delay), 1.Const()));
            Return(typeof(int).CallStatic(nameof(int.Parse), result));
        }).Compile();
    }

    [Benchmark]
    public Task<int> CompiledAsyncMethod() => generatedAsyncFunc();

    [Benchmark]
    public async Task<int> CSharpAsyncMethod()
    {
        var result = "42";
        await Task.Delay(0);
        result += "3";
        await Task.Delay(1);
        return int.Parse(result);
    }
}