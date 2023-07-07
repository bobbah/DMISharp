
using BenchmarkDotNet.Running;

namespace DMISharp.Benchmark;

public static class Program
{
    public static void Main(string[] args)
    {
        _ = BenchmarkSwitcher
            .FromAssembly(typeof(Program).Assembly)
            .Run(args);
    }
}