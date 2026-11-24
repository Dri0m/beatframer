using System;
using BeatFramer.Configuration;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.XR;

namespace BeatFramer.Rendering
{
    internal static class QualityOptimizer
    {
        private static bool _gcDisabledByUs;
        private static bool _savedAutoSyncTransforms = true;
        private static bool _physicsTunedByUs;

        public static void ApplyGlobalSettings(string reason)
        {
            var cfg = PluginConfig.Instance;
            if (cfg == null) return;

            try
            {
                bool optimized = cfg.IsOptimizedOrLite;
                bool isGameplay = reason == "GameCore" || (reason != null && reason.Contains("Gameplay"));

                // 1. Desktop mirror window presentation: stop blitting VR eyes to the 1024x576 desktop window
                if (optimized && cfg.DisableDesktopMirror)
                {
                    XRSettings.gameViewRenderMode = GameViewRenderMode.None;
                }
                else
                {
                    XRSettings.gameViewRenderMode = GameViewRenderMode.LeftEye;
                }

                // 2. Render viewport scale (dynamic/manual resolution scaling)
                float scale = optimized ? Mathf.Clamp(cfg.RenderScale, 0.5f, 1.4f) : 1.0f;
                if (Mathf.Abs(XRSettings.renderViewportScale - scale) > 0.005f)
                {
                    XRSettings.renderViewportScale = scale;
                }

                // 3. Unity QualitySettings tuned for Adreno 750 tile-based GPU
                if (optimized && cfg.OptimizeUnityQuality)
                {
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.pixelLightCount = 0;
                    QualitySettings.softParticles = false;
                    QualitySettings.realtimeReflectionProbes = false;
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                    QualitySettings.vSyncCount = 0;
                    QualitySettings.maxQueuedFrames = 1;
                    QualitySettings.skinWeights = SkinWeights.TwoBones;
                    Application.backgroundLoadingPriority = isGameplay ? ThreadPriority.Low : ThreadPriority.Normal;
                }

                // 4. Gameplay-only CPU optimizations (restored cleanly on menu return)
                if (optimized && isGameplay && cfg.OptimizeUnityQuality)
                {
                    if (!_physicsTunedByUs)
                    {
                        _savedAutoSyncTransforms = Physics.autoSyncTransforms;
                        _physicsTunedByUs = true;
                    }
                    Physics.autoSyncTransforms = false;
                }
                else if (_physicsTunedByUs)
                {
                    Physics.autoSyncTransforms = _savedAutoSyncTransforms;
                    _physicsTunedByUs = false;
                }

                // 5. Pause GC during gameplay to eliminate mid-song frame spikes; collect cleanly on exit
                if (optimized && isGameplay && cfg.DisableGameplayGC)
                {
                    if (!_gcDisabledByUs && GarbageCollector.GCMode != GarbageCollector.Mode.Disabled)
                    {
                        GarbageCollector.GCMode = GarbageCollector.Mode.Disabled;
                        _gcDisabledByUs = true;
                    }
                }
                else if (_gcDisabledByUs)
                {
                    GarbageCollector.GCMode = GarbageCollector.Mode.Enabled;
                    _gcDisabledByUs = false;
                    GC.Collect(0, GCCollectionMode.Optimized, false);
                }

                // 6. Main camera optimizations (DepthTexture off, tighter farClipPlane; keep allowHDR intact in Optimized so dot-note glow shaders stay bright white)
                var mainCam = Camera.main;
                if (mainCam != null && optimized && cfg.OptimizeUnityQuality)
                {
                    if (cfg.IsLiteRenderer)
                    {
                        mainCam.allowHDR = false;
                    }
                    mainCam.depthTextureMode = DepthTextureMode.None;
                    float clip = Mathf.Clamp(cfg.CameraFarClip, 60f, 5000f);
                    if (mainCam.farClipPlane > clip)
                    {
                        mainCam.farClipPlane = clip;
                    }
                }

                Plugin.Log?.Info($"[QualityOptimizer] Applied ({reason}): Mode={cfg.Mode}, GameView={XRSettings.gameViewRenderMode}, ViewportScale={XRSettings.renderViewportScale:F2}, GC={GarbageCollector.GCMode}, AutoSync={Physics.autoSyncTransforms}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.Warn($"[QualityOptimizer] Error applying settings ({reason}): {ex.Message}");
            }
        }
    }
}
