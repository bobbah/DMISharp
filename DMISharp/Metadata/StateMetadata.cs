using System.Collections.Generic;

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
        public double[] Delay { get; internal set; }
        public bool Rewind { get; internal set; }
        public bool Movement { get; internal set; }
        public int Loop { get; internal set; }
        public List<int[]> Hotspots { get; set; }

        internal StateMetadata()
        {
            
        }
        
        public StateMetadata(string name, DirectionDepth directionDepth, int frames)
        {
            State = name;
            Dirs = (int)directionDepth;
            Frames = frames;
        }
    }
}
