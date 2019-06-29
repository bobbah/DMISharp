using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DMISharp.Metadata;

namespace DMISharp
{
    /// <summary>
    /// Provides a means to interact with BYOND DMI files.
    /// </summary>
    public class DMIFile : IDisposable
    {
        public DMIMetadata Metadata { get; private set; }
        public IEnumerable<DMIState> States { get; private set; }
        
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
            States = GetStates(stream);

            stream.Dispose();
        }

        /// <summary>
        /// Constructor helper for DMI File object initialization.
        /// </summary>
        /// <param name="file">The path to the DMI file.</param>
        public DMIFile(string file)
            : this(File.Open(file, FileMode.Open))
        {

        }

        /// <summary>
        /// Processes DMI metadata into DMI State objects.
        /// </summary>
        /// <param name="data">The array of strings representing lines of a DMI metadata tag.</param>
        /// <param name="source">The stream containing the DMI file data.</param>
        /// <returns>An enumerable collection of DMI State objects representing the states of the DMI File.</returns>
        private IEnumerable<DMIState> GetStates(Stream source)
        {
            var states = new List<DMIState>();

            using (var img = Image.Load(source))
            {
                Metadata.Height = img.Height;
                Metadata.Width = img.Width;
                
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

                int wFrames = (int)img.Width / Metadata.FrameWidth;
                int hFrames = (int)img.Height / Metadata.FrameHeight;
                int processedImages = 0;
                int currWIndex = 0;
                int currHIndex = 0;
                
                foreach (var state in Metadata.States)
                {
                    var toAdd = new DMIState(state, img, currWIndex, wFrames, currHIndex, hFrames, Metadata.FrameWidth, Metadata.FrameHeight);
                    processedImages += toAdd.Images.Length;
                    currHIndex = processedImages / wFrames;
                    currWIndex = processedImages % wFrames;
                    states.Add(toAdd);
                }
            }

            return states;
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
