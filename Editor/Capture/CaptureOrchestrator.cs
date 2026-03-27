using System;
using System.Collections.Generic;
using FrameAnalyzer.Editor.Collectors;
using FrameAnalyzer.Runtime.Collectors;
using FrameAnalyzer.Runtime.Data;
using FrameAnalyzer.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace FrameAnalyzer.Editor.Capture
{
    public class CaptureOrchestrator
    {
        public enum CaptureState { Idle, Capturing, Complete, Cancelled }

        public CaptureState State { get; private set; } = CaptureState.Idle;
        public int CurrentFrame { get; private set; }
        public int TotalFrames { get; private set; }
        public CaptureSession Session { get; private set; }
        public float Progress => TotalFrames > 0 ? (float)CurrentFrame / TotalFrames : 0f;

        private readonly List<IFrameDataCollector> _collectors;

        /// <summary>
        /// Creates a CaptureOrchestrator, auto-detecting the active render pipeline.
        /// If collectors list is null, it creates default collectors based on the pipeline.
        /// </summary>
        public CaptureOrchestrator(List<IFrameDataCollector> collectors = null)
        {
            if (collectors == null)
            {
                collectors = new List<IFrameDataCollector>();
                AddDefaultCollectors(collectors);
            }
            _collectors = collectors;
        }

        /// <summary>
        /// Adds default collectors based on the current render pipeline (URP vs HDRP).
        /// </summary>
        private static void AddDefaultCollectors(List<IFrameDataCollector> collectors)
        {
            // Add common collectors
            collectors.Add(new CpuTimingCollector());
            collectors.Add(new MemoryCollector());
            collectors.Add(new RenderingStatsCollector());
            collectors.Add(new GpuTimingCollector());
            collectors.Add(new BottleneckCollector());

            // Add pipeline-specific collector
            if (PipelineDetector.IsHdrpActive())
            {
                collectors.Add(new HdrpPassCollector());
#if HDRP_AVAILABLE
                // ProfilingSampler-based collector gives real GPU per-pass timing
                collectors.Add(new HdrpProfilingSamplerCollector());
#endif
            }
            else
            {
                // Default to URP or generic render pass collector
                collectors.Add(new UrpPassCollector());
            }
        }

        private bool _profilerWasEnabled;

        public void StartCapture(int frameCount)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Frame capture requires Play Mode.");

            if (State == CaptureState.Capturing)
                throw new InvalidOperationException("Capture already in progress.");

            // Enable profiler for per-method hierarchy capture
            _profilerWasEnabled = UnityEngine.Profiling.Profiler.enabled;
            UnityEngine.Profiling.Profiler.enabled = true;

            TotalFrames = frameCount;
            CurrentFrame = 0;
            State = CaptureState.Capturing;

            Session = new CaptureSession
            {
                RequestedFrameCount = frameCount
            };
            Session.PopulateSystemInfo();

            foreach (var collector in _collectors)
                collector.Begin();
        }

        /// <summary>
        /// Call once per frame during capture. Returns true when capture is complete.
        /// </summary>
        public bool CaptureFrame()
        {
            if (State != CaptureState.Capturing) return true;

            var snapshot = new FrameSnapshot { FrameIndex = CurrentFrame };
            foreach (var collector in _collectors)
            {
                try
                {
                    collector.Collect(snapshot);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning($"[FrameAnalyzer] Collector {collector.GetType().Name} failed: {e.Message}");
                }
            }
            Session.Frames.Add(snapshot);

            CurrentFrame++;
            if (CurrentFrame >= TotalFrames)
            {
                EndCapture();
                return true;
            }
            return false;
        }

        public void Cancel()
        {
            if (State != CaptureState.Capturing) return;
            foreach (var collector in _collectors)
                collector.End();
            State = CaptureState.Cancelled;
        }

        private void EndCapture()
        {
            foreach (var collector in _collectors)
                collector.End();

            // Run profiler hierarchy analysis before disabling
            Session.ProfilerHierarchy = ProfilerHierarchyAnalyzer.Analyze(TotalFrames);

            // Restore profiler state
            UnityEngine.Profiling.Profiler.enabled = _profilerWasEnabled;

            Session.ComputeSummary();
            State = CaptureState.Complete;
        }
    }
}
