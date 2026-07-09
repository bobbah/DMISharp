using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace DMISharp.Benchmark;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[Config(typeof(Net8And10BenchmarkConfig))]
public class DMISaveBenchmarks
{
    private const int FrameSize = 32;
    private const int FrameCount = 256;

    private DMIFile _paletteFile = null!;
    private DMIFile _trueColorFile = null!;

    public enum SaveInput
    {
        Palette,
        TrueColor
    }

    [ParamsAllValues]
    public SaveInput Input { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _paletteFile = CreateFile(CreatePaletteFrame);
        _trueColorFile = CreateFile(CreateTrueColorFrame);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _paletteFile.Dispose();
        _trueColorFile.Dispose();
    }

    [Benchmark]
    public long Save()
    {
        using var stream = new MemoryStream();
        GetFile().Save(stream);
        return stream.Length;
    }

    private DMIFile GetFile() => Input == SaveInput.Palette ? _paletteFile : _trueColorFile;

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
    private static DMIFile CreateFile(Func<int, Image<Rgba32>> createFrame)
    {
        var file = new DMIFile(FrameSize, FrameSize);
        var state = new DMIState("benchmark", DirectionDepth.One, FrameCount, FrameSize, FrameSize);

        for (var frame = 0; frame < FrameCount; frame++)
            state.SetFrame(createFrame(frame), frame);

        file.AddState(state);
        return file;
    }

    private static Image<Rgba32> CreatePaletteFrame(int frame)
    {
        var image = new Image<Rgba32>(FrameSize, FrameSize);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < FrameSize; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < FrameSize; x++)
                {
                    var color = (byte)((x / 4 + y / 4 + frame) & 15);
                    row[x] = new Rgba32(
                        (byte)(color * 17),
                        (byte)((color * 53) & 255),
                        (byte)((color * 97) & 255),
                        color == 0 ? (byte)0 : byte.MaxValue);
                }
            }
        });
        return image;
    }

    private static Image<Rgba32> CreateTrueColorFrame(int frame)
    {
        var image = new Image<Rgba32>(FrameSize, FrameSize);
        image.ProcessPixelRows(accessor =>
        {
            var value = (uint)(frame + 1);
            for (var y = 0; y < FrameSize; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < FrameSize; x++)
                {
                    value ^= value << 13;
                    value ^= value >> 17;
                    value ^= value << 5;
                    row[x] = new Rgba32(
                        (byte)value,
                        (byte)(value >> 8),
                        (byte)(value >> 16),
                        byte.MaxValue);
                }
            }
        });
        return image;
    }
}
