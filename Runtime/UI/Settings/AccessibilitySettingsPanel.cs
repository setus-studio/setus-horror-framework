using Setus.HorrorFramework.Core.Services;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Setus.HorrorFramework.UI.Settings
{
    [DisallowMultipleComponent]
    public sealed class AccessibilitySettingsPanel : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private Slider masterVolume;
        [SerializeField] private Slider ambienceVolume;
        [SerializeField] private Slider sfxVolume;
        [SerializeField] private Slider uiVolume;
        [SerializeField] private Slider voiceVolume;
        [SerializeField] private Text masterVolumeValue;
        [SerializeField] private Text ambienceVolumeValue;
        [SerializeField] private Text sfxVolumeValue;
        [SerializeField] private Text uiVolumeValue;
        [SerializeField] private Text voiceVolumeValue;

        [Header("Accessibility")]
        [SerializeField] private Toggle subtitlesEnabled;
        [SerializeField] private Slider brightness;
        [SerializeField] private Slider cameraShakeIntensity;
        [SerializeField] private Toggle headBobEnabled;
        [SerializeField] private Slider headBobIntensity;
        [SerializeField] private Toggle sprintToggle;
        [SerializeField] private Slider mouseSensitivity;
        [SerializeField] private InputField localeCode;
        [SerializeField] private Text brightnessValue;
        [SerializeField] private Text cameraShakeValue;
        [SerializeField] private Text headBobIntensityValue;
        [SerializeField] private Text mouseSensitivityValue;

        private RuntimeSettingsModel settings;

        private void OnEnable()
        {
            settings = HorrorGameContext.Ensure().RuntimeSettings;
            settings.Changed += Refresh;
            Refresh(settings.Current);
        }

        private void OnDisable()
        {
            if (settings != null)
            {
                settings.Flush();
                settings.Changed -= Refresh;
                settings = null;
            }
        }

        public void SetMasterVolume(float value) => settings?.SetMasterVolume(value);
        public void SetAmbienceVolume(float value) => settings?.SetAmbienceVolume(value);
        public void SetSfxVolume(float value) => settings?.SetSfxVolume(value);
        public void SetUiVolume(float value) => settings?.SetUiVolume(value);
        public void SetVoiceVolume(float value) => settings?.SetVoiceVolume(value);
        public void SetSubtitlesEnabled(bool value) => settings?.SetSubtitlesEnabled(value);
        public void SetBrightness(float value) => settings?.SetBrightness(value);
        public void SetCameraShakeIntensity(float value) => settings?.SetCameraShakeIntensity(value);
        public void SetHeadBobEnabled(bool value) => settings?.SetHeadBobEnabled(value);
        public void SetHeadBobIntensity(float value) => settings?.SetHeadBobIntensity(value);
        public void SetSprintToggle(bool value) => settings?.SetSprintInputMode(
            value ? SprintInputMode.Toggle : SprintInputMode.Hold);
        public void SetMouseSensitivity(float value) => settings?.SetMouseSensitivity(value);
        public void SetLocaleCode(string value) => settings?.SetLocaleCode(value);
        public void ResetAudioDefaults() => settings?.ResetAudioDefaults();
        public void ResetMovementDefaults() => settings?.ResetMovementDefaults();
        public void ResetAccessibilityDefaults() => settings?.ResetAccessibilityDefaults();

        private void Refresh(RuntimeSettingsState state)
        {
            masterVolume?.SetValueWithoutNotify(state.MasterVolume);
            ambienceVolume?.SetValueWithoutNotify(state.AmbienceVolume);
            sfxVolume?.SetValueWithoutNotify(state.SfxVolume);
            uiVolume?.SetValueWithoutNotify(state.UiVolume);
            voiceVolume?.SetValueWithoutNotify(state.VoiceVolume);
            subtitlesEnabled?.SetIsOnWithoutNotify(state.SubtitlesEnabled);
            brightness?.SetValueWithoutNotify(state.Brightness);
            cameraShakeIntensity?.SetValueWithoutNotify(state.CameraShakeIntensity);
            headBobEnabled?.SetIsOnWithoutNotify(state.HeadBobEnabled);
            headBobIntensity?.SetValueWithoutNotify(state.HeadBobIntensity);
            sprintToggle?.SetIsOnWithoutNotify(state.SprintInputMode == SprintInputMode.Toggle);
            mouseSensitivity?.SetValueWithoutNotify(state.MouseSensitivity);
            localeCode?.SetTextWithoutNotify(state.LocaleCode);
            SetPercent(masterVolumeValue, state.MasterVolume);
            SetPercent(ambienceVolumeValue, state.AmbienceVolume);
            SetPercent(sfxVolumeValue, state.SfxVolume);
            SetPercent(uiVolumeValue, state.UiVolume);
            SetPercent(voiceVolumeValue, state.VoiceVolume);
            SetPercent(brightnessValue, state.Brightness);
            SetPercent(cameraShakeValue, state.CameraShakeIntensity);
            SetPercent(headBobIntensityValue, state.HeadBobIntensity);
            if (mouseSensitivityValue != null)
            {
                mouseSensitivityValue.text = state.MouseSensitivity.ToString("0.0#", CultureInfo.CurrentCulture);
            }
            if (headBobIntensity != null)
            {
                headBobIntensity.interactable = state.HeadBobEnabled;
            }
        }

        private static void SetPercent(Text label, float value)
        {
            if (label != null)
            {
                label.text = Mathf.RoundToInt(value * 100f).ToString(CultureInfo.CurrentCulture) + "%";
            }
        }
    }
}
