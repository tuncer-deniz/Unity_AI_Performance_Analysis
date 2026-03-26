using System.IO;
using System.Text;
using FrameAnalyzer.Runtime.Data;
using FrameAnalyzer.Runtime.Serialization;
using FrameAnalyzer.Runtime.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace FrameAnalyzer.Editor.Claude
{
    public static class AnalysisPromptBuilder
    {
        /// <summary>
        /// Builds the full prompt for Claude: agent instructions + frame data + scene data.
        /// Automatically selects URP or HDRP-specific agent prompt based on detected pipeline.
        /// </summary>
        public static string Build(CaptureSession session, string sceneSnapshot = null, bool mcpAvailable = false, string userNotes = null)
        {
            var sb = new StringBuilder();

            // Agent prompt (performance analysis expertise) - pipeline-specific
            string agentContent = LoadAgentPrompt();
            if (!string.IsNullOrEmpty(agentContent))
            {
                sb.AppendLine(agentContent);
                sb.AppendLine();
            }

            // Frame data + scene data
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("# Captured Performance Data");
            sb.AppendLine();
            sb.Append(SessionSerializer.ToAnalysisPrompt(session, sceneSnapshot));

            // Analysis instructions
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("# Your Task");
            sb.AppendLine();
            sb.AppendLine("Analyze the captured performance data above and provide a comprehensive performance report. Structure your response as:");
            sb.AppendLine();
            sb.AppendLine("1. **Executive Summary** — One paragraph: overall health, FPS assessment, primary bottleneck");
            sb.AppendLine("2. **Critical Issues** — Ranked by impact. For each: what the data shows, why it's a problem, specific fix");
            sb.AppendLine("3. **Script Hotspots** — From the per-method profiler hierarchy: which specific methods consume the most self-time, which allocate GC memory, and what to do about each");
            sb.AppendLine("4. **Rendering Analysis** — Draw call efficiency, batching, set-pass calls, URP pass breakdown");
            sb.AppendLine("5. **CPU Analysis** — Overall script/physics/animation time. Use the aggregate timing for category breakdown, but prefer per-method hierarchy data for specifics (don't repeat insights from both)");
            sb.AppendLine("6. **Memory Analysis** — GC allocations per frame (cite specific allocating methods from the GC allocator table), heap growth. Use the Loaded Asset Memory Breakdown to identify what's consuming the heap: oversized textures, uncompressed audio, read/write meshes, etc. Name specific assets and their sizes.");
            sb.AppendLine("7. **GPU Analysis** — Frame time breakdown, bottleneck classification, which URP passes are expensive");
            if (!string.IsNullOrEmpty(sceneSnapshot))
                sb.AppendLine("8. **Scene Structure Issues** — Based on the scene analysis: hierarchy depth, static flags, LOD coverage, material/shader issues");
            sb.AppendLine();
            sb.AppendLine("Be specific with numbers from the data. Reference exact values. Suggest concrete, actionable fixes — not generic advice.");
            sb.AppendLine();
            sb.AppendLine("**Important context:** This data was captured in the Unity Editor, not on a target device. Absolute frame times and FPS will differ from real hardware. Focus on *relative* costs (which systems/methods dominate), *patterns* (per-frame allocations, excessive draw calls), and *structural issues* (scene setup, missing optimizations) — these transfer to any platform. Do not make claims about whether the game will hit a specific FPS target on end-user hardware.");

            if (!string.IsNullOrEmpty(userNotes))
            {
                sb.AppendLine();
                sb.AppendLine("**Developer notes — READ CAREFULLY:** The developer has provided the following guidance. Respect these notes: skip topics they say to ignore, focus on what they ask about, and do not flag issues they have explicitly accepted.");
                sb.AppendLine();
                sb.AppendLine(userNotes);
            }

            if (mcpAvailable)
            {
                sb.AppendLine();
                sb.AppendLine("You have access to the Unity MCP server. If you find issues in the data that need deeper investigation (e.g., checking specific GameObject components, material settings, or shader source), use the MCP tools to query the scene for more details.");
            }

            return sb.ToString();
        }

        // Public so ComparisonPromptBuilder can reuse it
        public static string LoadAgentPromptPublic() => LoadAgentPrompt();

        static string LoadAgentPrompt()
        {
            bool isHdrp = PipelineDetector.IsHdrpActive();
            string promptFileName = isHdrp ? "FrameAnalysis_HDRP.md" : "FrameAnalysis.md";

            // Look for the agent markdown file relative to this script
            var guids = UnityEditor.AssetDatabase.FindAssets($"{Path.GetFileNameWithoutExtension(promptFileName)} t:TextAsset",
                new[] { "Packages/com.tonythedev.frame-analyzer/Editor/Agents/Definitions" });

            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(promptFileName))
                {
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                    if (asset != null) return asset.text;
                }
            }

            // Fallback: try direct file read
            var directPath = Path.Combine(Application.dataPath,
                $"../Packages/com.tonythedev.frame-analyzer/Editor/Agents/Definitions/{promptFileName}");
            if (File.Exists(directPath))
                return File.ReadAllText(directPath);

            // If HDRP file not found, try URP as fallback
            if (isHdrp)
            {
                directPath = Path.Combine(Application.dataPath,
                    "../Packages/com.tonythedev.frame-analyzer/Editor/Agents/Definitions/FrameAnalysis.md");
                if (File.Exists(directPath))
                    return File.ReadAllText(directPath);
            }

            return GetFallbackAgentPrompt(isHdrp);
        }

        static string GetFallbackAgentPrompt(bool isHdrp)
        {
            if (isHdrp)
            {
                return @"You are a Unity performance analysis expert specializing in HDRP (High Definition Render Pipeline).
You analyze profiler data, rendering statistics, GPU/CPU timing, memory patterns, and scene structure to identify
performance bottlenecks and provide actionable optimization recommendations.

Your expertise includes: Deferred vs forward rendering, ray tracing optimization, volumetric lighting,
GPU/CPU bottleneck identification, GC allocation reduction, HDRP render pass optimization, LOD configuration,
physics optimization, custom pass volumes, material complexity (Lit vs StackLit), and shader performance.";
            }

            return @"You are a Unity performance analysis expert specializing in URP (Universal Render Pipeline).
You analyze profiler data, rendering statistics, memory patterns, and scene structure to identify
performance bottlenecks and provide actionable optimization recommendations.

Your expertise includes: SRP Batcher compatibility, draw call optimization, GPU/CPU bottleneck
identification, GC allocation reduction, URP render pass optimization, LOD configuration,
physics optimization, UI Canvas best practices, and shader performance.";
        }
    }
}
