using System;
using System.Reflection;
using BeatFramer.Configuration;
using BeatFramer.Rendering;
using HarmonyLib;
using UnityEngine;

namespace BeatFramer.Patches
{
    [HarmonyPatch]
    internal static class MirrorRendererTexturePatch
    {
        static bool Prepare() => AccessTools.Method(typeof(MirrorRendererSO), "RenderMirrorTexture", new[] { typeof(Camera), typeof(Vector3), typeof(Vector3) }) != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(MirrorRendererSO), "RenderMirrorTexture", new[] { typeof(Camera), typeof(Vector3), typeof(Vector3) });

        static bool Prefix(ref Texture __result)
        {
            var cfg = PluginConfig.Instance;
            if (cfg != null && (cfg.IsLiteRenderer || (cfg.IsOptimizedOrLite && cfg.DisableFloorMirror)))
            {
                // Returning null causes Mirror.RenderMirror to take Beat Saber's native no-mirror path:
                // swaps _renderer.sharedMaterial to _noMirrorMaterial (dark/black floor) and skips rendering.
                __result = null;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    internal static class MainEffectPreRenderPatch
    {
        private static readonly FieldInfo ImageEffectField = AccessTools.Field(typeof(MainEffectController), "_imageEffectController");

        static bool Prepare() => AccessTools.Method(typeof(MainEffectController), "OnPreRender") != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(MainEffectController), "OnPreRender");

        static bool Prefix(MainEffectController __instance)
        {
            var cfg = PluginConfig.Instance;
            bool skip = cfg != null && (cfg.IsLiteRenderer || (cfg.IsOptimizedOrLite && cfg.DisableMainEffectPostProcess));
            if (skip)
            {
                // Preserve Beat Saber's real _baseColorBoost / _baseColorBoostThreshold shader globals
                // (required for dot-note NoteCircleGlow to render bright white when bloom is off).
                var mainEffect = __instance.container?.mainEffect;
                if (mainEffect != null)
                {
                    mainEffect.PreRender();
                }
                MainEffectCore.UpdateKeyword(false);
                if (ImageEffectField != null)
                {
                    var iec = ImageEffectField.GetValue(__instance) as Behaviour;
                    if (iec != null && iec.enabled)
                    {
                        iec.enabled = false;
                    }
                }
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch]
    internal static class BloomPrePassPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(BloomPrePass), "OnPreRender") != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(BloomPrePass), "OnPreRender");

        static bool Prefix()
        {
            var cfg = PluginConfig.Instance;
            return cfg == null || !(cfg.IsLiteRenderer || (cfg.IsOptimizedOrLite && cfg.DisableBloomPrePass));
        }
    }

    [HarmonyPatch]
    internal static class BloomPrePassWidthPatch
    {
        static bool Prepare() => AccessTools.PropertyGetter(typeof(BloomPrePassEffectSO), "textureWidth") != null;
        static MethodBase TargetMethod() => AccessTools.PropertyGetter(typeof(BloomPrePassEffectSO), "textureWidth");

        static void Postfix(ref int __result)
        {
            var cfg = PluginConfig.Instance;
            if (cfg != null && cfg.IsOptimizedOrLite && !cfg.DisableBloomPrePass && cfg.DownscaleBloomPrePass)
            {
                __result = Mathf.Max(64, __result / 2);
            }
        }
    }

    [HarmonyPatch]
    internal static class BloomPrePassHeightPatch
    {
        static bool Prepare() => AccessTools.PropertyGetter(typeof(BloomPrePassEffectSO), "textureHeight") != null;
        static MethodBase TargetMethod() => AccessTools.PropertyGetter(typeof(BloomPrePassEffectSO), "textureHeight");

        static void Postfix(ref int __result)
        {
            var cfg = PluginConfig.Instance;
            if (cfg != null && cfg.IsOptimizedOrLite && !cfg.DisableBloomPrePass && cfg.DownscaleBloomPrePass)
            {
                __result = Mathf.Max(64, __result / 2);
            }
        }
    }

