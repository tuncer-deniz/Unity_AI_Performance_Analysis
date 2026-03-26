using System.Collections.Generic;
using FrameAnalyzer.Runtime.Data;
using Unity.Profiling;

namespace FrameAnalyzer.Runtime.Collectors
{
    /// <summary>
    /// Captures per-HDRP-render-pass CPU timing using ProfilerRecorder.
    /// HDRP profiler markers are defined in HDRenderPipeline and related render components.
    /// These markers track deferred rendering, volumetric effects, ray tracing, and post-processing passes.
    /// 
    /// ASSUMPTIONS about marker names (these are based on FPSSample and HDRP documentation):
    /// - Main render pipeline markers use "HDRenderPipeline::*" or "Render.*" patterns
    /// - Pass names are derived from HDProfileId enums and ProfilingSample declarations
    /// - Some passes may not always be active depending on HDRP settings
    /// </summary>
    public class HdrpPassCollector : IFrameDataCollector
    {
        // HDRP render pass marker names matching the render pipeline execution flow
        // These are organized by rendering stage for clarity
        static readonly string[] PassNames =
        {
            // Main pipeline
            "HDRenderPipeline.Render",
            "Build Light List",
            "Render Camera Stack",

            // Depth and GBuffer
            "Depth Prepass",
            "Gbuffer",
            "Copy Depth",

            // Lighting and Shadows
            "RenderShadowMaps",
            "Render Directional Shadow Map",
            "Render Punctual Shadow Map",
            "ContactShadows",
            "Direct Lighting",
            "Deferred Lighting",

            // Volumetric and Advanced
            "VolumetricLighting",
            "VolumetricClouds",
            "VolumetricFog",
            "ScreenSpaceReflection",
            "ScreenSpaceGlobalIllumination",
            "Raytracing GI",
            "Raytracing Reflections",
            "Raytracing AO",

            // Post-processing
            "TemporalAntialiasing",
            "MotionVectors",
            "PostProcess",
            "Bloom",
            "ChromaticAberration",
            "ColorGrading",
            "DepthOfField",
            "LensDistortion",
            "MotionBlur",
            "PaniniProjection",
            "ToneMappingAndColorGrading",

            // Additional Effects
            "SubsurfaceScattering",
            "Distortion",
            "TransparentPrepass",
            "TransparentDepthPrepass",
            "Opaque Forward Pass",
            "PreRefractive Forward Pass",
            "Transparent Forward Pass",

            // Decals and Utility
            "DecalProjector",
            "DecalNormalBuffer",
            "Decal Quads",

            // Sky
            "Render Sky",

            // Final
            "Final Blit",

            // Sky reflection / Lighting
            "Build Lighting Cluster",
            "Prepare Material Constants",

            // UI and canvas-like
            "DrawFullscreen",
        };

        private struct RecorderEntry
        {
            public string Name;
            public ProfilerRecorder Recorder;
        }

        private readonly List<RecorderEntry> _activeRecorders = new List<RecorderEntry>();

        public void Begin()
        {
            _activeRecorders.Clear();
            foreach (var name in PassNames)
            {
                var rec = ProfilerRecorder.StartNew(ProfilerCategory.Render, name);
                if (rec.Valid)
                    _activeRecorders.Add(new RecorderEntry { Name = name, Recorder = rec });
                else
                    rec.Dispose();
            }

            if (_activeRecorders.Count == 0)
                UnityEngine.Debug.LogWarning("[FrameAnalyzer] HdrpPassCollector: No HDRP profiler markers found. " +
                    "Marker names may not match this HDRP version. Run the Unity Profiler manually to verify marker names.");
        }

        public void Collect(FrameSnapshot snapshot)
        {
            var hdrp = HdrpPassTimingData.Create();
            hdrp.WasCollected = _activeRecorders.Count > 0;

            foreach (var entry in _activeRecorders)
            {
                long ns = entry.Recorder.LastValue;
                if (ns <= 0) continue;

                hdrp.Passes.Add(new HdrpPassEntry
                {
                    PassName = entry.Name,
                    CpuMs = ns / 1_000_000.0,
                    GpuMs = 0 // GPU per-pass timing not available via ProfilerRecorder
                });
            }

            if (_activeRecorders.Count == 0)
            {
                hdrp.WasCollected = false;
                hdrp.CollectionNote = "No HDRP profiler markers matched. Verify marker names against your HDRP version.";
            }

            snapshot.HdrpPasses = hdrp;
        }

        public void End()
        {
            foreach (var entry in _activeRecorders)
                entry.Recorder.Dispose();
            _activeRecorders.Clear();
        }
    }
}
