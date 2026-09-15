using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SceneFlow.Loading;
using Setus.HorrorFramework.SceneFlow.Transitions;
using UnityEngine;
using UnityEngine.UI;

namespace Setus.HorrorFramework.UI.Menus
{
    [DisallowMultipleComponent]
    public sealed class UiShellCommandAdapter : MonoBehaviour
    {
        [SerializeField] private StandaloneGameConfig gameConfig;
        [SerializeField] private SceneTransitionRunner transitionRunner;
        [SerializeField] private PauseInputAdapter pauseInputAdapter;
        [SerializeField] private Button continueButton;

        private PauseFlowController pauseFlow;
        private UiShellStateOwner uiShell;

        private void Awake()
        {
            var context = HorrorGameContext.Ensure();
            pauseFlow = context.Services.GetRequired<PauseFlowController>();

            if (transitionRunner == null)
            {
                transitionRunner = GetComponent<SceneTransitionRunner>();
            }

            if (transitionRunner == null)
            {
                throw new System.InvalidOperationException(
                    "UiShellCommandAdapter requires the canonical SceneTransitionRunner.");
            }

            if (pauseInputAdapter == null)
            {
                pauseInputAdapter = GetComponent<PauseInputAdapter>();
            }

            uiShell = context.Services.GetRequired<UiShellStateOwner>();
            uiShell.Changed += OnUiShellChanged;
            RefreshContinueButton();
        }

        private void OnDestroy()
        {
            if (uiShell != null)
            {
                uiShell.Changed -= OnUiShellChanged;
                uiShell = null;
            }
        }

        public void StartNewGame()
        {
            transitionRunner.StartNewGame();
        }

        public void LoadMainMenu()
        {
            pauseFlow.ResumeWithoutRestoringGameplayInput();
            transitionRunner.LoadMainMenu();
        }

        public void ContinueFromSave()
        {
            if (transitionRunner == null)
            {
                return;
            }

            if (!transitionRunner.TryContinueFromSave(
                    SingleSaveSlotContract.SlotId,
                    out _))
            {
                uiShell.Show(UiShellScreen.SaveLoadSlots);
            }
        }

        public void Pause()
        {
            pauseFlow.Pause();
        }

        public void Resume()
        {
            if (pauseInputAdapter != null)
            {
                pauseInputAdapter.ResumeAfterUiClick();
                return;
            }

            pauseFlow.Resume();
        }

        private void OnUiShellChanged(UiShellStateChanged changed)
        {
            if (changed.CurrentState.CurrentScreen == UiShellScreen.MainMenu ||
                changed.PreviousState.CurrentScreen == UiShellScreen.MainMenu)
            {
                RefreshContinueButton();
            }
        }

        private void RefreshContinueButton()
        {
            if (continueButton == null)
            {
                return;
            }

            continueButton.interactable = HorrorGameContext.Ensure().SaveLoad.HasValidSlot(
                SingleSaveSlotContract.SlotId);
        }
    }
}
