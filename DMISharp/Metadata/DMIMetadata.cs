using MetadataExtractor;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// <summary>
        /// The version of the DMI metadata, dictated by BYOND
        /// </summary>
        public double Version { get; private set; }
        
        /// <summary>
        /// The width of each frame in the DMI file, can be -1 indicating the frames are square.
        /// </summary>
        public int FrameWidth { get; internal set; }
        
        /// <summary>
        /// The height of each frame in the DMI file, can be -1 indicating the frames are square.
        /// </summary>
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
        /// <param name="stream">The data stream to read from</param>
        public DMIMetadata(Stream stream)
        {
            States = new List<StateMetadata>();
            FrameWidth = -1;
            FrameHeight = -1;
            
            // Get each line of the DMI metadata
            var data = GetDMIData(ImageMetadataReader.ReadMetadata(stream));
            ParseMetadata(data);
        }

        /// <summary>
        /// Gets a collection of DMI metadata directories and breaks it into individual lines of DMI metadata
        /// </summary>
        /// <param name="directories">The metadata directories to search</param>
        /// <returns>A ReadOnlySpan of the DMI's metadata if found</returns>
        private static ReadOnlySpan<char> GetDMIData(IEnumerable<MetadataExtractor.Directory> directories)
        {
            string metaDesc = null;
            foreach (var t in directories)
            {
                foreach (var tag in t.Tags)
                {
                    if (tag.Description != null && !_DMIStart.IsMatch(tag.Description)) 
                        continue;
                    
                    metaDesc = tag.Description;
                    break;
                }

                if (metaDesc != null)
                    break;
            }

            if (metaDesc == null)
            {
                throw new Exception("Failed to find BYOND DMI metadata in PNG text data!");
            }
            
            return metaDesc.AsSpan()[metaDesc.IndexOf('#')..];
        }

        /// <summary>
        /// Attempts to apply all data from the provided metadata to this DMIMetadata object
        /// </summary>
        /// <param name="data">The metadata string to parse</param>
        private void ParseMetadata(ReadOnlySpan<char> data)
        {
            StateMetadata currentState = null;
            var tokenizer = new DMITokenizer(data);
            
            // Parse header
            var hasBody = ParseHeader(ref tokenizer);
            if (Version == 0d)
                throw new Exception("Failed to parse required header data of DMI file, this file may be corrupt.");

            // Check for any additional data after the header
            if (!hasBody)
                return;
            
            do
            {
#if NETSTANDARD || NET472 || NET461
                // Handle any new states
                if (tokenizer.CurrentKey.Equals("state".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    if (currentState != null)
                        States.Add(currentState);

                    currentState = new StateMetadata()
                    {
                        State = tokenizer.CurrentValue.ToString()
                    };

                    continue;
                }
                
                // At this point if no state is present, then we have invalid data
                if (currentState == null)
                    throw new Exception("Started to read state data without a state, this file may be corrupt");
                
                // Handle value
                if (tokenizer.CurrentKey.Equals("dirs".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Dirs =
                            int.Parse(tokenizer.CurrentValue.ToString(), provider: CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to dirs from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'", e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("frames".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Frames =
                            int.Parse(tokenizer.CurrentValue.ToString(), provider: CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to parse number of frames from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'", e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("delay".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Delay = tokenizer.CurrentValue.ToString().Split(',')
                            .Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to parse delay from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'", e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("rewind".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Rewind =
                            int.Parse(tokenizer.CurrentValue.ToString(), provider: CultureInfo.InvariantCulture) == 1;
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to parse rewind flag from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'", e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("movement".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Movement =
                            int.Parse(tokenizer.CurrentValue.ToString(), provider: CultureInfo.InvariantCulture) == 1;
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to parse movement flag from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'", e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("loop".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Loop =
                            int.Parse(tokenizer.CurrentValue.ToString(), provider: CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to parse loop from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'", e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("hotspot".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Hotspots ??= new List<int[]>();
                        currentState.Hotspots.Add(tokenizer.CurrentValue.ToString().Split(',')
                            .Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray());
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to parse hotspot from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'", e);
                    }
                }
#else
                // Handle any new states
                if (tokenizer.CurrentKey.Equals("state", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentState != null)
                        States.Add(currentState);

                    currentState = new StateMetadata()
                    {
                        State = tokenizer.CurrentValue.ToString()
                    };

                    continue;
                }

                // At this point if no state is present, then we have invalid data
                if (currentState == null)
                    throw new Exception("Started to read state data without a state, this file may be corrupt");

                // Handle value
                if (tokenizer.CurrentKey.Equals("dirs", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Dirs = int.Parse(tokenizer.CurrentValue, provider: CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException(
                            $"Failed to dirs from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'",
                            e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("frames", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Frames = int.Parse(tokenizer.CurrentValue, provider: CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException(
                            $"Failed to parse number of frames from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'",
                            e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("delay", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Delay = tokenizer.CurrentValue.ToString().Split(',')
                            .Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException(
                            $"Failed to parse delay from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'",
                            e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("rewind", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Rewind =
                            int.Parse(tokenizer.CurrentValue, provider: CultureInfo.InvariantCulture) == 1;
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException(
                            $"Failed to parse rewind flag from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'",
                            e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("movement", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Movement =
                            int.Parse(tokenizer.CurrentValue, provider: CultureInfo.InvariantCulture) == 1;
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException(
                            $"Failed to parse movement flag from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'",
                            e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("loop", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Loop = int.Parse(tokenizer.CurrentValue, provider: CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException(
                            $"Failed to parse loop from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'",
                            e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("hotspot", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        currentState.Hotspots ??= new List<int[]>();
                        currentState.Hotspots.Add(tokenizer.CurrentValue.ToString().Split(',')
                            .Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray());
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException(
                            $"Failed to parse hotspot from line '{tokenizer.CurrentValue.ToString()}' in state '{currentState.State}'",
                            e);
                    }
                }
#endif
            } while (tokenizer.MoveNext());
            
            // Catch the last state
            States.Add(currentState);
        }

        /// <summary>
        /// Attempts to parse the header data (version, frame size) to this DMIMetadata object
        /// </summary>
        /// <param name="tokenizer">The tokenizer containing the data</param>
        /// <returns>True if more data (states) follows, false if no data is found after the header</returns>
        private bool ParseHeader(ref DMITokenizer tokenizer)
        {
            while (tokenizer.MoveNext())
            {
#if NETSTANDARD || NET472 || NET461
// Handle value
                if (tokenizer.CurrentKey.Equals("version".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    if (Version != 0d)
                        throw new Exception("Found more than one version line, this file may be corrupt!");
                    
                    try
                    {
                        Version = double.Parse(tokenizer.CurrentValue.ToString(), provider: CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to parse version number from line:\n{tokenizer.CurrentValue.ToString()}", e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("width".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    if (FrameWidth != -1)
                        throw new Exception("Found more than one frame width line, this file may be corrupt!");
                    
                    try
                    {
                        FrameWidth = int.Parse(tokenizer.CurrentValue.ToString(), provider: CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to parse frame width from line:\n{tokenizer.CurrentValue.ToString()}", e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("height".AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    if (FrameHeight != -1)
                        throw new Exception("Found more than one frame height line, this file may be corrupt!");
                    
                    try
                    {
                        FrameHeight = int.Parse(tokenizer.CurrentValue.ToString());
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to parse frame height from line:\n{tokenizer.CurrentValue.ToString()}", e);
                    }
                }
#else
                // Handle value
                if (tokenizer.CurrentKey.Equals("version", StringComparison.OrdinalIgnoreCase))
                {
                    if (Version != 0d)
                        throw new Exception("Found more than one version line, this file may be corrupt!");
                    
                    try
                    {
                        Version = double.Parse(tokenizer.CurrentValue, provider: CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to parse version number from line:\n{tokenizer.CurrentValue.ToString()}", e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("width", StringComparison.OrdinalIgnoreCase))
                {
                    if (FrameWidth != -1)
                        throw new Exception("Found more than one frame width line, this file may be corrupt!");
                    
                    try
                    {
                        FrameWidth = int.Parse(tokenizer.CurrentValue, provider: CultureInfo.InvariantCulture);
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to parse frame width from line:\n{tokenizer.CurrentValue.ToString()}", e);
                    }
                }
                else if (tokenizer.CurrentKey.Equals("height", StringComparison.OrdinalIgnoreCase))
                {
                    if (FrameHeight != -1)
                        throw new Exception("Found more than one frame height line, this file may be corrupt!");
                    
                    try
                    {
                        FrameHeight = int.Parse(tokenizer.CurrentValue);
                    }
                    catch (FormatException e)
                    {
                        throw new FormatException($"Failed to parse frame height from line:\n{tokenizer.CurrentValue.ToString()}", e);
                    }
                }
#endif
                else
                {
                    return true;
                }
            }

            return false;
        }
    }
}
