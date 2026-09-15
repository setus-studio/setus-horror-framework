using Setus.HorrorFramework.Core.Services;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Setus.HorrorFramework.Atmosphere.Scares
{
    [DisallowMultipleComponent]
    public sealed class ScareDebugTrigger : MonoBehaviour
    {
        [SerializeField] private string scareId;
        [SerializeField] private Key triggerKey = Key.F7;
        [SerializeField] private Key resetKey = Key.F8;

        private InputAction triggerAction;
        private InputAction resetAction;

        private void Awake()
        {
            triggerAction = CreateAction("TriggerDebugScare", triggerKey, Trigger);
            resetAction = CreateAction("ResetDebugScare", resetKey, ResetScare);
        }

        private void OnEnable()
        {
            triggerAction?.Enable();
            resetAction?.Enable();
        }

        private void OnDisable()
        {
            triggerAction?.Disable();
            resetAction?.Disable();
        }

        private void OnDestroy()
        {
            triggerAction?.Dispose();
            resetAction?.Dispose();
            triggerAction = null;
            resetAction = null;
        }

        [ContextMenu("Trigger Debug Scare")]
        public void Trigger()
        {
            var context = HorrorGameContext.Active;
            if (context == null)
            {
                Debug.LogWarning("[Setus:Horror:Atmosphere] Debug scare is unavailable because the game context is not initialized.", this);
                return;
            }

            if (!context.Debug.IsEnabled)
            {
                Debug.LogWarning("[Setus:Horror:Atmosphere] Debug scare is disabled. Enable global debug before triggering it.", this);
                return;
            }

            if (!context.Scares.TryTrigger(scareId, true))
            {
                Debug.LogWarning(
                    $"[Setus:Horror:Atmosphere] Debug scare could not trigger: {scareId}. It must be configured and Armed.",
                    this);
            }
        }

        [ContextMenu("Reset Debug Scare")]
        public void ResetScare()
        {
            var context = HorrorGameContext.Active;
            if (context == null)
            {
                Debug.LogWarning("[Setus:Horror:Atmosphere] Debug scare is unavailable because the game context is not initialized.", this);
                return;
            }

            if (!context.Debug.IsEnabled)
            {
                Debug.LogWarning("[Setus:Horror:Atmosphere] Debug scare is disabled. Enable global debug before resetting it.", this);
                return;
            }

            if (!context.Scares.ResetForDebug(scareId))
            {
                Debug.LogWarning(
                    $"[Setus:Horror:Atmosphere] Debug scare could not reset: {scareId}. It must be configured.",
                    this);
            }
        }

        private static InputAction CreateAction(string actionName, Key key, System.Action callback)
        {
            if (key == Key.None)
            {
                return null;
            }

            var action = new InputAction(
                actionName,
                InputActionType.Button,
                $"<Keyboard>/{key.ToString().ToLowerInvariant()}");
            action.performed += _ => callback();
            return action;
        }
    }
}
