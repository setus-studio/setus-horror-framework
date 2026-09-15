namespace Setus.HorrorFramework.Player.State
{
    public readonly struct PlayerControlStatePolicy
    {
        public PlayerControlStatePolicy(bool canMove, bool canLook, bool cursorLocked)
            : this(canMove, canLook, cursorLocked, canMove && canLook)
        {
        }

        public PlayerControlStatePolicy(bool canMove, bool canLook, bool cursorLocked, bool canInteract)
        {
            CanMove = canMove;
            CanLook = canLook;
            CursorLocked = cursorLocked;
            CanInteract = canInteract;
        }

        public bool CanMove { get; }
        public bool CanLook { get; }
        public bool CursorLocked { get; }
        public bool CanInteract { get; }

        public static PlayerControlStatePolicy For(PlayerControlState state)
        {
            switch (state)
            {
                case PlayerControlState.Normal:
                    return new PlayerControlStatePolicy(true, true, true, true);
                case PlayerControlState.InteractionLocked:
                    return new PlayerControlStatePolicy(false, true, true, false);
                case PlayerControlState.Inspecting:
                case PlayerControlState.Menu:
                    return new PlayerControlStatePolicy(false, false, false, false);
                case PlayerControlState.Cutscene:
                case PlayerControlState.Disabled:
                default:
                    return new PlayerControlStatePolicy(false, false, true, false);
            }
        }

        public static PlayerControlState NormalizeForPersistence(PlayerControlState state)
        {
            return state == PlayerControlState.Menu
                ? PlayerControlState.Normal
                : state;
        }
    }
}
