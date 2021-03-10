using MetadataExtractor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DMISharp.Metadata
{
    /// <summary>
    /// Represents the header data of a DMI file
    /// </summary>
    public class DMIMetadata
    {
        public double Version { get; private set; } // BYOND version
        public int FrameWidth { get; internal set; }
        public int FrameHeight { get; internal set; }
        public List<StateMetadata> States { get; }
        private static readonly Regex _DMIStart = new Regex(@"#\s{0,1}BEGIN DMI", RegexOptions.Compiled);

        public DMIMetadata(double byondVersion, int frameWidth, int frameHeight)
        {
            Version = byondVersion;
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            States = new List<StateMetadata>();
        }

        /// <summary>
        /// Instantiates a DMIMetadata object from a file stream of that DMI file
        /// </summary>
        /// <param name="stream"></param>
        public DMIMetadata(Stream stream)
        {
            // Get each line of the DMI metadata
            var data = GetDMIData(ImageMetadataReader.ReadMetadata(stream));

            // Get header data
            var headerData = data.Take(data.TakeWhile(x => !x.StartsWith("state", StringComparison.InvariantCulture)).Count());
            var bodyData = data.Skip(headerData.Count());

            // Get version
            Version = GetFileVersion(headerData.ToList());

            // Get possible frame dimensions, if available
            GetFrameDimensions(headerData.ToList());

            // Get state metadata from body
            States = GetStateMetadata(bodyData.ToList());
        }

        /// <summary>
        /// Gets a collection of DMI metadata directories and breaks it into individual lines of DMI metadata
        /// </summary>
        /// <param name="directories">The metadata directories to search</param>
        /// <returns>An array of strings representing the lines of the DMI data</returns>
        private static string[] GetDMIData(IEnumerable<MetadataExtractor.Directory> directories)
        {
            var metaDesc = directories
                .SelectMany(x => x.Tags)
                .Select(x => x.Description)
                .FirstOrDefault(x => _DMIStart.IsMatch(x));
            
            if (metaDesc == null)
            {
                throw new Exception("Failed to find BYOND DMI metadata in PNG text data!");
            }


#if NETSTANDARD || NET461 || NET472
            metaDesc = metaDesc.Substring(metaDesc.IndexOf('#'));
#else
            metaDesc = metaDesc[metaDesc.IndexOf('#')..];
#endif
            return metaDesc.Split(new char[] { '\n', '\r' });
        }

        /// <summary>
        /// Attempts to find the BYOND DMI file version from header metadata.
        /// </summary>
        /// <param name="headerData">The header metadata from a DMI file.</param>
        /// <returns>The version of DMI file if found, otherwise 0.</returns>
        private static double GetFileVersion(List<string> headerData)
        {
            var possibleLines = headerData.Where(x => x.StartsWith("version", StringComparison.InvariantCulture));

            switch (possibleLines.Count())
            {
                case 0:
                    return 0.0;
                case 1:
                    try
                    {
                        return double.Parse(possibleLines.First().Split(new char[] { '=', ' ' }).Last(), System.Globalization.CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to parse version number from line:\n{possibleLines.First()}", e);
                    }
                default:
                    throw new ArgumentException("Found more than one version number line, possibly corrupt file");
            }
        }

        /// <summary>
        /// Attempts to get the frame dimensions of a DMI file from the header metadata.
        /// </summary>
        /// <param name="headerData">The header metadata from a DMI file.</param>
        /// <returns>True if successful, false if failed</returns>
        private bool GetFrameDimensions(List<string> headerData)
        {
            // Get width
            var widthLines = headerData.Where(x => x.StartsWith("\twidth", StringComparison.InvariantCulture));
            if (widthLines.Any())
            {
                try
                {
                    FrameWidth = int.Parse(widthLines.First().Split(new char[] { '=', ' ' }).Last(), System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (FormatException e)
                {
                    throw new FormatException($"Failed to parse width value from line\n{widthLines.First()}", e);
                }
            }
            else
            {
                FrameWidth = -1;
            }

            var heightLines = headerData.Where(x => x.StartsWith("\theight", StringComparison.InvariantCulture));
            if (heightLines.Any())
            {
                try
                {
                    FrameHeight = int.Parse(heightLines.First().Split(new char[] { '=', ' ' }).Last(), System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (FormatException e)
                {
                    throw new FormatException($"Failed to parse width value from line\n{heightLines.First()}", e);
                }
            }
            else
            {
                FrameHeight = -1;
            }

            return FrameWidth != -1 && FrameHeight != -1;
        }

        /// <summary>
        /// Creates a collection of state metadata from the body of a BYOND DMI metadata set.
        /// </summary>
        /// <param name="bodyData">The body metadata of the DMI file.</param>
        /// <returns>A collection of StateMetadata objects representing each state in the file.</returns>
        private static List<StateMetadata> GetStateMetadata(List<string> bodyData)
        {
            var toReturn = new List<StateMetadata>();
            var bodyLength = bodyData.Count;

            for (int line = 0; line < bodyLength; line++)
            {
                // Iterate through each state
                if (bodyData[line].StartsWith("state", StringComparison.InvariantCulture))
                {
                    var test = bodyData.Skip(line).TakeWhile(x => !x.StartsWith("state", StringComparison.InvariantCulture)).ToList();

                    var toParse = new List<string>() { bodyData[line] };
                    toParse.AddRange(bodyData.Skip(line + 1).TakeWhile(x => !x.StartsWith("state", StringComparison.InvariantCulture)));
                    toReturn.Add(new StateMetadata(toParse));
                }
            }

            return toReturn;
        }
    }
}
