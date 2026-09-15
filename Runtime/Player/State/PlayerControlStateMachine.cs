using System;
using Setus.HorrorFramework.Core.Services;

namespace Setus.HorrorFramework.Player.State
{
    public sealed class PlayerControlStateMachine
    {
        public PlayerControlStateMachine(PlayerControlState initialState = PlayerControlState.Normal)
        {
            CurrentState = initialState;
            CurrentPolicy = PlayerControlStatePolicy.For(initialState);
        }

        public PlayerControlState CurrentState { get; private set; }
        public PlayerControlStatePolicy CurrentPolicy { get; private set; }
        public event Action<PlayerControlStateChanged> StateChanged;

        public bool SetState(PlayerControlState state)
        {
            if (CurrentState == state)
            {
                return false;
            }

            var previous = CurrentState;
            CurrentState = state;
            CurrentPolicy = PlayerControlStatePolicy.For(state);

            var changed = new PlayerControlStateChanged(previous, state);
            StateChanged?.Invoke(changed);
            HorrorGameContext.Active?.Events.Publish(changed);
            return true;
        }

        internal void RestoreStateWithoutNotification(PlayerControlState state)
        {
            CurrentState = state;
            CurrentPolicy = PlayerControlStatePolicy.For(state);
        }
    }
}
