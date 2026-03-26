# HDRP Support — Testing Guide

**Branch:** `hdrp-port`  
**Commits:** `159dd38` (HDRP port) + `99a640b` (blocker fixes)  
**Tester:** Arvin David  

---

## What Changed

The Frame Analyzer now supports **both URP and HDRP** projects. It auto-detects which pipeline is active and adjusts data collection, scene analysis, and AI prompts accordingly. No manual configuration needed.

### New Files
- `Runtime/Utils/PipelineDetector.cs` — Pipeline auto-detection (type hierarchy, not string matching)
- `Runtime/Collectors/HdrpPassCollector.cs` — HDRP render pass profiler data collection
- `Runtime/Data/HdrpPassTimingData.cs` — HDRP pass timing data structures
- `Editor/Agents/Definitions/FrameAnalysis_HDRP.md` — HDRP-specific AI agent prompt

### Modified Files
- `Editor/Capture/CaptureOrchestrator.cs` — Auto-selects URP or HDRP collector
- `Editor/Claude/AnalysisPromptBuilder.cs` — Loads pipeline-specific agent prompt
- `Editor/SceneAnalysis/SceneAnalyzer.cs` — Collects HDRP-specific scene data (StackLit materials, decal projectors, custom pass volumes, volumetric fog detection)

---

## Prerequisites

1. **Unity 6** (6000.0+) with **HDRP** configured as the active render pipeline
2. **Claude Code CLI** installed: `npm install -g @anthropic-ai/claude-code`
3. Import the package via Package Manager → Add from disk → select `package.json`

> **Note:** `package.json` currently lists both URP and HDRP as hard dependencies. In a URP-only project, you may need to remove `com.unity.render-pipelines.high-definition` from the dependencies. This is a known issue to address before release.

---

## Test Plan

### Test 1: Auto-Detection (HDRP Project)

**Goal:** Verify the tool detects HDRP and uses the correct collector.

1. Open an HDRP project (or create one from the HDRP template)
2. Open the tool: **Window → AI Performance and Frame Analysis** (or `Ctrl+Shift+J`)
3. Enter Play Mode
4. Click **Capture** (use default frame count)
5. **Verify:**
   - The capture completes without errors in the Console
   - The report includes an **HDRP Configuration** section in the scene analysis
   - Render pass data shows HDRP-specific passes (e.g., "Depth Prepass", "Gbuffer", "Deferred Lighting") — not URP passes like "DrawOpaqueObjects"

### Test 2: Auto-Detection (URP Project)

**Goal:** Verify URP still works and doesn't accidentally load HDRP components.

1. Open a URP project
2. Run the same capture flow
3. **Verify:**
   - No HDRP-related data appears in the report
   - No errors or warnings about HDRP in the Console
   - URP pass timing data appears as before

### Test 3: HDRP Profiler Markers

**Goal:** Check which markers actually fire in your HDRP version.

1. In an HDRP project, run a capture
2. Check the Console for this warning:
   > `[FrameAnalyzer] HdrpPassCollector: No HDRP profiler markers found.`
3. **If the warning appears:**
   - Open Unity's built-in Profiler (`Window → Analysis → Profiler`)
   - Capture a few frames in Play Mode
   - In the **CPU Usage** module, expand the render thread hierarchy
   - Note the actual HDRP marker names you see (e.g., do they use "HDRenderPipeline.Render" or something different?)
   - Report the marker names back — we need to update `HdrpPassCollector.PassNames[]`
4. **If the warning does NOT appear:** Great — the markers are matching. Check that pass timing values are non-zero and reasonable.

### Test 4: HDRP Scene Analysis

**Goal:** Verify HDRP-specific scene data collection.

Set up a scene with some or all of these, then capture:

| Feature | What to check in report |
|---------|------------------------|
| StackLit or Hair material on any object | `StackLit/LayeredLit/Complex Materials: N` appears |
| Decal Projector in scene | `Decal Projectors: N` appears |
| Custom Pass Volume | `Custom Pass Volumes: N` appears |
| Volume with Fog component (active) | `Volumetric Fog/Clouds: ENABLED` appears |
| Volume with Fog component (inactive) | Should NOT show as enabled |
| Volume with no fog/cloud overrides | Should NOT show as enabled |

### Test 5: AI Report Quality (HDRP)

**Goal:** Verify Claude gives HDRP-specific advice, not generic URP advice.

1. Run a capture in an HDRP scene with some deliberate performance issues (lots of shadow-casting lights, complex materials, volumetric fog on high quality)
2. Let Claude generate the report
3. **Verify:**
   - Report references HDRP concepts (deferred rendering, GBuffer, HDRP volume settings)
   - Report does NOT reference URP-specific concepts (SRP Batcher in URP context, URP render features)
   - Recommendations are HDRP-appropriate (e.g., "reduce StackLit usage" not "check URP asset settings")

### Test 6: Comparison Mode

**Goal:** Verify comparing an HDRP session with another HDRP session works.

1. Capture two sessions in the same HDRP project (change something between them — e.g., disable volumetric fog)
2. Use the comparison feature
3. **Verify:** The comparison report correctly identifies the delta

---

## Known Limitations

- **Profiler marker names are unverified** — based on HDRP docs and FPSSample. Test 3 is critical to validate these against your actual Unity 6 HDRP version. If markers don't match, the pass timing section will be empty (with a warning).
- **GPU per-pass timing unavailable** — `ProfilerRecorder` only gives CPU-side timing. GPU column will show 0.
- **StackLit counter** counts material instances across renderers, not unique materials. A StackLit material on 10 objects shows as 10.
- **Mixed pipeline scenes** not handled — if someone has both URP and HDRP assets in a project, behavior is undefined.

---

## Reporting Issues

When reporting a bug, include:
1. Unity version (exact, e.g., `6000.1.2f1`)
2. HDRP package version (`com.unity.render-pipelines.high-definition` version from Package Manager)
3. Console output (any warnings/errors during capture)
4. Screenshot of the generated report (if applicable)

File issues in the project's repo or message Tuncer directly.
