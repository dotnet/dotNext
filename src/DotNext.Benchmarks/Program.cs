using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using System;


namespace DotNext;

internal static class Program
{
    private static IConfig BenchConfig => DefaultConfig.Instance.AddJob(Job.Default.AsDefault()
        .WithArguments([new MsBuildArgument("/p:Optimize=true")])
        .WithCustomBuildConfiguration("Release"));

    static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, BenchConfig);
}