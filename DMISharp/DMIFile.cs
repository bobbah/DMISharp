using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using DMISharp.Interfaces;
using DMISharp.Metadata;
using DMISharp.Quantization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Png.Chunks;
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
    private ReadOnlyCollection<DMIState> _statesView;

    /// <summary>
    /// Constructs a new <see cref="DMIFile"/> for a provided pair of state dimensions.
    /// </summary>
    /// <param name="frameWidth">The width of frames in each state in pixels</param>
    /// <param name="frameHeight">The height of frames in each state in pixels</param>
    public DMIFile(int frameWidth, int frameHeight)
    {
        Metadata = new DMIMetadata(4.0, frameWidth, frameHeight);
        _states = new List<DMIState>();
        _statesView = _states.AsReadOnly();
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

        using var atlas = new DMIImageAtlas(Image.Load<Rgba32>(stream));
        Metadata = new DMIMetadata(atlas.TextData);
        _states = GetStates(atlas);
        _statesView = _states.AsReadOnly();

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
    public IReadOnlyCollection<DMIState> States => _statesView;

    /// <summary>
    /// Saves a DMI File to a stream. The resulting file is .dmi-ready
    /// </summary>
    /// <param name="dataStream">The stream to save the DMI File to.</param>
    /// <returns>True if the file was saved, false otherwise</returns>
    public void Save(Stream dataStream)
    {
        if (dataStream == null) throw new ArgumentNullException(nameof(dataStream), "Target stream cannot be null!");

        // Get dimensions in frames using the same logic that BYOND does internally (adapted from byondcore.dll)
        // (more specifically from iconToPixels)
        var numFrames = States.Sum(state => state.Frames * state.Dirs);
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

        // Build the atlas and analyze its colors in one pass.
        var hasTransparency = false;
        var pristineGreyscale = true;
        var colors = new HashSet<uint>();
        var paletteCompatible = true;
        var transparent = Color.Transparent;
        using var img = new Image<Rgba32>(xFrames * Metadata.FrameWidth, yFrames * Metadata.FrameHeight);
        var frameIndex = 0;
        foreach (var state in States)
        {
            for (var frame = 0; frame < state.Frames; frame++)
            {
                for (var dir = 0; dir < state.Dirs; dir++)
                {
                    var sourceFrame = state.GetFrame((StateDirection)dir, frame);
                    if (sourceFrame == null)
                        throw new InvalidOperationException(
                            $"Failed to get frame for state: {dir} frame {frame} is null");

                    var atlasX = frameIndex % xFrames * Metadata.FrameWidth;
                    var atlasY = frameIndex / xFrames * Metadata.FrameHeight;
                    img.ProcessPixelRows(sourceFrame, (imgAccessor, sourceAccessor) =>
                    {
                        for (var ypx = 0; ypx < Metadata.FrameHeight; ypx++)
                        {
                            var sourceSpan = sourceAccessor.GetRowSpan(ypx);
                            var destSpan = imgAccessor.GetRowSpan(ypx + atlasY)
                                .Slice(atlasX, Metadata.FrameWidth);
                            sourceSpan.CopyTo(destSpan);

                            for (var xpx = 0; xpx < destSpan.Length; xpx++)
                            {
                                ref var pixel = ref destSpan[xpx];

                                if (!hasTransparency && pixel.A < byte.MaxValue)
                                    hasTransparency = true;

                                if (pixel.A == 0)
                                    pixel.FromRgba32(transparent);

                                if (pristineGreyscale && !(pixel.R == pixel.G && pixel.G == pixel.B))
                                    pristineGreyscale = false;

                                if (paletteCompatible && colors.Add(pixel.PackedValue) && colors.Count > 256)
                                    paletteCompatible = false;
                            }
                        }
                    });
                    frameIndex++;
                }
            }
        }

        // BYOND's layout can leave unused atlas cells, which remain transparent black.
        if (frameIndex < xFrames * yFrames)
        {
            hasTransparency = true;
            if (paletteCompatible && colors.Add(default(Rgba32).PackedValue) && colors.Count > 256)
                paletteCompatible = false;
        }

        var md = img.Metadata.GetFormatMetadata(PngFormat.Instance);
        md.TextData.Add(new PngTextData("Description", GetTextChunk(), string.Empty, string.Empty));

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
            // ImageSharp 3.1.4 ignored explicit filters, so None preserves existing output and performance.
            FilterMethod = PngFilterMethod.None,
            TransparentColorMode = PngTransparentColorMode.Clear,
            TextCompressionThreshold = 0, // always compress text chunks
            ChunkFilter = PngChunkFilter.ExcludePhysicalChunk | PngChunkFilter.ExcludeExifChunk |
                          PngChunkFilter.ExcludeGammaChunk,
            ColorType = colorType
        };

        // If there is no possibility of a color palette we can return at this point
        if (!paletteCompatible)
        {
            img.SaveAsPng(dataStream, pngEncoder);
            return;
        }

        // We can use a color palette
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
            BitDepth = GetBitDepth(colors.Count)
        };

        // Appending to a resizable stream lets the palette candidate be written directly. If the normal
        // candidate is smaller, it safely replaces the appended bytes without touching existing data.
        if (dataStream is MemoryStream or FileStream
            && dataStream.Position == dataStream.Length)
        {
            var outputStart = dataStream.Position;
            try
            {
                img.SaveAsPng(dataStream, paletteEncoder);
                var paletteLength = dataStream.Position - outputStart;

                using var normalMs = new MemoryStream();
                img.SaveAsPng(normalMs, pngEncoder);
                if (normalMs.Length <= paletteLength)
                {
                    dataStream.Position = outputStart;
                    normalMs.Position = 0;
                    normalMs.CopyTo(dataStream);
                    dataStream.SetLength(dataStream.Position);
                }
            }
            catch
            {
                dataStream.SetLength(outputStart);
                dataStream.Position = outputStart;
                throw;
            }

            return;
        }

        // Preserve overwrite and non-seekable stream behavior by buffering both candidates.
        using var paletteMs = new MemoryStream();
        using var bufferedNormalMs = new MemoryStream();

        img.SaveAsPng(paletteMs, paletteEncoder);
        img.SaveAsPng(bufferedNormalMs, pngEncoder);

        var smallest = paletteMs.Length < bufferedNormalMs.Length ? paletteMs : bufferedNormalMs;
        smallest.Seek(0, SeekOrigin.Begin);
        smallest.CopyTo(dataStream);
    }

    /// <summary>
    /// Determines if a DMI file is ready to be saved
    /// </summary>
    /// <returns>True if the file is ready to be saved, otherwise false</returns>
    public bool CanSave()
    {
        if (_states.Count == 0)
            return false;

        foreach (var state in _states)
        {
            if (!state.IsReadyForSave())
                return false;
        }

        return true;
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
    /// <param name="atlas">The decoded DMI atlas.</param>
    /// <returns>A collection of DMI State objects representing the states of the DMI File.</returns>
    private List<DMIState> GetStates(DMIImageAtlas atlas)
    {
        var states = new List<DMIState>();

        // DMI data did not include widths or heights, assume that it is then
        // perfect squares, thus we will determine the w/h programatically...
        if (Metadata.FrameWidth == -1 || Metadata.FrameHeight == -1)
        {
            var totalFrames = Metadata.States.Sum(x => x.Frames * x.Dirs);

            for (var rows = 1; totalFrames >= rows; rows++)
            {
                if (atlas.Width / (totalFrames / rows) == atlas.Height / rows)
                {
                    Metadata.FrameHeight = atlas.Height / rows;
                    Metadata.FrameWidth = atlas.Width / (totalFrames / rows);
                    break;
                }
            }
        }

        if (Metadata.FrameHeight == 0 || Metadata.FrameWidth == 0)
        {
            return states;
        }

        var wFrames = atlas.Width / Metadata.FrameWidth;
        var hFrames = atlas.Height / Metadata.FrameHeight;
        var processedImages = 0;

        try
        {
            foreach (var state in Metadata.States)
            {
                var toAdd = new DMIState(state, atlas, processedImages, wFrames, hFrames, Metadata.FrameWidth,
                    Metadata.FrameHeight);
                processedImages += toAdd.TotalFrames;
                states.Add(toAdd);
            }
        }
        catch
        {
            foreach (var state in states)
            {
                state.Dispose();
            }

            throw;
        }

        return states;
    }

    /// <summary>
    /// Sorts the states of this DMI File alphabetically by their state name.
    /// </summary>
    public void SortStates()
    {
        var sortedStates = _states.OrderBy(x => x.Name).ToArray();
        _states.Clear();
        _states.AddRange(sortedStates);
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
        var sortedStates = _states.OrderBy(x => x, comparer).ToArray();
        _states.Clear();
        _states.AddRange(sortedStates);
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
            var added = other._states.Count;
            if (added == 0)
                return 0;

            _states.EnsureCapacity(_states.Count + added);
            Metadata.States.EnsureCapacity(Metadata.States.Count + added);
            foreach (var state in other._states)
            {
                _states.Add(state);
                Metadata.States.Add(state.Data);
            }

            if (MetadataMatchesStates(other))
            {
                other.Metadata.States.Clear();
            }
            else
            {
                // Preserve RemoveState's behavior if callers have directly altered public metadata.
                RemoveTransferredMetadata(other);
            }

            other._states.Clear();
            return added;
        }

        return 0;
    }

    private static bool MetadataMatchesStates(DMIFile file)
    {
        if (file.Metadata.States.Count != file._states.Count)
            return false;

        var comparer = EqualityComparer<StateMetadata>.Default;
        for (var i = 0; i < file._states.Count; i++)
        {
            if (!comparer.Equals(file.Metadata.States[i], file._states[i].Data))
                return false;
        }

        return true;
    }

    private static void RemoveTransferredMetadata(DMIFile file)
    {
        var removals = new Dictionary<StateMetadata, int>();
        foreach (var state in file._states)
        {
            removals.TryGetValue(state.Data, out var count);
            removals[state.Data] = count + 1;
        }

        var writeIndex = 0;
        for (var readIndex = 0; readIndex < file.Metadata.States.Count; readIndex++)
        {
            var metadata = file.Metadata.States[readIndex];
            if (removals.TryGetValue(metadata, out var count) && count != 0)
            {
                removals[metadata] = count - 1;
                continue;
            }

            file.Metadata.States[writeIndex++] = metadata;
        }

        if (writeIndex != file.Metadata.States.Count)
        {
            file.Metadata.States.RemoveRange(writeIndex, file.Metadata.States.Count - writeIndex);
        }
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

    private static PngBitDepth GetBitDepth(int colorCount)
    {
        return colorCount switch
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