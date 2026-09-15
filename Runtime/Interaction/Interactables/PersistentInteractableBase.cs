using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEngine;

namespace Setus.HorrorFramework.Interaction.Interactables
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StableId))]
    public abstract class PersistentInteractableBase : MonoBehaviour, IInteractable, ISaveableStateOwner
    {
        [Header("Interaction")]
        [SerializeField] private string promptLocalizationKey = FrameworkTextKeys.InteractionPrompt;
        [SerializeField] private string promptText = "Interact";
        [SerializeField] private string disabledPromptLocalizationKey = FrameworkTextKeys.InteractionUnavailablePrompt;
        [SerializeField] private string disabledPrompt = "Cannot interact";
        [SerializeField] private InteractionType interactionType = InteractionType.Use;
        [SerializeField] private bool interactionEnabled = true;
        [SerializeField] private Transform promptAnchor;

        private StableId stableId;
        private IDisposable registration;
        private bool hasValidPersistentIdentity;
        private bool missingIdentityReported;
        private bool hasConfiguredPrompt;
        private LocalizedTextReference configuredPrompt;

        public string StableId => stableId != null ? stableId.Id : string.Empty;
        public string StateKey => "interaction.state";
        public Type StateType => typeof(InteractionObjectState);
        public virtual bool HasActiveState => false;
        public virtual SaveRestorePolicy ActiveRestorePolicy => SaveRestorePolicy.ResumeDeterministicPhase;
        public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;
        public bool IsInteractionEnabled => interactionEnabled;

        protected bool InteractionEnabled
        {
            get => interactionEnabled;
            set => interactionEnabled = value;
        }

        protected virtual void Awake()
        {
            EnsureSaveRegistration();
        }

        protected virtual void OnEnable()
        {
            EnsureSaveRegistration();
        }

        protected virtual void OnDestroy()
        {
            registration?.Dispose();
            registration = null;
        }

        public virtual InteractionPrompt GetPrompt(InteractionContext context)
        {
            EnsureSaveRegistration();

            if (!CanInteract(context, out var reason, out var rejectionMessage))
            {
                var text = string.IsNullOrWhiteSpace(rejectionMessage) ? disabledPrompt : rejectionMessage;
                return new InteractionPrompt(
                    GetLocalizedPrompt(text, false, reason),
                    false,
                    interactionType,
                    reason,
                    rejectionMessage);
            }

            return new InteractionPrompt(GetLocalizedPrompt(promptText, true, InteractionRejectionReason.None), true, interactionType);
        }

        public InteractionResult Interact(InteractionContext context)
        {
            EnsureSaveRegistration();

            if (!CanInteract(context, out var reason, out var rejectionMessage))
            {
                var text = string.IsNullOrWhiteSpace(rejectionMessage) ? disabledPrompt : rejectionMessage;
                return InteractionResult.RejectedLocalized(reason, GetLocalizedPrompt(text, false, reason));
            }

            return ExecuteInteraction(context);
        }

        public object CaptureState()
        {
            return CaptureInteractionState().WithEnabled(interactionEnabled);
        }

        public void RestoreState(object state)
        {
            if (state is InteractionObjectState interactionState)
            {
                interactionEnabled = interactionState.IsEnabled;
                RestoreInteractionState(interactionState);
                return;
            }

            throw new ArgumentException($"Expected {nameof(InteractionObjectState)}.", nameof(state));
        }

        protected virtual bool CanInteract(
            InteractionContext context,
            out InteractionRejectionReason reason,
            out string rejectionMessage)
        {
            if (!hasValidPersistentIdentity)
            {
                reason = InteractionRejectionReason.Disabled;
                rejectionMessage = "Persistent interaction requires an authored Stable ID.";
                return false;
            }

            if (!interactionEnabled)
            {
                reason = InteractionRejectionReason.Disabled;
                rejectionMessage = disabledPrompt;
                return false;
            }

            reason = InteractionRejectionReason.None;
            rejectionMessage = null;
            return true;
        }

        protected abstract InteractionResult ExecuteInteraction(InteractionContext context);
        protected abstract InteractionObjectState CaptureInteractionState();
        protected abstract void RestoreInteractionState(InteractionObjectState state);

        protected void ConfigurePrompt(string text, InteractionType type)
        {
            promptText = text;
            interactionType = type;
            configuredPrompt = new LocalizedTextReference(string.Empty, text);
            hasConfiguredPrompt = true;
        }

        protected void ConfigurePrompt(string localizationKey, string text, InteractionType type)
        {
            promptText = text;
            interactionType = type;
            configuredPrompt = new LocalizedTextReference(localizationKey, text);
            hasConfiguredPrompt = true;
        }

        protected virtual LocalizedTextReference GetLocalizedPrompt(
            string text,
            bool canInteract,
            InteractionRejectionReason rejectionReason)
        {
            if (canInteract)
            {
                return hasConfiguredPrompt
                    ? configuredPrompt
                    : new LocalizedTextReference(promptLocalizationKey, promptText);
            }

            if (string.Equals(text, disabledPrompt, StringComparison.Ordinal))
            {
                return new LocalizedTextReference(disabledPromptLocalizationKey, disabledPrompt);
            }

            return new LocalizedTextReference(string.Empty, text);
        }

        public void SetInteractionEnabled(bool enabled)
        {
            interactionEnabled = enabled;
        }

        private void EnsureSaveRegistration()
        {
            ResolveStableId();
            RegisterSaveable();
        }

        private void ResolveStableId()
        {
            if (stableId == null)
            {
                stableId = GetComponent<StableId>();
            }

            hasValidPersistentIdentity = stableId != null && stableId.HasStableId;
            if (hasValidPersistentIdentity || missingIdentityReported)
            {
                return;
            }

            missingIdentityReported = true;
            Debug.LogError(
                $"Persistent interactable '{name}' ({GetType().Name}) requires an authored, non-empty {nameof(StableId)}. " +
                "Runtime-generated IDs are intentionally unsupported for authored persistent objects.",
                this);
        }

        private void RegisterSaveable()
        {
            if (registration != null || !hasValidPersistentIdentity)
            {
                return;
            }

            registration = HorrorGameContext.Ensure().Saveables.RegisterStable(this);
        }
    }
}
