using Setus.HorrorFramework.Player.State;
using UnityEngine;

namespace Setus.HorrorFramework.Player.Input
{
    public sealed class CursorInputModeCoordinator
    {
        public CursorInputMode CurrentMode { get; private set; } = CursorInputMode.Gameplay;
        public CursorInputModePolicy CurrentPolicy { get; private set; } =
            CursorInputModePolicy.For(CursorInputMode.Gameplay);
        public bool HasPendingGameplayLock { get; private set; }

        public CursorInputModePolicy Apply(CursorInputMode mode, IPlayerControlStateTarget playerTarget = null)
        {
            HasPendingGameplayLock = false;
            CurrentMode = mode;
            CurrentPolicy = CursorInputModePolicy.For(mode);

            Cursor.lockState = CurrentPolicy.CursorLockMode;
            Cursor.visible = CurrentPolicy.CursorVisible;
            playerTarget?.SetControlState(CurrentPolicy.PlayerControlState);

            return CurrentPolicy;
        }

        public void RequestGameplayLock()
        {
            HasPendingGameplayLock = true;
        }

        public CursorInputModePolicy ApplyPendingGameplayLock(IPlayerControlStateTarget playerTarget = null)
        {
            if (!HasPendingGameplayLock)
            {
                return CurrentPolicy;
            }

            return ApplyGameplayInputForCurrentPlayerState(playerTarget);
        }

        public CursorInputModePolicy ApplyGameplayInputForCurrentPlayerState(IPlayerControlStateTarget playerTarget)
        {
            HasPendingGameplayLock = false;
            var playerState = playerTarget != null
                ? playerTarget.CurrentControlState
                : PlayerControlState.Normal;
            var playerPolicy = PlayerControlStatePolicy.For(playerState);

            CurrentMode = CursorInputMode.Gameplay;
            CurrentPolicy = new CursorInputModePolicy(
                playerPolicy.CursorLocked ? CursorLockMode.Locked : CursorLockMode.None,
                !playerPolicy.CursorLocked,
                playerState);

            Cursor.lockState = CurrentPolicy.CursorLockMode;
            Cursor.visible = CurrentPolicy.CursorVisible;
            return CurrentPolicy;
        }
    }
}
