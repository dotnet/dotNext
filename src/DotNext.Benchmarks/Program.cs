using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using System;


namespace DotNext
{
    internal static class Program
    {
        private static IConfig BenchConfig => DefaultConfig.Instance.With(Job.Default.AsDefault().WithCustomBuildConfiguration("Benchmark"));

        static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, BenchConfig);
            Console.ReadKey();
        }
    }
}
