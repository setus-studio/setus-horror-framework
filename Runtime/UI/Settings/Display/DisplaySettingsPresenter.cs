using System;
using System.Collections.Generic;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.EventSystems;

namespace Setus.HorrorFramework.UI.Settings.Display
{
    [DisallowMultipleComponent]
    public sealed class DisplaySettingsPresenter : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Dropdown mode;
        [SerializeField] private UnityEngine.UI.Dropdown resolution;
        [SerializeField] private UnityEngine.UI.Dropdown refresh;
        [SerializeField] private UnityEngine.UI.Toggle vSync;
        [SerializeField] private UnityEngine.UI.Dropdown frameRate;
        [SerializeField] private UnityEngine.UI.Dropdown quality;
        [SerializeField] private UnityEngine.UI.Dropdown antiAliasing;
        [SerializeField] private UnityEngine.UI.Text status;
        [SerializeField] private UnityEngine.UI.Button apply;
        [SerializeField] private GameObject confirmationModal;
        [SerializeField] private UnityEngine.UI.Text countdown;
        [SerializeField] private UnityEngine.UI.Button keep;
        [SerializeField] private UnityEngine.UI.Button revert;
        [SerializeField] private UnityEngine.CanvasGroup settingsLayoutGroup;

        private static readonly int[] FrameRateChoices = { 0, 30, 60, 120, 144, 240 };
        private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
        private readonly List<Vector2Int> refreshRates = new List<Vector2Int>();
        private readonly List<int> frameRates = new List<int>();
        private RuntimeSettingsModel owner;
        private UnityDisplaySettingsDevice device;
        private DisplaySettingsPreviewSession preview;
        private string statusKey;
        private string statusFallback;

        public bool IsConfirming => preview != null && preview.IsActive;

        public bool Configure(UnityEngine.UI.Dropdown modeDropdown,
            UnityEngine.UI.Dropdown resolutionDropdown, UnityEngine.UI.Dropdown refreshDropdown,
            UnityEngine.UI.Toggle vSyncToggle, UnityEngine.UI.Dropdown frameRateDropdown,
            UnityEngine.UI.Dropdown qualityDropdown, UnityEngine.UI.Dropdown antiAliasingDropdown,
            UnityEngine.UI.Text statusLabel,
            UnityEngine.UI.Button applyButton, GameObject modal, UnityEngine.UI.Text timer,
            UnityEngine.UI.Button keepButton, UnityEngine.UI.Button revertButton,
            UnityEngine.CanvasGroup layoutGroup)
        {
            var changed = mode != modeDropdown || resolution != resolutionDropdown ||
                refresh != refreshDropdown || vSync != vSyncToggle ||
                frameRate != frameRateDropdown || quality != qualityDropdown ||
                antiAliasing != antiAliasingDropdown ||
                status != statusLabel || apply != applyButton || confirmationModal != modal ||
                countdown != timer || keep != keepButton || revert != revertButton ||
                settingsLayoutGroup != layoutGroup;
            mode = modeDropdown;
            resolution = resolutionDropdown;
            refresh = refreshDropdown;
            vSync = vSyncToggle;
            frameRate = frameRateDropdown;
            quality = qualityDropdown;
            antiAliasing = antiAliasingDropdown;
            status = statusLabel;
            apply = applyButton;
            confirmationModal = modal;
            countdown = timer;
            keep = keepButton;
            revert = revertButton;
            settingsLayoutGroup = layoutGroup;
            return changed;
        }

        private void OnEnable()
        {
            if (!HasRequiredReferences())
            {
                Debug.LogError(
                    "Display settings presenter has missing serialized references. " +
                    "Run Setus > Horror Framework > Settings > Apply Display Settings.",
                    this);
                enabled = false;
                return;
            }

            owner = HorrorGameContext.Ensure().RuntimeSettings;
            device = new UnityDisplaySettingsDevice(
                owner.Current.DisplaySettings?.AntiAliasing ?? GameAntiAliasingMode.Off);
            preview = new DisplaySettingsPreviewSession(owner, device);
            BuildOptions();
            Select(owner.Current.DisplaySettings ?? device.CaptureCurrent());
            if (resolution != null) resolution.onValueChanged.AddListener(OnResolutionChanged);
            if (mode != null) mode.onValueChanged.AddListener(OnModeChanged);
            if (LocalizationSettings.HasSettings)
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            if (confirmationModal != null) confirmationModal.SetActive(false);
        }

