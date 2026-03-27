#if HDRP_AVAILABLE
using System.Collections.Generic;
using FrameAnalyzer.Runtime.Collectors;
using FrameAnalyzer.Runtime.Data;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace FrameAnalyzer.Editor.Collectors
{
    /// <summary>
    /// Captures per-pass GPU and CPU timing from HDRP's internal ProfilingSamplers.
    /// These give real GPU elapsed time per render pass — far more reliable than
    /// FrameTimingManager's single total GPU frame time.
    /// 
    /// Only compiles when HDRP is installed (guarded by HDRP_AVAILABLE define).
    /// Falls back gracefully if ProfilingSampler data is unavailable.
    /// </summary>
    public class HdrpProfilingSamplerCollector : IFrameDataCollector
    {
        private struct SamplerEntry
        {
            public string Name;
            public ProfilingSampler Sampler;
        }

        private readonly List<SamplerEntry> _samplers = new List<SamplerEntry>();
        private bool _initialized;

        public void Begin()
        {
            _samplers.Clear();
            _initialized = false;
        }

        public void Collect(FrameSnapshot snapshot)
        {
            if (!_initialized)
            {
                InitializeSamplers();
                _initialized = true;
                // Skip first frame — samplers need one frame to start collecting
                return;
            }

            // Create or reuse the HDRP pass data — the existing HdrpPassCollector
            // may have already populated CPU timing via ProfilerRecorder markers.
            // We augment with GPU timing from ProfilingSampler.
            var gpuPasses = new List<HdrpPassEntry>();
            bool anyGpuData = false;

            foreach (var entry in _samplers)
            {
                double gpuMs = entry.Sampler.gpuElapsedTime; // already in ms
                double cpuMs = entry.Sampler.cpuElapsedTime; // already in ms

                if (gpuMs > 0.001 || cpuMs > 0.001)
                {
                    gpuPasses.Add(new HdrpPassEntry
                    {
                        PassName = entry.Name,
                        CpuMs = cpuMs,
                        GpuMs = gpuMs
                    });

                    if (gpuMs > 0.001)
                        anyGpuData = true;
                }
            }

            // Store in a separate field so we don't overwrite the ProfilerRecorder data
            snapshot.HdrpGpuPasses = new HdrpPassTimingData
            {
                WasCollected = gpuPasses.Count > 0,
                Passes = gpuPasses,
                CollectionNote = anyGpuData
                    ? null
                    : "ProfilingSampler GPU timing returned 0 for all passes — GPU timing may not be supported on this hardware/driver."
            };
        }

        public void End()
        {
            foreach (var entry in _samplers)
            {
                try { entry.Sampler.enableRecording = false; }
                catch { /* ignore */ }
            }
            _samplers.Clear();
            _initialized = false;
        }

        private void InitializeSamplers()
        {
            // HDProfileId covers all major HDRP render passes.
            // We query the ones that represent significant GPU work.
            TryAdd("Depth Prepass", HDProfileId.DepthPrepass);
            TryAdd("GBuffer", HDProfileId.GBuffer);
            TryAdd("Deferred Lighting", HDProfileId.RenderDeferredLighting);
            TryAdd("Forward Opaques", HDProfileId.ForwardOpaque);
            TryAdd("Forward Transparent", HDProfileId.ForwardTransparent);
            TryAdd("Shadow Maps", HDProfileId.RenderShadowMaps);
            TryAdd("Contact Shadows", HDProfileId.ContactShadows);
            TryAdd("Volumetric Lighting", HDProfileId.VolumetricLighting);
            TryAdd("Volumetric Clouds", HDProfileId.VolumetricClouds);
            TryAdd("SSR", HDProfileId.SsrTracing);
            TryAdd("SSAO", HDProfileId.RenderSSAO);
            TryAdd("Post Processing", HDProfileId.PostProcessing);
            TryAdd("Bloom", HDProfileId.Bloom);
            TryAdd("Temporal AA", HDProfileId.TemporalAntialiasing);
            TryAdd("Motion Vectors", HDProfileId.MotionVectors);
            TryAdd("Color Grading", HDProfileId.ColorGrading);
            TryAdd("Depth of Field", HDProfileId.DepthOfField);
            TryAdd("Motion Blur", HDProfileId.MotionBlur);
            TryAdd("SSS", HDProfileId.SubsurfaceScattering);
            TryAdd("Decals", HDProfileId.RenderDecals);
            TryAdd("Sky", HDProfileId.RenderSky);
            TryAdd("Distortion", HDProfileId.Distortion);
            TryAdd("Build Light List", HDProfileId.BuildLightList);
            TryAdd("Copy Depth", HDProfileId.CopyDepthBuffer);
        }

        private void TryAdd(string friendlyName, HDProfileId profileId)
        {
            try
            {
                var sampler = ProfilingSampler.Get<HDProfileId>(profileId);
                if (sampler != null)
                {
                    sampler.enableRecording = true;
                    _samplers.Add(new SamplerEntry
                    {
                        Name = friendlyName,
                        Sampler = sampler
                    });
                }
            }
            catch
            {
                // HDProfileId enum value may not exist in this HDRP version — skip silently
            }
        }
    }
}
#endif
