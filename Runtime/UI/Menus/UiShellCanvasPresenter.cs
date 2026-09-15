using Setus.HorrorFramework.Core.Services;
using UnityEngine;

namespace Setus.HorrorFramework.UI.Menus
{
    [DisallowMultipleComponent]
    public sealed class UiShellCanvasPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject saveLoadSlotsPanel;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private bool showInitialScreenOnEnable = true;
        [SerializeField] private UiShellScreen initialScreen = UiShellScreen.MainMenu;

        private UiShellStateOwner uiShell;
        private UiShellScreen returnFromSaveLoadScreen = UiShellScreen.MainMenu;
        private UiShellScreen returnFromSettingsScreen = UiShellScreen.MainMenu;

        private void Awake()
        {
            uiShell = HorrorGameContext.Ensure().Services.GetRequired<UiShellStateOwner>();
        }

        private void OnEnable()
        {
            uiShell.Changed += OnUiShellChanged;
            if (showInitialScreenOnEnable &&
                uiShell.Current.CurrentScreen == UiShellScreen.Hidden &&
                initialScreen != UiShellScreen.Hidden)
            {
                uiShell.Show(initialScreen);
                return;
            }

            Apply(uiShell.Current);
        }

        private void OnDisable()
        {
            uiShell.Changed -= OnUiShellChanged;
        }

        public void ShowMainMenu()
        {
            uiShell.Show(UiShellScreen.MainMenu);
        }

        public void ShowSaveLoadSlots()
        {
            var currentScreen = uiShell.Current.CurrentScreen;
            if (currentScreen != UiShellScreen.Hidden &&
                currentScreen != UiShellScreen.Loading &&
                currentScreen != UiShellScreen.SaveLoadSlots)
            {
                returnFromSaveLoadScreen = currentScreen;
            }

            uiShell.Show(UiShellScreen.SaveLoadSlots);
        }

        public void ShowReturnScreen()
        {
            uiShell.Show(returnFromSaveLoadScreen);
        }

        public void ShowLoading()
        {
            uiShell.Show(UiShellScreen.Loading);
        }

        public void ShowSettings()
        {
            var currentScreen = uiShell.Current.CurrentScreen;
            if (currentScreen != UiShellScreen.Hidden && currentScreen != UiShellScreen.Loading)
            {
                returnFromSettingsScreen = currentScreen;
            }

            uiShell.Show(UiShellScreen.Settings);
        }

        public void ShowReturnFromSettings()
        {
            uiShell.Show(returnFromSettingsScreen);
        }

        public void Hide()
        {
            uiShell.Hide();
        }

        private void OnUiShellChanged(UiShellStateChanged changed)
        {
            Apply(changed.CurrentState);
        }

        private void Apply(UiShellState state)
        {
            SetActive(mainMenuPanel, state.CurrentScreen == UiShellScreen.MainMenu);
            SetActive(pauseMenuPanel, state.CurrentScreen == UiShellScreen.PauseMenu);
            SetActive(saveLoadSlotsPanel, state.CurrentScreen == UiShellScreen.SaveLoadSlots);
            SetActive(loadingPanel, state.CurrentScreen == UiShellScreen.Loading);
            SetActive(settingsPanel, state.CurrentScreen == UiShellScreen.Settings);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
