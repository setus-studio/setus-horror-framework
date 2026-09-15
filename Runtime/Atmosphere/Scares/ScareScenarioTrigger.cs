using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.Interaction.Triggers;
using UnityEngine;

namespace Setus.HorrorFramework.Atmosphere.Scares
{
    [DisallowMultipleComponent]
    public sealed class ScareScenarioTrigger : MonoBehaviour
    {
        [SerializeField] private string triggerId;
        [SerializeField] private string scareId;

        private HorrorGameContext context;
        private IDisposable subscription;
        private bool invalidReferenceReported;

        public string TriggerId => triggerId;
        public string ScareId => scareId;

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
        }

        private void OnEnable()
        {
            context ??= HorrorGameContext.Ensure();
            invalidReferenceReported = false;
            subscription?.Dispose();
            subscription = context.Events.Subscribe<InteractionTriggerEntered>(OnTriggerEntered);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
        }

        private void OnTriggerEntered(InteractionTriggerEntered entered)
        {
            if (string.Equals(entered.TriggerId, triggerId, StringComparison.Ordinal))
            {
                if (!context.Scares.TryGetDefinition(scareId, out _))
                {
                    ReportInvalidScareReference();
                    return;
                }

                context.Scares.TryTrigger(scareId);
            }
        }

        private void ReportInvalidScareReference()
        {
            if (invalidReferenceReported || context?.Debug.IsEnabled != true)
            {
                return;
            }

            invalidReferenceReported = true;
            context.Logger.Warning(
                HorrorLogCategory.Atmosphere,
                $"ScareScenarioTrigger '{name}' references scareId '{scareId}', but no active scene catalog registers it.");
        }

        private void OnDrawGizmos()
        {
            if (HorrorGameContext.Active == null || !HorrorGameContext.Active.Debug.IsEnabled)
            {
                return;
            }

            Gizmos.color = new Color(0.95f, 0.25f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);
        }
    }
}
