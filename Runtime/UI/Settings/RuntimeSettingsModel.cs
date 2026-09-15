using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.UI.Settings.Persistence;
using UnityEngine;

namespace Setus.HorrorFramework.UI.Settings
{
    public sealed class RuntimeSettingsModel : RuntimeStateOwnerBase<RuntimeSettingsState>,
        IRestoreStateValidator,
        IRuntimeRollbackOwner
    {
        private readonly IUserSettingsStore userSettingsStore;
        private RuntimeSettingsState state = new RuntimeSettingsState();
        private RuntimeSettingsState pendingLegacyImport;
        private bool hasAuthoritativeUserSettings;
        private bool persistenceDirty;

        public RuntimeSettingsModel()
            : this(null)
        {
        }

        public RuntimeSettingsModel(IUserSettingsStore userSettingsStore)
            : base("ui.runtime-settings")
        {
            this.userSettingsStore = userSettingsStore;
            LoadStatus = UserSettingsLoadStatus.Missing;
            if (userSettingsStore == null)
            {
                return;
            }

            UserSettingsLoadResult result;
            try
            {
                result = userSettingsStore.Load();
            }
            catch (Exception exception)
            {
                result = UserSettingsLoadResult.Invalid(
                    $"User settings store failed to load: {exception.Message}");
            }
            LoadStatus = result.Status;
            PersistenceDiagnostic = result.Diagnostic;
            if (result.Status == UserSettingsLoadStatus.Loaded ||
                result.Status == UserSettingsLoadStatus.RecoveredFromBackup)
            {
                state = result.State;
                hasAuthoritativeUserSettings = true;
                return;
            }

            if (result.Status == UserSettingsLoadStatus.Invalid)
            {
                // Corruption falls back to defaults instead of making an arbitrary save slot
                // the new preference authority.
                var loadDiagnostic = PersistenceDiagnostic;
                hasAuthoritativeUserSettings = true;
                persistenceDirty = true;
                Flush();
                if (string.IsNullOrWhiteSpace(PersistenceDiagnostic))
                {
                    PersistenceDiagnostic = loadDiagnostic;
                }
            }
        }

        public RuntimeSettingsState Current => state;
        public UserSettingsLoadStatus LoadStatus { get; }
        public string PersistenceDiagnostic { get; private set; } = string.Empty;
        public bool HasAuthoritativeUserSettings => hasAuthoritativeUserSettings;
        public event Action<RuntimeSettingsState> Changed;

        public void SetMasterVolume(float value) => Replace(masterVolume: value);
        public void SetAmbienceVolume(float value) => Replace(ambienceVolume: value);
        public void SetSfxVolume(float value) => Replace(sfxVolume: value);
        public void SetUiVolume(float value) => Replace(uiVolume: value);
        public void SetVoiceVolume(float value) => Replace(voiceVolume: value);
        public void SetMouseSensitivity(float value) => Replace(mouseSensitivity: Mathf.Max(0.01f, value));
        public void SetSubtitlesEnabled(bool enabled) => Replace(subtitlesEnabled: enabled);
        public void SetBrightness(float value) => Replace(brightness: value);
        public void SetCameraShakeIntensity(float value) => Replace(cameraShakeIntensity: value);
        public void SetHeadBobEnabled(bool enabled) => Replace(headBobEnabled: enabled);
        public void SetHeadBobIntensity(float value) => Replace(headBobIntensity: value);
        public void SetSprintInputMode(SprintInputMode value) => Replace(sprintInputMode: value);
        public void SetLocaleCode(string value) => Replace(localeCode: value);
        public void SetInputBindingOverridesJson(string value) => Replace(inputBindingOverridesJson: value);

        public override RuntimeSettingsState CaptureState()
        {
            return state;
        }

        public override void RestoreState(RuntimeSettingsState restoredState)
        {
            var candidate = restoredState ?? new RuntimeSettingsState();
            var validation = ValidateState(candidate);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.Reason, nameof(restoredState));
            }

            if (userSettingsStore == null)
            {
                ApplyState(candidate, true);
                return;
            }

            if (hasAuthoritativeUserSettings)
            {
                return;
            }

