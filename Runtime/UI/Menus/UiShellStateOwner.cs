using System;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;

namespace Setus.HorrorFramework.UI.Menus
{
    public sealed class UiShellStateOwner : RuntimeStateOwnerBase<UiShellState>
    {
        private readonly IGameplayEventBus events;
        private UiShellState state = new UiShellState();

        public UiShellStateOwner(IGameplayEventBus events = null)
            : base("ui.shell")
        {
            this.events = events;
        }

        public UiShellState Current => state;
        public event Action<UiShellStateChanged> Changed;

        public void Show(UiShellScreen screen)
        {
            SetState(new UiShellState(screen, state.IsPaused));
        }

        public void Hide()
        {
            SetState(new UiShellState(UiShellScreen.Hidden, state.IsPaused));
        }

        public void SetPaused(bool isPaused)
        {
            var screen = isPaused ? UiShellScreen.PauseMenu : UiShellScreen.Hidden;
            SetState(new UiShellState(screen, isPaused));
        }

        public override UiShellState CaptureState()
        {
            return state;
        }

        public override void RestoreState(UiShellState restoredState)
        {
            SetState(restoredState ?? new UiShellState());
        }

        private void SetState(UiShellState nextState)
        {
            var previous = state;
            state = nextState;

            var changed = new UiShellStateChanged(previous, state);
            Changed?.Invoke(changed);
            events?.Publish(changed);
        }
    }
}
