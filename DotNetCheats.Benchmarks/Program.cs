using System;
using BenchmarkDotNet.Running;


namespace Cheats
{
	internal static class Program
	{
		static void Main(string[] args)
		{
			BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
			//BenchmarkRunner.Run<MethodReflectionBenchmark>();
			Console.ReadKey();
		}
	}
}
