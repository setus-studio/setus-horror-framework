using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.UI.Settings;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Setus.HorrorFramework.Localization
{
    [DisallowMultipleComponent]
    public sealed class RuntimeLocaleController : MonoBehaviour
    {
        private RuntimeSettingsModel settings;
        private RuntimeSettingsState pendingState;
        private AsyncOperationHandle<LocalizationSettings> initialization;
        private bool waitingForInitialization;

        private void OnEnable()
        {
            settings = HorrorGameContext.Ensure().RuntimeSettings;
            settings.Changed += Apply;
            Apply(settings.Current);
        }

        private void OnDisable()
        {
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
        }

        private void Apply(RuntimeSettingsState state)
        {
            if (!LocalizationSettings.HasSettings)
            {
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

            ApplyInitializedLocale(state);
        }

        private void OnLocalizationInitialized(AsyncOperationHandle<LocalizationSettings> _)
        {
            waitingForInitialization = false;
            ApplyInitializedLocale(pendingState);
        }

        private static void ApplyInitializedLocale(RuntimeSettingsState state)
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(state.LocaleCode));
            if (locale != null && LocalizationSettings.SelectedLocale != locale)
            {
                LocalizationSettings.SelectedLocale = locale;
            }
        }
    }
}
