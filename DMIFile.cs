using System;
using System.Collections.Generic;
using MetadataExtractor;
using SixLabors.ImageSharp;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DMISharp
{
    /// <summary>
    /// Provides a means to interact with BYOND DMI files.
    /// </summary>
    public class DMIFile : IDisposable
    {
        public float Version { get; private set; } // BYOND DMI version
        public int FrameWidth { get; private set; }
        public int FrameHeight { get; private set; }
        public IEnumerable<DMIState> States { get; private set; }
        private Regex statePattern = new Regex("^state = \"(?<label>.+)\"$");
        private Regex stateSubKeysPattern = new Regex("^\t(?<key>.+) = (?<val>.+)$");

        /// <summary>
        /// Initializes a new instance of a DMI File.
        /// </summary>
        /// <param name="stream">The Stream containing the DMI file data.</param>
        public DMIFile(Stream stream)
        {
            // As DMI data is an embedded metadata tag, we must handle the image metadata.
            var metadata = ImageMetadataReader.ReadMetadata(stream);
            var dmiData = GetDMIData(metadata);

            // Parse data necessary for instancing from the retrieved metadata.
            try
            {
                Version = float.Parse(dmiData[1].Split(new char[] { '=', ' ' }).Last());
                FrameWidth = int.Parse(dmiData[2].Split(new char[] { '=', ' ' }).Last());
                FrameHeight = int.Parse(dmiData[3].Split(new char[] { '=', ' ' }).Last());
            }
            catch (FormatException e)
            {
                throw new FormatException("Found metadata for DMI file, but failed to successfully parse version and frame dimensions.", e);
            }

            // Reset stream position for processing image data.
            stream.Seek(0, SeekOrigin.Begin);
            States = GetStates(dmiData, stream);

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
        /// Retrieves DMI metadata from a set of metadata directories.
        /// </summary>
        /// <param name="directories">The directories of metadata tags from a DMI file.</param>
        /// <returns>An array of strings representing each line of the DMI metadata entry.</returns>
        private string[] GetDMIData(IEnumerable<MetadataExtractor.Directory> directories)
        {
            MetadataExtractor.Directory cursor = null;

            foreach (var directory in directories)
            {
                if (directory.Name == "PNG-zTXt")
                {
                    cursor = directory;
                }
            }

            if (cursor == null || cursor.Tags == null || !cursor.Tags.Any())
            {
                return null;
            }

            var lines = cursor.Tags.First().Description.Split(new char[] { '\n', '\r' });

            if (lines.Any())
            {
                lines[0] = lines[0].Replace("Description: ", "");
            }

            return lines;
        }

        /// <summary>
        /// Processes DMI metadata into DMI State objects.
        /// </summary>
        /// <param name="data">The array of strings representing lines of a DMI metadata tag.</param>
        /// <param name="source">The stream containing the DMI file data.</param>
        /// <returns>An enumerable collection of DMI State objects representing the states of the DMI File.</returns>
        private IEnumerable<DMIState> GetStates(string[] data, Stream source)
        {
            var states = new List<DMIState>();

            using (var img = Image.Load(source))
            {
                int wFrames = (int)img.Width / FrameWidth;
                int hFrames = (int)img.Height / FrameHeight;
                int processedImages = 0;
                int currWIndex = 0;
                int currHIndex = 0;
                for (int i = 4; i < data.Length; i++)
                {
                    if (statePattern.IsMatch(data[i]))
                    {
                        var name = statePattern.Match(data[i]).Groups["label"].Value;
                        var stateData = new Dictionary<string, string>();

                        while (stateSubKeysPattern.IsMatch(data[i + 1]))
                        {
                            i++;
                            var result = stateSubKeysPattern.Match(data[i]);
                            stateData.Add(result.Groups["key"].Value, result.Groups["val"].Value);
                        }

                        var toAdd = new DMIState(name, img, currWIndex, wFrames, currHIndex, hFrames, FrameWidth, FrameHeight, stateData);
                        processedImages += toAdd.Images.Length;
                        currHIndex = processedImages / wFrames;
                        currWIndex = processedImages % wFrames;
                        states.Add(toAdd);
                    }
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
