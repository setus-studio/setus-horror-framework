using System;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEngine;

namespace Setus.HorrorFramework.AI.Memory
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StalkerAiController), typeof(StableId))]
    public sealed class StalkerAiSaveAdapter : MonoBehaviour, ISaveableStateOwner, IRestoreStateValidator, IRuntimeRollbackOwner
    {
        [SerializeField] private StalkerAiController stalker;
        [SerializeField] private StableId stableId;
        private IDisposable registration;

        public string StableId => stableId != null ? stableId.Id : string.Empty;
        public string StateKey => stalker != null ? stalker.StateKey : StalkerAiRuntimeModel.StateKeyValue;
        public Type StateType => typeof(StalkerAiRuntimeState);
        public bool HasActiveState => stalker != null && stalker.IsInitialized && stalker.HasActiveState;
        public SaveRestorePolicy ActiveRestorePolicy => SaveRestorePolicy.ResumeDeterministicPhase;

        private void Awake()
        {
            if (stalker == null)
            {
                stalker = GetComponent<StalkerAiController>();
            }

            if (stableId == null)
            {
                stableId = GetComponent<StableId>();
            }
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void Start()
        {
            // Start retries after all scene Awake calls, independent of component execution order.
            TryRegister();
        }

        private void OnDisable()
        {
            registration?.Dispose();
            registration = null;
        }

        public Action CaptureRollbackAction()
        {
            EnsureReadyForPersistence();
            return stalker.CaptureRollbackAction();
        }

        public object CaptureState()
        {
            EnsureReadyForPersistence();
            return stalker.CaptureState();
        }

        public void RestoreState(object state)
        {
            if (!(state is StalkerAiRuntimeState typedState))
            {
                throw new ArgumentException("Expected StalkerAiRuntimeState.", nameof(state));
            }

            stalker.RestoreState(typedState);
        }

        public RestoreStateValidationResult ValidateRestoreState(object state)
        {
            return stalker != null && stalker.IsInitialized
                ? stalker.ValidateRestoreState(state)
                : RestoreStateValidationResult.Invalid(
                    "Stalker AI save adapter has no initialized runtime controller.");
        }

        private void TryRegister()
        {
            if (registration != null ||
                stalker == null ||
                !stalker.IsInitialized ||
                stableId == null ||
                !stableId.HasStableId)
            {
                return;
            }

            registration = HorrorGameContext.Ensure().Saveables.RegisterStable(this);
        }

        private void EnsureReadyForPersistence()
        {
            if (stalker == null || !stalker.IsInitialized)
            {
                throw new InvalidOperationException(
                    "Stalker AI save adapter cannot capture before its runtime controller is initialized.");
            }
        }
    }
}
