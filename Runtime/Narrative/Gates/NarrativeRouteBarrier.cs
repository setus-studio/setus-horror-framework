using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Narrative.Scenario;
using UnityEngine;

namespace Setus.HorrorFramework.Narrative.Gates
{
    [DisallowMultipleComponent]
    public sealed class NarrativeRouteBarrier : MonoBehaviour
    {
        [SerializeField] private string routeId;
        [SerializeField] private GameObject lockedVisual;

        private HorrorGameContext context;
        private IDisposable stateSubscription;

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

            stateSubscription?.Dispose();
            stateSubscription = context.Events.Subscribe<NarrativeStateChanged>(_ => Refresh());
            Refresh();
        }

        private void OnDisable()
        {
            stateSubscription?.Dispose();
            stateSubscription = null;
        }

        private void Refresh()
        {
            if (lockedVisual != null)
            {
                lockedVisual.SetActive(context?.Narrative?.IsRouteUnlocked(routeId) != true);
            }
        }

        private void OnDrawGizmos()
        {
            var activeContext = HorrorGameContext.Active;
            if (activeContext?.Debug.IsEnabled != true || activeContext.Narrative == null)
            {
                return;
            }

            Gizmos.color = activeContext.Narrative.IsRouteUnlocked(routeId)
                ? new Color(0.25f, 0.8f, 0.35f, 0.8f)
                : new Color(0.95f, 0.3f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);
        }
    }
}
