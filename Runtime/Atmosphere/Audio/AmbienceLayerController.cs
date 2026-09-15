using System;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Core.Services;
using UnityEngine;
using Setus.HorrorFramework.UI.Settings;

namespace Setus.HorrorFramework.Atmosphere.Audio
{
    [DisallowMultipleComponent]
    public sealed class AmbienceLayerController : MonoBehaviour
    {
        [SerializeField] private AudioSource lowTensionLayer;
        [SerializeField] private AudioSource highTensionLayer;
        [SerializeField, Range(0f, 1f)] private float lowLayerBaseVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float highLayerMaxVolume = 0.45f;

        private HorrorGameContext context;
        private IDisposable subscription;
        private RuntimeSettingsModel settings;
        private float currentTension;

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
        }

        private void OnEnable()
        {
            context ??= HorrorGameContext.Ensure();
            subscription?.Dispose();
            subscription = context.Events.Subscribe<TensionChanged>(Apply);
            settings = context.RuntimeSettings;
            settings.Changed += OnSettingsChanged;
            Apply(new TensionChanged(context.Tension.CurrentIntensity, string.Empty));
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
            if (settings != null)
            {
                settings.Changed -= OnSettingsChanged;
                settings = null;
            }
        }

        private void Apply(TensionChanged changed)
        {
            var intensity = Mathf.Clamp01(changed.Intensity);
            currentTension = intensity;
            var volumeScale = context.RuntimeSettings.Current.MasterVolume *
                context.RuntimeSettings.Current.AmbienceVolume;
            if (lowTensionLayer != null)
            {
                lowTensionLayer.volume = lowLayerBaseVolume * (1f - intensity * 0.65f) * volumeScale;
            }

            if (highTensionLayer != null)
            {
                highTensionLayer.volume = highLayerMaxVolume * intensity * volumeScale;
            }
        }

        private void OnSettingsChanged(RuntimeSettingsState _)
        {
            Apply(new TensionChanged(currentTension, string.Empty));
        }
    }
}
