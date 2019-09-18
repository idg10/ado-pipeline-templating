using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using TestBenchmark.Lib;

namespace TestBenchmark.Benchmark
{
    [JsonExporterAttribute.Full]
    public class AllBenchmarks
    {
        [Benchmark]
        public void InvokeOp1000()
        {
            var x = new Counter();
            for (int i = 0; i < 1000; ++i)
            {
                x.Increment();
            }
        }
    }
}
