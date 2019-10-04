using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Gif;
using DMISharp.Metadata;
using System.Linq;

namespace DMISharp
{
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
    public class DMIState : IDisposable
    {
        public string Name { get { return Data.State; } set { Data.State = value; } }
        public int Dirs { get { return Data.Dirs; } }
        public int Frames { get { return Data.Frames; } }
        public int Height { get; private set; }
        public int Width { get; private set; }
        public int TotalFrames { get { return _Images.Sum(x => x.Count(y => y != null)); } }
        public int FrameCapacity { get { return _Images.Sum(x => x.Length); } }
        public DirectionDepth DirectionDepth { get; private set; }
        public StateMetadata Data { get; private set; } // Stores key, value pairs from DMI file metadata.
        private Image<Rgba32>[][] _Images { get; set; } // Stores each frame image following the [direction][frame] pattern.

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
            _Images = new Image<Rgba32>[(int)directionDepth][];
            Width = frameWidth;
            Height = frameHeight;

            for (int dir = 0; dir < Dirs; dir++)
            {
                _Images[dir] = new Image<Rgba32>[Frames];
            }
        }

        /// <summary>
        /// Initializes a new instance of a DMI State.
        /// </summary>
        /// <param name="source">The image that contains the state.</param>
        /// <param name="currWIndex">The current positional index of frames in the x direction.</param>
        /// <param name="wIndex">The maximum index of the x direction.</param>
        /// <param name="currHIndex">The current positional index of frames in the y direction.</param>
        /// <param name="hIndex">The maximum index of the y direction.</param>
        /// <param name="width">The width of an individual frame.</param>
        /// <param name="height">The height of an individual frame.</param>
        public DMIState(StateMetadata state, Image<Rgba32> source, int currWIndex, int wIndex, int currHIndex, int hIndex, int width, int height)
        {
            Data = state ?? throw new ArgumentNullException("The provided state metadata cannot be null when instantiating a DMIState");
            Height = height;
            Width = width;

            // Develop frames
            _Images = SeperateImages(source, currWIndex, wIndex, currHIndex, hIndex, width, height);

            // Set directionality
            DirectionDepth = (DirectionDepth)_Images.Length;
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
        private Image<Rgba32>[][] SeperateImages(Image<Rgba32> source, int currWIndex, int wIndex, int currHIndex, int hIndex, int width, int height)
        {
            var images = new Image<Rgba32>[Dirs][];
            images[0] = new Image<Rgba32>[Frames];
            int currDir = 0, currFrame = 0;

            // Iterate through each row of frames.
            for (; currHIndex < hIndex; currHIndex++)
            {
                // Iterate through each column of frames.
                for (; currWIndex < wIndex; currWIndex++)
                {
                    // Account for when there are empty frames on an image.
                    if (currDir >= Dirs)
                    {
                        return images;
                    }

                    // Copy frame pixels from source image
                    var frame = new Image<Rgba32>(width, height);
                    var xOffset = currWIndex * width;
                    var yOffset = currHIndex * height;
                    for(int xpx = 0; xpx < width; xpx++)
                    {
                        for (int ypx = 0; ypx < height; ypx++)
                        {
                            frame[xpx, ypx] = source[xpx + xOffset, ypx + yOffset];
                        }
                    }

                    images[currDir][currFrame] = frame;

                    if (currFrame >= Frames - 1)
                    {
                        currFrame = 0;
                        currDir++;
                        if (currDir < Dirs) images[currDir] = new Image<Rgba32>[Frames];
                    }
                    else
                    {
                        currFrame++;
                    }
                }

                currWIndex = 0;
            }

            return images;
        }

        /// <summary>
        /// Determines if a DMI State has any animations based on the existence of the delay property.
        /// </summary>
        /// <returns>A boolean value representing if this state has animations.</returns>
        public bool IsAnimated()
        {
            return Data.Delay != null;
        }

        /// <summary>
        /// Produces animated images for a DMI State.
        /// </summary>
        /// <returns>An array of Image objects containing the directional animations for the state.</returns>
        public Image<Rgba32>[] GetAnimated()
        {
            if (!IsAnimated()) return null;

            var toReturn = new Image<Rgba32>[Dirs];
            var delay = Data.Delay;

            // Iterate through each direction and create an animation.
            for (int i = 0; i < Dirs; i++)
            {
                var toAdd = new Image<Rgba32>(_Images[0][0].Width, _Images[0][0].Height); // Create the new image to hold animation.

                // Iterate through each frame for the animated image.
                for (int j = 0; j < Frames; j++)
                {
                    using (var cpy = _Images[i][j].Clone())
                    {
                        cpy.Frames.RootFrame.Metadata.GetFormatMetadata(GifFormat.Instance).FrameDelay = (int)(delay[j] * 10.0);
                        cpy.Frames.RootFrame.Metadata.GetFormatMetadata(GifFormat.Instance).DisposalMethod = GifDisposalMethod.RestoreToBackground; // Ensures transparent pixels behave
                        toAdd.Frames.InsertFrame(j, cpy.Frames.RootFrame);
                    }
                }

                toAdd.Frames.RemoveFrame(toAdd.Frames.Count - 1); // Remove empty frame at end of animation.
                toAdd.Mutate(x => x.BackgroundColor(Rgba32.Transparent)); // Specify the animation has a transparent background for transparent pixels.
                toReturn[i] = toAdd;
            }

            return toReturn;
        }

        /// <summary>
        /// Retrieves a frame from a state
        /// </summary>
        /// <param name="direction">The direction of the frame</param>
        /// <param name="frame">The frame index</param>
        /// <returns>An ImageSharp Image represesnting the frame</returns>
        public Image<Rgba32> GetFrame(StateDirection direction, int frame)
        {
            return _Images[(int)direction][frame];
        }

        /// <summary>
        /// Helper for frame retrieval, defaults to the North (1) direction
        /// </summary>
        /// <param name="frame">Thte frame index</param>
        /// <returns>An ImageSharp Image represesnting the frame</returns>
        public Image<Rgba32> GetFrame(int frame)
        {
            return GetFrame(StateDirection.South, frame);
        }

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
                throw new ArgumentException("Cannot insert a frame that is different than the size of the state's frame size.");
            }

