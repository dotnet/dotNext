using System;
using BenchmarkDotNet.Running;


namespace DotNetCheats
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
