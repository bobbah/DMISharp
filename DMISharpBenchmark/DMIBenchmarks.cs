using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using BenchmarkDotNet.Jobs;
using DMISharp;

namespace DMISharpBenchmark
{
    [EtwProfiler]
    public class DMIBenchmarks
    {
        [Benchmark]
        public DMIFile ReadSmallDMIFile()
        {
            return new DMIFile("Data/small.dmi");
        }

        [Benchmark]
        public DMIFile ReadLargeDMIFile()
        {
            return new DMIFile("Data/large.dmi");
        }
    }
}
