﻿using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using SixLabors.ImageSharp.Formats.Gif;

namespace DMISharp
{
    /// <summary>
    /// Represents an icon state in a BYOND DMI file.
    /// Allows for interacting with each frame of the state.
    /// </summary>
    public class DMIState : IDisposable
    {
        public string Name { get; private set; }
        public int Dirs { get; private set; }
        public int Frames { get; private set; }
        public Dictionary<string, string> Data { get; private set; } // Stores key, value pairs from DMI file metadata.
        public Image<Rgba32>[,] Images { get; private set; } // Stores each frame image following the [direction, frame] pattern.

        /// <summary>
        /// Initializes a new instance of a DMI State.
        /// </summary>
        /// <param name="name">The name of the state.</param>
        /// <param name="source">The image that contains the state.</param>
        /// <param name="currWIndex">The current positional index of frames in the x direction.</param>
        /// <param name="wIndex">The maximum index of the x direction.</param>
        /// <param name="currHIndex">The current positional index of frames in the y direction.</param>
        /// <param name="hIndex">The maximum index of the y direction.</param>
        /// <param name="width">The width of an individual frame.</param>
        /// <param name="height">The height of an individual frame.</param>
        /// <param name="data">Key, value pairs of metadata for the state.</param>
        public DMIState(string name, Image<Rgba32> source, int currWIndex, int wIndex, int currHIndex, int hIndex, int width, int height, Dictionary<string, string> data)
        {
            Name = name;
            Data = data;
            
            // Ensure metadata exists for creating the state.
            if (Data.ContainsKey("dirs") && Data.ContainsKey("frames"))
            {
                // Ensure validity of metadata
                try
                {
                    Dirs = int.Parse(Data["dirs"]);
                    Frames = int.Parse(Data["frames"]);
                }
                catch (FormatException e)
                {
                    throw new ArgumentException("Found dir and frame data, but the data cannot be cast in DMI State initialization.", e);
                }
            }
            else
            {
                throw new ArgumentException("Failed to find dir and frame data for DMI State initialization");
            }

            // Develop frames
            Images = SeperateImages(source, currWIndex, wIndex, currHIndex, hIndex, width, height);
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
        private Image<Rgba32>[,] SeperateImages(Image<Rgba32> source, int currWIndex, int wIndex, int currHIndex, int hIndex, int width, int height)
        {
            var images = new Image<Rgba32>[Dirs, Frames];
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

                    // Clone and select the frame from the source image.
                    var frame = source.Clone();
                    frame.Mutate(
                            img => img.Crop(new Rectangle(currWIndex * width, currHIndex * height, width, height)));
                    images[currDir, currFrame] = frame;

                    if (currFrame >= Frames - 1)
                    {
                        currFrame = 0;
                        currDir++;
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
            return Data.ContainsKey("delay");
        }

        /// <summary>
        /// Produces animated images for a DMI State.
        /// </summary>
        /// <returns>An array of Image objects containing the directional animations for the state.</returns>
        public Image<Rgba32>[] GetAnimated()
        {
            if (!IsAnimated()) return null;

            var toReturn = new Image<Rgba32>[Dirs];
            var delayStrings = Data["delay"].Split(','); // Delay is provided in a string of float vals separated by ,'s
            float[] delay = new float[delayStrings.Length];

            // Convert delay times into float values and ensure validity.
            for (int i = 0; i < delayStrings.Length; i++)
            {
                try
                {
                    delay[i] = float.Parse(delayStrings[i]);
                }
                catch (FormatException e)
                {
                    throw new FormatException("Found frame delay times when animating, but parsing into float values failed.", e);
                }
            }

            // Iterate through each direction and create an animation.
            for (int i = 0; i < Dirs; i++)
            {
                var toAdd = new Image<Rgba32>(Images[0, 0].Width, Images[0, 0].Height); // Create the new image to hold animation.

                // Iterate through each frame for the animated image.
                for (int j = 0; j < Frames; j++)
                {
                    using (var cpy = Images[i, j].Clone())
                    {
                        cpy.Frames.RootFrame.MetaData.GetFormatMetaData(GifFormat.Instance).FrameDelay = (int) (delay[j] * 10.0);
                        cpy.Frames.RootFrame.MetaData.GetFormatMetaData(GifFormat.Instance).DisposalMethod = GifDisposalMethod.RestoreToBackground; // Ensures transparent pixels behave
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
        /// Ensure when the DMI State is diposed of that all images are properly disposed of.
        /// </summary>
        public void Dispose()
        {
            foreach (var image in Images)
            {
                image.Dispose();
            }
        }
    }
}
