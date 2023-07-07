using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using DMISharp.Metadata;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace DMISharp;

/// <summary>
/// Representative of the BYOND state directions
/// </summary>
public enum StateDirection
{
    South,
    North,
    East,
    West,
    SouthEast,
    SouthWest,
    NorthEast,
    NorthWest
}

/// <summary>
/// Representative of the depth of directions available to a state.
/// One = S
/// Four = S, N, E, W
/// Eight = S, N, E, W, SE, SW, NE, NW
/// </summary>
public enum DirectionDepth
{
    One = 1,
    Four = 4,
    Eight = 8
}

/// <summary>
/// Represents an icon state in a BYOND DMI file.
/// Allows for interacting with each frame of the state.
/// </summary>
public sealed class DMIState : IDisposable
{
    private Image<Rgba32>[][] _images; // Stores each frame image following the [direction][frame] pattern.

    /// <summary>
    /// Initializes a blank DMI state.
    /// </summary>
    /// <param name="name">The name of the state</param>
    /// <param name="directionDepth">The number of directions the state will have frames for</param>
    /// <param name="frames">The number of frames each direction will have</param>
    /// <param name="frameWidth">The width of each frame in pixels</param>
    /// <param name="frameHeight">The height of each frame in pixels</param>
    public DMIState(string name, DirectionDepth directionDepth, int frames, int frameWidth, int frameHeight)
    {
        Data = new StateMetadata(name, directionDepth, frames);
        DirectionDepth = directionDepth;
        _images = new Image<Rgba32>[(int)directionDepth][];
        Width = frameWidth;
        Height = frameHeight;

        for (var dir = 0; dir < Dirs; dir++)
        {
            _images[dir] = new Image<Rgba32>[Frames];
        }
    }

    /// <summary>
    /// Initializes a new instance of a DMI State.
    /// </summary>
    /// <param name="state">The metadata for the state</param>
    /// <param name="source">The image that contains the state.</param>
    /// <param name="currWIndex">The current positional index of frames in the x direction.</param>
    /// <param name="wIndex">The maximum index of the x direction.</param>
    /// <param name="currHIndex">The current positional index of frames in the y direction.</param>
    /// <param name="hIndex">The maximum index of the y direction.</param>
    /// <param name="width">The width of an individual frame.</param>
    /// <param name="height">The height of an individual frame.</param>
    public DMIState(StateMetadata state, Image<Rgba32> source, int currWIndex, int wIndex, int currHIndex,
        int hIndex, int width, int height)
    {
        Data = state ?? throw new ArgumentNullException(nameof(state),
            "The provided state metadata cannot be null when instantiating a DMIState");
        Height = height;
        Width = width;

        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        // Develop frames
        _images = SeperateImages(source, currWIndex, wIndex, currHIndex, hIndex, width, height);

        // Set directionality
        DirectionDepth = (DirectionDepth)_images.Length;
    }

    public string Name
    {
        get => Data.State;
        set => Data.State = value;
    }

    public int Dirs => Data.Dirs;
    public int Frames => Data.Frames;
    public int Height { get; }
    public int Width { get; }
    public int TotalFrames => _images.Sum(x => x.Count(y => y != null));
    public int FrameCapacity => _images.Sum(x => x.Length);
    public DirectionDepth DirectionDepth { get; private set; }
    public StateMetadata Data { get; } // Stores key, value pairs from DMI file metadata.

    /// <summary>
    /// Ensure when the DMI State is disposed of that all images are properly disposed of.
    /// </summary>
    public void Dispose()
    {
        foreach (var image in _images.SelectMany(x => x).Where(x => x != null))
        {
            image.Dispose();
        }
    }