        private void OnDisable()
        {
            if (LocalizationSettings.HasSettings)
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            if (resolution != null) resolution.onValueChanged.RemoveListener(OnResolutionChanged);
            if (mode != null) mode.onValueChanged.RemoveListener(OnModeChanged);
            if (preview != null && preview.IsActive)
            {
                if (preview.Revert() == DisplayPreviewResult.RevertFailed)
                    Debug.LogError($"Display preview could not be reverted: {preview.LastDiagnostic}", this);
            }
            CloseConfirmation();
            owner = null;
            preview = null;
            device = null;
        }

        private void Update()
        {
            if (!IsConfirming) return;
            var now = Time.realtimeSinceStartup;
            var result = preview.Tick(now);
            if (result != DisplayPreviewResult.None)
            {
                if (result == DisplayPreviewResult.RevertFailed)
                {
                    if (statusKey != FrameworkTextKeys.DisplayRevertFailed)
                        Debug.LogError($"Display preview could not be reverted: {preview.LastDiagnostic}", this);
                    SetStatus(FrameworkTextKeys.DisplayRevertFailed, "Display could not be restored. Check your screen settings.");
                    if (keep != null) keep.interactable = false;
                    return;
                }
                CloseConfirmation();
                Select(preview.LastBaseline ?? owner.Current.DisplaySettings ?? device.CaptureCurrent());
                if (result == DisplayPreviewResult.ApplyFailed)
                    SetStatus(FrameworkTextKeys.DisplayApplyFailed, "Display change failed; previous settings restored.");
                else
                    SetStatus(FrameworkTextKeys.DisplayTimedOut, "Display change timed out; previous settings restored.");
                return;
            }

            if (countdown != null)
                countdown.text = LocalizationTextResolver.Resolve(new LocalizedTextReference(
                    FrameworkTextKeys.DisplayConfirmPrompt, "Keep these display settings?")) +
                    " " + Mathf.CeilToInt(preview.SecondsRemaining(now)) + "s";
            if (keep != null) keep.interactable = preview.CanConfirm(now);
        }

