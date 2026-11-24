using System.Collections.Generic;
using BeatFramer.Configuration;
using BeatFramer.Rendering;
using BeatSaberMarkupLanguage.Attributes;

namespace BeatFramer.UI
{
    internal class BeatFramerSettingsHost
    {
        [UIValue("mode-options")]
        public List<object> ModeOptions => new List<object> { "Optimized", "LiteRenderer", "Stock" };

        [UIValue("mode")]
        public string Mode
        {
            get => PluginConfig.Instance.Mode;
            set
            {
                PluginConfig.Instance.Mode = value;
                QualityOptimizer.ApplyGlobalSettings("UI.Mode");
            }
        }

        [UIValue("disable-desktop-mirror")]
        public bool DisableDesktopMirror
        {
            get => PluginConfig.Instance.DisableDesktopMirror;
            set { PluginConfig.Instance.DisableDesktopMirror = value; QualityOptimizer.ApplyGlobalSettings("UI"); }
        }

        [UIValue("disable-floor-mirror")]
        public bool DisableFloorMirror
        {
            get => PluginConfig.Instance.DisableFloorMirror;
            set => PluginConfig.Instance.DisableFloorMirror = value;
        }

        [UIValue("disable-post-process")]
        public bool DisableMainEffectPostProcess
        {
            get => PluginConfig.Instance.DisableMainEffectPostProcess;
            set { PluginConfig.Instance.DisableMainEffectPostProcess = value; QualityOptimizer.ApplyGlobalSettings("UI"); }
        }

        [UIValue("disable-bloom-prepass")]
        public bool DisableBloomPrePass
        {
            get => PluginConfig.Instance.DisableBloomPrePass;
            set => PluginConfig.Instance.DisableBloomPrePass = value;
        }

        [UIValue("downscale-bloom-prepass")]
        public bool DownscaleBloomPrePass
        {
            get => PluginConfig.Instance.DownscaleBloomPrePass;
            set => PluginConfig.Instance.DownscaleBloomPrePass = value;
        }

        [UIValue("optimize-saber-trails")]
        public bool OptimizeSaberTrails
        {
            get => PluginConfig.Instance.OptimizeSaberTrails;
            set => PluginConfig.Instance.OptimizeSaberTrails = value;
        }

        [UIValue("disable-gameplay-gc")]
        public bool DisableGameplayGC
        {
            get => PluginConfig.Instance.DisableGameplayGC;
            set => PluginConfig.Instance.DisableGameplayGC = value;
        }

        [UIValue("disable-grab-passes")]
        public bool DisableScreenGrabPasses
        {
            get => PluginConfig.Instance.DisableScreenGrabPasses;
            set => PluginConfig.Instance.DisableScreenGrabPasses = value;
        }

        [UIValue("disable-burn-marks")]
        public bool DisableBurnMarks
        {
            get => PluginConfig.Instance.DisableBurnMarks;
            set => PluginConfig.Instance.DisableBurnMarks = value;
        }

        [UIValue("disable-debris")]
        public bool DisableNoteDebris
        {
            get => PluginConfig.Instance.DisableNoteDebris;
            set => PluginConfig.Instance.DisableNoteDebris = value;
        }

        [UIValue("disable-cut-particles")]
        public bool DisableCutParticles
        {
            get => PluginConfig.Instance.DisableCutParticles;
            set => PluginConfig.Instance.DisableCutParticles = value;
        }

        [UIValue("render-scale")]
        public float RenderScale
        {
            get => PluginConfig.Instance.RenderScale;
            set { PluginConfig.Instance.RenderScale = value; QualityOptimizer.ApplyGlobalSettings("UI.RenderScale"); }
        }

        [UIValue("lite-env-lights")]
        public bool LiteEnvironmentLights
        {
            get => PluginConfig.Instance.LiteEnvironmentLights;
            set => PluginConfig.Instance.LiteEnvironmentLights = value;
        }

        [UIValue("lite-saber-trails")]
        public bool LiteSaberTrails
        {
            get => PluginConfig.Instance.LiteSaberTrails;
            set => PluginConfig.Instance.LiteSaberTrails = value;
        }

        [UIValue("enable-perf-logger")]
        public bool EnablePerfLogger
        {
            get => PluginConfig.Instance.EnablePerfLogger;
            set => PluginConfig.Instance.EnablePerfLogger = value;
        }
    }
}
