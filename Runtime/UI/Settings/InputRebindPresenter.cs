using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Setus.HorrorFramework.UI.Settings
{
    [DisallowMultipleComponent]
    public sealed class InputRebindPresenter : MonoBehaviour
    {
        private static readonly string[] ActionPaths =
        {
            "Player/Move", "Player/Move", "Player/Move", "Player/Move",
            "Player/Look", "Player/Sprint", "Player/Crouch", "Player/Interact", "Player/Pause"
        };

        private static readonly string[] CompositeParts =
        {
            "up", "down", "left", "right", null, null, null, null, null
        };

        [SerializeField] private InputActionAsset actions;
        [SerializeField] private Text statusText;
        [SerializeField] private Text[] bindingLabels;
        [SerializeField] private GameObject rebindModal;
        [SerializeField] private CanvasGroup settingsLayoutGroup;
        [SerializeField] private Text modalTitle;
        [SerializeField] private Text modalPrompt;
        [SerializeField] private Text modalCurrent;
        [SerializeField] private Button modalRetry;
        [SerializeField] private Button modalConfirmReset;
        [SerializeField] private Button modalCancel;

        private InputActionRebindingExtensions.RebindingOperation operation;
        private InputAction rebindingAction;
        private int rebindingIndex = -1;
        private bool actionWasEnabled;
        private string previousOverridePath;
        private bool resetConfirmationOpen;
        private RuntimeSettingsModel settings;
        private string displayedOverridesJson;
        private LocalizedTextReference currentStatus;
        private string statusDetail;
        private GameObject previousSelection;

        public bool IsModalOpen => rebindModal != null && rebindModal.activeSelf;

        public bool Configure(
            Text[] currentBindings, GameObject modal, CanvasGroup layoutGroup,
            Text title, Text prompt, Text current, Button retry, Button confirmReset, Button cancel)
        {
            var changed = bindingLabels == null || bindingLabels.Length != currentBindings.Length ||
                rebindModal != modal || settingsLayoutGroup != layoutGroup || modalTitle != title ||
                modalPrompt != prompt || modalCurrent != current || modalRetry != retry ||
                modalConfirmReset != confirmReset || modalCancel != cancel;
            if (!changed)
            {
                for (var i = 0; i < currentBindings.Length; i++)
                {
                    if (bindingLabels[i] != currentBindings[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }
            bindingLabels = currentBindings;
            rebindModal = modal;
            settingsLayoutGroup = layoutGroup;
            modalTitle = title;
            modalPrompt = prompt;
            modalCurrent = current;
            modalRetry = retry;
            modalConfirmReset = confirmReset;
            modalCancel = cancel;
            return changed;
        }

        private void OnEnable()
        {
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            }
            settings = HorrorGameContext.Ensure().RuntimeSettings;
            settings.Changed += OnSettingsChanged;
            ApplySavedOverrides(settings.Current.InputBindingOverridesJson);
            RefreshBindingLabels();
            RefreshStatus();
        }

        private void OnDisable()
        {
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            }
            if (settings != null)
            {
                settings.Changed -= OnSettingsChanged;
                settings = null;
            }
            CancelCurrentRebind();
        }

        private void Update()
        {
            if (IsModalOpen && operation == null &&
                Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelCurrentRebind();
            }
        }

        public void RebindMoveForward() => BeginRebind(0);
        public void RebindMoveBackward() => BeginRebind(1);
        public void RebindMoveLeft() => BeginRebind(2);
        public void RebindMoveRight() => BeginRebind(3);
        public void RebindLook() => BeginRebind(4);
        public void RebindSprint() => BeginRebind(5);
        public void RebindCrouch() => BeginRebind(6);
        public void RebindInteract() => BeginRebind(7);
        public void RebindPause() => BeginRebind(8);

        public void RequestResetBindings()
        {
            if (actions == null)
            {
                SetStatus(FrameworkTextKeys.RebindMissingActions, "Input Actions asset is not assigned.");
                return;
            }
            if (rebindModal == null)
            {
                SetStatus(FrameworkTextKeys.RebindModalUnavailable, "Rebind dialog is not configured.");
                return;
            }

            CancelOperation();
            resetConfirmationOpen = true;
            SetModalText(FrameworkTextKeys.RebindResetTitle, "Reset all bindings?",
                FrameworkTextKeys.RebindResetPrompt, "Default controls will be restored.");
            if (modalCurrent != null) modalCurrent.gameObject.SetActive(false);
            if (modalRetry != null) modalRetry.gameObject.SetActive(false);
            if (modalConfirmReset != null) modalConfirmReset.gameObject.SetActive(true);
            ShowModal();
        }

        public void ConfirmResetBindings()
        {
            if (!resetConfirmationOpen) return;
            ResetBindings();
            resetConfirmationOpen = false;
            HideModal();
        }

        public void ResetBindings()
        {
            CancelOperation();
            if (actions == null)
            {
                SetStatus(FrameworkTextKeys.RebindMissingActions, "Input Actions asset is not assigned.");
                return;
            }

            actions.RemoveAllBindingOverrides();
            displayedOverridesJson = string.Empty;
            var owner = settings ?? HorrorGameContext.Ensure().RuntimeSettings;
            owner.SetInputBindingOverridesJson(string.Empty);
            owner.Flush();
            RefreshBindingLabels();
            SetStatus(FrameworkTextKeys.RebindReset, "Bindings reset.");
        }

        public void RetryCurrentRebind()
        {
            if (resetConfirmationOpen || rebindingIndex < 0 || rebindingAction == null) return;
            StartOperation(rebindingAction, rebindingIndex);
        }

        public void CancelCurrentRebind()
        {
            if (operation != null)
            {
                CancelOperation();
                return;
            }
            var wasOpen = IsModalOpen;
            rebindingAction = null;
            rebindingIndex = -1;
            resetConfirmationOpen = false;
            HideModal();
            if (wasOpen)
            {
                SetStatus(FrameworkTextKeys.RebindCancelled, "Rebind cancelled.");
            }
        }

        public static bool TryFindConflict(InputActionAsset asset, InputAction changedAction,
            int changedIndex, out string otherBinding)
        {
            otherBinding = string.Empty;
            if (asset == null || changedAction == null || changedIndex < 0 ||
                changedIndex >= changedAction.bindings.Count)
            {
                return false;
            }

            var path = changedAction.bindings[changedIndex].effectivePath;
            if (string.IsNullOrEmpty(path)) return false;
            if (string.Equals(path, "<Keyboard>/escape", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(changedAction.name, "Pause", StringComparison.Ordinal))
            {
                otherBinding = "Pause / Cancel";
                return true;
            }

            foreach (var map in asset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    for (var i = 0; i < action.bindings.Count; i++)
                    {
                        var binding = action.bindings[i];
                        if ((action == changedAction && i == changedIndex) || binding.isComposite ||
                            !string.Equals(path, binding.effectivePath, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        otherBinding = action.name + (binding.isPartOfComposite ? "/" + binding.name : string.Empty);
                        return true;
                    }
                }
            }
            return false;
        }

        private void BeginRebind(int slot)
        {
            if (actions == null || slot < 0 || slot >= ActionPaths.Length)
            {
                SetStatus(FrameworkTextKeys.RebindMissingActions, "Input Actions asset is not assigned.");
                return;
            }
            if (rebindModal == null || settingsLayoutGroup == null)
            {
                SetStatus(FrameworkTextKeys.RebindModalUnavailable, "Rebind dialog is not configured.");
                return;
            }

            var action = actions.FindAction(ActionPaths[slot], false);
            var index = FindBindingIndex(action, CompositeParts[slot]);
            if (index < 0)
            {
                SetStatus(FrameworkTextKeys.RebindActionUnavailable, "Input action is unavailable.");
                return;
            }

            CancelOperation();
            resetConfirmationOpen = false;
            rebindingAction = action;
            rebindingIndex = index;
            if (modalTitle != null)
            {
                var rowLabel = bindingLabels != null && slot < bindingLabels.Length && bindingLabels[slot] != null
                    ? bindingLabels[slot].transform.parent?.Find("Label")?.GetComponent<Text>()
                    : null;
                modalTitle.text = rowLabel != null ? rowLabel.text : action.name;
            }
            if (modalCurrent != null)
            {
                modalCurrent.gameObject.SetActive(true);
                modalCurrent.text = action.GetBindingDisplayString(index);
            }
            ShowModal();
            StartOperation(action, index);
        }

        private void StartOperation(InputAction action, int index)
        {
            previousOverridePath = action.bindings[index].overridePath;
            actionWasEnabled = action.enabled;
            action.Disable();
            if (modalRetry != null) modalRetry.gameObject.SetActive(false);
            if (modalConfirmReset != null) modalConfirmReset.gameObject.SetActive(false);
            SetStatus(FrameworkTextKeys.RebindWaiting, "Press a control. Esc cancels.");
            try
            {
                operation = action.PerformInteractiveRebinding(index)
                    .WithCancelingThrough("<Keyboard>/escape")
                    .WithControlsExcluding("<Mouse>/position")
                    .WithControlsExcluding("<Mouse>/scroll")
                    .OnCancel(_ => FinishCancelledRebind())
                    .OnComplete(_ => FinishRebind());
                operation.Start();
            }
            catch (Exception exception)
            {
                operation?.Dispose();
                operation = null;
                if (actionWasEnabled) action.Enable();
                rebindingAction = null;
                rebindingIndex = -1;
                HideModal();
                SetStatus(FrameworkTextKeys.RebindActionUnavailable, "Input action is unavailable.");
                Debug.LogWarning($"Input rebind could not start: {exception.Message}", this);
            }
        }

        private void FinishRebind()
        {
            operation?.Dispose();
            operation = null;
            if (rebindingAction == null) return;
            if (actionWasEnabled) rebindingAction.Enable();

            if (TryFindConflict(actions, rebindingAction, rebindingIndex, out var conflict))
            {
                RestorePreviousOverride();
                SetStatus(FrameworkTextKeys.RebindConflict, "Already assigned to", conflict);
                if (modalRetry != null) modalRetry.gameObject.SetActive(true);
                return;
            }

            var owner = settings ?? HorrorGameContext.Ensure().RuntimeSettings;
            var json = actions.SaveBindingOverridesAsJson();
            displayedOverridesJson = json;
            owner.SetInputBindingOverridesJson(json);
            owner.Flush();
            rebindingAction = null;
            rebindingIndex = -1;
            HideModal();
            RefreshBindingLabels();
            SetStatus(FrameworkTextKeys.RebindUpdated, "Binding updated.");
        }

        private void RestorePreviousOverride()
        {
            if (previousOverridePath == null)
                rebindingAction.RemoveBindingOverride(rebindingIndex);
            else
                rebindingAction.ApplyBindingOverride(rebindingIndex, previousOverridePath);
        }

        private void CancelOperation()
        {
            if (operation == null) return;
            var current = operation;
            current.Cancel();
            if (operation == current) FinishCancelledRebind();
        }

        private void FinishCancelledRebind()
        {
            var current = operation;
            operation = null;
            current?.Dispose();
            if (rebindingAction != null && actionWasEnabled) rebindingAction.Enable();
            CancelCurrentRebind();
        }

        private void ShowModal()
        {
            if (!IsModalOpen && EventSystem.current != null)
                previousSelection = EventSystem.current.currentSelectedGameObject;
            if (settingsLayoutGroup != null)
            {
                settingsLayoutGroup.interactable = false;
                settingsLayoutGroup.blocksRaycasts = false;
            }
            rebindModal?.SetActive(true);
            if (modalCancel != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(modalCancel.gameObject);
        }

        private void HideModal()
        {
            rebindModal?.SetActive(false);
            if (settingsLayoutGroup != null)
            {
                settingsLayoutGroup.interactable = true;
                settingsLayoutGroup.blocksRaycasts = true;
            }
            if (previousSelection != null && previousSelection.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousSelection);
            previousSelection = null;
        }

        private static int FindBindingIndex(InputAction action, string part)
        {
            if (action == null) return -1;
            for (var i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (part != null)
                {
                    if (binding.isPartOfComposite &&
                        string.Equals(binding.name, part, StringComparison.OrdinalIgnoreCase)) return i;
                }
                else if (!binding.isComposite && !binding.isPartOfComposite &&
                    !string.IsNullOrEmpty(binding.path) &&
                    (binding.path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase) ||
                     binding.path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase)))
                {
                    return i;
                }
            }
            return -1;
        }

        private void RefreshBindingLabels()
        {
            if (bindingLabels == null) return;
            for (var i = 0; i < bindingLabels.Length && i < ActionPaths.Length; i++)
            {
                if (bindingLabels[i] == null) continue;
                var action = actions != null ? actions.FindAction(ActionPaths[i], false) : null;
                var index = FindBindingIndex(action, CompositeParts[i]);
                bindingLabels[i].text = index >= 0 ? action.GetBindingDisplayString(index) : "-";
            }
        }

        private void ApplySavedOverrides(string json)
        {
            if (actions == null || string.Equals(displayedOverridesJson, json, StringComparison.Ordinal)) return;
            if (!InputBindingOverridesJsonValidator.TryValidate(json, out var diagnostic))
            {
                Debug.LogWarning($"Input binding overrides were rejected: {diagnostic}", this);
                return;
            }
            var previous = actions.SaveBindingOverridesAsJson();
            try
            {
                actions.LoadBindingOverridesFromJson(string.IsNullOrWhiteSpace(json) ? string.Empty : json);
                displayedOverridesJson = json;
            }
            catch (Exception exception)
            {
                actions.LoadBindingOverridesFromJson(previous);
                Debug.LogWarning($"Input binding overrides could not be applied: {exception.Message}", this);
            }
        }

        private void OnSettingsChanged(RuntimeSettingsState state)
        {
            if (string.Equals(displayedOverridesJson, state.InputBindingOverridesJson, StringComparison.Ordinal))
                return;
            if (operation != null) CancelCurrentRebind();
            ApplySavedOverrides(state.InputBindingOverridesJson);
            RefreshBindingLabels();
        }

        private void SetModalText(string titleKey, string titleFallback, string promptKey, string promptFallback)
        {
            if (modalTitle != null)
                modalTitle.text = LocalizationTextResolver.Resolve(new LocalizedTextReference(titleKey, titleFallback));
            if (modalPrompt != null)
                modalPrompt.text = LocalizationTextResolver.Resolve(new LocalizedTextReference(promptKey, promptFallback));
        }

        private void SetStatus(string key, string fallback, string detail = null)
        {
            currentStatus = new LocalizedTextReference(key, fallback);
            statusDetail = detail;
            RefreshStatus();
        }

        private void OnLocaleChanged(Locale _)
        {
            RefreshStatus();
            RefreshBindingLabels();
            if (resetConfirmationOpen)
                SetModalText(FrameworkTextKeys.RebindResetTitle, "Reset all bindings?",
                    FrameworkTextKeys.RebindResetPrompt, "Default controls will be restored.");
        }

        private void RefreshStatus()
        {
            var text = LocalizationTextResolver.Resolve(currentStatus);
            if (!string.IsNullOrEmpty(statusDetail)) text += " " + statusDetail + ".";
            if (statusText != null) statusText.text = text;
            if (IsModalOpen && !resetConfirmationOpen && modalPrompt != null) modalPrompt.text = text;
        }
    }
}
