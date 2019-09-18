using System;

namespace TestBenchmark.Lib
{
    public class Counter
    {
        private int count;

        public int Increment()
        {
            return ++count;
        }
    }
}