    /// <summary>
    /// Separates individual frames from a given source image.
    /// </summary>
    /// <param name="source">The image that contains the frames.</param>
    /// <param name="currWIndex">The current positional index of frames in the x direction.</param>
    /// <param name="wIndex">The maximum index of the x direction.</param>
    /// <param name="currHIndex">The current positional index of frames in the y direction.</param>
    /// <param name="hIndex">The maximum index of the y direction.</param>
    /// <param name="width">The width of an individual frame.</param>
    /// <param name="height">The height of an individual frame.</param>
    /// <returns>A 2-D Image array containing frames in the [direction, format] pattern.</returns>
    private Image<Rgba32>[][] SeperateImages(Image<Rgba32> source, int currWIndex, int wIndex, int currHIndex,
        int hIndex, int width, int height)
    {
        var images = new Image<Rgba32>[Dirs][];
        int currDir = 0, currFrame = 0;

        // Create dirs
        for (var dir = 0; dir < Dirs; dir++)
        {
            images[dir] = new Image<Rgba32>[Frames];
        }

        // Iterate through each row of frames.
        for (; currHIndex < hIndex; currHIndex++)
        {
            // Iterate through each column of frames.
            for (; currWIndex < wIndex; currWIndex++)
            {
                // Catch end of sprite
                if (currDir == Dirs)
                {
                    if (currFrame == Frames - 1)
                    {
                        return images;
                    }
                    else
                    {
                        currDir = 0;
                        currFrame++;
                    }
                }

                // Copy frame pixels from source image
                var frame = new Image<Rgba32>(width, height);
                var xOffset = currWIndex * width;
                var yOffset = currHIndex * height;

                source.ProcessPixelRows(frame, (sourceAccessor, frameAccessor) =>
                {
                    for (var ypx = 0; ypx < height; ypx++)
                    {
                        var sourceSpan = sourceAccessor.GetRowSpan(ypx + yOffset);
                        var frameSpan = frameAccessor.GetRowSpan(ypx);
                        for (var xpx = 0; xpx < width; xpx++)
                        {
                            frameSpan[xpx] = sourceSpan[xpx + xOffset];
                        }
                    }
                });

                images[currDir][currFrame] = frame;
                currDir++;
            }

            currWIndex = 0;
        }

        return images;
    }

    /// <summary>
    /// Determines if a DMI State has any animations based on the existence of the delay property.
    /// </summary>
    /// <returns>A boolean value representing if this state has animations.</returns>
    public bool IsAnimated() => Data.Delay != null;

    /// <summary>
    /// Produces animated images for a DMI State.
    /// </summary>
    /// <returns>An array of Image objects containing the directional animations for the state.</returns>
    public Image<Rgba32>[] GetAnimated()
    {
        if (!IsAnimated())
        {
            throw new InvalidOperationException("Invalid operation, this state is not animated.");
        }

        var toReturn = new Image<Rgba32>[Dirs];

        // Iterate through each direction and create an animation.
        for (var dir = 0; dir < Dirs; dir++)
        {
            toReturn[dir] = GetAnimated((StateDirection)dir);
        }

        return toReturn;
    }

    /// <summary>
    /// Gets the animated image for a requested direction.
    /// </summary>
    /// <param name="direction">The state direction to request</param>
    /// <returns>The requested image</returns>
    public Image<Rgba32> GetAnimated(StateDirection direction)
    {
        if (!IsAnimated())
        {
            throw new InvalidOperationException("Invalid operation, this state is not animated.");
        }

        if ((int)direction >= (int)DirectionDepth)
        {
            throw new InvalidOperationException(
                "Invalid operation, requested a direction that does not exist on this state.");
        }

        // Develop gif
        var toReturn = new Image<Rgba32>(Width, Height);
        for (var frame = 0; frame < Frames; frame++)
        {
            var cursor = _images[(int)direction][frame];
            var metadata = cursor.Frames.RootFrame.Metadata.GetFormatMetadata(GifFormat.Instance);
            metadata.FrameDelay = (int)(Data.Delay[frame] * 10.0); // GIF frames are 10ms compared to 100ms tick
            metadata.DisposalMethod = GifDisposalMethod.RestoreToBackground; // Ensures transparent pixels 
            toReturn.Frames.InsertFrame(frame, cursor.Frames.RootFrame);
        }

        // Final process
        var gifMetadata = toReturn.Metadata.GetFormatMetadata(GifFormat.Instance);
        gifMetadata.ColorTableMode = GifColorTableMode.Local;
        toReturn.Frames.RemoveFrame(toReturn.Frames.Count - 1); // Remove empty frame at end of the animation
        toReturn.Mutate(x =>
            x.BackgroundColor(
                new Rgba32())); // Specify the animation has a transparent background for transparent pixels
        gifMetadata.RepeatCount = (ushort)Data.Loop;

        return toReturn;
    }

    /// <summary>
    /// Saves an animated gif to a provided stream.
    /// </summary>
    /// <param name="stream">The stream to write data to.</param>
    /// <param name="direction">The direction of the state to retrieve the gif for.</param>
    /// <param name="encoder">The GifEncoder to use, if null a default is generated. Only override if required.</param>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public void SaveAnimatedGIF(Stream stream, StateDirection direction, GifEncoder encoder = null)
    {
        if (!IsAnimated())
        {
            throw new InvalidOperationException("Invalid operation, this state is not animated.");
        }

        encoder ??= new GifEncoder()
        {
            Quantizer = new OctreeQuantizer(new QuantizerOptions()
                { Dither = null }) // Disable dithering as this generally negatively impacts pixelart animations.
        };

        using var img = GetAnimated(direction);
        img.SaveAsGif(stream, encoder);
    }

