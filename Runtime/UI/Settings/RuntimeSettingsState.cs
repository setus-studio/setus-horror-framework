using System;
using UnityEngine;

namespace Setus.HorrorFramework.UI.Settings
{
    [Serializable]
    public sealed class RuntimeSettingsState
    {
        public RuntimeSettingsState(
            float masterVolume = 1f,
            float mouseSensitivity = 1f,
            bool subtitlesEnabled = true,
            float ambienceVolume = 1f,
            float sfxVolume = 1f,
            float uiVolume = 1f,
            float voiceVolume = 1f,
            float brightness = 0.5f,
            float cameraShakeIntensity = 1f,
            bool headBobEnabled = true,
            float headBobIntensity = 1f,
            SprintInputMode sprintInputMode = SprintInputMode.Hold,
            string localeCode = "en",
            string inputBindingOverridesJson = "")
        {
            MasterVolume = Mathf.Clamp01(masterVolume);
            MouseSensitivity = Mathf.Max(0.01f, mouseSensitivity);
            SubtitlesEnabled = subtitlesEnabled;
            AmbienceVolume = Mathf.Clamp01(ambienceVolume);
            SfxVolume = Mathf.Clamp01(sfxVolume);
            UiVolume = Mathf.Clamp01(uiVolume);
            VoiceVolume = Mathf.Clamp01(voiceVolume);
            Brightness = Mathf.Clamp01(brightness);
            CameraShakeIntensity = Mathf.Clamp01(cameraShakeIntensity);
            HeadBobEnabled = headBobEnabled;
            HeadBobIntensity = Mathf.Clamp01(headBobIntensity);
            SprintInputMode = sprintInputMode;
            LocaleCode = string.IsNullOrWhiteSpace(localeCode) ? "en" : localeCode.Trim();
            InputBindingOverridesJson = inputBindingOverridesJson ?? string.Empty;
        }

        [SerializeField] private float masterVolume;
        [SerializeField] private float mouseSensitivity;
        [SerializeField] private bool subtitlesEnabled;
        [SerializeField] private float ambienceVolume;
        [SerializeField] private float sfxVolume;
        [SerializeField] private float uiVolume;
        [SerializeField] private float voiceVolume;
        [SerializeField] private float brightness;
        [SerializeField] private float cameraShakeIntensity;
        [SerializeField] private bool headBobEnabled;
        [SerializeField] private float headBobIntensity;
        [SerializeField] private SprintInputMode sprintInputMode;
        [SerializeField] private string localeCode;
        [SerializeField, TextArea] private string inputBindingOverridesJson;

        public float MasterVolume
        {
            get => masterVolume;
            private set => masterVolume = value;
        }

        public float MouseSensitivity
        {
            get => mouseSensitivity;
            private set => mouseSensitivity = value;
        }

        public bool SubtitlesEnabled
        {
            get => subtitlesEnabled;
            private set => subtitlesEnabled = value;
        }

        public float AmbienceVolume { get => ambienceVolume; private set => ambienceVolume = value; }
        public float SfxVolume { get => sfxVolume; private set => sfxVolume = value; }
        public float UiVolume { get => uiVolume; private set => uiVolume = value; }
        public float VoiceVolume { get => voiceVolume; private set => voiceVolume = value; }
        public float Brightness { get => brightness; private set => brightness = value; }
        public float CameraShakeIntensity { get => cameraShakeIntensity; private set => cameraShakeIntensity = value; }
        public bool HeadBobEnabled { get => headBobEnabled; private set => headBobEnabled = value; }
        public float HeadBobIntensity { get => headBobIntensity; private set => headBobIntensity = value; }
        public SprintInputMode SprintInputMode { get => sprintInputMode; private set => sprintInputMode = value; }
        public string LocaleCode { get => localeCode; private set => localeCode = value; }
        public string InputBindingOverridesJson
        {
            get => inputBindingOverridesJson ?? string.Empty;
            private set => inputBindingOverridesJson = value;
        }
    }
}
