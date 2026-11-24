using System;
using System.IO;
using BeatFramer.Configuration;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatFramer.Telemetry
{
    /// <summary>
    /// Lightweight per-scene frame-time + Unity ProfilerRecorder logger so Stock vs Optimized
    /// vs LiteRenderer can be compared with real CPU/GPU/draw-call numbers in UserData/BeatFramer/perf.log.
    /// </summary>
    internal class PerfMonitor : MonoBehaviour
    {
        private const int MaxSamples = 18000;
        private readonly float[] _frameMs = new float[MaxSamples];
        private int _sampleCount;
        private int _totalFrames;
        private double _sumMs;
        private string _sceneName = "";
        private int _gc0Start, _gc1Start, _gc2Start;
        private string _logPath;

        private ProfilerRecorder _mainThreadRec;
        private ProfilerRecorder _renderThreadRec;
        private ProfilerRecorder _drawCallsRec;
        private ProfilerRecorder _setPassRec;
        private ProfilerRecorder _trisRec;

        private double _sumMainMs, _sumRenderMs, _sumDrawCalls, _sumSetPass, _sumTris;
        private int _profSamples;
        private bool _recordersActive;

        private void Awake()
        {
            string dir = Path.Combine(Environment.CurrentDirectory, "UserData", "BeatFramer");
            try
            {
                _logPath = Path.Combine(dir, "perf.log");
            }
            catch
            {
                _logPath = null;
            }

            if (PluginConfig.Instance?.EnablePerfLogger == true)
            {
                StartRecorders();
            }

            SceneManager.activeSceneChanged += OnSceneChanged;
            ResetStats(SceneManager.GetActiveScene().name);
        }

        private void StartRecorders()
        {
            if (_recordersActive) return;
            _recordersActive = true;
            try
            {
                _mainThreadRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
                _renderThreadRec = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 1);
                _drawCallsRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
                _setPassRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
                _trisRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
            }
            catch { }
        }

        private void StopRecorders()
        {
            if (!_recordersActive) return;
            _recordersActive = false;
            if (_mainThreadRec.Valid) _mainThreadRec.Dispose();
            if (_renderThreadRec.Valid) _renderThreadRec.Dispose();
            if (_drawCallsRec.Valid) _drawCallsRec.Dispose();
            if (_setPassRec.Valid) _setPassRec.Dispose();
            if (_trisRec.Valid) _trisRec.Dispose();
        }

        private void OnDestroy()
        {
            FlushStats();
            SceneManager.activeSceneChanged -= OnSceneChanged;
            StopRecorders();
        }

        private void OnSceneChanged(Scene prev, Scene next)
        {
            FlushStats();
            ResetStats(next.name);
        }

        private void ResetStats(string sceneName)
        {
            _sceneName = sceneName ?? "Unknown";
            _sampleCount = 0;
            _totalFrames = 0;
            _sumMs = 0.0;
            _sumMainMs = _sumRenderMs = _sumDrawCalls = _sumSetPass = _sumTris = 0.0;
            _profSamples = 0;
            _gc0Start = GC.CollectionCount(0);
            _gc1Start = GC.CollectionCount(1);
            _gc2Start = GC.CollectionCount(2);
        }

        private void Update()
        {
            var cfg = PluginConfig.Instance;
            if (cfg == null || !cfg.EnablePerfLogger)
            {
                if (_recordersActive) StopRecorders();
                return;
            }
            if (!_recordersActive) StartRecorders();

            float dtMs = Time.unscaledDeltaTime * 1000f;
            if (dtMs <= 0.1f || dtMs > 500f) return;

            _totalFrames++;
            _sumMs += dtMs;
            if (_sampleCount < MaxSamples)
            {
                _frameMs[_sampleCount++] = dtMs;
            }

            if ((_totalFrames & 3) == 0) // sample profiler counters every 4th frame (~36 Hz)
            {
                _profSamples++;
                if (_mainThreadRec.Valid) _sumMainMs += _mainThreadRec.LastValue * 1e-6;
                if (_renderThreadRec.Valid) _sumRenderMs += _renderThreadRec.LastValue * 1e-6;
                if (_drawCallsRec.Valid) _sumDrawCalls += _drawCallsRec.LastValue;
                if (_setPassRec.Valid) _sumSetPass += _setPassRec.LastValue;
                if (_trisRec.Valid) _sumTris += _trisRec.LastValue;
            }
        }

        private void FlushStats()
        {
            var cfg = PluginConfig.Instance;
            if (cfg == null || !cfg.EnablePerfLogger) return;
            if (_sampleCount < 60) return;

            Array.Sort(_frameMs, 0, _sampleCount);
            double avgMs = _sumMs / _totalFrames;
            double avgFps = 1000.0 / avgMs;
            float p50 = _frameMs[(int)(_sampleCount * 0.50f)];
            float p95 = _frameMs[Mathf.Min(_sampleCount - 1, (int)(_sampleCount * 0.95f))];
            float p99 = _frameMs[Mathf.Min(_sampleCount - 1, (int)(_sampleCount * 0.99f))];
            double low1Fps = p99 > 0.01f ? 1000.0 / p99 : 0.0;

            int gc0 = GC.CollectionCount(0) - _gc0Start;
            int gc1 = GC.CollectionCount(1) - _gc1Start;
            int gc2 = GC.CollectionCount(2) - _gc2Start;

            string prof = "";
            if (_profSamples > 0)
            {
                double mMs = _sumMainMs / _profSamples;
                double rMs = _sumRenderMs / _profSamples;
                double dc = _sumDrawCalls / _profSamples;
                double sp = _sumSetPass / _profSamples;
                double tr = _sumTris / _profSamples;
                prof = $" | MainMs={mMs:F2} | RenderMs={rMs:F2} | DrawCalls={dc:F0} | SetPass={sp:F0} | Tris={tr:F0}";
            }

            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | Scene={_sceneName} | Mode={cfg.Mode} | Scale={cfg.RenderScale:F2} | Frames={_totalFrames} | AvgFPS={avgFps:F1} | 1%LowFPS={low1Fps:F1} | AvgMs={avgMs:F2} | P50={p50:F2} | P95={p95:F2} | P99={p99:F2} | GC={gc0}/{gc1}/{gc2}{prof}";
            Plugin.Log?.Info($"[Perf] {line}");

            if (!string.IsNullOrEmpty(_logPath))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_logPath));
                    File.AppendAllText(_logPath, line + Environment.NewLine);
                }
                catch { }
            }
        }
    }
}
