using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Interaction.Triggers;
using UnityEngine;

namespace Setus.HorrorFramework.Narrative.Gates
{
    [DisallowMultipleComponent]
    public sealed class NarrativeStoryBeatTrigger : MonoBehaviour
    {
        [SerializeField] private string triggerId;
        [SerializeField] private string storyBeatId;

        private HorrorGameContext context;
        private IDisposable triggerSubscription;

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
        }

        private void OnEnable()
        {
            if (context == null)
            {
                context = HorrorGameContext.Ensure();
            }

            triggerSubscription?.Dispose();
            triggerSubscription = context.Events.Subscribe<InteractionTriggerEntered>(OnTriggerEntered);
        }

        private void OnDisable()
        {
            triggerSubscription?.Dispose();
            triggerSubscription = null;
        }

        private void OnTriggerEntered(InteractionTriggerEntered entered)
        {
            if (!string.Equals(entered.TriggerId, triggerId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(storyBeatId))
            {
                return;
            }

            context.Narrative.TryConsumeStoryBeat(storyBeatId);
        }

        private void OnDrawGizmos()
        {
            if (HorrorGameContext.Active == null || !HorrorGameContext.Active.Debug.IsEnabled)
            {
                return;
            }

            Gizmos.color = new Color(0.95f, 0.7f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);
        }
    }
}
