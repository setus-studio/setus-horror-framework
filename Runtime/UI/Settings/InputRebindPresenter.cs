using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Setus.HorrorFramework.UI.Settings
{
    [DisallowMultipleComponent]
    public sealed class InputRebindPresenter : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private Text statusText;

        private InputActionRebindingExtensions.RebindingOperation operation;
        private InputAction rebindingAction;
        private LocalizedTextReference currentStatus;

        private void OnEnable()
        {
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            }
        }

        private void OnDisable()
        {
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            }
            CancelCurrentRebind();
        }

        public void RebindMoveForward() => BeginCompositePartRebind("Player/Move", "up");
        public void RebindMoveBackward() => BeginCompositePartRebind("Player/Move", "down");
        public void RebindMoveLeft() => BeginCompositePartRebind("Player/Move", "left");
        public void RebindMoveRight() => BeginCompositePartRebind("Player/Move", "right");
        public void RebindLook() => BeginSimpleRebind("Player/Look");
        public void RebindSprint() => BeginSimpleRebind("Player/Sprint");
        public void RebindCrouch() => BeginSimpleRebind("Player/Crouch");

        public void ResetBindings()
        {
            CancelCurrentRebind();
            if (actions == null)
            {
                SetStatus(FrameworkTextKeys.RebindMissingActions, "Input Actions asset is not assigned.");
                return;
            }

            actions.RemoveAllBindingOverrides();
            var settings = HorrorGameContext.Ensure().RuntimeSettings;
            settings.SetInputBindingOverridesJson(string.Empty);
            settings.Flush();
            SetStatus(FrameworkTextKeys.RebindReset, "Bindings reset.");
        }

        private void BeginSimpleRebind(string actionPath)
        {
            var action = actions != null ? actions.FindAction(actionPath, false) : null;
            if (action == null)
            {
                SetStatus(FrameworkTextKeys.RebindActionUnavailable, "Input action is unavailable.");
                return;
            }

            for (var index = 0; index < action.bindings.Count; index++)
            {
                var binding = action.bindings[index];
                if (!binding.isComposite && !binding.isPartOfComposite &&
                    (binding.effectivePath.StartsWith("<Keyboard>") || binding.effectivePath.StartsWith("<Mouse>")))
                {
                    BeginRebind(action, index);
                    return;
                }
            }

            SetStatus(FrameworkTextKeys.RebindActionUnavailable, "Input action is unavailable.");
        }

        private void BeginCompositePartRebind(string actionPath, string partName)
        {
            var action = actions != null ? actions.FindAction(actionPath, false) : null;
            if (action == null)
            {
                SetStatus(FrameworkTextKeys.RebindActionUnavailable, "Input action is unavailable.");
                return;
            }

            for (var index = 0; index < action.bindings.Count; index++)
            {
                var binding = action.bindings[index];
                if (binding.isPartOfComposite &&
                    string.Equals(binding.name, partName, System.StringComparison.OrdinalIgnoreCase))
                {
                    BeginRebind(action, index);
                    return;
                }
            }

            SetStatus(FrameworkTextKeys.RebindActionUnavailable, "Input action is unavailable.");
        }

        private void BeginRebind(InputAction action, int bindingIndex)
        {
            CancelCurrentRebind();
            rebindingAction = action;
            rebindingAction.Disable();
            SetStatus(FrameworkTextKeys.RebindWaiting, "Press a control. Esc cancels.");
            operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(_ => FinishRebind(false))
                .OnComplete(_ => FinishRebind(true));
            operation.Start();
        }

        private void FinishRebind(bool save)
        {
            operation?.Dispose();
            operation = null;
            rebindingAction?.Enable();
            rebindingAction = null;

            if (save && actions != null)
            {
                var settings = HorrorGameContext.Ensure().RuntimeSettings;
                settings.SetInputBindingOverridesJson(actions.SaveBindingOverridesAsJson());
                settings.Flush();
                SetStatus(FrameworkTextKeys.RebindUpdated, "Binding updated.");
            }
            else
            {
                SetStatus(FrameworkTextKeys.RebindCancelled, "Rebind cancelled.");
            }
        }

        private void CancelCurrentRebind()
        {
            if (operation != null)
            {
                operation.Cancel();
            }
        }

        private void SetStatus(string key, string fallback)
        {
            currentStatus = new LocalizedTextReference(key, fallback);
            RefreshStatus();
        }

        private void OnLocaleChanged(Locale _) => RefreshStatus();

        private void RefreshStatus()
        {
            if (statusText != null)
            {
                statusText.text = LocalizationTextResolver.Resolve(currentStatus);
            }
        }
    }
}
