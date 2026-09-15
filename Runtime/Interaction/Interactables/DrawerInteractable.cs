using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Interaction.Locks;
using Setus.HorrorFramework.Localization;
using UnityEngine;

namespace Setus.HorrorFramework.Interaction.Interactables
{
    public sealed class DrawerInteractable : PersistentInteractableBase
    {
        [SerializeField] private Transform movingRoot;
        [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 0f, -0.45f);
        [SerializeField] private bool isOpen;
        [SerializeField] private bool isLocked;
        [SerializeField] private string openPromptLocalizationKey = FrameworkTextKeys.DrawerOpenPrompt;
        [SerializeField] private string openPrompt = "Open drawer";
        [SerializeField] private string closePromptLocalizationKey = FrameworkTextKeys.DrawerClosePrompt;
        [SerializeField] private string closePrompt = "Close drawer";
        [SerializeField] private string lockedPromptLocalizationKey = FrameworkTextKeys.DrawerLockedPrompt;
        [SerializeField] private string lockedPrompt = "Drawer is locked";
        [SerializeField] private LockRequirement lockRequirement = new LockRequirement();

        private Vector3 closedPosition;

        protected override void Awake()
        {
            base.Awake();
            if (movingRoot == null)
            {
                movingRoot = transform;
            }

            closedPosition = movingRoot.localPosition;
            ApplyVisualState();
        }

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
            if (movingRoot != null)
            {
                movingRoot.localPosition = isOpen ? closedPosition + openLocalOffset : closedPosition;
            }
        }
    }
}
