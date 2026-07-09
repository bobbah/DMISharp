using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

namespace DMISharp.Benchmark;

[MemoryDiagnoser, Config(typeof(DMIReadBenchmarkConfig))]
public class DMIReadBenchmarks
{
    private string _path = null!;

    [Params("small.dmi", "large.dmi", "352x352.dmi")]
    public string FileName { get; set; } = null!;

    [GlobalSetup]
    public void Setup() => _path = Path.Combine("Data", "Input", FileName);

    [Benchmark]
    public void ReadDMIFile()
    {
        using var file = new DMIFile(_path);
    }

    [Benchmark]
    public void ReadAndMaterializeAllFrames()
    {
        using var file = new DMIFile(_path);
        foreach (var state in file.States)
        {
            for (var frame = 0; frame < state.Frames; frame++)
            {
                for (var direction = 0; direction < state.Dirs; direction++)
                {
                    _ = state.GetFrame((StateDirection)direction, frame);
                }
            }
        }
    }
}

internal sealed class DMIReadBenchmarkConfig : ManualConfig
{
    public DMIReadBenchmarkConfig()
    {
        AddJob(Job.Default.WithRuntime(CoreRuntime.Core80).AsBaseline());
        AddJob(Job.Default.WithRuntime(CoreRuntime.Core10_0));
    }
}

internal sealed class Net8And10BenchmarkConfig : ManualConfig
{
    public Net8And10BenchmarkConfig()
    {
        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core80)
            .WithWarmupCount(3)
            .WithIterationCount(5)
            .AsBaseline());
        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core10_0)
            .WithWarmupCount(3)
            .WithIterationCount(5));
    }
}

[MemoryDiagnoser, Config(typeof(DMIBenchmarkConfig))]
[SuppressMessage("Performance", "CA1822:Mark members as static")]
public class DMIBenchmarks
{
    private class DMIBenchmarkConfig : ManualConfig
    {
        public DMIBenchmarkConfig()
        {
            AddJob(Job.Default.WithRuntime(CoreRuntime.Core80).AsBaseline());
            AddJob(Job.Default.WithRuntime(CoreRuntime.Core10_0));
        }
    }
    
    [Benchmark]
    public DMIFile ReadSmallDMIFile() => new("Data/Input/small.dmi");

    [Benchmark]
    public DMIFile ReadLargeDMIFile() => new("Data/Input/large.dmi");

    [Benchmark]
    [Arguments("small.dmi")]
    [Arguments("air_meter.dmi")]
    public void WritePaletteDMIFile(string fileName)
    {
        using var ms = new MemoryStream();
        using var file = new DMIFile(Path.Combine("Data", "Input", fileName));
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