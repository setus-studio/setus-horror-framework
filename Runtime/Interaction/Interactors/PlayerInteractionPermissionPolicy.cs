using Setus.HorrorFramework.Player.State;

namespace Setus.HorrorFramework.Interaction.Interactors
{
    public static class PlayerInteractionPermissionPolicy
    {
        public static bool CanInteract(
            PlayerControlState playerState,
            bool isPaused,
            bool hasBlockingUi,
            bool isTransitioning)
        {
            return PlayerControlStatePolicy.For(playerState).CanInteract &&
                   !isPaused &&
                   !hasBlockingUi &&
                   !isTransitioning;
        }
    }
}
