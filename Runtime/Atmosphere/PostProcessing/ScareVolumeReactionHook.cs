using System;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Core.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace Setus.HorrorFramework.Atmosphere.PostProcessing
{
    [DisallowMultipleComponent]
    public sealed class ScareVolumeReactionHook : MonoBehaviour
    {
        [SerializeField] private string scareId;
        [SerializeField] private Volume volume;
        [SerializeField, Range(0f, 1f)] private float activeWeight = 0.65f;

        private HorrorGameContext context;
        private IDisposable subscription;
        private float baseWeight;

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
            if (volume != null)
            {
                baseWeight = volume.weight;
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
            ResetVolume();
        }

        private void OnScareStateChanged(ScareStateChanged changed)
        {
            if (!string.Equals(changed.ScareId, scareId, StringComparison.Ordinal) || volume == null)
            {
                return;
            }

            volume.weight = changed.State == ScareLifecycleState.Playing && !changed.Restored
                ? activeWeight
                : baseWeight;
        }

        private void ResetVolume()
        {
            if (volume != null)
            {
                volume.weight = baseWeight;
            }
        }
    }
}
