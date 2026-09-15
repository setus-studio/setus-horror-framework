using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Player.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Setus.HorrorFramework.UI.Menus
{
    [DisallowMultipleComponent]
    public sealed class PauseInputAdapter : MonoBehaviour
    {
        [SerializeField] private bool escapeTogglesPause = true;

        private PauseFlowController pauseFlow;
        private CursorInputModeCoordinator inputModes;
        private UiShellStateOwner uiShell;

        private void Awake()
        {
            var context = HorrorGameContext.Ensure();
            pauseFlow = context.Services.GetRequired<PauseFlowController>();
            inputModes = context.Services.GetRequired<CursorInputModeCoordinator>();
            uiShell = context.Services.GetRequired<UiShellStateOwner>();
        }

        private void Update()
        {
            if (!escapeTogglesPause)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                TogglePauseFromKeyboard();
            }
        }

        public void ResumeAfterUiClick()
        {
            pauseFlow.ResumeWithoutRestoringGameplayInput();
            pauseFlow.RequestGameplayInputRestore();
        }

        private void LateUpdate()
        {
            pauseFlow.ApplyPendingGameplayInputRestore();
        }

        private void TogglePauseFromKeyboard()
        {
            if (pauseFlow.Current.IsPaused)
            {
                if (uiShell.Current.CurrentScreen == UiShellScreen.PauseMenu)
                {
                    pauseFlow.Resume();
                }

                return;
            }

            if (!pauseFlow.HasGameplayTarget || uiShell.Current.CurrentScreen != UiShellScreen.Hidden)
            {
                return;
            }

            pauseFlow.Pause();
        }
    }
}
