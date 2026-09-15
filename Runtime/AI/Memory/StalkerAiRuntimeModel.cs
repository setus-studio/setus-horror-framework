using System;
using Setus.HorrorFramework.AI.Perception;
using Setus.HorrorFramework.AI.States;
using Setus.HorrorFramework.AI.Tuning;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.SaveProgression.Registry;
using UnityEngine;

namespace Setus.HorrorFramework.AI.Memory
{
    public sealed class StalkerAiRuntimeModel : RuntimeStateOwnerBase<StalkerAiRuntimeState>, IRestoreStateValidator, IRuntimeRollbackOwner
    {
        public const string StateKeyValue = "ai.stalker-state";

        private readonly IGameplayEventBus events;
        private readonly string stableId;
        private StalkerAiTuningProfile profile;
        private int patrolPointCount;
        private StalkerAiState state = StalkerAiState.Disabled;
        private float suspicion;
        private Vector3 lastKnownPosition;
        private int patrolPointIndex;
        private float lostSightRemaining;
        private float searchRemaining;
        private bool chaseAnticipationActive;
        private float chaseAnticipationRemaining;

        public StalkerAiRuntimeModel(string stableId, IGameplayEventBus events)
            : base(StateKeyValue)
        {
            this.stableId = stableId ?? string.Empty;
            this.events = events;
        }

        public StalkerAiState State => state;
        public float Suspicion => suspicion;
        public Vector3 LastKnownPosition => lastKnownPosition;
        public int PatrolPointIndex => patrolPointIndex;
        public bool HasActiveState => state == StalkerAiState.Searching || state == StalkerAiState.Chasing;
        public bool IsChaseAnticipating => chaseAnticipationActive;
        public float ChaseAnticipationRemaining => chaseAnticipationRemaining;

        public void Configure(StalkerAiTuningProfile tuningProfile, int routePointCount)
        {
            profile = tuningProfile;
            patrolPointCount = Mathf.Max(0, routePointCount);
            patrolPointIndex = patrolPointCount == 0 ? 0 : Mathf.Clamp(patrolPointIndex, 0, patrolPointCount - 1);
            if (state == StalkerAiState.Disabled && profile != null)
            {
                TransitionTo(StalkerAiState.Patrol, StalkerAiTransitionReason.Initialized, false);
            }
        }

        public void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                CancelChaseAnticipation();
                TransitionTo(StalkerAiState.Disabled, StalkerAiTransitionReason.DisabledByOwner, false);
                return;
            }

