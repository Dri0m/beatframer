using System;
using System.Reflection;
using BeatFramer.Configuration;
using BeatFramer.Rendering;
using BeatFramer.Telemetry;
using BeatFramer.UI;
using HarmonyLib;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using SiraUtil.Zenject;
using UnityEngine;
using UnityEngine.SceneManagement;
using IPALogger = IPA.Logging.Logger;

namespace BeatFramer
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        public const string HarmonyId = "com.beatframer.perf";

        internal static IPALogger Log { get; private set; }
        private readonly Harmony _harmony;
        private GameObject _hostObject;

        [Init]
        public Plugin(IPALogger logger, Config conf, Zenjector zenjector)
        {
            Log = logger;
            PluginConfig.Instance = conf.Generated<PluginConfig>();
            _harmony = new Harmony(HarmonyId);

            zenjector.UseLogger(logger);
            zenjector.Install<BeatFramerMenuInstaller>(Location.Menu);

            Log.Info($"BeatFramer initialized (Mode={PluginConfig.Instance.Mode})");
        }

        [OnStart]
        public void OnApplicationStart()
        {
            try
            {
                _harmony.PatchAll(Assembly.GetExecutingAssembly());
                Log.Info("BeatFramer Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Log.Error($"BeatFramer failed to apply Harmony patches: {ex}");
            }

            _hostObject = new GameObject("BeatFramerHost");
            UnityEngine.Object.DontDestroyOnLoad(_hostObject);
            _hostObject.AddComponent<PerfMonitor>();
            _hostObject.AddComponent<LiteRendererController>();

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            QualityOptimizer.ApplyGlobalSettings("Startup");
        }

        [OnExit]
        public void OnApplicationQuit()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            try { _harmony.UnpatchSelf(); } catch { }
            if (_hostObject != null) UnityEngine.Object.Destroy(_hostObject);
        }

        private void OnActiveSceneChanged(Scene prevScene, Scene nextScene)
        {
            QualityOptimizer.ApplyGlobalSettings(nextScene.name);
        }
    }
}
