using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.UI.Settings;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Setus.HorrorFramework.Localization
{
    [DisallowMultipleComponent]
    public sealed class RuntimeLocaleController : MonoBehaviour
    {
        [SerializeField] private LocaleFontProfile fontProfile;

        private RuntimeSettingsModel settings;
        private RuntimeSettingsState pendingState;
        private AsyncOperationHandle<LocalizationSettings> initialization;
        private bool waitingForInitialization;
        private Coroutine pendingLocaleApplication;
        private readonly Dictionary<UnityEngine.UI.Text, Font> authoredFonts =
            new Dictionary<UnityEngine.UI.Text, Font>();

        public LocaleFontProfile FontProfile => fontProfile;
        public string EffectiveLocaleCode { get; private set; } = "en";
        public bool HasAppliedInitialLocale { get; private set; }

        public bool IsLocaleReady(string localeCode)
        {
            if (string.Equals(localeCode, "en", System.StringComparison.OrdinalIgnoreCase) &&
                fontProfile == null)
            {
                return true;
            }

            return fontProfile != null && fontProfile.IsLocaleConfigured(localeCode);
        }

        private void OnEnable()
        {
            HasAppliedInitialLocale = false;
            if (!Application.isPlaying)
            {
                return;
            }

            settings = HorrorGameContext.Ensure().RuntimeSettings;
            settings.Changed += Apply;
            Apply(settings.Current);
        }

        private void OnDisable()
        {
            HasAppliedInitialLocale = false;
            if (settings != null)
            {
                settings.Changed -= Apply;
                settings = null;
            }

            if (waitingForInitialization)
            {
                initialization.Completed -= OnLocalizationInitialized;
                waitingForInitialization = false;
            }

            if (pendingLocaleApplication != null)
            {
                StopCoroutine(pendingLocaleApplication);
                pendingLocaleApplication = null;
            }

            RestoreAuthoredFonts();
        }

        private void Apply(RuntimeSettingsState state)
        {
            if (!LocalizationSettings.HasSettings)
            {
                HasAppliedInitialLocale = true;
                return;
            }

            pendingState = state;
            initialization = LocalizationSettings.InitializationOperation;
            if (!initialization.IsDone)
            {
                if (!waitingForInitialization)
                {
                    waitingForInitialization = true;
                    initialization.Completed += OnLocalizationInitialized;
                }
                return;
            }

            if (pendingLocaleApplication != null)
            {
                return;
            }

            if (initialization.Status != AsyncOperationStatus.Succeeded)
            {
                HasAppliedInitialLocale = true;
                return;
            }

            ApplyInitializedLocale(state);
        }

        private void OnLocalizationInitialized(AsyncOperationHandle<LocalizationSettings> _)
        {
            waitingForInitialization = false;
            pendingLocaleApplication = StartCoroutine(ApplyLocaleNextFrame());
        }

        private IEnumerator ApplyLocaleNextFrame()
        {
            yield return null;
            pendingLocaleApplication = null;
            if (isActiveAndEnabled && settings != null)
            {
                Apply(pendingState);
            }
        }

        private void ApplyInitializedLocale(RuntimeSettingsState state)
        {
            var requestedCode = IsLocaleReady(state.LocaleCode) ? state.LocaleCode : "en";
            var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(requestedCode));
            if (locale == null && requestedCode != "en")
            {
                requestedCode = "en";
                locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(requestedCode));
            }

            EffectiveLocaleCode = requestedCode;
            if (locale != null && LocalizationSettings.SelectedLocale != locale)
            {
                LocalizationSettings.SelectedLocale = locale;
            }

            ApplyFont(requestedCode);
            HasAppliedInitialLocale = true;
        }

        private void ApplyFont(string localeCode)
        {
            CaptureAuthoredFonts();
            Font fontOverride = null;
            var hasOverride = fontProfile != null &&
                fontProfile.TryGetFontOverride(localeCode, out fontOverride);
            foreach (var pair in authoredFonts)
            {
                if (pair.Key != null)
                {
                    pair.Key.font = hasOverride ? fontOverride : pair.Value;
                }
            }
        }

        private void CaptureAuthoredFonts()
        {
            foreach (var label in GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                if (label != null && !authoredFonts.ContainsKey(label))
                {
                    authoredFonts.Add(label, label.font);
                }
            }
        }

        private void RestoreAuthoredFonts()
        {
            foreach (var pair in authoredFonts)
            {
                if (pair.Key != null)
                {
                    pair.Key.font = pair.Value;
                }
            }
        }
    }
}
