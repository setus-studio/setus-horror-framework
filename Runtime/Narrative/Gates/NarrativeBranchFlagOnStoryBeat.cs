using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Narrative.Scenario;
using UnityEngine;

namespace Setus.HorrorFramework.Narrative.Gates
{
    [DisallowMultipleComponent]
    public sealed class NarrativeBranchFlagOnStoryBeat : MonoBehaviour
    {
        [SerializeField] private string storyBeatId;
        [SerializeField] private string flagId;
        [SerializeField] private bool value = true;

        private HorrorGameContext context;
        private IDisposable subscription;

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

            subscription?.Dispose();
            subscription = context.Events.Subscribe<NarrativeStoryBeatConsumed>(OnStoryBeatConsumed);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
        }

        private void OnStoryBeatConsumed(NarrativeStoryBeatConsumed consumed)
        {
            if (string.Equals(consumed.StoryBeatId, storyBeatId, StringComparison.Ordinal))
            {
                context.Narrative.SetBranchFlag(flagId, value);
            }
        }
    }
}
