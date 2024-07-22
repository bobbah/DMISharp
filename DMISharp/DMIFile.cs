using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using DMISharp.Interfaces;
using DMISharp.Metadata;
using DMISharp.Quantization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace DMISharp;

/// <summary>
/// Provides a means to interact with BYOND DMI files.
/// </summary>
public sealed class DMIFile : IDisposable, IExportable
{
    private bool _disposedValue;
    private List<DMIState> _states;

    /// <summary>
    /// Constructs a new <see cref="DMIFile"/> for a provided pair of state dimensions.
    /// </summary>
    /// <param name="frameWidth">The width of frames in each state in pixels</param>
    /// <param name="frameHeight">The height of frames in each state in pixels</param>
    public DMIFile(int frameWidth, int frameHeight)
    {
        Metadata = new DMIMetadata(4.0, frameWidth, frameHeight);
        _states = new List<DMIState>();
    }

    /// <summary>
    /// Constructs a new <see cref="DMIFile"/> from a provided stream.
    /// </summary>
    /// <param name="stream">The Stream containing the DMI file data.</param>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public DMIFile(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        // As the metadata is embedded in the PNG file, extract into a usable object.
        Metadata = new DMIMetadata(stream);

        // Reset stream position for processing image data.
        stream.Seek(0, SeekOrigin.Begin);
        _states = GetStates(stream).ToList();

