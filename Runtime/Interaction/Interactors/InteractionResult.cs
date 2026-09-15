namespace Setus.HorrorFramework.Interaction.Interactors
{
    using Setus.HorrorFramework.Localization;

    public readonly struct InteractionResult
    {
        public InteractionResult(
            bool success,
            string message = null,
            InteractionRejectionReason rejectionReason = InteractionRejectionReason.None)
        {
            Success = success;
            LocalizedMessage = new LocalizedTextReference(string.Empty, message);
            RejectionReason = rejectionReason;
        }

        public InteractionResult(
            bool success,
            LocalizedTextReference message,
            InteractionRejectionReason rejectionReason = InteractionRejectionReason.None)
        {
            Success = success;
            LocalizedMessage = message;
            RejectionReason = rejectionReason;
        }

        public bool Success { get; }
        public LocalizedTextReference LocalizedMessage { get; }
        public string Message => LocalizedMessage.FallbackText;
        public InteractionRejectionReason RejectionReason { get; }

        public static InteractionResult Succeeded(string message = null)
        {
            return new InteractionResult(true, message);
        }

        public static InteractionResult SucceededLocalized(LocalizedTextReference message)
        {
            return new InteractionResult(true, message);
        }

        public static InteractionResult Rejected(InteractionRejectionReason reason, string message)
        {
            return new InteractionResult(false, message, reason);
        }

        public static InteractionResult RejectedLocalized(
            InteractionRejectionReason reason,
            LocalizedTextReference message)
        {
            return new InteractionResult(false, message, reason);
        }
    }
}
