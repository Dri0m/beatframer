using System;
using BeatSaberMarkupLanguage.Settings;
using Zenject;

namespace BeatFramer.UI
{
    internal class BeatFramerMenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesTo<BeatFramerSettingsRegistrar>().AsSingle();
        }
    }

    internal class BeatFramerSettingsRegistrar : IInitializable, IDisposable
    {
        private readonly BSMLSettings _bsmlSettings;
        private readonly BeatFramerSettingsHost _host = new BeatFramerSettingsHost();

        public BeatFramerSettingsRegistrar(BSMLSettings bsmlSettings)
        {
            _bsmlSettings = bsmlSettings;
        }

        public void Initialize()
        {
            try
            {
                _bsmlSettings.AddSettingsMenu("BeatFramer", "BeatFramer.UI.SettingsView.bsml", _host);
            }
            catch (Exception ex)
            {
                Plugin.Log?.Warn($"[UI] Failed to register BSML settings menu: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try { _bsmlSettings?.RemoveSettingsMenu(_host); } catch { }
        }
    }
}
