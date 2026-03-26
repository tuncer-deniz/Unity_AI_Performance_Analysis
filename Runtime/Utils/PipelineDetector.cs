using UnityEngine.Rendering;

namespace FrameAnalyzer.Runtime.Utils
{
    public static class PipelineDetector
    {
        public enum PipelineType { BuiltIn, URP, HDRP, Unknown }

        /// <summary>
        /// Detects the active render pipeline using type hierarchy, not string matching.
        /// </summary>
        public static PipelineType DetectPipeline()
        {
            var asset = GraphicsSettings.currentRenderPipeline;
            if (asset == null) return PipelineType.BuiltIn;

            // Walk the type hierarchy — catches subclasses and custom pipelines
            var type = asset.GetType();
            while (type != null)
            {
                string fullName = type.FullName ?? "";
                // Check FullName (includes namespace) for reliable detection
                if (fullName == "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset"
                    || fullName.StartsWith("UnityEngine.Rendering.HighDefinition."))
                    return PipelineType.HDRP;
                if (fullName == "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
                    || fullName.StartsWith("UnityEngine.Rendering.Universal."))
                    return PipelineType.URP;
                type = type.BaseType;
            }

            return PipelineType.Unknown;
        }

        public static bool IsHdrpActive() => DetectPipeline() == PipelineType.HDRP;
        public static bool IsUrpActive() => DetectPipeline() == PipelineType.URP;
    }
}