    [HarmonyPatch]
    internal static class SaberTrailOptimizePatch
    {
        private static readonly FieldInfo GranularityField = AccessTools.Field(typeof(SaberTrail), "_granularity");
        private static readonly FieldInfo SamplingFreqField = AccessTools.Field(typeof(SaberTrail), "_samplingFrequency");

        static bool Prepare() => AccessTools.Method(typeof(SaberTrail), "Init") != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(SaberTrail), "Init");

        static void Prefix(SaberTrail __instance)
        {
            var cfg = PluginConfig.Instance;
            if (cfg == null || !cfg.IsOptimizedOrLite || !cfg.OptimizeSaberTrails) return;

            if (GranularityField != null)
            {
                int g = (int)GranularityField.GetValue(__instance);
                if (g > 24) GranularityField.SetValue(__instance, 24);
            }
            if (SamplingFreqField != null)
            {
                int sf = (int)SamplingFreqField.GetValue(__instance);
                if (sf > 60) SamplingFreqField.SetValue(__instance, 60);
            }
        }
    }

    [HarmonyPatch]
    internal static class CommandBufferGrabPassPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(CommandBufferGOCore), "OnWillRenderObject") != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(CommandBufferGOCore), "OnWillRenderObject");

        static bool Prefix()
        {
            var cfg = PluginConfig.Instance;
            return cfg == null || !(cfg.IsLiteRenderer || (cfg.IsOptimizedOrLite && cfg.DisableScreenGrabPasses));
        }
    }

    [HarmonyPatch]
    internal static class SaberBurnMarkAreaPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(SaberBurnMarkArea), "LateUpdate") != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(SaberBurnMarkArea), "LateUpdate");

        static bool Prefix()
        {
            var cfg = PluginConfig.Instance;
            return cfg == null || !(cfg.IsLiteRenderer || (cfg.IsOptimizedOrLite && cfg.DisableBurnMarks));
        }
    }

    [HarmonyPatch]
    internal static class SaberBurnMarkSparklesPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(SaberBurnMarkSparkles), "LateUpdate") != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(SaberBurnMarkSparkles), "LateUpdate");

        static bool Prefix()
        {
            var cfg = PluginConfig.Instance;
            return cfg == null || !(cfg.IsLiteRenderer || (cfg.IsOptimizedOrLite && cfg.DisableBurnMarks));
        }
    }

    [HarmonyPatch]
    internal static class NoteDebrisSpawnerPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(NoteDebrisSpawner), "SpawnDebris") != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(NoteDebrisSpawner), "SpawnDebris");

        static bool Prefix()
        {
            var cfg = PluginConfig.Instance;
            return cfg == null || !(cfg.IsLiteRenderer || (cfg.IsOptimizedOrLite && cfg.DisableNoteDebris));
        }
    }

    [HarmonyPatch]
    internal static class NoteCutParticlesPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(NoteCutParticlesEffect), "SpawnParticles") != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(NoteCutParticlesEffect), "SpawnParticles");

        static bool Prefix()
        {
            var cfg = PluginConfig.Instance;
            return cfg == null || !(cfg.IsLiteRenderer || (cfg.IsOptimizedOrLite && cfg.DisableCutParticles));
        }
    }

    [HarmonyPatch]
    internal static class ShockwaveEffectPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(ShockwaveEffect), "SpawnShockwave") != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(ShockwaveEffect), "SpawnShockwave");

        static bool Prefix()
        {
            var cfg = PluginConfig.Instance;
            return cfg == null || !(cfg.IsLiteRenderer || (cfg.IsOptimizedOrLite && cfg.DisableShockwaves));
        }
    }

    [HarmonyPatch]
    internal static class LightWithIdFeedPatch
    {
        static bool Prepare() => AccessTools.Method(typeof(LightWithIdManager), "SetColorForId", new[] { typeof(int), typeof(Color) }) != null;
        static MethodBase TargetMethod() => AccessTools.Method(typeof(LightWithIdManager), "SetColorForId", new[] { typeof(int), typeof(Color) });

        static void Postfix(int lightId, Color color)
        {
            LiteRendererController.OnLightColorChanged(lightId, color);
        }
    }
}
