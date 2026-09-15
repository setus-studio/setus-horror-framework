using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Player.Input;
using Setus.HorrorFramework.Player.State;
using UnityEngine;

namespace Setus.HorrorFramework.UI.Menus
{
    public sealed class PauseFlowController : RuntimeStateOwnerBase<PauseState>
    {
        private readonly UiShellStateOwner uiShell;
        private readonly CursorInputModeCoordinator inputModes;
        private readonly IGameplayEventBus events;
        private PauseState state = new PauseState();
        private float previousTimeScale = 1f;
        private bool previousAudioPause;
        private IPlayerControlStateTarget playerTarget;

        public PauseFlowController(
            UiShellStateOwner uiShell,
            CursorInputModeCoordinator inputModes,
            IGameplayEventBus events = null)
            : base("ui.pause")
        {
            this.uiShell = uiShell;
            this.inputModes = inputModes;
            this.events = events;

            if (this.uiShell != null)
            {
                this.uiShell.Changed += OnUiShellChanged;
                ApplyVisibleUiInputMode(this.uiShell.Current);
            }
        }

        public PauseState Current => state;
        public bool HasGameplayTarget => playerTarget != null;

        public void SetPlayerTarget(IPlayerControlStateTarget target)
        {
            if (playerTarget != null && playerTarget != target)
            {
                SetGameplayInputLocked(false);
            }

            playerTarget = target;
            ApplyVisibleUiInputMode(uiShell?.Current);
        }

        public void ClearPlayerTarget(IPlayerControlStateTarget target)
        {
            if (playerTarget == target)
            {
                SetGameplayInputLocked(false);
                playerTarget = null;
            }
        }

        public void RequestGameplayInputRestore()
        {
            inputModes?.RequestGameplayLock();
        }

        public void ApplyPendingGameplayInputRestore()
        {
            inputModes?.ApplyPendingGameplayLock(playerTarget);
        }

        public void Toggle()
        {
            SetPaused(!state.IsPaused, true);
        }

        public void Pause()
        {
            SetPaused(true);
        }

        public void Resume()
        {
            SetPaused(false, true);
        }

        public void ResumeWithoutRestoringGameplayInput()
        {
            SetPaused(false, false);
        }

        public void SetPaused(bool paused)
        {
            SetPaused(paused, true);
        }

        public void SetPaused(bool paused, bool restoreGameplayInput)
        {
            if (state.IsPaused == paused)
            {
                return;
            }

            if (paused)
            {
                previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                previousAudioPause = AudioListener.pause;
                Time.timeScale = 0f;
                AudioListener.pause = true;
                state = new PauseState(true);
                SetGameplayInputLocked(true);
                uiShell?.SetPaused(true);
                // Pause is an overlay. It owns time, audio, and cursor presentation, not player semantics.
                inputModes?.Apply(CursorInputMode.Menu);
            }
            else
            {
                Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
                AudioListener.pause = previousAudioPause;
                state = new PauseState(false);
                uiShell?.SetPaused(false);
                if (restoreGameplayInput)
                {
                    inputModes?.ApplyGameplayInputForCurrentPlayerState(playerTarget);
                }
            }

            events?.Publish(new PauseStateChanged(state.IsPaused));
        }

        public override PauseState CaptureState()
        {
            return state;
        }

        public override void RestoreState(PauseState restoredState)
        {
            SetPaused(restoredState != null && restoredState.IsPaused);
        }

        private void OnUiShellChanged(UiShellStateChanged changed)
        {
            if (changed.CurrentState.CurrentScreen == UiShellScreen.Hidden)
            {
                SetGameplayInputLocked(false);
                return;
            }

            ApplyVisibleUiInputMode(changed.CurrentState);
        }

        private void ApplyVisibleUiInputMode(UiShellState uiState)
        {
            if (uiState == null || inputModes == null)
            {
                return;
            }

            switch (uiState.CurrentScreen)
            {
                case UiShellScreen.MainMenu:
                    SetGameplayInputLocked(false);
                    inputModes.Apply(CursorInputMode.Menu, playerTarget);
                    break;
                case UiShellScreen.PauseMenu:
                case UiShellScreen.SaveLoadSlots:
                case UiShellScreen.Settings:
                    SetGameplayInputLocked(state.IsPaused);
                    inputModes.Apply(CursorInputMode.Menu, state.IsPaused ? null : playerTarget);
                    break;
                case UiShellScreen.Loading:
                    SetGameplayInputLocked(true);
                    inputModes.Apply(CursorInputMode.Loading);
                    break;
            }
        }

        private void SetGameplayInputLocked(bool isLocked)
        {
            if (playerTarget is IPlayerInputLockTarget lockTarget)
            {
                lockTarget.SetGameplayInputLocked(isLocked);
            }
        }
    }
}
