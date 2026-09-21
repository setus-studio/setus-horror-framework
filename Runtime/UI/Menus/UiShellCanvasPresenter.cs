using System.Collections;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Setus.HorrorFramework.UI.Menus
{
    [DisallowMultipleComponent]
    public sealed class UiShellCanvasPresenter : MonoBehaviour
    {
        private const float StartupLocaleTimeoutSeconds = 10f;

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
        private RuntimeLocaleController localeController;
        private Canvas shellCanvas;
        private GraphicRaycaster raycaster;
        private Coroutine startupReveal;
        private bool authoredCanvasEnabled;
        private bool authoredRaycasterEnabled;

        private void Awake()
        {
            uiShell = HorrorGameContext.Ensure().Services.GetRequired<UiShellStateOwner>();
            localeController = GetComponent<RuntimeLocaleController>();
            shellCanvas = GetComponent<Canvas>();
            raycaster = GetComponent<GraphicRaycaster>();
            authoredCanvasEnabled = shellCanvas != null && shellCanvas.enabled;
            authoredRaycasterEnabled = raycaster != null && raycaster.enabled;
            GateStartupUi();
        }

        private void OnEnable()
        {
            uiShell.Changed += OnUiShellChanged;
            if (ShouldWaitForLocale())
            {
                GateStartupUi();
                startupReveal = StartCoroutine(RevealAfterLocaleApplied());
            }
            else
            {
                RestoreStartupUi();
            }
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
            if (startupReveal != null)
            {
                StopCoroutine(startupReveal);
                startupReveal = null;
            }

            RestoreStartupUi();
        }

        private bool ShouldWaitForLocale() =>
            Application.isPlaying && localeController != null && localeController.isActiveAndEnabled &&
            LocalizationSettings.HasSettings && !localeController.HasAppliedInitialLocale;

        private void GateStartupUi()
        {
            if (!ShouldWaitForLocale())
            {
                return;
            }

            if (shellCanvas != null) shellCanvas.enabled = false;
            if (raycaster != null) raycaster.enabled = false;
        }

        private IEnumerator RevealAfterLocaleApplied()
        {
            var deadline = Time.realtimeSinceStartup + StartupLocaleTimeoutSeconds;
            while (ShouldWaitForLocale() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            foreach (var presenter in GetComponentsInChildren<LocalizedTextPresenter>(true))
            {
                presenter.Refresh();
            }

            startupReveal = null;
            RestoreStartupUi();
        }

        private void RestoreStartupUi()
        {
            if (shellCanvas != null) shellCanvas.enabled = authoredCanvasEnabled;
            if (raycaster != null) raycaster.enabled = authoredRaycasterEnabled;
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
