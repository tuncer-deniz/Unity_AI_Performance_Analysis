using System;
using System.Collections.Generic;

namespace FrameAnalyzer.Runtime.Data
{
    [Serializable]
    public struct HdrpPassEntry
    {
        public string PassName;
        public double CpuMs;
        public double GpuMs;
    }

    [Serializable]
    public struct HdrpPassTimingData
    {
        public bool WasCollected;
        public List<HdrpPassEntry> Passes;
        public string CollectionNote;

        public static HdrpPassTimingData Create()
        {
            return new HdrpPassTimingData
            {
                WasCollected = false,
                Passes = new List<HdrpPassEntry>(),
                CollectionNote = null
            };
        }
    }
}
