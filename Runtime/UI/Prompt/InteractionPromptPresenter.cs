using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Interaction.Prompts;
using Setus.HorrorFramework.Interaction.Triggers;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Setus.HorrorFramework.Localization;

namespace Setus.HorrorFramework.UI.Prompt
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text promptText;
        [SerializeField] private Text debugText;
        [SerializeField, Min(0.1f)] private float feedbackSeconds = 1.75f;
        [SerializeField] private Color normalPromptColor = Color.white;
        [SerializeField] private Color rejectedPromptColor = new Color(1f, 0.78f, 0.35f, 1f);
        [SerializeField] private Color feedbackColor = new Color(0.72f, 0.9f, 1f, 1f);

        private HorrorGameContext context;
        private IDisposable promptSubscription;
        private IDisposable debugSubscription;
        private IDisposable interactionSubscription;
        private IDisposable triggerSubscription;
        private InteractionPromptChanged lastPrompt;
        private string transientFeedback;
        private float transientFeedbackUntil;

        private void Awake()
        {
            EnsureContext();
            if (root == null)
            {
                root = gameObject;
            }
        }

        private void OnEnable()
        {
            EnsureContext();
            promptSubscription = context.Events.Subscribe<InteractionPromptChanged>(OnPromptChanged);
            debugSubscription = context.Events.Subscribe<HorrorDebugStateChanged>(_ => Apply(lastPrompt));
            interactionSubscription = context.Events.Subscribe<InteractionPerformed>(OnInteractionPerformed);
            triggerSubscription = context.Events.Subscribe<InteractionTriggerEntered>(OnTriggerEntered);
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            }
            Apply(lastPrompt);
        }

        private void OnDisable()
        {
            promptSubscription?.Dispose();
            promptSubscription = null;
            debugSubscription?.Dispose();
            debugSubscription = null;
            interactionSubscription?.Dispose();
            interactionSubscription = null;
            triggerSubscription?.Dispose();
            triggerSubscription = null;
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            }
        }

        public void Configure(GameObject rootObject, Text promptLabel, Text debugLabel)
        {
            EnsureContext();
            root = rootObject;
            promptText = promptLabel;
            debugText = debugLabel;
            Apply(lastPrompt);
        }

        private void OnPromptChanged(InteractionPromptChanged changed)
        {
            lastPrompt = changed;
            Apply(changed);
        }

        private void OnLocaleChanged(Locale _) => Apply(lastPrompt);

        private void Update()
        {
            if (!string.IsNullOrWhiteSpace(transientFeedback) && Time.unscaledTime >= transientFeedbackUntil)
            {
                transientFeedback = null;
                Apply(lastPrompt);
            }
        }

        private void OnInteractionPerformed(InteractionPerformed performed)
        {
            var message = LocalizationTextResolver.Resolve(performed.Result.LocalizedMessage);
            if (!performed.Result.Success && string.IsNullOrWhiteSpace(message))
            {
                message = performed.Result.RejectionReason.ToString();
            }

            ShowTransientFeedback(message);
        }

        private void OnTriggerEntered(InteractionTriggerEntered entered)
        {
            if (context != null && context.Debug.IsEnabled)
            {
                ShowTransientFeedback($"Entered trigger: {entered.TriggerId}");
            }
        }

        private void Apply(InteractionPromptChanged changed)
        {
            EnsureContext();
            if (IsShowingTransientFeedback())
            {
                ShowText(transientFeedback, feedbackColor);
                SetActive(debugText != null ? debugText.gameObject : null, false);
                SetActive(root, true);
                return;
            }

            var hasPromptText = !string.IsNullOrWhiteSpace(changed.Prompt.Text);
            var hasDebugRejection = context.Debug.IsEnabled && changed.Prompt.RejectionReason != InteractionRejectionReason.None;
            SetActive(root, hasPromptText || hasDebugRejection);

            if (promptText != null)
            {
                promptText.text = hasPromptText
                    ? LocalizationTextResolver.Resolve(changed.Prompt.LocalizedText)
                    : string.Empty;
                promptText.color = changed.Prompt.CanInteract ? normalPromptColor : rejectedPromptColor;
            }

            if (debugText != null)
            {
                var showDebug = hasDebugRejection;
                debugText.gameObject.SetActive(showDebug);
                debugText.text = showDebug
                    ? $"{changed.TargetName}: {changed.Prompt.RejectionReason} {changed.Prompt.RejectionMessage}"
                    : string.Empty;
            }
        }

        private void ShowTransientFeedback(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            transientFeedback = message;
            transientFeedbackUntil = Time.unscaledTime + feedbackSeconds;
            Apply(lastPrompt);
        }

        private bool IsShowingTransientFeedback()
        {
            return !string.IsNullOrWhiteSpace(transientFeedback) && Time.unscaledTime < transientFeedbackUntil;
        }

        private void ShowText(string text, Color color)
        {
            if (promptText == null)
            {
                return;
            }

            promptText.text = text;
            promptText.color = color;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void EnsureContext()
        {
            if (context == null)
            {
                context = HorrorGameContext.Ensure();
            }
        }
    }
}
