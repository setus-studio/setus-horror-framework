using System;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;
using UnityEngine;
using Setus.HorrorFramework.UI.Settings;

namespace Setus.HorrorFramework.Atmosphere.Audio
{
    [DisallowMultipleComponent]
    public sealed class ScareAudioCueHook : MonoBehaviour
    {
        [SerializeField] private string scareId;
        [SerializeField] private AudioSource cueSource;

        private HorrorGameContext context;
        private IDisposable subscription;
        private bool missingCueReported;
        private RuntimeSettingsModel settings;
        private float authoredVolume = 1f;

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
            authoredVolume = cueSource != null ? cueSource.volume : 1f;
        }

        private void OnEnable()
        {
            context ??= HorrorGameContext.Ensure();
            subscription?.Dispose();
            subscription = context.Events.Subscribe<ScareStateChanged>(OnScareStateChanged);
            settings = context.RuntimeSettings;
            settings.Changed += ApplyVolume;
            ApplyVolume(settings.Current);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
            if (settings != null)
            {
                settings.Changed -= ApplyVolume;
                settings = null;
            }
            cueSource?.Stop();
        }

        private void OnScareStateChanged(ScareStateChanged changed)
        {
            if (!string.Equals(changed.ScareId, scareId, StringComparison.Ordinal))
            {
                return;
            }

            if (changed.State == ScareLifecycleState.Playing && !changed.Restored)
            {
                if (cueSource?.clip != null)
                {
                    cueSource.Play();
                }
                else if (!missingCueReported && context.Debug.IsEnabled)
                {
                    missingCueReported = true;
                    context.Logger.Warning(HorrorLogCategory.Atmosphere, $"Scare audio cue is unassigned: {scareId}");
                }
            }
            else if (changed.State == ScareLifecycleState.Consumed || changed.State == ScareLifecycleState.Restored)
            {
                cueSource?.Stop();
            }
        }

        private void ApplyVolume(RuntimeSettingsState state)
        {
            if (cueSource != null)
            {
                cueSource.volume = authoredVolume * state.MasterVolume * state.SfxVolume;
            }
        }
    }
}
