using System;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Core.Services;
using UnityEngine;

namespace Setus.HorrorFramework.Atmosphere.Lighting
{
    [DisallowMultipleComponent]
    public sealed class ScareLightFlickerHook : MonoBehaviour
    {
        [SerializeField] private string scareId;
        [SerializeField] private Light[] affectedLights = Array.Empty<Light>();
        [SerializeField, Range(0f, 1f)] private float minimumIntensityMultiplier = 0.15f;
        [SerializeField, Min(0.1f)] private float flickerFrequency = 12f;

        private HorrorGameContext context;
        private IDisposable subscription;
        private float[] baseIntensities;
        private bool isPlaying;

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
            baseIntensities = new float[affectedLights.Length];
            for (var i = 0; i < affectedLights.Length; i++)
            {
                if (affectedLights[i] != null)
                {
                    baseIntensities[i] = affectedLights[i].intensity;
                }
            }
        }

        private void OnEnable()
        {
            context ??= HorrorGameContext.Ensure();
            subscription?.Dispose();
            subscription = context.Events.Subscribe<ScareStateChanged>(OnScareStateChanged);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
            ResetLights();
        }

        private void Update()
        {
            if (!isPlaying)
            {
                return;
            }

            var phase = Mathf.PingPong(Time.unscaledTime * flickerFrequency, 1f);
            var multiplier = Mathf.Lerp(minimumIntensityMultiplier, 1f, phase);
            for (var i = 0; i < affectedLights.Length; i++)
            {
                if (affectedLights[i] != null)
                {
                    affectedLights[i].intensity = baseIntensities[i] * multiplier;
                }
            }
        }

        private void OnScareStateChanged(ScareStateChanged changed)
        {
            if (!string.Equals(changed.ScareId, scareId, StringComparison.Ordinal))
            {
                return;
            }

            isPlaying = changed.State == ScareLifecycleState.Playing && !changed.Restored;
            if (!isPlaying)
            {
                ResetLights();
            }
        }

        private void ResetLights()
        {
            isPlaying = false;
            if (baseIntensities == null)
            {
                return;
            }

            for (var i = 0; i < affectedLights.Length; i++)
            {
                if (affectedLights[i] != null)
                {
                    affectedLights[i].intensity = baseIntensities[i];
                }
            }
        }
    }
}
