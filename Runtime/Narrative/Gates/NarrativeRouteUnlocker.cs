using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Narrative.Objectives;
using UnityEngine;

namespace Setus.HorrorFramework.Narrative.Gates
{
    [DisallowMultipleComponent]
    public sealed class NarrativeRouteUnlocker : MonoBehaviour
    {
        [SerializeField] private string completedObjectiveId;
        [SerializeField] private string routeId;

        private HorrorGameContext context;
        private IDisposable completionSubscription;

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

            completionSubscription?.Dispose();
            completionSubscription = context.Events.Subscribe<ObjectiveCompleted>(OnObjectiveCompleted);
        }

        private void OnDisable()
        {
            completionSubscription?.Dispose();
            completionSubscription = null;
        }

        private void OnObjectiveCompleted(ObjectiveCompleted completed)
        {
            if (string.Equals(completed.ObjectiveId, completedObjectiveId, StringComparison.Ordinal))
            {
                context.Narrative.TryUnlockRoute(routeId);
            }
        }
    }
}
