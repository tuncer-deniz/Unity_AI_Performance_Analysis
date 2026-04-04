using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using FrameAnalyzer.Runtime.Collectors;
using FrameAnalyzer.Runtime.Data;
using FrameAnalyzer.Runtime.Serialization;
using FrameAnalyzer.Runtime.Utils;

namespace FrameAnalyzer.Runtime
{
    [AddComponentMenu("AI Performance Analysis/Build Capture Controller")]
    public class BuildCaptureController : MonoBehaviour
    {
        [Header("Capture Settings")]
        [Tooltip("Duration in seconds to capture frames.")]
        public float captureDurationSeconds = 5f;

        [Tooltip("Automatically start capturing when the scene loads.")]
        public bool captureOnStart = true;

        [Tooltip("Subdirectory under Application.persistentDataPath for saved captures.")]
        public string outputDirectory = "PerformanceCaptures";

        PipelineDetector.PipelineType detectedPipeline;
        List<IFrameDataCollector> collectors;
        bool capturing;

        void Awake()
        {
            detectedPipeline = PipelineDetector.DetectPipeline();
            Debug.Log($"[BuildCaptureController] Detected pipeline: {detectedPipeline}");

            collectors = new List<IFrameDataCollector>
            {
                new CpuTimingCollector(),
                new GpuTimingCollector(),
                new MemoryCollector()
            };

            if (detectedPipeline == PipelineDetector.PipelineType.URP)
                collectors.Add(new UrpPassCollector());
            else if (detectedPipeline == PipelineDetector.PipelineType.HDRP)
                collectors.Add(new HdrpPassCollector());
        }

        void Start()
        {
            if (captureOnStart)
                StartCapture();
        }

        public void StartCapture()
        {
            if (capturing)
            {
                Debug.LogWarning("[BuildCaptureController] Capture already in progress.");
                return;
            }
            StartCoroutine(CaptureCoroutine());
        }

        IEnumerator CaptureCoroutine()
        {
            capturing = true;

            foreach (var c in collectors)
                c.Begin();

            var frames = new List<FrameSnapshot>();
            float elapsed = 0f;
            int frameIndex = 0;

            Debug.Log($"[BuildCaptureController] Capture started ({captureDurationSeconds}s).");

            while (elapsed < captureDurationSeconds)
            {
                yield return null;

                var snapshot = new FrameSnapshot { FrameIndex = frameIndex++ };
                foreach (var c in collectors)
                    c.Collect(snapshot);
                frames.Add(snapshot);

                elapsed += Time.unscaledDeltaTime;
            }

            foreach (var c in collectors)
                c.End();

            Debug.Log($"[BuildCaptureController] Capture finished. {frames.Count} frames collected.");

            var session = new CaptureSession();
            session.PopulateSystemInfo();
            session.RequestedFrameCount = frames.Count;
            session.CaptureTimeIso = System.DateTime.UtcNow.ToString("o");
            session.Frames = frames;
            session.Summary = session.ComputeSummary();

            SaveSession(session);
            capturing = false;
        }

        void SaveSession(CaptureSession session)
        {
            string dir = Path.Combine(Application.persistentDataPath, outputDirectory);
            Directory.CreateDirectory(dir);

            string timestamp = System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string filePath = Path.Combine(dir, $"{timestamp}.json");

            string json = SessionSerializer.ToJson(session);
            File.WriteAllText(filePath, json);

            Debug.Log($"[BuildCaptureController] Session saved to: {filePath}");
        }
    }
}
