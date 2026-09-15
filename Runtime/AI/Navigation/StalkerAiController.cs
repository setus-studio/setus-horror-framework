using System;
using Setus.HorrorFramework.AI.Memory;
using Setus.HorrorFramework.AI.Perception;
using Setus.HorrorFramework.AI.States;
using Setus.HorrorFramework.AI.Tuning;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEngine;
using UnityEngine.AI;

namespace Setus.HorrorFramework.AI.Navigation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class StalkerAiController : MonoBehaviour
    {
        [SerializeField] private StalkerAiTuningProfile tuningProfile;
        [SerializeField] private StalkerPatrolRoute patrolRoute;
        [SerializeField] private Transform target;
        [SerializeField] private Transform eye;
        [SerializeField] private StableId stableId;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private LayerMask targetMask;
        [SerializeField] private LayerMask occluderMask;

        private readonly StalkerPerceptionSensor perceptionSensor = new StalkerPerceptionSensor();
        private StalkerAiRuntimeModel model;
        private IDisposable footstepSubscription;
        private IDisposable stateSubscription;
        private float perceptionElapsed;
        private Vector3 lastDestination;
        private bool hasDestination;
        private StalkerSightResult lastSight;
        private StalkerAiTransitionReason lastTransitionReason;
        private string navigationDiagnostic = string.Empty;

        public string StableId => stableId != null ? stableId.Id : string.Empty;
        public string StateKey => StalkerAiRuntimeModel.StateKeyValue;
        public Type StateType => typeof(StalkerAiRuntimeState);
        public StalkerAiState State => model != null ? model.State : StalkerAiState.Disabled;
        public StalkerAiTransitionReason LastTransitionReason => lastTransitionReason;
        public StalkerSightResult LastSight => lastSight;
        public Transform Target => target;
        public StalkerAiTuningProfile TuningProfile => tuningProfile;
        public string NavigationDiagnostic => navigationDiagnostic;
        public bool IsInitialized => model != null;
        public bool HasActiveState => model != null && model.HasActiveState;
        public bool IsChaseAnticipating => model != null && model.IsChaseAnticipating;
        public float ChaseAnticipationRemaining => model != null ? model.ChaseAnticipationRemaining : 0f;

        private void Awake()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (eye == null)
            {
                eye = transform;
            }

            if (stableId == null)
            {
                stableId = GetComponent<StableId>();
            }

            if (target == null)
            {
                var player = FindFirstObjectByType<FirstPersonPlayerController>(FindObjectsInactive.Include);
                target = player != null ? player.transform : null;
            }

            if (stableId == null || !stableId.HasStableId)
            {
                enabled = false;
                Debug.LogError(
                    $"Stalker AI '{name}' requires an authored, non-empty StableId.",
                    this);
                return;
            }

            if (tuningProfile == null)
            {
                enabled = false;
                Debug.LogError(
                    $"Stalker AI '{name}' requires an assigned StalkerAiTuningProfile. " +
                    "Run Setus > Horror Framework > AI > Apply M8 Enemy AI Foundation, then verify the controller reference.",
                    this);
                return;
            }

            var context = HorrorGameContext.Ensure();
            model = new StalkerAiRuntimeModel(stableId.Id, context.Events);
            model.Configure(tuningProfile, patrolRoute != null ? patrolRoute.Count : 0);
            ApplySpeedForState();
        }

        private void OnEnable()
        {
            var context = HorrorGameContext.Active;
            if (context == null || model == null)
            {
                return;
            }

            footstepSubscription = context.Events.Subscribe<PlayerFootstepEmitted>(OnPlayerFootstep);
            stateSubscription = context.Events.Subscribe<StalkerAiStateChanged>(OnStateChanged);
        }

        private void OnDisable()
        {
            footstepSubscription?.Dispose();
            footstepSubscription = null;
            stateSubscription?.Dispose();
            stateSubscription = null;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            hasDestination = false;
        }

        private void Update()
        {
            if (model == null || tuningProfile == null)
            {
                return;
            }

            perceptionElapsed += Time.deltaTime;
            if (perceptionElapsed >= tuningProfile.PerceptionInterval)
            {
                perceptionElapsed = 0f;
                lastSight = perceptionSensor.EvaluateSight(
                    transform,
                    target,
                    eye != null ? eye.position : transform.position,
                    targetMask,
                    occluderMask,
                    tuningProfile);
            }

            var proximityAware = perceptionSensor.IsWithinProximity(transform.position, target, tuningProfile);
            var input = new StalkerPerceptionInput(
                lastSight,
                proximityAware,
                proximityAware && target != null ? target.position : Vector3.zero);
            model.Tick(Time.deltaTime, input);
            UpdateNavigation();
        }

        public StalkerAiRuntimeState CaptureState()
        {
            if (model == null)
            {
                throw new InvalidOperationException("Stalker AI has not initialized its runtime model.");
            }

            return model.CaptureState();
        }

        public Action CaptureRollbackAction()
        {
            if (model == null)
            {
                throw new InvalidOperationException("Stalker AI has not initialized its runtime model.");
            }

            var restoreModel = model.CaptureRollbackAction();
            var previousReason = lastTransitionReason;
            var previousHasDestination = hasDestination;
            var previousDestination = lastDestination;
            return () =>
            {
                restoreModel();
                lastTransitionReason = previousReason;
                hasDestination = previousHasDestination;
                lastDestination = previousDestination;
                ApplySpeedForState();
            };
        }

        public void RestoreState(StalkerAiRuntimeState state)
        {
            if (model == null)
            {
                throw new InvalidOperationException("Stalker AI has not initialized its runtime model.");
            }

            model.RestoreState(state);
            ApplySpeedForState();
            hasDestination = false;
        }

        public RestoreStateValidationResult ValidateRestoreState(object state)
        {
            return model != null
                ? model.ValidateRestoreState(state)
                : RestoreStateValidationResult.Invalid("Stalker AI has not initialized its runtime model.");
        }

        private void OnPlayerFootstep(PlayerFootstepEmitted footstep)
        {
            if (model == null || tuningProfile == null)
            {
                return;
            }

            var distance = Vector3.Distance(transform.position, footstep.Position);
            if (distance > tuningProfile.HearingRange)
            {
                return;
            }

            var loudness = 1f - distance / Mathf.Max(0.01f, tuningProfile.HearingRange);
            model.Hear(footstep.Position, loudness);
        }

        private void OnStateChanged(StalkerAiStateChanged changed)
        {
            if (!string.Equals(changed.StableId, StableId, StringComparison.Ordinal))
            {
                return;
            }

            lastTransitionReason = changed.Reason;
            ApplySpeedForState();
            if (HorrorGameContext.Active?.Debug.IsEnabled == true)
            {
                HorrorGameContext.Active.Logger.Log(
                    HorrorLogCategory.AI,
                    $"Stalker={changed.StableId}, state={changed.State}, reason={changed.Reason}, " +
                    $"sight={lastSight.Reason}, blocker={lastSight.Blocker?.name ?? "none"}, " +
                    $"layer={(lastSight.Blocker != null ? LayerMask.LayerToName(lastSight.Blocker.gameObject.layer) : "none")}");
            }
        }

        private void UpdateNavigation()
        {
            if (agent == null || !agent.isOnNavMesh || model == null)
            {
                navigationDiagnostic = agent == null ? "NavMeshAgent is missing." : "Stalker is not on a NavMesh.";
                return;
            }

            if (TryGetDesiredDestination(out var destination))
            {
                if (!hasDestination || (destination - lastDestination).sqrMagnitude > 0.04f)
                {
                    agent.isStopped = false;
                    agent.SetDestination(destination);
                    lastDestination = destination;
                    hasDestination = true;
                }
            }
            else
            {
                agent.isStopped = true;
                hasDestination = false;
            }

            if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathPartial)
            {
                navigationDiagnostic = "NavMesh path is partial; check the patrol route and bake coverage.";
            }
            else if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                navigationDiagnostic = "NavMesh path is invalid; check the patrol route and agent placement.";
            }
            else
            {
                navigationDiagnostic = string.Empty;
            }

            if (model.State == StalkerAiState.Patrol &&
                !agent.pathPending &&
                agent.remainingDistance <= tuningProfile.ArrivalDistance)
            {
                model.NotifyPatrolPointReached();
                hasDestination = false;
            }
        }

        private bool TryGetDesiredDestination(out Vector3 destination)
        {
            destination = transform.position;
            switch (model.State)
            {
                case StalkerAiState.Patrol:
                    return patrolRoute != null && patrolRoute.TryGetWaypoint(model.PatrolPointIndex, out var waypoint) &&
                        AssignDestination(waypoint.position, out destination);
                case StalkerAiState.Suspicious:
                case StalkerAiState.Searching:
                case StalkerAiState.Chasing:
                    return AssignDestination(model.LastKnownPosition, out destination);
                default:
                    return false;
            }
        }

        private static bool AssignDestination(Vector3 value, out Vector3 destination)
        {
            destination = value;
            return true;
        }

        private void ApplySpeedForState()
        {
            if (agent == null || tuningProfile == null || !agent.isOnNavMesh)
            {
                return;
            }

            switch (State)
            {
                case StalkerAiState.Chasing:
                    agent.speed = tuningProfile.ChaseSpeed;
                    break;
                case StalkerAiState.Searching:
                case StalkerAiState.Suspicious:
                    agent.speed = tuningProfile.SearchSpeed;
                    break;
                default:
                    agent.speed = tuningProfile.PatrolSpeed;
                    break;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (tuningProfile == null)
            {
                return;
            }

            Gizmos.color = lastSight.IsConfirmed ? Color.red : new Color(1f, 0.8f, 0.2f, 0.8f);
            var source = eye != null ? eye.position : transform.position;
            if (lastSight.IsConfirmed)
            {
                Gizmos.DrawLine(source, lastSight.TargetPosition);
            }
            else if (lastSight.Blocker != null)
            {
                Gizmos.DrawLine(source, lastSight.Blocker.bounds.center);
            }

            if (lastSight.Blocker != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(lastSight.Blocker.bounds.center, 0.2f);
            }
        }
    }
}
