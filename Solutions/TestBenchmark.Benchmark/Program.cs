using BenchmarkDotNet.Running;
using System;

namespace TestBenchmark.Benchmark
{
    internal static class Program
    {
        private static void Main()
        {
            BenchmarkRunner.Run<AllBenchmarks>();
        }
    }
}
