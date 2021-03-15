using BenchmarkDotNet.Running;

namespace DMISharpBenchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            _ = BenchmarkSwitcher
                .FromAssembly(typeof(Program).Assembly)
                .Run(args);
        }
    }
}
