using System;
using Setus.HorrorFramework.Core.Services;
using UnityEngine;

namespace Setus.HorrorFramework.Atmosphere.Scares
{
    [DisallowMultipleComponent]
    public sealed class ScareLifecyclePresentationDriver : MonoBehaviour
    {
        [SerializeField] private string scareId;

        private HorrorGameContext context;
        private IDisposable subscription;
        private float completeAt;

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
        }

        private void OnEnable()
        {
            context ??= HorrorGameContext.Ensure();
            subscription?.Dispose();
            subscription = context.Events.Subscribe<ScareStateChanged>(OnScareStateChanged);
            SynchronizeWithRuntimeState();
        }

        private void OnDisable()
        {
            context?.Scares.Interrupt(scareId);
            subscription?.Dispose();
            subscription = null;
            completeAt = 0f;
        }

        private void Update()
        {
            if (completeAt <= 0f || Time.unscaledTime < completeAt)
            {
                return;
            }

            completeAt = 0f;
            context.Scares.Complete(scareId);
        }

        private void OnScareStateChanged(ScareStateChanged changed)
        {
            if (!string.Equals(changed.ScareId, scareId, StringComparison.Ordinal))
            {
                return;
            }

            if (changed.State == ScareLifecycleState.Playing && !changed.Restored &&
                context.Scares.TryGetDefinition(scareId, out var definition))
            {
                completeAt = Time.unscaledTime + definition.PresentationDuration;
            }
            else if (changed.State != ScareLifecycleState.Triggered)
            {
                completeAt = 0f;
            }
        }

        private void SynchronizeWithRuntimeState()
        {
            if (context.Scares.TryGetState(scareId, out var state) &&
                (state == ScareLifecycleState.Triggered || state == ScareLifecycleState.Playing))
            {
                context.Scares.Interrupt(scareId);
            }
            else
            {
                completeAt = 0f;
            }
        }
    }
}