            if (profile != null && state == StalkerAiState.Disabled)
            {
                TransitionTo(StalkerAiState.Patrol, StalkerAiTransitionReason.Initialized, false);
            }
        }

        public void Tick(float deltaTime, StalkerPerceptionInput perception)
        {
            if (profile == null || state == StalkerAiState.Disabled || deltaTime <= 0f)
            {
                return;
            }

            if (perception.Sight.IsConfirmed)
            {
                lastKnownPosition = perception.Sight.TargetPosition;
                suspicion = 1f;
                lostSightRemaining = profile.LostSightDelay;
                if (state == StalkerAiState.Chasing)
                {
                    return;
                }

                if (!chaseAnticipationActive)
                {
                    BeginChaseAnticipation();
                    return;
                }

                chaseAnticipationRemaining = Mathf.Max(0f, chaseAnticipationRemaining - deltaTime);
                if (chaseAnticipationRemaining <= 0f)
                {
                    CancelChaseAnticipation();
                    TransitionTo(StalkerAiState.Chasing, StalkerAiTransitionReason.ChaseAnticipationElapsed, false);
                }

                return;
            }

            CancelChaseAnticipation();
            if (perception.ProximityAware)
            {
                RegisterAwareness(
                    perception.ProximityTargetPosition,
                    StalkerAiTransitionReason.ProximityAwareness,
                    deltaTime);
            }
            else
            {
                suspicion = Mathf.Max(0f, suspicion - profile.SuspicionDecayPerSecond * deltaTime);
            }

            if (state == StalkerAiState.Chasing)
            {
                lostSightRemaining = Mathf.Max(0f, lostSightRemaining - deltaTime);
                if (lostSightRemaining <= 0f)
                {
                    BeginSearch(StalkerAiTransitionReason.LostSightDelayElapsed);
                }

                return;
            }

            if (state == StalkerAiState.Suspicious)
            {
                if (suspicion >= profile.SearchThreshold)
                {
                    BeginSearch(StalkerAiTransitionReason.SuspicionThresholdReached);
                }
                else if (suspicion <= 0f)
                {
                    TransitionTo(StalkerAiState.Patrol, StalkerAiTransitionReason.SuspicionExpired, false);
                }

                return;
            }

            if (state == StalkerAiState.Searching)
            {
                searchRemaining = Mathf.Max(0f, searchRemaining - deltaTime);
                if (searchRemaining <= 0f)
                {
                    suspicion = 0f;
                    TransitionTo(StalkerAiState.Patrol, StalkerAiTransitionReason.SearchExpired, false);
                }
            }
        }

        public void Hear(Vector3 position, float normalizedLoudness)
        {
            if (profile == null || state == StalkerAiState.Disabled || normalizedLoudness <= 0f)
            {
                return;
            }

            RegisterAwareness(position, StalkerAiTransitionReason.HeardTarget, normalizedLoudness);
        }

        public void NotifyPatrolPointReached()
        {
            if (state != StalkerAiState.Patrol || patrolPointCount <= 0)
            {
                return;
            }

            patrolPointIndex = (patrolPointIndex + 1) % patrolPointCount;
            PublishTransition(state, state, StalkerAiTransitionReason.PatrolPointReached, false);
        }

        public override StalkerAiRuntimeState CaptureState()
        {
            return new StalkerAiRuntimeState(
                state,
                suspicion,
                lastKnownPosition,
                patrolPointIndex,
                lostSightRemaining,
                searchRemaining);
        }

        public Action CaptureRollbackAction()
        {
            var captured = CaptureState();
            var anticipating = chaseAnticipationActive;
            var anticipationRemaining = chaseAnticipationRemaining;
            return () =>
            {
                state = captured.State;
                suspicion = captured.Suspicion;
                lastKnownPosition = captured.LastKnownPosition;
                patrolPointIndex = captured.PatrolPointIndex;
                lostSightRemaining = captured.LostSightRemaining;
                searchRemaining = captured.SearchRemaining;
                chaseAnticipationActive = anticipating;
                chaseAnticipationRemaining = anticipationRemaining;
            };
        }

        public override void RestoreState(StalkerAiRuntimeState restoredState)
        {
            var validation = ValidateRestoreState(restoredState);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.Reason, nameof(restoredState));
            }

            suspicion = Mathf.Clamp01(restoredState.Suspicion);
            lastKnownPosition = restoredState.LastKnownPosition;
            patrolPointIndex = patrolPointCount == 0 ? 0 : Mathf.Clamp(restoredState.PatrolPointIndex, 0, patrolPointCount - 1);
            lostSightRemaining = Mathf.Max(0f, restoredState.LostSightRemaining);
            searchRemaining = Mathf.Max(0f, restoredState.SearchRemaining);
            CancelChaseAnticipation();

            // A target reference is scene-bound. A restored chase therefore becomes a deterministic
            // search at the captured last known position instead of chasing without confirmed sight.
            var restoredLifecycle = restoredState.State == StalkerAiState.Chasing
                ? StalkerAiState.Searching
                : restoredState.State;
            if (restoredLifecycle == StalkerAiState.Searching && searchRemaining <= 0f && profile != null)
            {
                searchRemaining = profile.SearchDuration;
            }

            TransitionTo(restoredLifecycle, StalkerAiTransitionReason.Restored, true);
        }

        public RestoreStateValidationResult ValidateRestoreState(object candidate)
        {
            if (!(candidate is StalkerAiRuntimeState stateCandidate))
            {
                return RestoreStateValidationResult.Invalid("Expected StalkerAiRuntimeState.");
            }

            if (!Enum.IsDefined(typeof(StalkerAiState), stateCandidate.State))
            {
                return RestoreStateValidationResult.Invalid("Saved stalker state is unsupported.");
            }

            if (float.IsNaN(stateCandidate.Suspicion) || float.IsInfinity(stateCandidate.Suspicion) ||
                stateCandidate.Suspicion < 0f || stateCandidate.Suspicion > 1f)
            {
                return RestoreStateValidationResult.Invalid("Saved stalker suspicion must be between zero and one.");
            }

            if (!IsFinite(stateCandidate.LastKnownPosition) ||
                !IsFinite(stateCandidate.LostSightRemaining) ||
                !IsFinite(stateCandidate.SearchRemaining) ||
                stateCandidate.LostSightRemaining < 0f || stateCandidate.SearchRemaining < 0f ||
                stateCandidate.PatrolPointIndex < 0)
            {
                return RestoreStateValidationResult.Invalid("Saved stalker state contains invalid navigation or timer data.");
            }

            return RestoreStateValidationResult.Success;
        }

        private void RegisterAwareness(Vector3 position, StalkerAiTransitionReason reason, float awarenessAmount)
        {
            if (profile == null || awarenessAmount <= 0f)
            {
                return;
            }

            lastKnownPosition = position;
            suspicion = Mathf.Clamp01(suspicion + profile.SuspicionGainPerSecond * awarenessAmount);
            if (state == StalkerAiState.Patrol)
            {
                TransitionTo(StalkerAiState.Suspicious, reason, false);
            }
        }

        private void BeginSearch(StalkerAiTransitionReason reason)
        {
            searchRemaining = profile != null ? profile.SearchDuration : 0f;
            lostSightRemaining = 0f;
            TransitionTo(StalkerAiState.Searching, reason, false);
        }

        private void BeginChaseAnticipation()
        {
            chaseAnticipationActive = true;
            chaseAnticipationRemaining = Mathf.Max(0.05f, profile.ChaseAnticipationDuration);
            events?.Publish(new StalkerAiHighStakesFeedbackRequested(stableId, profile.LocalizedChaseSubtitle));
            if (state == StalkerAiState.Patrol)
            {
                TransitionTo(StalkerAiState.Suspicious, StalkerAiTransitionReason.ChaseAnticipationStarted, false);
            }
        }

        private void CancelChaseAnticipation()
        {
            chaseAnticipationActive = false;
            chaseAnticipationRemaining = 0f;
        }

        private void TransitionTo(StalkerAiState next, StalkerAiTransitionReason reason, bool restored)
        {
            var previous = state;
            if (previous == next && reason != StalkerAiTransitionReason.PatrolPointReached)
            {
                return;
            }

            state = next;
            PublishTransition(previous, next, reason, restored);
        }

        private void PublishTransition(
            StalkerAiState previous,
            StalkerAiState next,
            StalkerAiTransitionReason reason,
            bool restored)
        {
            events?.Publish(new StalkerAiStateChanged(stableId, previous, next, reason, lastKnownPosition, restored));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
