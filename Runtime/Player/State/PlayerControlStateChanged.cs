using Setus.HorrorFramework.Core.Events;

namespace Setus.HorrorFramework.Player.State
{
    public readonly struct PlayerControlStateChanged : IGameplayEvent
    {
        public PlayerControlStateChanged(PlayerControlState previousState, PlayerControlState currentState)
        {
            PreviousState = previousState;
            CurrentState = currentState;
        }

        public PlayerControlState PreviousState { get; }
        public PlayerControlState CurrentState { get; }
    }
}
