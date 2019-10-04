﻿using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using System.IO;
using System.Linq;
using DMISharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using SixLabors.ImageSharp.Formats.Png;
using System.Text;

namespace DMISharp
{
    /// <summary>
    /// Provides a means to interact with BYOND DMI files.
    /// </summary>
    public class DMIFile : IDisposable
    {
        public DMIMetadata Metadata { get; private set; }
        private List<DMIState> _States { get; set; }
        public IReadOnlyCollection<DMIState> States { get { return _States.AsReadOnly(); } }
        
        public DMIFile(int frameWidth, int frameHeight)
        {
            Metadata = new DMIMetadata(4.0, frameWidth, frameHeight);
            _States = new List<DMIState>();
        }

        /// <summary>
        /// Initializes a new instance of a DMI File.
        /// </summary>
        /// <param name="stream">The Stream containing the DMI file data.</param>
        public DMIFile(Stream stream)
        {
            // As the metadata is embedded in the PNG file, extract into a usable object.
            Metadata = new DMIMetadata(stream);

            // Reset stream position for processing image data.
            stream.Seek(0, SeekOrigin.Begin);
            _States = GetStates(stream).ToList();

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
        /// Saves a DMI File to a stream. The resulting file is .dmi-ready
        /// </summary>
        /// <param name="stream">The stream to save the DMI File to.</param>
        /// <returns>True if the file was saved, false otherwise</returns>
        public bool Save(Stream stream)
        {
            if (!CanSave()) return false;

            // prepare frames
            var frames = new List<Image>();
            foreach (var state in States)
            {
                for (int i = 0; i < state.Dirs; i++)
                {
                    for (int j = 0; j < state.Frames; j++)
                    {
                        frames.Add(state.GetFrame((StateDirection)i, j));
                    }
                }
            }
            var numFrames = frames.Count;

            // Get dimensions in frames
            var xFrames = Math.Max(1, (int)Math.Sqrt(numFrames));
            var yFrames = Math.Max(1, (int)Math.Ceiling(numFrames * 1.0 / xFrames));

            using (var img = new Image<Rgba32>(xFrames * Metadata.FrameWidth, yFrames * Metadata.FrameHeight))
            {
                for (int y = 0, i = 0; y < yFrames && i < numFrames; y++)
                {
                    for (int x = 0; x < xFrames && i < numFrames; x++, i++) 
                    {
                        var targetFrame = frames[i];
                        var targetPoint = new Point(x * Metadata.FrameWidth, y * Metadata.FrameHeight);
                        img.Mutate(ctx => ctx.DrawImage(targetFrame, targetPoint, PixelColorBlendingMode.Normal, 1));
                    }
                }

                PngMetadata md = img.Metadata.GetFormatMetadata(PngFormat.Instance);
                md.TextData.Add(new PngTextData("Description", GetTextChunk(), string.Empty, string.Empty));

                img.SaveAsPng(stream);
            }

            return true;
        }

        /// <summary>
        /// Saves a DMI File to a specific file path.
        /// </summary>
        /// <param name="path">The path to save the image to.</param>
        /// <returns>True if the file was saved, false otherwise</returns>
        public bool Save(string path)
        {
            using (var fs = File.OpenWrite(path))
            {
                return Save(fs);
            }
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
        /// Develops BYOND txt header for DMI files
        /// </summary>
        /// <returns>The BYOND txt header for this DMI file</returns>
        private string GetTextChunk()
        {
            var builder = new StringBuilder();
            builder.Append($"# BEGIN DMI\nversion = {Metadata.Version : 0.0}\n\twidth = {Metadata.FrameWidth}\n\theight = {Metadata.FrameHeight}\n");

            foreach (var state in States)
            {
                builder.Append($"state = \"{state.Name}\"\n\tdirs = {state.Dirs}\n\tframes = {state.Frames}\n");
                if (state.Data.Delay != null) builder.Append($"\tdelay = {string.Join(",", state.Data.Delay)}\n");
                if (state.Data.Loop) builder.Append($"\tloop = 1\n");
                if (state.Data.Hotspots != null) builder.Append($"\thotspots = {string.Join(",", state.Data.Hotspots)}\n");
                if (state.Data.Movement) builder.Append("\tmovement = 1\n");
                if (state.Data.Rewind) builder.Append("\trewind = 1\n");
            }

            builder.Append("# END DMI");
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

            using (var img = Image.Load<Rgba32>(source))
            {              
                // DMI data did not include widths or heights, assume that it is then
                // perfect squares, thus we will determine the w/h programatically...
                if (Metadata.FrameWidth == -1 || Metadata.FrameHeight == -1)
                {
                    var totalFrames = Metadata.States.Sum(x => x.Frames * x.Dirs);

                    for (int rows = 1; totalFrames >= rows; rows++)
                    {
                        if (img.Width / (totalFrames / rows) == img.Height / rows)
                        {
                            Metadata.FrameHeight = img.Height / rows;
                            Metadata.FrameWidth = img.Width / (totalFrames / rows);
                        }
                    }
                }

                if (Metadata.FrameHeight == 0 || Metadata.FrameWidth == 0)
                {
                    return states;
                }

                int wFrames = img.Width / Metadata.FrameWidth;
                int hFrames = img.Height / Metadata.FrameHeight;
                int processedImages = 0;
                int currWIndex = 0;
                int currHIndex = 0;
                
                foreach (var state in Metadata.States)
                {
                    var toAdd = new DMIState(state, img, currWIndex, wFrames, currHIndex, hFrames, Metadata.FrameWidth, Metadata.FrameHeight);
                    processedImages += toAdd.TotalFrames;
                    currHIndex = processedImages / wFrames;
                    currWIndex = processedImages % wFrames;
                    states.Add(toAdd);
                }
            }

            return states;
        }

        /// <summary>
        /// Sorts the states of this DMI File alphabetically by their state name.
        /// </summary>
        public void SortStates()
        {
            _States = _States.OrderBy(x => x.Name).ToList();
            Metadata.States = _States.Select(x => x.Data).ToList();
        }

        /// <summary>
        /// Sorts the states of this DMI File using a provided comparer for DMIStates.
        /// </summary>
        /// <param name="comparer">The comparer to use</param>
        public void SortStates(IComparer<DMIState> comparer)
        {
            _States = _States.OrderBy(x => x, comparer).ToList();
            Metadata.States = _States.Select(x => x.Data).ToList();
        }

        /// <summary>
        /// Imports states from another DMI file.
        /// </summary>
        /// <param name="other">The DMI file to import states from</param>
        /// <returns>The number of states imported</returns>
        public int ImportStates(DMIFile other)
        {
            if (other != null 
                && other.States != null
                && other.Metadata != null 
                && Metadata != null
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
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Clears the states from a DMI file.
        /// </summary>
        public void ClearStates()
        {
            _States.Clear();
        }

        /// <summary>
        /// Removes a state from a DMI File
        /// </summary>
        /// <param name="toRemove">The DMIState to remove</param>
        /// <returns>True if the state was removed, otherwise false</returns>
        public bool RemoveState(DMIState toRemove)
        {
            if (toRemove != null && toRemove.Data != null && _States.Remove(toRemove))
            {
                return Metadata.States.Remove(toRemove.Data);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Adds a state to a DMI File
        /// </summary>
        /// <param name="toAdd">The DMIState to add</param>
        /// <returns>True if the state was added, otherwise false</returns>
        public bool AddState(DMIState toAdd)
        {
            if (StateValidForFile(toAdd) && toAdd?.Data != null)
            {
                _States.Add(toAdd);
                Metadata.States.Add(toAdd.Data);
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Ensures that a state is valid for a DMI File's existing dimensions
        /// </summary>
        /// <param name="toCheck">The DMIState to check against the file</param>
        /// <returns>True if the state is compatable with the file, false otherwise</returns>
        private bool StateValidForFile(DMIState toCheck)
        {
            return toCheck != null
                && toCheck.Height == Metadata.FrameHeight
                && toCheck.Width == Metadata.FrameWidth;
        }

        /// <summary>
        /// Ensure when the DMI File is disposed of that all DMI States and their respective images are disposed of.
        /// </summary>
        public void Dispose()
        {
            foreach (var state in States)
            {
                state.Dispose();
            }
        }
    }
}