            // Save-slot settings are legacy import data. Defer mutation and persistence until
            // SaveGameOwner publishes transaction success so a later owner failure stays atomic.
            pendingLegacyImport = candidate;
        }

        public Action CaptureRollbackAction()
        {
            var capturedState = state;
            var capturedPendingImport = pendingLegacyImport;
            var capturedAuthority = hasAuthoritativeUserSettings;
            var capturedDirty = persistenceDirty;
            return () =>
            {
                state = capturedState;
                pendingLegacyImport = capturedPendingImport;
                hasAuthoritativeUserSettings = capturedAuthority;
                persistenceDirty = capturedDirty;
            };
        }

        internal void CommitPendingLegacyImport()
        {
            if (pendingLegacyImport == null || hasAuthoritativeUserSettings)
            {
                pendingLegacyImport = null;
                return;
            }

            var imported = pendingLegacyImport;
            pendingLegacyImport = null;
            hasAuthoritativeUserSettings = true;
            ApplyState(imported, false);
            persistenceDirty = true;
            Flush();
            Changed?.Invoke(state);
        }

        public void Flush()
        {
            if (userSettingsStore == null || !persistenceDirty)
            {
                return;
            }

            TryPersistCurrentState();
        }

        public RestoreStateValidationResult ValidateRestoreState(object restoredState)
        {
            if (!(restoredState is RuntimeSettingsState settings))
            {
                return RestoreStateValidationResult.Invalid(
                    $"Expected {nameof(RuntimeSettingsState)}.");
            }

            return ValidateState(settings);
        }

        internal static RestoreStateValidationResult ValidateState(RuntimeSettingsState settings)
        {
            if (settings == null)
            {
                return RestoreStateValidationResult.Invalid(
                    $"Expected {nameof(RuntimeSettingsState)}.");
            }

            if (!IsUnitValue(settings.MasterVolume) ||
                !IsUnitValue(settings.AmbienceVolume) ||
                !IsUnitValue(settings.SfxVolume) ||
                !IsUnitValue(settings.UiVolume) ||
                !IsUnitValue(settings.VoiceVolume) ||
                !IsUnitValue(settings.Brightness) ||
                !IsUnitValue(settings.CameraShakeIntensity) ||
                !IsUnitValue(settings.HeadBobIntensity))
            {
                return RestoreStateValidationResult.Invalid(
                    "Volume, brightness, camera shake, and head bob intensity must be finite values between 0 and 1.");
            }

            if (float.IsNaN(settings.MouseSensitivity) ||
                float.IsInfinity(settings.MouseSensitivity) ||
                settings.MouseSensitivity < 0.01f)
            {
                return RestoreStateValidationResult.Invalid(
                    "Mouse sensitivity must be finite and at least 0.01.");
            }

            if (!Enum.IsDefined(typeof(SprintInputMode), settings.SprintInputMode))
            {
                return RestoreStateValidationResult.Invalid(
                    $"Sprint input mode '{settings.SprintInputMode}' is unsupported.");
            }

            if (string.IsNullOrWhiteSpace(settings.LocaleCode))
            {
                return RestoreStateValidationResult.Invalid("Locale code must not be empty.");
            }

            if (!InputBindingOverridesJsonValidator.TryValidate(
                    settings.InputBindingOverridesJson,
                    out var bindingDiagnostic))
            {
                return RestoreStateValidationResult.Invalid(bindingDiagnostic);
            }

            return RestoreStateValidationResult.Success;
        }

        private void Replace(
            float? masterVolume = null,
            float? mouseSensitivity = null,
            bool? subtitlesEnabled = null,
            float? ambienceVolume = null,
            float? sfxVolume = null,
            float? uiVolume = null,
            float? voiceVolume = null,
            float? brightness = null,
            float? cameraShakeIntensity = null,
            bool? headBobEnabled = null,
            float? headBobIntensity = null,
            SprintInputMode? sprintInputMode = null,
            string localeCode = null,
            string inputBindingOverridesJson = null)
        {
            var replacement = new RuntimeSettingsState(
                masterVolume ?? state.MasterVolume,
                mouseSensitivity ?? state.MouseSensitivity,
                subtitlesEnabled ?? state.SubtitlesEnabled,
                ambienceVolume ?? state.AmbienceVolume,
                sfxVolume ?? state.SfxVolume,
                uiVolume ?? state.UiVolume,
                voiceVolume ?? state.VoiceVolume,
                brightness ?? state.Brightness,
                cameraShakeIntensity ?? state.CameraShakeIntensity,
                headBobEnabled ?? state.HeadBobEnabled,
                headBobIntensity ?? state.HeadBobIntensity,
                sprintInputMode ?? state.SprintInputMode,
                localeCode ?? state.LocaleCode,
                inputBindingOverridesJson ?? state.InputBindingOverridesJson);
            ApplyState(replacement, false);
            if (userSettingsStore != null)
            {
                hasAuthoritativeUserSettings = true;
                pendingLegacyImport = null;
                persistenceDirty = true;
            }

            Changed?.Invoke(state);
        }

        private void ApplyState(RuntimeSettingsState replacement, bool notify)
        {
            state = replacement;
            if (notify)
            {
                Changed?.Invoke(state);
            }
        }

        private void TryPersistCurrentState()
        {
            try
            {
                userSettingsStore.Save(state);
                persistenceDirty = false;
                PersistenceDiagnostic = string.Empty;
            }
            catch (Exception exception)
            {
                PersistenceDiagnostic = $"User settings could not be persisted: {exception.Message}";
            }
        }

        private static bool IsUnitValue(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;
        }
    }
}
