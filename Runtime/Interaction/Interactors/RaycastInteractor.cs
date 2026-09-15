using System.Collections.Generic;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Interaction.Prompts;
using Setus.HorrorFramework.Player.State;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Setus.HorrorFramework.Interaction.Interactors
{
    [DisallowMultipleComponent]
    public sealed class RaycastInteractor : MonoBehaviour
    {
        [SerializeField] private Transform rayOrigin;
        [SerializeField, Min(0.1f)] private float maxDistance = 2.4f;
        [SerializeField] private LayerMask interactionMask = Physics.DefaultRaycastLayers;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
        [SerializeField] private InputActionProperty interactAction;
        [SerializeField] private Key fallbackInteractKey = Key.E;
        [SerializeField] private bool drawDebugRay = true;
        [SerializeField, Min(0f)] private float focusLostGraceSeconds = 0.12f;

        private HorrorGameContext context;
        private IPlayerControlStateTarget playerControlTarget;
        private IInteractable focusedInteractable;
        private InteractionPrompt lastPrompt;
        private string lastTargetName;
        private float lastDistance = -1f;
        private float lastFocusSeenAt = float.NegativeInfinity;
        private readonly List<MonoBehaviour> interactableCandidates = new List<MonoBehaviour>(8);

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
            if (rayOrigin == null)
            {
                var playerCamera = GetComponentInChildren<UnityEngine.Camera>(true);
                if (playerCamera != null)
                {
                    rayOrigin = playerCamera.transform;
                }
            }

            var behaviours = GetComponentsInParent<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPlayerControlStateTarget controlTarget)
                {
                    playerControlTarget = controlTarget;
                    break;
                }
            }
        }

        private void OnEnable()
        {
            if (interactAction.action != null && !interactAction.action.enabled)
            {
                interactAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (interactAction.action != null && interactAction.action.enabled)
            {
                interactAction.action.Disable();
            }

            ClearFocus(true);
        }

        private void Update()
        {
            if (rayOrigin == null || !CanInteract() || Time.timeScale <= 0f)
            {
                ClearFocus(true);
                return;
            }

            var ray = new Ray(rayOrigin.position, rayOrigin.forward);
            if (drawDebugRay && context.Debug.IsEnabled)
            {
                Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.cyan);
            }

            if (!Physics.Raycast(ray, out var hit, maxDistance, interactionMask, triggerInteraction))
            {
                ClearFocusAfterGrace();
                return;
            }

            var interactable = FindInteractable(hit.collider);
            if (interactable == null)
            {
                ClearFocusAfterGrace();
                return;
            }

            focusedInteractable = interactable;
            lastFocusSeenAt = Time.unscaledTime;
            var interactionContext = new InteractionContext(gameObject, context);
            var prompt = interactable.GetPrompt(interactionContext);
            var targetName = ResolveTargetName(interactable, hit.collider);
            PublishPrompt(interactable, targetName, hit.distance, prompt);

            if (WasInteractPressed())
            {
                var result = interactable.Interact(interactionContext);
                context.Events.Publish(new InteractionPerformed(targetName, prompt.InteractionType, result));
                var nextPrompt = interactable.GetPrompt(interactionContext);
                PublishPrompt(interactable, targetName, hit.distance, nextPrompt, true);
            }
        }

        public void ConfigureRayOrigin(Transform origin)
        {
            rayOrigin = origin;
        }

        public void ConfigureInteractAction(InputAction action)
        {
            interactAction = new InputActionProperty(action);
        }

        private bool WasInteractPressed()
        {
            if (interactAction.action != null && interactAction.action.WasPressedThisFrame())
            {
                return true;
            }

            var keyboard = Keyboard.current;
            return keyboard != null && keyboard[fallbackInteractKey].wasPressedThisFrame;
        }

        private bool CanInteract()
        {
            return context != null &&
                   playerControlTarget != null &&
                   PlayerInteractionPermissionPolicy.CanInteract(
                       playerControlTarget.CurrentControlState,
                       context.PauseFlow.Current.IsPaused,
                       context.UiShell.Current.CurrentScreen != Setus.HorrorFramework.UI.Menus.UiShellScreen.Hidden,
                       context.Transitions.IsTransitioning);
        }

        private void ClearFocusAfterGrace()
        {
            if (focusedInteractable != null && Time.unscaledTime - lastFocusSeenAt <= focusLostGraceSeconds)
            {
                return;
            }

            ClearFocus(false);
        }

        private void ClearFocus(bool force)
        {
            focusedInteractable = null;
            PublishPrompt(
                default,
                null,
                -1f,
                new InteractionPrompt(string.Empty, false, InteractionType.Use, InteractionRejectionReason.NoTarget),
                force);
        }

        private void PublishPrompt(
            IInteractable interactable,
            string targetName,
            float distance,
            InteractionPrompt prompt,
            bool force = false)
        {
            if (!force &&
                focusedInteractable == interactable &&
                string.Equals(lastTargetName, targetName, System.StringComparison.Ordinal) &&
                lastPrompt.Text == prompt.Text &&
                lastPrompt.CanInteract == prompt.CanInteract &&
                lastPrompt.RejectionReason == prompt.RejectionReason)
            {
                return;
            }

            lastPrompt = prompt;
            lastTargetName = targetName;
            lastDistance = distance;
            context.Events.Publish(new InteractionPromptChanged(prompt, targetName, distance));
        }

        private IInteractable FindInteractable(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            // Reuse the buffer because focus resolution runs every frame while aiming at the world.
            interactableCandidates.Clear();
            collider.GetComponentsInParent(true, interactableCandidates);
            for (var i = 0; i < interactableCandidates.Count; i++)
            {
                if (interactableCandidates[i] is IInteractable interactable)
                {
                    return interactable;
                }
            }

            return null;
        }

        private static string ResolveTargetName(IInteractable interactable, Collider collider)
        {
            if (interactable is MonoBehaviour behaviour)
            {
                return behaviour.name;
            }

            return collider != null ? collider.name : null;
        }
    }
}
