using System.Diagnostics.CodeAnalysis;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace DMISharp.Benchmark;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "BenchmarkDotNet invokes GlobalCleanup after benchmark execution.")]
public class GifConstructionBenchmarks
{
    private DMIFile _tinyAnimationFile = null!;
    private DMIFile _largeAnimationFile = null!;
    private DMIState _tinyAnimation = null!;
    private DMIState _largeAnimation = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tinyAnimationFile = new DMIFile(@"Data/Input/animal.dmi");
        _tinyAnimation = _tinyAnimationFile.States.First(x => x.Name == "mushroom");
        _largeAnimationFile = new DMIFile(@"Data/Input/352x352.dmi");
        _largeAnimation = _largeAnimationFile.States.First(x => x.Name == "singularity_s11");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _tinyAnimationFile.Dispose();
        _largeAnimationFile.Dispose();
    }

    [Benchmark]
    public void ConstructTinyAnimation()
    {
        using var image = _tinyAnimation.GetAnimated(StateDirection.South);
    }

    [Benchmark]
    public void ConstructLargeAnimation()
    {
        using var image = _largeAnimation.GetAnimated(StateDirection.South);
    }
}
