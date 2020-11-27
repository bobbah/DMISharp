using BenchmarkDotNet.Running;

namespace DMISharpBenchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<DMIBenchmarks>();
        }
    }
}
