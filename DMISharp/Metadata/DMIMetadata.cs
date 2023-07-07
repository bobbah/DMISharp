using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MetadataExtractor.Formats.Png;

namespace DMISharp.Metadata;

/// <summary>
/// Represents the header data of a DMI file.
/// </summary>
public class DMIMetadata
{
    private static readonly Regex DMIStart = new(@"#\s{0,1}BEGIN DMI", RegexOptions.Compiled);
    
    /// <summary>
    /// Constructs a new <see cref="DMIMetadata"/> for a provided BYOND version and pair of state dimensions.
    /// </summary>
    /// <param name="byondVersion">The version of BYOND this metadata is for</param>
    /// <param name="frameWidth">The width of each state frame in pixels</param>
    /// <param name="frameHeight">The height of each state frame in pixels</param>
    public DMIMetadata(double byondVersion, int frameWidth, int frameHeight)
    {
        Version = byondVersion;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        States = new List<StateMetadata>();
    }

    /// <summary>
    /// Instantiates a DMIMetadata object from a file stream of that DMI file.
    /// </summary>
    /// <param name="stream">The data stream to read from</param>
    public DMIMetadata(Stream stream)
    {
        States = new List<StateMetadata>();
        FrameWidth = -1;
        FrameHeight = -1;

        // Get the contents of the DMI metadata
        var data = GetDMIMetadata(stream);
        ParseMetadata(data);
    }

    /// <summary>
    /// The version of the DMI metadata, dictated by BYOND.
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

    /// <summary>
    /// The set of metadata for each of the states in the DMI file.
    /// </summary>
    public List<StateMetadata> States { get; }

    /// <summary>
    /// Gets a collection of DMI metadata directories and breaks it into individual lines of DMI metadata.
    /// </summary>
    /// <param name="stream">The file to get the metadata of</param>
    /// <returns>A ReadOnlySpan of the DMI's metadata if found</returns>
    public static ReadOnlySpan<char> GetDMIMetadata(Stream stream)
    {
        var directories = PngMetadataReader.ReadMetadata(stream);
        string? metaDesc = null;
        foreach (var t in directories)
        {
            foreach (var tag in t.Tags)
            {
                if (tag.Description != null && !DMIStart.IsMatch(tag.Description))
                    continue;

                metaDesc = tag.Description;
                break;
            }

            if (metaDesc != null)
                break;
        }

        if (metaDesc == null)
        {
            throw new InvalidOperationException("Failed to find BYOND DMI metadata in PNG text data!");
        }

        return metaDesc.AsSpan()[metaDesc.IndexOf('#', StringComparison.InvariantCultureIgnoreCase)..];
    }

    /// <summary>
    /// Attempts to apply all data from the provided metadata to this DMIMetadata object.
    /// </summary>
    /// <param name="data">The metadata string to parse</param>
    private void ParseMetadata(ReadOnlySpan<char> data)
    {
        StateMetadata? currentState = null;
        var tokenizer = new DMITokenizer(data);

        // Parse header
        var hasBody = ParseHeader(ref tokenizer);
        if (Version == 0d)
            throw new InvalidOperationException("Failed to parse required header data of DMI file, this file may be corrupt.");

        // Check for any additional data after the header
        if (!hasBody)
            return;

        do
        {
            // Handle any new states
            if (tokenizer.KeyEquals("state"))
            {
                if (currentState != null)
                    States.Add(currentState);

                currentState = new StateMetadata(tokenizer.CurrentValue.ToString());
                continue;
            }

            // At this point if no state is present, then we have invalid data
            if (currentState == null)
                throw new InvalidOperationException("Started to read state data without a state, this file may be corrupt");

            // Handle value
            if (tokenizer.KeyEquals("dirs"))
                currentState.Dirs = tokenizer.ValueAsInt();
            else if (tokenizer.KeyEquals("frames"))
                currentState.Frames = tokenizer.ValueAsInt();
            else if (tokenizer.KeyEquals("rewind"))
                currentState.Rewind = tokenizer.ValueAsBool();
            else if (tokenizer.KeyEquals("movement"))
                currentState.Movement = tokenizer.ValueAsBool();
            else if (tokenizer.KeyEquals("loop"))
                currentState.Loop = tokenizer.ValueAsInt();
            else if (tokenizer.KeyEquals("delay"))
                currentState.Delay = tokenizer.ValueAsDoubleArray();
            else if (tokenizer.KeyEquals("hotspot"))
            {
                currentState.Hotspots ??= new List<int[]>();
                currentState.Hotspots.Add(tokenizer.ValueAsIntArray());
            }
        } while (tokenizer.MoveNext());

        // Catch the last state
        States.Add(currentState);
    }

    /// <summary>
    /// Attempts to parse the header data (version, frame size) to this DMIMetadata object.
    /// </summary>
    /// <param name="tokenizer">The tokenizer containing the data</param>
    /// <returns>True if more data (states) follows, false if no data is found after the header</returns>
    private bool ParseHeader(ref DMITokenizer tokenizer)
    {
        while (tokenizer.MoveNext())
        {
            if (tokenizer.KeyEquals("version"))
            {
                if (Version != 0d)
                    throw new InvalidOperationException("Found more than one version line, this file may be corrupt!");

                Version = tokenizer.ValueAsDouble();
            }
            else if (tokenizer.KeyEquals("width"))
            {
                if (FrameWidth != -1)
                    throw new InvalidOperationException("Found more than one frame width line, this file may be corrupt!");

                FrameWidth = tokenizer.ValueAsInt();
            }
            else if (tokenizer.KeyEquals("height"))
            {
                if (FrameHeight != -1)
                    throw new InvalidOperationException("Found more than one frame height line, this file may be corrupt!");

                FrameHeight = tokenizer.ValueAsInt();
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}