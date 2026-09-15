using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Interaction.Locks;
using Setus.HorrorFramework.Localization;
using UnityEngine;

namespace Setus.HorrorFramework.Interaction.Interactables
{
    public sealed class DoorInteractable : PersistentInteractableBase
    {
        [SerializeField] private Transform movingRoot;
        [SerializeField] private float openYaw = 90f;
        [SerializeField] private bool isOpen;
        [SerializeField] private bool isLocked;
        [SerializeField] private string openPromptLocalizationKey = FrameworkTextKeys.DoorOpenPrompt;
        [SerializeField] private string openPrompt = "Open";
        [SerializeField] private string closePromptLocalizationKey = FrameworkTextKeys.DoorClosePrompt;
        [SerializeField] private string closePrompt = "Close";
        [SerializeField] private string lockedPromptLocalizationKey = FrameworkTextKeys.DoorLockedPrompt;
        [SerializeField] private string lockedPrompt = "Locked";
        [SerializeField] private LockRequirement lockRequirement = new LockRequirement();

        private Quaternion closedRotation;

        protected override void Awake()
        {
            base.Awake();
            if (movingRoot == null)
            {
                movingRoot = transform;
            }

            closedRotation = movingRoot.localRotation;
            ApplyVisualState();
        }

        public override bool HasActiveState => false;

        public override InteractionPrompt GetPrompt(InteractionContext context)
        {
            ConfigurePrompt(
                isOpen ? closePromptLocalizationKey : openPromptLocalizationKey,
                isOpen ? closePrompt : openPrompt,
                isOpen ? InteractionType.Close : InteractionType.Open);
            return base.GetPrompt(context);
        }

        protected override LocalizedTextReference GetLocalizedPrompt(
            string text,
            bool canInteract,
            InteractionRejectionReason rejectionReason)
        {
            if (!canInteract && rejectionReason == InteractionRejectionReason.MissingItem &&
                string.Equals(text, lockedPrompt, System.StringComparison.Ordinal))
            {
                return new LocalizedTextReference(lockedPromptLocalizationKey, lockedPrompt);
            }

            return base.GetLocalizedPrompt(text, canInteract, rejectionReason);
        }

        protected override bool CanInteract(
            InteractionContext context,
            out InteractionRejectionReason reason,
            out string rejectionMessage)
        {
            if (!base.CanInteract(context, out reason, out rejectionMessage))
            {
                return false;
            }

            if (isLocked && !lockRequirement.CanUnlock(context.Inventory))
            {
                reason = InteractionRejectionReason.MissingItem;
                rejectionMessage = lockedPrompt;
                return false;
            }

            return true;
        }

        protected override InteractionResult ExecuteInteraction(InteractionContext context)
        {
            if (isLocked)
            {
                lockRequirement.ApplyUnlockCost(context.Inventory);
                isLocked = false;
            }

            isOpen = !isOpen;
            ApplyVisualState();
            return InteractionResult.SucceededLocalized(new LocalizedTextReference(
                isOpen ? openPromptLocalizationKey : closePromptLocalizationKey,
                isOpen ? openPrompt : closePrompt));
        }

        protected override InteractionObjectState CaptureInteractionState()
        {
            return new InteractionObjectState(isOpen, isLocked);
        }

        protected override void RestoreInteractionState(InteractionObjectState state)
        {
            isOpen = state != null && state.IsOpen;
            isLocked = state != null && state.IsLocked;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (movingRoot == null)
            {
                return;
            }

            movingRoot.localRotation = isOpen
                ? closedRotation * Quaternion.Euler(0f, openYaw, 0f)
                : closedRotation;
        }
    }
}
