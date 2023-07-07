using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace DMISharp.Benchmark;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net70)]
[SuppressMessage("Performance", "CA1822:Mark members as static")]
public class DMIBenchmarks
{
    [Benchmark]
    public DMIFile ReadSmallDMIFile() => new("Data/Input/small.dmi");

    [Benchmark]
    public DMIFile ReadLargeDMIFile() => new("Data/Input/large.dmi");

    [Benchmark]
    public void WriteDMIFile()
    {
        using var ms = new MemoryStream();
        using var file = new DMIFile(@"Data/Input/air_meter.dmi");
        file.Save(ms);
    }

    [Benchmark]
    public void SortDMIFile()
    {
        using var ms = new MemoryStream();
        using var file = new DMIFile(@"Data/Input/animal.dmi");
        file.SortStates();
        file.Save(ms);
    }

    [Benchmark]
    public void WriteAnimations()
    {
        using var file = new DMIFile(@"Data/Input/animal.dmi");
        var toTest = file.States.First(x => x.Name == "mushroom");

        for (var dir = StateDirection.South; dir != StateDirection.SouthEast; dir++)
        {
            using var ms = new MemoryStream();
            toTest.SaveAnimatedGIF(ms, dir);
        }
    }

    [Benchmark]
    public void AnimationOfBarsignsConstructsCorrectly()
    {
        using var ms = new MemoryStream();
        using var file = new DMIFile(@"Data/Input/barsigns.dmi");
        var toTest = file.States.First(x => x.Name == "thegreytide");
        toTest.SaveAnimatedGIF(ms, StateDirection.South);
    }

    [Benchmark]
    public void AnimationOfSingularityConstructsCorrectly()
    {
        using var ms = new MemoryStream();
        using var file = new DMIFile(@"Data/Input/352x352.dmi");
        var toTest = file.States.First(x => x.Name == "singularity_s11");
        toTest.SaveAnimatedGIF(ms, StateDirection.South);
    }
}