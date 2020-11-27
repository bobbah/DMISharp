using BenchmarkDotNet.Running;

namespace DMISharpBenchmark
{
    class Program
    {
        static void Main()
        {
            _ = BenchmarkRunner.Run<DMIBenchmarks>();
        }
    }
}
