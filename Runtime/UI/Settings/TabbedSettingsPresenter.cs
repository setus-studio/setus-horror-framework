using System.Collections;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.UI.Menus;
using Setus.HorrorFramework.UI.Settings;
using Setus.HorrorFramework.UI.Settings.Display;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Setus.HorrorFramework.UI.Settings
{
    [DisallowMultipleComponent]
    public sealed class TabbedSettingsPresenter : MonoBehaviour
    {
        [SerializeField] private UiShellCanvasPresenter shell;
        [SerializeField] private GameObject[] pages;
        [SerializeField] private UnityEngine.UI.Button[] tabs;
        [SerializeField] private UnityEngine.UI.Button[] localeButtons;
        [SerializeField] private string[] localeCodes;
        [SerializeField] private InputRebindPresenter rebindPresenter;
        [SerializeField] private DisplaySettingsPresenter displayPresenter;
        [SerializeField] private UnityEngine.UI.Button mainMenuSettingsButton;
        [SerializeField] private UnityEngine.UI.Button pauseSettingsButton;

        private static readonly Color SelectedColor = new Color(0.16f, 0.41f, 0.4f);
        private static readonly Color IdleColor = new Color(0.17f, 0.19f, 0.2f);
        private RuntimeSettingsModel settings;
        private Setus.HorrorFramework.Localization.RuntimeLocaleController localeController;
        private Coroutine localeRefresh;

        public void Configure(
            UiShellCanvasPresenter owner,
            GameObject[] tabPages,
            UnityEngine.UI.Button[] tabButtons,
            UnityEngine.UI.Button[] languageButtons,
            string[] languageCodes,
            InputRebindPresenter rebind,
            UnityEngine.UI.Button mainButton,
            UnityEngine.UI.Button pauseButton)
        {
            shell = owner;
            pages = tabPages;
            tabs = tabButtons;
            localeButtons = languageButtons;
            localeCodes = languageCodes;
            rebindPresenter = rebind;
            mainMenuSettingsButton = mainButton;
            pauseSettingsButton = pauseButton;
        }

        public bool ConfigureDisplayTab(GameObject displayPage, UnityEngine.UI.Button displayTab,
            DisplaySettingsPresenter presenter)
        {
            if (pages != null && tabs != null && pages.Length == 5 && tabs.Length == 5 &&
                pages[4] == displayPage && tabs[4] == displayTab && displayPresenter == presenter)
                return false;

            System.Array.Resize(ref pages, 5);
            System.Array.Resize(ref tabs, 5);
            pages[4] = displayPage;
            tabs[4] = displayTab;
            displayPresenter = presenter;
            return true;
        }

        private void OnEnable()
        {
            settings = HorrorGameContext.Ensure().RuntimeSettings;
            localeController = shell != null
                ? shell.GetComponent<Setus.HorrorFramework.Localization.RuntimeLocaleController>()
                : GetComponentInParent<Setus.HorrorFramework.Localization.RuntimeLocaleController>();
            settings.Changed += OnSettingsChanged;
            ShowAudio();
            localeRefresh = StartCoroutine(RefreshLocalesWhenReady());
        }

        private void OnDisable()
        {
            if (localeRefresh != null)
            {
                StopCoroutine(localeRefresh);
                localeRefresh = null;
            }

            if (settings != null)
            {
                settings.Changed -= OnSettingsChanged;
                settings = null;
            }

            localeController = null;
        }

        public void ShowAudio() => ShowTab(0);
        public void ShowControls() => ShowTab(1);
        public void ShowAccessibility() => ShowTab(2);
        public void ShowLanguage() => ShowTab(3);
        public void ShowDisplay() => ShowTab(4);

        public void Close()
        {
            if ((rebindPresenter != null && rebindPresenter.IsModalOpen) ||
                (displayPresenter != null && displayPresenter.IsConfirming)) return;
            shell?.ShowReturnFromSettings();
            var returnButton = pauseSettingsButton != null && pauseSettingsButton.gameObject.activeInHierarchy
                ? pauseSettingsButton
                : mainMenuSettingsButton;
            if (returnButton != null && returnButton.gameObject.activeInHierarchy && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(returnButton.gameObject);
            }
        }

        public void SelectEnglish() => SelectLocale("en");
        public void SelectVietnamese() => SelectLocale("vi");
        public void SelectJapanese() => SelectLocale("ja");
        public void SelectKorean() => SelectLocale("ko");
        public void SelectSpanish() => SelectLocale("es");
        public void SelectChineseSimplified() => SelectLocale("zh-Hans");
        public void SelectChineseTraditional() => SelectLocale("zh-Hant");

        private void ShowTab(int index)
        {
            if ((rebindPresenter != null && rebindPresenter.IsModalOpen) ||
                (displayPresenter != null && displayPresenter.IsConfirming)) return;
            if (pages == null || tabs == null || index < 0 || index >= pages.Length)
            {
                return;
            }

            for (var i = 0; i < pages.Length; i++)
            {
                if (pages[i] != null)
                {
                    pages[i].SetActive(i == index);
                }
            }

            if (rebindPresenter != null)
            {
                rebindPresenter.enabled = index == 1;
            }

            for (var i = 0; i < tabs.Length; i++)
            {
                SetButtonColor(tabs[i], i == index ? SelectedColor : IdleColor);
            }

            if (index < tabs.Length && tabs[index] != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(tabs[index].gameObject);
            }
        }

        private IEnumerator RefreshLocalesWhenReady()
        {
            if (!LocalizationSettings.HasSettings)
            {
                yield break;
            }

            while (!LocalizationSettings.InitializationOperation.IsDone)
            {
                yield return null;
            }

            if (localeButtons == null || localeCodes == null)
            {
                yield break;
            }

            for (var i = 0; i < localeButtons.Length && i < localeCodes.Length; i++)
            {
                var button = localeButtons[i];
                if (button != null)
                {
                    button.gameObject.SetActive(
                        LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(localeCodes[i])) != null &&
                        (localeController == null
                            ? localeCodes[i] == "en"
                            : localeController.IsLocaleReady(localeCodes[i])));
                }
            }

            RefreshSelectedLocale();
            localeRefresh = null;
        }

        private void SelectLocale(string code)
        {
            if (settings == null || !LocalizationSettings.HasSettings ||
                !LocalizationSettings.InitializationOperation.IsDone ||
                (localeController != null && !localeController.IsLocaleReady(code)) ||
                LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code)) == null)
            {
                return;
            }

            settings.SetLocaleCode(code);
        }

        private void OnSettingsChanged(RuntimeSettingsState _) => RefreshSelectedLocale();

        private void RefreshSelectedLocale()
        {
            if (settings == null || localeButtons == null || localeCodes == null)
            {
                return;
            }

            for (var i = 0; i < localeButtons.Length && i < localeCodes.Length; i++)
            {
                var selectedCode = localeController != null
                    ? localeController.IsLocaleReady(settings.Current.LocaleCode)
                        ? settings.Current.LocaleCode
                        : "en"
                    : settings.Current.LocaleCode;
                SetButtonColor(localeButtons[i], localeCodes[i] == selectedCode
                    ? SelectedColor
                    : IdleColor);
            }
        }

        private static void SetButtonColor(UnityEngine.UI.Button button, Color color)
        {
            if (button != null && button.targetGraphic != null)
            {
                button.targetGraphic.color = color;
            }
        }
    }
}
