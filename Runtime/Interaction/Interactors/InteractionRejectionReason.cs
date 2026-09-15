namespace Setus.HorrorFramework.Interaction.Interactors
{
    public enum InteractionRejectionReason
    {
        None = 0,
        NoTarget = 1,
        OutOfRange = 2,
        Disabled = 3,
        Locked = 4,
        MissingItem = 5,
        AlreadyUsed = 6,
        Blocked = 7
    }
}
