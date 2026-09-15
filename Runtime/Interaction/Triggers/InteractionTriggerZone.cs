using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEngine;

namespace Setus.HorrorFramework.Interaction.Triggers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(StableId))]
    public sealed class InteractionTriggerZone : MonoBehaviour, ISaveableStateOwner
    {
        [SerializeField] private bool fireOnce = true;
        [SerializeField] private string requiredTag;
        [SerializeField] private bool requirePlayerController;
        [SerializeField] private bool hasFired;

        private StableId stableId;
        private IDisposable registration;
        private bool hasValidPersistentIdentity;
        private bool missingIdentityReported;

        public string StableId => stableId != null ? stableId.Id : string.Empty;
        public string StateKey => "interaction.trigger";
        public Type StateType => typeof(InteractionTriggerState);
        public bool HasActiveState => false;
        public SaveRestorePolicy ActiveRestorePolicy => SaveRestorePolicy.ResumeDeterministicPhase;

        private void Awake()
        {
            EnsureSaveRegistration();
            EnsureTriggerCollider();
            EnsureKinematicRigidbody();
        }

        private void OnEnable()
        {
            EnsureSaveRegistration();
        }

        private void OnDestroy()
        {
            registration?.Dispose();
            registration = null;
        }

        private void Reset()
        {
            EnsureTriggerCollider();
            EnsureKinematicRigidbody();
        }

        private void OnValidate()
        {
            EnsureTriggerCollider();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!hasValidPersistentIdentity)
            {
                return;
            }

            if (fireOnce && hasFired)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(requiredTag) && !other.CompareTag(requiredTag))
            {
                return;
            }

            if (requirePlayerController && other.GetComponentInParent<FirstPersonPlayerController>() == null)
            {
                return;
            }

            hasFired = true;
            HorrorGameContext.Active?.Events.Publish(new InteractionTriggerEntered(stableId.Id, other.name));
        }

        public object CaptureState()
        {
            return new InteractionTriggerState(hasFired);
        }

        public void RestoreState(object state)
        {
            if (state is InteractionTriggerState triggerState)
            {
                hasFired = triggerState.HasFired;
                return;
            }

            throw new ArgumentException($"Expected {nameof(InteractionTriggerState)}.", nameof(state));
        }

        private void OnDrawGizmos()
        {
            if (HorrorGameContext.Active == null || !HorrorGameContext.Active.Debug.IsEnabled)
            {
                return;
            }

            Gizmos.color = hasFired ? Color.gray : new Color(0f, 0.9f, 0.35f, 0.65f);
            Gizmos.DrawWireCube(transform.position, transform.lossyScale);
        }

        private void EnsureTriggerCollider()
        {
            var trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void EnsureKinematicRigidbody()
        {
            var body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;
        }

        private void EnsureSaveRegistration()
        {
            if (stableId == null)
            {
                stableId = GetComponent<StableId>();
            }

            hasValidPersistentIdentity = stableId != null && stableId.HasStableId;
            if (!hasValidPersistentIdentity && !missingIdentityReported)
            {
                missingIdentityReported = true;
                Debug.LogError(
                    $"Persistent trigger '{name}' ({GetType().Name}) requires an authored, non-empty {nameof(StableId)}. " +
                    "Runtime-generated IDs are intentionally unsupported for authored persistent objects.",
                    this);
            }

            if (registration == null && hasValidPersistentIdentity)
            {
                registration = HorrorGameContext.Ensure().Saveables.RegisterStable(this);
            }
        }
    }
}
