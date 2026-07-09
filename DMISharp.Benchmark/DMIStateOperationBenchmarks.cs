using System.Collections.Generic;
using System.Globalization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

#pragma warning disable CA1001, CA1024, CA2000

namespace DMISharp.Benchmark;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class ImportStatesBenchmarks
{
    private DMIFile _destination = null!;
    private DMIFile _source = null!;

    [Params(128, 1024, 4096)]
    public int StateCount { get; set; }

    [IterationSetup]
    public void Setup()
    {
        _destination = new DMIFile(1, 1);
        _source = new DMIFile(1, 1);

        for (var i = 0; i < StateCount; i++)
        {
            _source.AddState(new DMIState(i.ToString(CultureInfo.InvariantCulture), DirectionDepth.One, 1, 1, 1));
        }
    }

    [IterationCleanup]
    public void Cleanup()
    {
        _destination.Dispose();
        _source.Dispose();
    }

    [Benchmark]
    public int ImportStates() => _destination.ImportStates(_source);
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class StateCollectionBenchmarks
{
    private DMIFile _file = null!;

    [GlobalSetup]
    public void Setup()
    {
        _file = new DMIFile(1, 1);
        _file.AddState(new DMIState("state", DirectionDepth.One, 1, 1, 1));
    }

    [GlobalCleanup]
    public void Cleanup() => _file.Dispose();

    [Benchmark]
    public IReadOnlyCollection<DMIState> GetStates() => _file.States;
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class StateReadinessBenchmarks
{
    private DMIState _state = null!;

    [Params(1, 32)]
    public int Frames { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _state = new DMIState("state", DirectionDepth.Eight, Frames, 1, 1);
        for (var direction = 0; direction < _state.Dirs; direction++)
        {
            for (var frame = 0; frame < _state.Frames; frame++)
            {
                _state.SetFrame(new Image<Rgba32>(1, 1), (StateDirection)direction, frame);
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _state.Dispose();

    [Benchmark]
    public int TotalFrames() => _state.TotalFrames;

    [Benchmark]
    public int FrameCapacity() => _state.FrameCapacity;

    [Benchmark]
    public bool IsReadyForSave() => _state.IsReadyForSave();
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class StateDisposalBenchmarks
{
    private DMIState _state = null!;

    [Params(1, 32)]
    public int Frames { get; set; }

    [IterationSetup]
    public void Setup()
    {
        _state = new DMIState("state", DirectionDepth.Eight, Frames, 1, 1);
        for (var direction = 0; direction < _state.Dirs; direction++)
        {
            for (var frame = 0; frame < _state.Frames; frame++)
            {
                _state.SetFrame(new Image<Rgba32>(1, 1), (StateDirection)direction, frame);
            }
        }
    }

    [Benchmark]
    public void Dispose() => _state.Dispose();
}

#pragma warning restore CA1001, CA1024, CA2000
