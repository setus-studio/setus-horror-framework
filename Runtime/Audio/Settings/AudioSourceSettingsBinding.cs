using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.UI.Settings;
using UnityEngine;

namespace Setus.HorrorFramework.Audio.Settings
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioSourceSettingsBinding : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioVolumeCategory category = AudioVolumeCategory.Sfx;

        private RuntimeSettingsModel settings;
        private float authoredVolume;

        private void Awake()
        {
            if (source == null)
            {
                source = GetComponent<AudioSource>();
            }

            authoredVolume = source != null ? source.volume : 1f;
        }

        private void OnEnable()
        {
            settings = HorrorGameContext.Ensure().RuntimeSettings;
            settings.Changed += Apply;
            Apply(settings.Current);
        }

        private void OnDisable()
        {
            if (settings != null)
            {
                settings.Changed -= Apply;
                settings = null;
            }
        }

        private void Apply(RuntimeSettingsState state)
        {
            if (source == null)
            {
                return;
            }

            var categoryVolume = category switch
            {
                AudioVolumeCategory.Ambience => state.AmbienceVolume,
                AudioVolumeCategory.Ui => state.UiVolume,
                AudioVolumeCategory.Voice => state.VoiceVolume,
                _ => state.SfxVolume
            };
            source.volume = authoredVolume * state.MasterVolume * categoryVolume;
        }
    }
}