    /// <summary>
    /// Retrieves a frame from a state
    /// </summary>
    /// <param name="direction">The direction of the frame</param>
    /// <param name="frame">The frame index</param>
    /// <returns>An ImageSharp Image representing the frame</returns>
    public Image<Rgba32> GetFrame(StateDirection direction, int frame) => _images[(int)direction][frame];

    /// <summary>
    /// Helper for frame retrieval, defaults to the North (1) direction
    /// </summary>
    /// <param name="frame">The frame index</param>
    /// <returns>An ImageSharp Image representing the frame</returns>
    public Image<Rgba32> GetFrame(int frame) => GetFrame(StateDirection.South, frame);

    /// <summary>
    /// Sets the content of a frame in a state.
    /// </summary>
    /// <param name="newFrame">The ImageSharp image to replace the frame with</param>
    /// <param name="direction">The direction to replace the frame on</param>
    /// <param name="frame">The frame index</param>
    public void SetFrame(Image<Rgba32> newFrame, StateDirection direction, int frame)
    {
        if (newFrame != null && (newFrame.Width != Width || newFrame.Height != Height))
        {
            throw new ArgumentException(
                "Cannot insert a frame that is different than the size of the state's frame size.");
        }

        // Delete old frame if necessary
        var cursor = _images[(int)direction][frame];
        if (cursor != null && cursor != newFrame)
        {
            cursor.Dispose();
        }

        _images[(int)direction][frame] = newFrame;
    }

    /// <summary>
    /// Helper for setting the content of a frame in a state. Defaults to the North (1) direction.
    /// </summary>
    /// <param name="newFrame">The ImageSharp image to replace the frame with</param>
    /// <param name="frame">The frame index</param>
    public void SetFrame(Image<Rgba32> newFrame, int frame)
    {
        SetFrame(newFrame, StateDirection.South, frame);
    }

    /// <summary>
    /// Deletes a frame from a state
    /// </summary>
    /// <param name="direction">The direction to delete the frame on</param>
    /// <param name="frame">The frame index</param>
    public void DeleteFrame(StateDirection direction, int frame)
    {
        _images[(int)direction][frame] = null;
    }

    /// <summary>
    /// Helper for deleting a frame from a state. Defaults to the North (1) direction.
    /// </summary>
    /// <param name="frame">The frame index</param>
    public void DeleteFrame(int frame)
    {
        DeleteFrame(StateDirection.South, frame);
    }

    /// <summary>
    /// Sets the directional depth of a DMI State. Will delete frames when downsizing.
    /// </summary>
    /// <param name="depth">The new directional depth for the state</param>
    public void SetDirectionDepth(DirectionDepth depth)
    {
        if (depth == DirectionDepth)
            return;

        var minDepth = Math.Min((int)depth, (int)DirectionDepth);
        var temp = new Image<Rgba32>[(int)depth][];
        for (var i = 0; i < minDepth; i++)
        {
            temp[i] = _images[i];
        }

        // Dispose of images outside of our new dirs
        if (depth < DirectionDepth)
        {
            for (var i = (int)depth; i < (int)DirectionDepth; i++)
            {
                for (var j = 0; j < Frames; j++)
                {
                    var cursor = _images[i][j];
                    cursor.Dispose();
                }
            }
        }

        // Insert empty arrays for extra new dirs if available
        if (depth > DirectionDepth)
        {
            for (var i = (int)DirectionDepth; i < (int)depth; i++)
            {
                temp[i] = new Image<Rgba32>[Frames];
            }
        }

        _images = temp;
        DirectionDepth = depth;
        Data.Dirs = (int)depth;
    }

    /// <summary>
    /// Sets the frame depth of a DMI state. Will delete frames when downsizing.
    /// </summary>
    /// <param name="frames">The new frame depth of the state</param>
    public void SetFrameCount(int frames)
    {
        if (Frames == frames)
            return;

        var temp = new Image<Rgba32>[Dirs][];
        var minFrames = Math.Min(Frames, frames);

        for (var dir = 0; dir < Dirs; dir++)
        {
            temp[dir] = new Image<Rgba32>[frames];
            for (var i = 0; i < minFrames; i++)
            {
                temp[dir][i] = _images[dir][i];
            }

            // Dispose of frames that we are no longer tracking ("lost" frames)
            if (frames >= Frames)
                continue;

            for (var i = minFrames; i < Frames; i++)
            {
                _images[dir][i].Dispose();
            }
        }

        _images = temp;
        Data.Frames = frames;
    }