        public void Apply()
        {
            if (preview == null || IsConfirming || mode == null || resolution == null ||
                refresh == null || vSync == null || frameRate == null || quality == null ||
                antiAliasing == null ||
                resolution.value >= resolutions.Count || refresh.value >= refreshRates.Count ||
                frameRate.value >= frameRates.Count)
                return;

            var candidate = CaptureDraft();
            if (!preview.Begin(candidate, Time.realtimeSinceStartup))
            {
                Debug.LogWarning($"Display preview rejected: {preview.LastDiagnostic}", this);
                SetStatus(FrameworkTextKeys.DisplayApplyFailed, "Display change was not applied. Choose a supported option.");
                return;
            }

            if (confirmationModal != null) confirmationModal.SetActive(true);
            if (settingsLayoutGroup != null)
            {
                settingsLayoutGroup.interactable = false;
                settingsLayoutGroup.blocksRaycasts = false;
            }
            if (keep != null) keep.interactable = false;
            if (apply != null) apply.interactable = false;
            if (revert != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(revert.gameObject);
            SetStatus(FrameworkTextKeys.DisplayPreviewing, "Display change is awaiting confirmation.");
        }

        public void Keep()
        {
            if (!IsConfirming || !preview.Confirm(Time.realtimeSinceStartup)) return;
            CloseConfirmation();
            if (string.IsNullOrEmpty(owner.PersistenceDiagnostic))
                SetStatus(FrameworkTextKeys.DisplaySaved, "Display settings saved.");
            else
                SetStatus(FrameworkTextKeys.DisplaySaveFailed,
                    "Display changed, but preferences could not be saved.");
        }

        public void Revert()
        {
            if (!IsConfirming) return;
            var result = preview.Revert();
            if (result == DisplayPreviewResult.RevertFailed)
            {
                Debug.LogError($"Display preview could not be reverted: {preview.LastDiagnostic}", this);
                SetStatus(FrameworkTextKeys.DisplayRevertFailed, "Display could not be restored. Check your screen settings.");
                if (keep != null) keep.interactable = false;
                return;
            }
            CloseConfirmation();
            Select(preview.LastBaseline ?? owner.Current.DisplaySettings ?? device.CaptureCurrent());
            SetStatus(FrameworkTextKeys.DisplayReverted, "Previous display settings restored.");
        }

        private void BuildOptions()
        {
            var modeOptions = new List<string>
            {
                Localize(FrameworkTextKeys.DisplayWindowed, "Windowed"),
                Localize(FrameworkTextKeys.DisplayBorderless, "Borderless"),
                Localize(FrameworkTextKeys.DisplayFullscreen, "Fullscreen")
            };
            mode.ClearOptions();
            mode.AddOptions(modeOptions);

            resolutions.Clear();
            AddResolution(Screen.width, Screen.height);
            foreach (var item in Screen.resolutions) AddResolution(item.width, item.height);
            resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            RebuildResolutionOptions();

            frameRates.Clear();
            frameRates.AddRange(FrameRateChoices);
            var currentCap = Application.targetFrameRate;
            if (currentCap > 0 && !frameRates.Contains(currentCap)) frameRates.Add(currentCap);
            frameRates.Sort();
            var caps = new List<string>();
            foreach (var cap in frameRates)
                caps.Add(cap == 0 ? Localize(FrameworkTextKeys.DisplayUnlimited, "Unlimited") : cap.ToString());
            frameRate.ClearOptions();
            frameRate.AddOptions(caps);

            var qualityNames = QualitySettings.names;
            quality.ClearOptions();
            var qualityLabels = new List<string>();
            foreach (var name in qualityNames)
                qualityLabels.Add(name switch
                {
                    "Low" => Localize(FrameworkTextKeys.DisplayQualityLow, "Low"),
                    "Medium" => Localize(FrameworkTextKeys.DisplayQualityMedium, "Medium"),
                    "High" => Localize(FrameworkTextKeys.DisplayQualityHigh, "High"),
                    _ => name
                });
            quality.AddOptions(qualityLabels);

            antiAliasing.ClearOptions();
            antiAliasing.AddOptions(new List<string>
            {
                Localize(FrameworkTextKeys.DisplayAaOff, "Off"),
                "FXAA", "SMAA"
            });
        }

        private void Select(DisplaySettingsState state)
        {
            if (state == null || mode == null) return;
            mode.SetValueWithoutNotify((int)state.Mode);
            if (AddResolution(state.Width, state.Height))
            {
                resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                RebuildResolutionOptions();
            }
            var size = resolutions.FindIndex(item => item.x == state.Width && item.y == state.Height);
            resolution.SetValueWithoutNotify(Mathf.Max(0, size));
            BuildRefreshOptions(state.RefreshNumerator, state.RefreshDenominator);
            vSync.SetIsOnWithoutNotify(state.VSyncCount > 0);
            if (!frameRates.Contains(state.FrameRateCap))
            {
                frameRates.Add(state.FrameRateCap);
                frameRates.Sort();
                var caps = new List<string>();
                foreach (var value in frameRates)
                    caps.Add(value == 0 ? Localize(FrameworkTextKeys.DisplayUnlimited, "Unlimited") : value.ToString());
                frameRate.ClearOptions();
                frameRate.AddOptions(caps);
            }
            var cap = frameRates.IndexOf(state.FrameRateCap);
            frameRate.SetValueWithoutNotify(cap >= 0 ? cap : 0);
            var qualityIndex = UnityDisplaySettingsDevice.ResolveQualityIndex(state.QualityName);
            quality.SetValueWithoutNotify(qualityIndex >= 0 ? qualityIndex : QualitySettings.GetQualityLevel());
            antiAliasing.SetValueWithoutNotify((int)state.AntiAliasing);
            RefreshInteractability();
        }

        private void OnResolutionChanged(int _) => BuildRefreshOptions(0, 0);
        private void OnModeChanged(int _)
        {
            if (mode.value == (int)GameDisplayMode.Borderless)
            {
                AddResolution(Screen.currentResolution.width, Screen.currentResolution.height);
                resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
                RebuildResolutionOptions();
                var desktop = resolutions.FindIndex(item =>
                    item.x == Screen.currentResolution.width && item.y == Screen.currentResolution.height);
                resolution.SetValueWithoutNotify(Mathf.Max(0, desktop));
            }
            if (mode.value != (int)GameDisplayMode.Fullscreen)
                BuildRefreshOptions(0, 0);
            RefreshInteractability();
        }

        private void BuildRefreshOptions(int numerator, int denominator)
        {
            refreshRates.Clear();
            refreshRates.Add(Vector2Int.zero);
            if (resolution.value < resolutions.Count)
            {
                var size = resolutions[resolution.value];
                foreach (var supported in Screen.resolutions)
                {
                    var rate = supported.refreshRateRatio;
                    if (supported.width != size.x || supported.height != size.y || rate.denominator == 0)
                        continue;
                    var value = new Vector2Int((int)rate.numerator, (int)rate.denominator);
                    if (!refreshRates.Contains(value)) refreshRates.Add(value);
                }
            }
            var selected = new Vector2Int(numerator, denominator);
            if (numerator > 0 && !refreshRates.Contains(selected)) refreshRates.Add(selected);
            var labels = new List<string>();
            foreach (var rate in refreshRates)
                labels.Add(rate.x == 0 ? Localize(FrameworkTextKeys.DisplayAutomatic, "Automatic") :
                    (rate.x / (float)rate.y).ToString("0.##") + " Hz");
            refresh.ClearOptions();
            refresh.AddOptions(labels);
            refresh.SetValueWithoutNotify(Mathf.Max(0, refreshRates.IndexOf(selected)));
            RefreshInteractability();
        }

        private void RefreshInteractability()
        {
            if (mode == null) return;
            if (resolution != null) resolution.interactable = mode.value != (int)GameDisplayMode.Borderless;
            if (refresh != null) refresh.interactable = mode.value == (int)GameDisplayMode.Fullscreen;
        }

        private bool AddResolution(int width, int height)
        {
            var size = new Vector2Int(width, height);
            if (width < 320 || height < 240 || resolutions.Contains(size)) return false;
            resolutions.Add(size);
            return true;
        }

        private void RebuildResolutionOptions()
        {
            var labels = new List<string>();
            foreach (var item in resolutions) labels.Add(item.x + " x " + item.y);
            resolution.ClearOptions();
            resolution.AddOptions(labels);
        }

        private DisplaySettingsState CaptureDraft()
        {
            var size = resolutions[resolution.value];
            var rate = refreshRates[refresh.value];
            if (mode.value == (int)GameDisplayMode.Borderless)
            {
                size = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
            }
            if (mode.value != (int)GameDisplayMode.Fullscreen) rate = Vector2Int.zero;
            var names = QualitySettings.names;
            var qualityName = quality.value < names.Length ? names[quality.value] : string.Empty;
            return new DisplaySettingsState((GameDisplayMode)mode.value,
                size.x, size.y, rate.x, rate.y, vSync.isOn ? 1 : 0,
                frameRates[frameRate.value], qualityName, (GameAntiAliasingMode)antiAliasing.value);
        }

        private void CloseConfirmation()
        {
            if (confirmationModal != null) confirmationModal.SetActive(false);
            if (settingsLayoutGroup != null)
            {
                settingsLayoutGroup.interactable = true;
                settingsLayoutGroup.blocksRaycasts = true;
            }
            if (apply != null) apply.interactable = true;
            if (apply != null && apply.gameObject.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(apply.gameObject);
        }

        private void SetStatus(string key, string fallback)
        {
            statusKey = key;
            statusFallback = fallback;
            if (status != null) status.text = Localize(key, fallback);
        }

        private void OnLocaleChanged(Locale _)
        {
            var draft = resolutions.Count > 0 && refreshRates.Count > 0 && frameRates.Count > 0
                ? CaptureDraft() : null;
            resolution.onValueChanged.RemoveListener(OnResolutionChanged);
            mode.onValueChanged.RemoveListener(OnModeChanged);
            BuildOptions();
            Select(draft ?? owner.Current.DisplaySettings ?? device.CaptureCurrent());
            resolution.onValueChanged.AddListener(OnResolutionChanged);
            mode.onValueChanged.AddListener(OnModeChanged);
            SetStatus(statusKey, statusFallback);
        }

        private static string Localize(string key, string fallback) =>
            LocalizationTextResolver.Resolve(new LocalizedTextReference(key, fallback));

        private bool HasRequiredReferences()
        {
            return mode != null && resolution != null && refresh != null && vSync != null &&
                frameRate != null && quality != null && antiAliasing != null &&
                status != null && apply != null &&
                confirmationModal != null && countdown != null && keep != null &&
                revert != null && settingsLayoutGroup != null;
        }
    }
}
