using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Localization;
using UnityEngine;

namespace Setus.HorrorFramework.Interaction.Interactables
{
    public sealed class InspectableInteractable : PersistentInteractableBase
    {
        [SerializeField] private string inspectPromptLocalizationKey = FrameworkTextKeys.InspectablePrompt;
        [SerializeField] private string inspectPrompt = "Inspect";
        [SerializeField] private string alreadyInspectedPromptLocalizationKey = FrameworkTextKeys.InspectableAlreadyInspectedPrompt;
        [SerializeField, TextArea] private string inspectText = "Nothing unusual.";
        [SerializeField] private bool oneShot;
        [SerializeField] private bool inspected;

        public override InteractionPrompt GetPrompt(InteractionContext context)
        {
            ConfigurePrompt(inspectPromptLocalizationKey, inspectPrompt, InteractionType.Inspect);
            return base.GetPrompt(context);
        }

        protected override LocalizedTextReference GetLocalizedPrompt(
            string text,
            bool canInteract,
            InteractionRejectionReason rejectionReason)
        {
            if (!canInteract && rejectionReason == InteractionRejectionReason.AlreadyUsed &&
                string.Equals(text, "Already inspected", System.StringComparison.Ordinal))
            {
                return new LocalizedTextReference(alreadyInspectedPromptLocalizationKey, "Already inspected");
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

            if (oneShot && inspected)
            {
                reason = InteractionRejectionReason.AlreadyUsed;
                rejectionMessage = "Already inspected";
                return false;
            }

            return true;
        }

        protected override InteractionResult ExecuteInteraction(InteractionContext context)
        {
            inspected = true;
            return InteractionResult.Succeeded(inspectText);
        }

        protected override InteractionObjectState CaptureInteractionState()
        {
            return new InteractionObjectState(isInspected: inspected);
        }

        protected override void RestoreInteractionState(InteractionObjectState state)
        {
            inspected = state != null && state.IsInspected;
        }
    }
}
