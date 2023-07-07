using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace DMISharp.Quantization;

internal class NoFrillsQuantizer : IQuantizer
{
    private readonly ReadOnlyMemory<Color> _colorPalette;

    public NoFrillsQuantizer(ReadOnlyMemory<Color> palette, QuantizerOptions options)
    {
        _colorPalette = palette;
        Options = options;
    }

    public QuantizerOptions Options { get; }

    public IQuantizer<TPixel> CreatePixelSpecificQuantizer<TPixel>(Configuration configuration)
        where TPixel : unmanaged, IPixel<TPixel>
        => CreatePixelSpecificQuantizer<TPixel>(configuration, Options);

    public IQuantizer<TPixel> CreatePixelSpecificQuantizer<TPixel>(Configuration configuration,
        QuantizerOptions options) where TPixel : unmanaged, IPixel<TPixel>
    {
        var length = Math.Min(_colorPalette.Length, options.MaxColors);
        var palette = new TPixel[length];

        Color.ToPixel(_colorPalette.Span, palette.AsSpan());
        return new NoFrillsQuantizer<TPixel>(configuration, options, palette);
    }
}

internal readonly struct NoFrillsQuantizer<TPixel> : IQuantizer<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Dictionary<TPixel, ushort> _paletteLookup;

    public NoFrillsQuantizer(Configuration configuration, QuantizerOptions options, ReadOnlyMemory<TPixel> palette)
    {
        Configuration = configuration;
        Options = options;
        Palette = palette;

        _paletteLookup = new Dictionary<TPixel, ushort>();
        for (ushort i = 0; i < palette.Length; i++)
        {
            _paletteLookup.Add(palette.Span[i], i);
        }
    }

    public Configuration Configuration { get; }
    public QuantizerOptions Options { get; }
    public ReadOnlyMemory<TPixel> Palette { get; }

    public IndexedImageFrame<TPixel> QuantizeFrame(ImageFrame<TPixel> source, Rectangle bounds)
        => QuantizerUtilities.QuantizeFrame(ref Unsafe.AsRef(this), source, bounds);

    public void AddPaletteColors(Buffer2DRegion<TPixel> pixelRegion)
    {
    }

    public byte GetQuantizedColor(TPixel color, out TPixel match)
    {
        if (!_paletteLookup.ContainsKey(color))
            throw new ArgumentException("Unknown or invalid color for existing palette");

        var paletteIdx = _paletteLookup[color];
        match = Palette.Span[paletteIdx];
        return (byte)paletteIdx;
    }

    public void Dispose()
    {
    }
}