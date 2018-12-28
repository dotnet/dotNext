using System;
using BenchmarkDotNet.Running;


namespace MissingPieces
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
