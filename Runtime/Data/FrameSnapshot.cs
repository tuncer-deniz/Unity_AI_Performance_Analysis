using System;

namespace FrameAnalyzer.Runtime.Data
{
    [Serializable]
    public class FrameSnapshot
    {
        public int FrameIndex;
        public CpuTimingData Cpu;
        public MemoryData Memory;
        public RenderingData Rendering;
        public GpuTimingData Gpu;
        public UrpPassTimingData UrpPasses;
        public HdrpPassTimingData HdrpPasses;
        public HdrpPassTimingData HdrpGpuPasses; // ProfilingSampler-based GPU timing (separate from ProfilerRecorder CPU timing)
        public BottleneckData Bottleneck;

        public FrameSnapshot()
        {
            UrpPasses = UrpPassTimingData.Create();
            HdrpPasses = HdrpPassTimingData.Create();
            HdrpGpuPasses = HdrpPassTimingData.Create();
        }
    }
}