            // Delete old frame if necessary
            var cursor = _Images[(int)direction][frame];
            if (cursor != null && cursor != newFrame)
            {
                cursor.Dispose();
            }
            
            _Images[(int)direction][frame] = newFrame;
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
            _Images[(int)direction][frame] = null;
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
            if (depth != DirectionDepth)
            {
                var temp = new Image<Rgba32>[(int)depth][];
                for (int i = 0; i < (int)depth; i++)
                {
                    temp[i] = _Images[i];
                }

                // Dispose of images outside of our new dirs
                if (depth < DirectionDepth)
                {
                    for (int i = (int)depth; i < (int)DirectionDepth; i++)
                    {
                        for (int j = 0; j < Frames; j++)
                        {
                            var cursor = _Images[i][j];
                            cursor.Dispose();
                        }
                    }
                }

                // Insert empty arrays for extra new dirs if available
                if (depth > DirectionDepth)
                {
                    for (int i = (int)DirectionDepth; i < (int)depth; i++) 
                    {
                        temp[i] = new Image<Rgba32>[Frames];
                    }
                }

                _Images = temp;
                DirectionDepth = depth;
                Data.Dirs = (int)depth;
            }
        }

        /// <summary>
        /// Sets the frame depth of a DMI state. Will delete frames when downsizing.
        /// </summary>
        /// <param name="frames">The new frame depth of the state</param>
        public void SetFrameCount(int frames)
        {
            if (Frames != frames)
            {
                var temp = new Image<Rgba32>[Dirs][];
                var minFrames = Math.Min(Frames, frames);

                for (int dir = 0; dir < Dirs; dir++)
                {
                    temp[dir] = new Image<Rgba32>[frames];
                    for (int i = 0; i < minFrames; i++)
                    {
                        temp[dir][i] = _Images[dir][i];
                    }

                    // Dispose of frames that we are no longer tracking ("lost" frames)
                    if (frames < Frames)
                    {
                        for (int i = minFrames; i < Frames; i++)
                        {
                            _Images[dir][i].Dispose();
                        }
                    }
                }

                _Images = temp;
                Data.Frames = frames;
            }
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
            if (_Images.Length != (int)DirectionDepth) return false;

            // check that animations delays are correct lengths
            if (Data.Delay != null && Data.Delay.Length != Data.Frames) return false;

            return true;
        }

        /// <summary>
        /// Ensure when the DMI State is diposed of that all images are properly disposed of.
        /// </summary>
        public void Dispose()
        {
            foreach (var image in _Images.SelectMany(x => x))
            {
                image.Dispose();
            }
        }
    }
}
