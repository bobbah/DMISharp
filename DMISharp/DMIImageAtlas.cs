using System;
using System.Collections.Generic;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.PixelFormats;

namespace DMISharp;

internal sealed class DMIImageAtlas : IDisposable
{
    private readonly object _imageLock = new();
    private Image<Rgba32>? _image;
    private int _referenceCount = 1;

    public DMIImageAtlas(Image<Rgba32> image)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
    }

    public int Width => GetImage().Width;

    public int Height => GetImage().Height;

    public IEnumerable<PngTextData> TextData =>
        GetImage().Metadata.GetFormatMetadata(PngFormat.Instance).TextData;

    public DMIImageAtlas AddReference()
    {
        while (true)
        {
            var referenceCount = Volatile.Read(ref _referenceCount);
            if (referenceCount == 0)
            {
                throw new ObjectDisposedException(nameof(DMIImageAtlas));
            }

            if (Interlocked.CompareExchange(ref _referenceCount, referenceCount + 1, referenceCount) ==
                referenceCount)
            {
                return this;
            }
        }
    }

    public Image<Rgba32> ExtractFrame(int sourceIndex, int framesPerRow, int width, int height)
    {
        var frame = new Image<Rgba32>(width, height);
        var xOffset = sourceIndex % framesPerRow * width;
        var yOffset = sourceIndex / framesPerRow * height;

        try
        {
            lock (_imageLock)
            {
                GetImage().ProcessPixelRows(frame, (sourceAccessor, frameAccessor) =>
                {
                    for (var y = 0; y < height; y++)
                    {
                        sourceAccessor.GetRowSpan(y + yOffset)
                            .Slice(xOffset, width)
                            .CopyTo(frameAccessor.GetRowSpan(y));
                    }
                });
            }

            return frame;
        }
        catch
        {
            frame.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Decrement(ref _referenceCount) != 0)
        {
            return;
        }

        lock (_imageLock)
        {
            _image?.Dispose();
            _image = null;
        }
    }

    private Image<Rgba32> GetImage() =>
        _image ?? throw new ObjectDisposedException(nameof(DMIImageAtlas));
}
