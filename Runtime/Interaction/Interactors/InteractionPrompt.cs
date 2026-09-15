namespace Setus.HorrorFramework.Interaction.Interactors
{
    using Setus.HorrorFramework.Localization;

    public readonly struct InteractionPrompt
    {
        public InteractionPrompt(
            string text,
            bool canInteract,
            InteractionType interactionType,
            InteractionRejectionReason rejectionReason = InteractionRejectionReason.None,
            string rejectionMessage = null)
        {
            LocalizedText = new LocalizedTextReference(string.Empty, text);
            CanInteract = canInteract;
            InteractionType = interactionType;
            RejectionReason = rejectionReason;
            RejectionMessage = rejectionMessage;
        }

        public InteractionPrompt(
            LocalizedTextReference text,
            bool canInteract,
            InteractionType interactionType,
            InteractionRejectionReason rejectionReason = InteractionRejectionReason.None,
            string rejectionMessage = null)
        {
            LocalizedText = text;
            CanInteract = canInteract;
            InteractionType = interactionType;
            RejectionReason = rejectionReason;
            RejectionMessage = rejectionMessage;
        }

        public LocalizedTextReference LocalizedText { get; }
        public string Text => LocalizedText.FallbackText;
        public bool CanInteract { get; }
        public InteractionType InteractionType { get; }
        public InteractionRejectionReason RejectionReason { get; }
        public string RejectionMessage { get; }
    }
}
