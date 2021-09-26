using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using System;


namespace DotNext;

internal static class Program
{
    private static IConfig BenchConfig => DefaultConfig.Instance.AddJob(Job.Default.AsDefault()
        .WithArguments(new[] { new MsBuildArgument("/p:Optimize=true") })
        .WithCustomBuildConfiguration("Bench"));

    static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, BenchConfig);
        Console.ReadKey();
    }
}