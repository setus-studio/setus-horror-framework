using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.Localization;
using UnityEngine;

namespace Setus.HorrorFramework.Narrative.Phone
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class PhoneMessageInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private PhoneMessageDefinition message;
        [SerializeField] private Transform promptAnchor;

        public Transform PromptAnchor => promptAnchor != null ? promptAnchor : transform;

        public InteractionPrompt GetPrompt(InteractionContext context)
        {
            if (!TryGetMessageId(out var messageId))
            {
                return UnavailablePrompt();
            }

            var narrative = context.GameContext?.Narrative;
            if (narrative == null || !narrative.TryGetPhoneProgress(messageId, out var progress))
            {
                return new InteractionPrompt(
                    new LocalizedTextReference(FrameworkTextKeys.PhoneCheckPrompt, "Check phone"),
                    true,
                    InteractionType.Read);
            }

            if (!progress.Read)
            {
                return new InteractionPrompt(
                    new LocalizedTextReference(FrameworkTextKeys.PhoneReadPrompt, "Read message"),
                    true,
                    InteractionType.Read);
            }

            if (message.RequiresReply && !progress.Replied)
            {
                return new InteractionPrompt(
                    new LocalizedTextReference(FrameworkTextKeys.PhoneReplyPrompt, "Reply"),
                    true,
                    InteractionType.Use);
            }

            return new InteractionPrompt(
                new LocalizedTextReference(FrameworkTextKeys.PhoneHandledPrompt, "Message read"),
                false,
                InteractionType.Read,
                InteractionRejectionReason.AlreadyUsed,
                "Message already handled");
        }

        public InteractionResult Interact(InteractionContext context)
        {
            if (!TryGetMessageId(out var messageId) || context.GameContext?.Narrative == null)
            {
                return InteractionResult.RejectedLocalized(
                    InteractionRejectionReason.Disabled,
                    new LocalizedTextReference(
                        FrameworkTextKeys.PhoneNotConfiguredResult,
                        "Phone message is not configured."));
            }

            var narrative = context.GameContext.Narrative;
            if (!narrative.TryGetPhoneProgress(messageId, out var progress))
            {
                return narrative.TryDeliverPhoneMessage(messageId)
                    ? InteractionResult.SucceededLocalized(new LocalizedTextReference(
                        FrameworkTextKeys.PhoneDeliveredResult,
                        "Message delivered"))
                    : InteractionResult.RejectedLocalized(
                        InteractionRejectionReason.Blocked,
                        new LocalizedTextReference(
                            FrameworkTextKeys.PhoneDeliveryFailedResult,
                            "Message delivery failed"));
            }

            if (!progress.Read)
            {
                if (narrative.TryReadPhoneMessage(messageId))
                {
                    context.GameContext.Events.Publish(new NarrativeSubtitleRequested(
                        message.LocalizedSender,
                        message.LocalizedMessage,
                        4.5f));
                    return InteractionResult.SucceededLocalized(new LocalizedTextReference(
                        FrameworkTextKeys.PhoneReadResult,
                        "Message read"));
                }
            }
            else if (message.RequiresReply && !progress.Replied && narrative.TryReplyToPhoneMessage(messageId))
            {
                context.GameContext.Events.Publish(new NarrativeSubtitleRequested(
                    new LocalizedTextReference(FrameworkTextKeys.PlayerSpeaker, "You"),
                    message.LocalizedReply,
                    3f));
                return InteractionResult.SucceededLocalized(new LocalizedTextReference(
                    FrameworkTextKeys.PhoneReplySentResult,
                    "Reply sent"));
            }

            return InteractionResult.RejectedLocalized(
                InteractionRejectionReason.AlreadyUsed,
                new LocalizedTextReference(
                    FrameworkTextKeys.PhoneAlreadyHandledResult,
                    "Message already handled"));
        }

        private bool TryGetMessageId(out string messageId)
        {
            messageId = message != null ? message.MessageId : null;
            return !string.IsNullOrWhiteSpace(messageId);
        }

        private static InteractionPrompt UnavailablePrompt()
        {
            return new InteractionPrompt(
                new LocalizedTextReference(FrameworkTextKeys.PhoneUnavailablePrompt, "Phone unavailable"),
                false,
                InteractionType.Read,
                InteractionRejectionReason.Disabled,
                "Phone message is not configured.");
        }
    }
}
