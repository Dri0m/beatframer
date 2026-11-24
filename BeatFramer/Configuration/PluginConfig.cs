using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace BeatFramer.Configuration
{
    public enum RenderMode
    {
        Stock = 0,
        Optimized = 1,
        LiteRenderer = 2
    }

    internal class PluginConfig
    {
        public static PluginConfig Instance { get; set; }

        // "Stock", "Optimized", or "LiteRenderer"
        public virtual string Mode { get; set; } = nameof(RenderMode.Optimized);

        // --- Stock-renderer optimizations (active in Optimized mode; also enforced in LiteRenderer) ---
        public virtual bool DisableDesktopMirror { get; set; } = true;
        public virtual bool DisableFloorMirror { get; set; } = true;
        public virtual bool DisableMainEffectPostProcess { get; set; } = true;
        public virtual bool DisableBloomPrePass { get; set; } = false;
        public virtual bool DownscaleBloomPrePass { get; set; } = true;
        public virtual bool DisableScreenGrabPasses { get; set; } = true;
        public virtual bool DisableBurnMarks { get; set; } = true;
        public virtual bool DisableNoteDebris { get; set; } = true;
        public virtual bool DisableCutParticles { get; set; } = false;
        public virtual bool DisableShockwaves { get; set; } = true;
        public virtual bool OptimizeSaberTrails { get; set; } = true;
        public virtual bool DisableGameplayGC { get; set; } = true;
        public virtual bool OptimizeUnityQuality { get; set; } = true;
        public virtual float CameraFarClip { get; set; } = 220f;
        public virtual float RenderScale { get; set; } = 1.0f;

        // --- Custom Lightweight Renderer options (active when Mode == "LiteRenderer") ---
        public virtual bool LiteEnvironmentLights { get; set; } = true;
        public virtual bool LiteSaberTrails { get; set; } = true;
        public virtual float LiteAmbientGlow { get; set; } = 0.85f;

        // --- Telemetry ---
        public virtual bool EnablePerfLogger { get; set; } = false;

        public RenderMode GetRenderMode()
        {
            if (Mode == nameof(RenderMode.LiteRenderer)) return RenderMode.LiteRenderer;
            if (Mode == nameof(RenderMode.Stock)) return RenderMode.Stock;
            return RenderMode.Optimized;
        }

        public bool IsOptimizedOrLite => GetRenderMode() != RenderMode.Stock;
        public bool IsLiteRenderer => GetRenderMode() == RenderMode.LiteRenderer;

        public virtual void Changed() { }
    }
}