        stream.Dispose();
    }

    /// <summary>
    /// Constructor helper for DMI File object initialization.
    /// </summary>
    /// <param name="file">The path to the DMI file.</param>
    public DMIFile(string file)
        : this(File.OpenRead(file))
    {
    }

    /// <summary>
    /// The metadata for this DMI File.
    /// </summary>
    public DMIMetadata Metadata { get; }

    /// <summary>
    /// All of the <see cref="DMIState"/> entries for this DMI file.
    /// </summary>
    public IReadOnlyCollection<DMIState> States => _states.AsReadOnly();

    /// <summary>
    /// Saves a DMI File to a stream. The resulting file is .dmi-ready
    /// </summary>
    /// <param name="dataStream">The stream to save the DMI File to.</param>
    /// <returns>True if the file was saved, false otherwise</returns>
    public void Save(Stream dataStream)
    {
        if (dataStream == null) throw new ArgumentNullException(nameof(dataStream), "Target stream cannot be null!");

        // prepare frames
        var frames = new List<Image<Rgba32>>();
        foreach (var state in States)
        {
            for (var frame = 0; frame < state.Frames; frame++)
            {
                for (var dir = 0; dir < state.Dirs; dir++)
                {
                    var foundFrame = state.GetFrame((StateDirection)dir, frame);
                    if (foundFrame != null)
                        frames.Add(foundFrame);
                    else
                        throw new InvalidOperationException(
                            $"Failed to get frame for state: {dir} frame {frame} is null");
                }
            }
        }

        // Get dimensions in frames using the same logic that BYOND does internally (adapted from byondcore.dll)
        // (more specifically from iconToPixels)
        var numFrames = frames.Count;
        var xRatio = Math.Sqrt((double)Metadata.FrameHeight * numFrames / Metadata.FrameWidth);
        var yRatio = Math.Sqrt((double)Metadata.FrameWidth * numFrames / Metadata.FrameHeight);

        var intermediateX = Math.Floor(xRatio);
        if (intermediateX * Math.Ceiling(yRatio) < numFrames)
            xRatio = Math.Ceiling(xRatio);

        intermediateX = Math.Ceiling(xRatio);
        var intermediateY = Math.Floor(yRatio);
        if (intermediateY * intermediateX < numFrames)
            yRatio = Math.Ceiling(yRatio);

        intermediateY = Math.Floor(yRatio);
        var intermediateY2 = Math.Ceiling(yRatio);
        xRatio = Math.Floor(xRatio);
        if (intermediateY * intermediateX <= xRatio * intermediateY2)
        {
            intermediateY2 = intermediateY;
            xRatio = intermediateX;
        }

        var xFrames = (int)(xRatio + 0.5);
        var yFrames = (int)(intermediateY2 + 0.5);

        using var img = new Image<Rgba32>(xFrames * Metadata.FrameWidth, yFrames * Metadata.FrameHeight);
        for (int y = 0, i = 0; y < yFrames && i < numFrames; y++)
        {
            for (var x = 0; x < xFrames && i < numFrames; x++, i++)
            {
                var targetFrame = frames[i];
                var yCap = y; // For closure
                var xCap = x; // For closure
                img.ProcessPixelRows(targetFrame, (imgAccessor, targetAccessor) =>
                {
                    for (var ypx = 0; ypx < Metadata.FrameHeight; ypx++)
                    {
                        var sourceSpan = targetAccessor.GetRowSpan(ypx);
                        var destSpan = imgAccessor.GetRowSpan(ypx + yCap * Metadata.FrameHeight);
                        sourceSpan.CopyTo(destSpan.Slice(xCap * Metadata.FrameWidth, Metadata.FrameWidth));
                    }
                });
            }
        }

        var md = img.Metadata.GetFormatMetadata(PngFormat.Instance);
        md.TextData.Add(new PngTextData("Description", GetTextChunk(), string.Empty, string.Empty));

        // Perform some brief analysis to determine optimal color type for saving
        var hasTransparency = false;
        var pristineGreyscale = true;
        var colors = new HashSet<uint>();
        var transparent = Color.Transparent;
        var height = img.Height;
        var width = img.Width;
        img.ProcessPixelRows(accessor =>
        {
            for (var ypx = 0; ypx < height; ypx++)
            {
                var row = accessor.GetRowSpan(ypx);
                for (var xpx = 0; xpx < width; xpx++)
                {
                    ref var pixel = ref row[xpx];

                    // Check for transparency, if we ultimately don't have any we can remove the alpha layer
                    if (!hasTransparency && pixel.A < byte.MaxValue)
                        hasTransparency = true;

                    // Set color to be transparent black if fully transparent
                    if (pixel.A == 0)
                        pixel.FromRgba32(transparent);

                    // Check for greyscale pristine-ness
                    if (pristineGreyscale && !(pixel.R == pixel.G && pixel.G == pixel.B))
                        pristineGreyscale = false;

                    // Count distinct colors to determine best approach with palette
                    colors.Add(pixel.PackedValue);
                }
            }
        });

        // Determine color type
        PngColorType? colorType = null;

        // No transparency needed if there is no transparency used
        if (!hasTransparency)
            colorType = PngColorType.Rgb;

        // If the image is fully greyscale then save as grayscale for space savings
        if (pristineGreyscale)
            colorType = hasTransparency ? PngColorType.GrayscaleWithAlpha : PngColorType.Grayscale;

        var pngEncoder = new PngEncoder()
        {
            InterlaceMethod = PngInterlaceMode.None,
            CompressionLevel = PngCompressionLevel.BestCompression,
            FilterMethod = PngFilterMethod.Adaptive,
            TransparentColorMode = PngTransparentColorMode.Clear,
            TextCompressionThreshold = 0, // always compress text chunks
            ChunkFilter = PngChunkFilter.ExcludePhysicalChunk | PngChunkFilter.ExcludeExifChunk |
                          PngChunkFilter.ExcludeGammaChunk,
            ColorType = colorType
        };

        // If there is no possibility of a color palette we can return at this point
        if (colors.Count > 256)
        {
            img.SaveAsPng(dataStream, pngEncoder);
            return;
        }

        // We can use a color palette
        var colorSpan = colors.ToImmutableArray().AsSpan();
        var paletteEncoder = new PngEncoder()
        {
            InterlaceMethod = pngEncoder.InterlaceMethod,
            CompressionLevel = pngEncoder.CompressionLevel,
            FilterMethod = pngEncoder.FilterMethod,
            TransparentColorMode = pngEncoder.TransparentColorMode,
            TextCompressionThreshold = pngEncoder.TextCompressionThreshold,
            ChunkFilter = pngEncoder.ChunkFilter,
            ColorType = PngColorType.Palette,
            Quantizer = new NoFrillsQuantizer(colors.Select(x => new Color(new Rgba32(x))).ToArray(),
                new QuantizerOptions() { Dither = null }),
            BitDepth = GetBitDepth(colorSpan)
        };

        // Test to see if the default saving or palette is smaller, then use the smallest of the two
        using var paletteMs = new MemoryStream();
        using var normalMs = new MemoryStream();

        img.SaveAsPng(paletteMs, paletteEncoder);
        img.SaveAsPng(normalMs, pngEncoder);

        var smallest = paletteMs.Length < normalMs.Length ? paletteMs : normalMs;
        smallest.Seek(0, SeekOrigin.Begin);
        smallest.CopyTo(dataStream);
    }

    /// <summary>
    /// Determines if a DMI file is ready to be saved
    /// </summary>
    /// <returns>True if the file is ready to be saved, otherwise false</returns>
    public bool CanSave()
    {
        var result = States.Count != 0;
        foreach (var state in States)
        {
            result = result && state.IsReadyForSave();
        }

        return result;
    }

    /// <summary>
    /// Saves a DMI File to a specific file path.
    /// </summary>
    /// <param name="path">The path to save the image to.</param>
    /// <returns>True if the file was saved, false otherwise</returns>
    public void Save(string path)
    {
        using var fs = File.OpenWrite(path);
        Save(fs);
    }

    /// <summary>
    /// Develops BYOND txt header for DMI files
    /// </summary>
    /// <returns>The BYOND txt header for this DMI file</returns>
    private string GetTextChunk()
    {
        var builder = new StringBuilder();
        builder.Append(
            FormattableString.Invariant(
                $"# BEGIN DMI\nversion = {Metadata.Version:0.0}\n\twidth = {Metadata.FrameWidth}\n\theight = {Metadata.FrameHeight}\n"));

        foreach (var state in States)
        {
            builder.Append(
                FormattableString.Invariant(
                    $"state = \"{state.Name}\"\n\tdirs = {state.Dirs}\n\tframes = {state.Frames}\n"));
            if (state.Data.Delay != null)
                builder.Append(FormattableString.Invariant($"\tdelay = {string.Join(",", state.Data.Delay)}\n"));
            if (state.Data.Loop > 0) builder.Append(FormattableString.Invariant($"\tloop = {state.Data.Loop}\n"));
            if (state.Data.Hotspots != null)
                builder.Append(FormattableString.Invariant($"\thotspots = {string.Join(",", state.Data.Hotspots)}\n"));
            if (state.Data.Movement) builder.Append("\tmovement = 1\n");
            if (state.Data.Rewind) builder.Append("\trewind = 1\n");
        }

        builder.Append("# END DMI\n");
        return builder.ToString();
    }

    /// <summary>
    /// Processes DMI metadata into DMI State objects.
    /// </summary>
    /// <param name="source">The stream containing the DMI file data.</param>
    /// <returns>An enumerable collection of DMI State objects representing the states of the DMI File.</returns>
    private IEnumerable<DMIState> GetStates(Stream source)
    {
        var states = new List<DMIState>();

        using var img = Image.Load<Rgba32>(source);
        // DMI data did not include widths or heights, assume that it is then
        // perfect squares, thus we will determine the w/h programatically...
        if (Metadata.FrameWidth == -1 || Metadata.FrameHeight == -1)
        {
            var totalFrames = Metadata.States.Sum(x => x.Frames * x.Dirs);

            for (var rows = 1; totalFrames >= rows; rows++)
            {
                if (img.Width / (totalFrames / rows) == img.Height / rows)
                {
                    Metadata.FrameHeight = img.Height / rows;
                    Metadata.FrameWidth = img.Width / (totalFrames / rows);
                    break;
                }
            }
        }

        if (Metadata.FrameHeight == 0 || Metadata.FrameWidth == 0)
        {
            return states;
        }

        var wFrames = img.Width / Metadata.FrameWidth;
        var hFrames = img.Height / Metadata.FrameHeight;
        var processedImages = 0;
        var currWIndex = 0;
        var currHIndex = 0;

        foreach (var state in Metadata.States)
        {
            var toAdd = new DMIState(state, img, currWIndex, wFrames, currHIndex, hFrames, Metadata.FrameWidth,
                Metadata.FrameHeight);
            processedImages += toAdd.TotalFrames;
            currHIndex = processedImages / wFrames;
            currWIndex = processedImages % wFrames;
            states.Add(toAdd);
        }

        return states;
    }

    /// <summary>
    /// Sorts the states of this DMI File alphabetically by their state name.
    /// </summary>
    public void SortStates()
    {
        _states = _states.OrderBy(x => x.Name).ToList();
        Metadata.States.Clear();
        foreach (var state in _states)
        {
            Metadata.States.Add(state.Data);
        }
    }

    /// <summary>
    /// Sorts the states of this DMI File using a provided comparer for DMIStates.
    /// </summary>
    /// <param name="comparer">The comparer to use</param>
    public void SortStates(IComparer<DMIState> comparer)
    {
        _states = _states.OrderBy(x => x, comparer).ToList();
        Metadata.States.Clear();
        foreach (var state in _states)
        {
            Metadata.States.Add(state.Data);
        }
    }

    /// <summary>
    /// Imports states from another DMI file.
    /// </summary>
    /// <param name="other">The DMI file to import states from</param>
    /// <returns>The number of states imported</returns>
    public int ImportStates(DMIFile? other)
    {
        if (other != null
            && other.Metadata.FrameHeight == Metadata.FrameHeight
            && other.Metadata.FrameWidth == Metadata.FrameWidth)
        {
            var added = 0;
            while (other.States.Count != 0)
            {
                var cursor = other.States.First();
                other.RemoveState(cursor);
                AddState(cursor);
                added++;
            }

            return added;
        }

        return 0;
    }

    /// <summary>
    /// Clears the states from a DMI file.
    /// </summary>
    public void ClearStates()
    {
        _states.Clear();
    }

    /// <summary>
    /// Removes a state from a DMI File.
    /// </summary>
    /// <param name="toRemove">The DMIState to remove</param>
    /// <returns>True if the state was removed, otherwise false</returns>
    public bool RemoveState(DMIState toRemove)
    {
        if (toRemove is { Data: not null } && _states.Remove(toRemove))
        {
            return Metadata.States.Remove(toRemove.Data);
        }

        return false;
    }

    /// <summary>
    /// Adds a state to a DMI File.
    /// </summary>
    /// <param name="toAdd">The DMIState to add</param>
    /// <returns>True if the state was added, otherwise false</returns>
    public bool AddState(DMIState toAdd)
    {
        if (toAdd == null)
            throw new ArgumentNullException(nameof(toAdd));
        if (!StateValidForFile(toAdd))
            return false;

        _states.Add(toAdd);
        Metadata.States.Add(toAdd.Data);
        return true;
    }

    /// <summary>
    /// Ensures that a state is valid for a DMI File's existing dimensions.
    /// </summary>
    /// <param name="toCheck">The DMIState to check against the file</param>
    /// <returns>True if the state is compatible with the file, false otherwise</returns>
    private bool StateValidForFile(DMIState toCheck) =>
        toCheck.Height == Metadata.FrameHeight
        && toCheck.Width == Metadata.FrameWidth;

    private static PngBitDepth GetBitDepth(ReadOnlySpan<uint> colors)
    {
        return colors.Length switch
        {
            <= 2 => PngBitDepth.Bit1,
            <= 4 => PngBitDepth.Bit2,
            <= 16 => PngBitDepth.Bit4,
            _ => PngBitDepth.Bit8
        };
    }

    /// <summary>
    /// Dispose of the DMI file.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
    }

    /// <summary>
    /// Ensure when the DMI File is disposed of that all DMI States and their respective images are disposed of.
    /// </summary>
    private void Dispose(bool disposing)
    {
        if (_disposedValue)
            return;

        if (disposing)
        {
            foreach (var state in _states)
            {
                state.Dispose();
            }
        }

        _states.Clear();
        _disposedValue = true;
    }
}