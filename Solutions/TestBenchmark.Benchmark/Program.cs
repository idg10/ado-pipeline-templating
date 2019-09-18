using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.IO;

namespace TestBenchmark.Benchmark
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            IConfig config = DefaultConfig.Instance;
            if (args.Length > 0)
            {
                string artifactsPath = args[0];
                Directory.CreateDirectory(artifactsPath);
                config = config.WithArtifactsPath(artifactsPath);
            }

            BenchmarkRunner.Run<AllBenchmarks>(config);
        }
    }
}
