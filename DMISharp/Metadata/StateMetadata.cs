using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DMISharp.Metadata
{
    /// <summary>
    /// Represents the header data for an individual DMI state
    /// </summary>
    public class StateMetadata
    {
        public string State { get; set; }
        public int Dirs { get; internal set; }
        public int Frames { get; internal set; }
#pragma warning disable CA1819 // Properties should not return arrays
        public double[] Delay { get; internal set; }
#pragma warning restore CA1819 // Properties should not return arrays
        public bool Rewind { get; internal set; }
        public bool Movement { get; internal set; }
        public int Loop { get; internal set; }
        public IEnumerable<double[]> Hotspots { get; set; }
        private readonly Regex statePattern = new Regex("^state = \"(?<label>.*)\"$");
        private readonly Regex stateSubKeysPattern = new Regex("^\t(?<key>.+) = (?<val>.+)$");

        public StateMetadata(string name, DirectionDepth directionDepth, int frames)
        {
            State = name;
            Dirs = (int)directionDepth;
            Frames = frames;
        }

        /// <summary>
        /// Instantiates a StateMetadata object from a collection of body metadata
        /// </summary>
        /// <param name="data">A collection of body metadata for the state</param>
        public StateMetadata(List<string> data)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // Get state name
            State = statePattern.Match(data[0]).Groups["label"].Value;

            // Get key, value pairs
            var rowKV = new List<(string key, string value)>();
            foreach (var row in data.Skip(1))
            {
                var match = stateSubKeysPattern.Match(row);
                rowKV.Add((match.Groups["key"].Value, match.Groups["val"].Value));
            }

            // Consume the pairs we are interested in
            if (rowKV.Any(x => x.key == "dirs"))
            {
                var (key, value) = rowKV.Where(x => x.key == "dirs").First();
                try
                {
                    Dirs = int.Parse(value);
                }
                catch (FormatException e)
                {
                    throw new FormatException($"Failed to parse value from k,v pair [{key} = {value}]", e);
                }
            }
            if (rowKV.Any(x => x.key == "frames"))
            {
                var (key, value) = rowKV.Where(x => x.key == "frames").First();
                try
                {
                    Frames = int.Parse(value);
                }
                catch (FormatException e)
                {
                    throw new FormatException($"Failed to parse value from k,v pair [{key} = {value}]", e);
                }
            }
            if (rowKV.Any(x => x.key == "delay"))
            {
                var (key, value) = rowKV.Where(x => x.key == "delay").First();
                try
                {
                    Delay = value.Split(',').Select(x => double.Parse(x)).ToArray();
                }
                catch (FormatException e)
                {
                    throw new FormatException($"Failed to parse value from k,v pair [{key} = {value}]", e);
                }
            }
            if (rowKV.Any(x => x.key == "rewind"))
            {
                var (key, value) = rowKV.Where(x => x.key == "rewind").First();
                try
                {
                    Rewind = int.Parse(value) == 1;
                }
                catch (FormatException e)
                {
                    throw new FormatException($"Failed to parse value from k,v pair [{key} = {value}]", e);
                }
            }
            if (rowKV.Any(x => x.key == "movement"))
            {
                var (key, value) = rowKV.Where(x => x.key == "movement").First();
                try
                {
                    Movement = int.Parse(value) == 1;
                }
                catch (FormatException e)
                {
                    throw new FormatException($"Failed to parse value from k,v pair [{key} = {value}]", e);
                }
            }
            if (rowKV.Any(x => x.key == "loop"))
            {
                var (key, value) = rowKV.Where(x => x.key == "loop").First();
                try
                {
                    Loop = int.Parse(value);
                }
                catch (FormatException e)
                {
                    throw new FormatException($"Failed to parse value from k,v pair [{key} = {value}]", e);
                }
            }
            if (rowKV.Any(x => x.key == "hotspot"))
            {
                var cursor = rowKV.Where(x => x.key == "dirs");
                try
                {
                    var processed = new List<double[]>();

                    foreach (var (key, value) in cursor)
                    {
                        processed.Add(value.Split(',').Select(x => double.Parse(x)).ToArray());
                    }
                }
                catch (FormatException e)
                {
                    throw new FormatException($"Failed to parse value from k,v pair [{string.Join(", ", cursor.Select(x => x.key))} = {string.Join(", ", cursor.Select(x => x.value))}]", e);
                }
            }
        }
    }
}
