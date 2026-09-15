using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.Core.Services;
using UnityEngine;

namespace Setus.HorrorFramework.AI.Debugging
{
    [DisallowMultipleComponent]
    public sealed class StalkerAiDebugView : MonoBehaviour
    {
        [SerializeField] private StalkerAiController stalker;
        [SerializeField] private Vector2 screenOffset = new Vector2(12f, 48f);

        private void Awake()
        {
            if (stalker == null)
            {
                stalker = GetComponent<StalkerAiController>();
            }
        }

        private void OnGUI()
        {
            if (stalker == null || HorrorGameContext.Active?.Debug.IsEnabled != true)
            {
                return;
            }

            var blocker = stalker.LastSight.Blocker;
            var blockerLayer = blocker != null ? LayerMask.LayerToName(blocker.gameObject.layer) : "none";
            var label = $"AI {stalker.StableId}\n" +
                        $"State: {stalker.State} ({stalker.LastTransitionReason})\n" +
                        $"Chase cue: {(stalker.IsChaseAnticipating ? $"waiting {stalker.ChaseAnticipationRemaining:0.00}s" : "inactive")}\n" +
                        $"Target: {(stalker.Target != null ? stalker.Target.name : "none")}\n" +
                        $"Sight: {stalker.LastSight.Reason}\n" +
                        $"Blocker: {(blocker != null ? blocker.name : "none")} [{blockerLayer}]\n" +
                        $"Nav: {(string.IsNullOrEmpty(stalker.NavigationDiagnostic) ? "ready" : stalker.NavigationDiagnostic)}";
            GUI.Label(new Rect(screenOffset.x, screenOffset.y, 420f, 150f), label);
        }
    }
}