    /// <summary>
    /// Determines if a state is valid to be saved.
    /// </summary>
    /// <returns>True if the state can be saved into DMI format, false otherwise</returns>
    public bool IsReadyForSave()
    {
        // check all directions are populated and equal
        if (TotalFrames != FrameCapacity) return false;

        // check all required directions are present
        if (_images.Length != (int)DirectionDepth) return false;

        // check that animations delays are correct lengths
        if (Data.Delay != null && Data.Delay.Length != Data.Frames) return false;

        return true;
    }

    #region Animation Attributes

    /// <summary>
    /// Initializes the delay array for a state, 'creates' an animated state.
    /// </summary>
    public void InitializeDelay()
    {
        if (Data.Delay == null)
        {
            Data.Delay = new double[Frames];

            for (var frame = 0; frame < Frames; frame++)
            {
                Data.Delay[frame] = 1.0;
            }
        }
        else
        {
            throw new InvalidOperationException("This state is already initialized for animations.");
        }
    }

    /// <summary>
    /// Destroys the delay array for a state, 'destroys' an animated state.
    /// </summary>
    public void ClearDelay()
    {
        if (Data.Delay != null)
        {
            Data.Delay = null;
        }
        else
        {
            throw new InvalidOperationException("This state has no initialized delay for animations.");
        }
    }

    /// <summary>
    /// Sets the delay for a frame with a provided array between a pair of indices.
    /// </summary>
    /// <param name="delay">The array of delay values to set</param>
    /// <param name="startIndex">The zero-based starting index of the desired frame</param>
    /// <param name="endIndex">The zero-based ending index of the desired frame, defaults to the end of the delay index.</param>
    /// <remarks>This will initialize the animated state if required.</remarks>
    public void SetDelay(double[] delay, int startIndex = 0, int endIndex = -1)
    {
        if (endIndex == -1) endIndex = Frames - 1;

        // If delay is null, at this point we can initialize it
        if (Data.Delay == null)
        {
            InitializeDelay();
        }

        if (delay is null)
        {
            throw new ArgumentNullException(nameof(delay));
        }

        // Catch invalid array sizes
        if (delay.Length > Data.Delay!.Length - startIndex)
        {
            throw new ArgumentException("Provided array of delays is longer than the state's number of frames.");
        }

        if (delay.Length == 0)
        {
            throw new ArgumentException("Provided array of delays is empty.");
        }

        if (startIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex), "Starting index cannot be negative");
        }

        if (startIndex > endIndex)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex),
                "Starting index cannot be greater than ending index.");
        }

        for (var frame = startIndex; frame <= endIndex; frame++)
        {
            Data.Delay[frame] = delay[frame];
        }
    }

    /// <summary>
    /// Sets the delay for an individual frame
    /// </summary>
    /// <param name="frame">The zero-based frame index to set delay for</param>
    /// <param name="delay">The delay value</param>
    /// <remarks>This will initialize the animated state if required.</remarks>
    public void SetDelay(int frame, double delay)
    {
        // If delay is null, at this point we can initialize it
        if (Data.Delay == null)
        {
            InitializeDelay();
        }

        // Catch invalid array sizes
        if (frame >= Frames)
        {
            throw new ArgumentException("Provided frame index is greater than the number of frames in this state.");
        }

        if (frame < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frame), "Frame index cannot be negative");
        }

        Data.Delay![frame] = delay;
    }

    /// <summary>
    /// Sets the movement attribute on this DMI state
    /// </summary>
    /// <param name="value">The desired value</param>
    public void SetMovement(bool value)
    {
        if (value == Data.Movement) return;
        if (value && Data.Delay == null)
        {
            throw new InvalidOperationException(
                "This state's delay is uninitialized, ensure this is an animated state before setting movement.");
        }

        Data.Movement = value;
    }

    /// <summary>
    /// Sets the number of times to loop the animation on this DMI state
    /// </summary>
    /// <param name="value">The number of times to loop this animation</param>
    /// <remarks>A value of zero will loop infinitely</remarks>
    public void SetLoop(int value)
    {
        if (value == Data.Loop) return;
        if (Data.Delay == null)
        {
            throw new InvalidOperationException(
                "This state's delay is uninitialized, ensure this is an animated state before setting loop.");
        }

        Data.Loop = value;
    }

    /// <summary>
    /// Sets the rewind attribute on this DMI state
    /// </summary>
    /// <param name="value">The desired value</param>
    public void SetRewind(bool value)
    {
        if (value == Data.Rewind) return;
        if (Data.Delay == null)
        {
            throw new InvalidOperationException(
                "This state's delay is uninitialized, ensure this is an animated state before setting rewind.");
        }

        Data.Rewind = value;
    }

    #endregion
}