using UnityEngine;
using UnityEngine.InputSystem;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.UI.Settings;

namespace Setus.HorrorFramework.Player.Input
{
    [DisallowMultipleComponent]
    public sealed class InputSystemPlayerInputSource : MonoBehaviour, IPlayerInputSource
    {
        [SerializeField] private InputActionAsset actionsAsset;
        [SerializeField] private string moveActionPath = "Player/Move";
        [SerializeField] private string lookActionPath = "Player/Look";
        [SerializeField] private string sprintActionPath = "Player/Sprint";
        [SerializeField] private string crouchActionPath = "Player/Crouch";
        [SerializeField] private InputActionProperty moveAction;
        [SerializeField] private InputActionProperty lookAction;
        [SerializeField] private InputActionProperty sprintAction;
        [SerializeField] private InputActionProperty crouchAction;

        private InputAction resolvedMoveAction;
        private InputAction resolvedLookAction;
        private InputAction resolvedSprintAction;
        private InputAction resolvedCrouchAction;
        private RuntimeSettingsModel runtimeSettings;
        private string appliedBindingOverridesJson;

        private void OnValidate()
        {
            ResolveActions();
        }

        private void OnEnable()
        {
            runtimeSettings = HorrorGameContext.Active?.RuntimeSettings;
            if (runtimeSettings != null)
            {
                runtimeSettings.Changed += ApplyBindingOverrides;
                ApplyBindingOverrides(runtimeSettings.Current);
            }

            ResolveActions();
            SetEnabled(resolvedMoveAction, true);
            SetEnabled(resolvedLookAction, true);
            SetEnabled(resolvedSprintAction, true);
            SetEnabled(resolvedCrouchAction, true);
        }

        private void OnDisable()
        {
            if (runtimeSettings != null)
            {
                runtimeSettings.Changed -= ApplyBindingOverrides;
                runtimeSettings = null;
            }

            SetEnabled(resolvedMoveAction, false);
            SetEnabled(resolvedLookAction, false);
            SetEnabled(resolvedSprintAction, false);
            SetEnabled(resolvedCrouchAction, false);
        }

        public PlayerInputSnapshot ReadInput()
        {
            return new PlayerInputSnapshot(
                ReadVector2(resolvedMoveAction),
                ReadVector2(resolvedLookAction),
                IsPressed(resolvedSprintAction),
                IsPressed(resolvedCrouchAction));
        }

        public void ResolveConfiguredActions()
        {
            ResolveActions();
        }

        public void ConfigureActionsAsset(InputActionAsset asset)
        {
            actionsAsset = asset;
            moveActionPath = "Player/Move";
            lookActionPath = "Player/Look";
            sprintActionPath = "Player/Sprint";
            crouchActionPath = "Player/Crouch";
            moveAction = default;
            lookAction = default;
            sprintAction = default;
            crouchAction = default;
            ResolveActions();
        }

        public InputActionAsset ActionsAsset => actionsAsset;

        private void ApplyBindingOverrides(RuntimeSettingsState settings)
        {
            var overridesJson = settings.InputBindingOverridesJson;
            if (actionsAsset == null || string.Equals(
                    appliedBindingOverridesJson,
                    overridesJson,
                    System.StringComparison.Ordinal))
            {
                return;
            }

            if (!InputBindingOverridesJsonValidator.TryValidate(overridesJson, out var diagnostic))
            {
                Debug.LogWarning($"Input binding overrides could not be applied: {diagnostic}", this);
                return;
            }

            var previousOverridesJson = actionsAsset.SaveBindingOverridesAsJson();
            try
            {
                actionsAsset.LoadBindingOverridesFromJson(
                    string.IsNullOrWhiteSpace(overridesJson) ? string.Empty : overridesJson);
            }
            catch (System.Exception exception)
            {
                actionsAsset.LoadBindingOverridesFromJson(previousOverridesJson);
                Debug.LogWarning($"Input binding overrides could not be applied: {exception.Message}", this);
                return;
            }

            appliedBindingOverridesJson = overridesJson;
            ResolveActions();
        }

        public void ConfigureActions(
            InputActionReference move,
            InputActionReference look,
            InputActionReference sprint,
            InputActionReference crouch)
        {
            moveAction = new InputActionProperty(move);
            lookAction = new InputActionProperty(look);
            sprintAction = new InputActionProperty(sprint);
            crouchAction = new InputActionProperty(crouch);
            ResolveActions();
        }

        private void ResolveActions()
        {
            resolvedMoveAction = ResolveAction(moveAction, moveActionPath);
            resolvedLookAction = ResolveAction(lookAction, lookActionPath);
            resolvedSprintAction = ResolveAction(sprintAction, sprintActionPath);
            resolvedCrouchAction = ResolveAction(crouchAction, crouchActionPath);
        }

        private InputAction ResolveAction(InputActionProperty property, string actionPath)
        {
            if (property.reference != null && property.reference.action != null)
            {
                return property.reference.action;
            }

            if (actionsAsset != null)
            {
                var assetAction = actionsAsset.FindAction(actionPath, false);
                if (assetAction != null)
                {
                    return assetAction;
                }
            }

            var embeddedAction = property.action;
            return embeddedAction != null && embeddedAction.bindings.Count > 0 ? embeddedAction : null;
        }

        private static Vector2 ReadVector2(InputAction action)
        {
            return action != null && action.enabled ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        private static bool IsPressed(InputAction action)
        {
            return action != null && action.enabled && action.IsPressed();
        }

        private static void SetEnabled(InputAction action, bool enabled)
        {
            if (action == null)
            {
                return;
            }

            if (enabled && !action.enabled)
            {
                action.Enable();
            }
            else if (!enabled && action.enabled)
            {
                action.Disable();
            }
        }
    }
}
