using Setus.HorrorFramework.Inventory.Items;
using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Localization;
using UnityEngine;

namespace Setus.HorrorFramework.Interaction.Interactables
{
    public sealed class PickupInteractable : PersistentInteractableBase
    {
        [SerializeField] private InventoryItemDefinition itemDefinition;
        [SerializeField] private string itemIdOverride;
        [SerializeField] private string pickupPromptLocalizationKey = FrameworkTextKeys.PickupPrompt;
        [SerializeField] private string pickupPrompt = "Pick up";
        [SerializeField] private string alreadyPickedUpPromptLocalizationKey = FrameworkTextKeys.PickupAlreadyCollectedPrompt;
        [SerializeField] private string alreadyPickedUpPrompt = "Already picked up";
        [SerializeField] private bool holdAfterPickup = true;
        [SerializeField] private bool consumed;

        private string ItemId =>
            itemDefinition != null && !string.IsNullOrWhiteSpace(itemDefinition.ItemId)
                ? itemDefinition.ItemId
                : itemIdOverride;

        protected override void Awake()
        {
            base.Awake();
            ApplyVisualState();
        }

        public override InteractionPrompt GetPrompt(InteractionContext context)
        {
            ConfigurePrompt(pickupPromptLocalizationKey, pickupPrompt, InteractionType.Pickup);
            return base.GetPrompt(context);
        }

        protected override LocalizedTextReference GetLocalizedPrompt(
            string text,
            bool canInteract,
            InteractionRejectionReason rejectionReason)
        {
            if (!canInteract && rejectionReason == InteractionRejectionReason.AlreadyUsed &&
                string.Equals(text, alreadyPickedUpPrompt, System.StringComparison.Ordinal))
            {
                return new LocalizedTextReference(alreadyPickedUpPromptLocalizationKey, alreadyPickedUpPrompt);
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

            if (consumed)
            {
                reason = InteractionRejectionReason.AlreadyUsed;
                rejectionMessage = alreadyPickedUpPrompt;
                return false;
            }

            if (string.IsNullOrWhiteSpace(ItemId))
            {
                reason = InteractionRejectionReason.Blocked;
                rejectionMessage = "Pickup has no item ID";
                return false;
            }

            return true;
        }

        protected override InteractionResult ExecuteInteraction(InteractionContext context)
        {
            context.Inventory?.AddItem(ItemId, holdAfterPickup);
            consumed = true;
            ApplyVisualState();
            return InteractionResult.Succeeded($"Picked up {ItemId}");
        }

        protected override InteractionObjectState CaptureInteractionState()
        {
            return new InteractionObjectState(isConsumed: consumed);
        }

        protected override void RestoreInteractionState(InteractionObjectState state)
        {
            consumed = state != null && state.IsConsumed;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            var colliders = GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = !consumed;
            }

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = !consumed;
            }
        }
    }
}
