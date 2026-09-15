using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.UI.Menus;
using Setus.HorrorFramework.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Setus.HorrorFramework.UI.Subtitles
{
    [DisallowMultipleComponent]
    public sealed class SubtitlePresenter : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text subtitleText;

        private HorrorGameContext context;
        private IDisposable subscription;
        private IDisposable atmosphereSubscription;
        private float visibleUntil;
        private LocalizedTextReference activeSpeaker;
        private LocalizedTextReference activeText;

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
        }

        private void OnEnable()
        {
            if (context == null)
            {
                context = HorrorGameContext.Ensure();
            }

            subscription?.Dispose();
            subscription = context.Events.Subscribe<NarrativeSubtitleRequested>(ShowSubtitle);
            atmosphereSubscription?.Dispose();
            atmosphereSubscription = context.Events.Subscribe<AtmosphereSubtitleCueRequested>(ShowAtmosphereSubtitle);
            context.UiShell.Changed += OnUiShellChanged;
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            }
            SetVisible(false);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
            atmosphereSubscription?.Dispose();
            atmosphereSubscription = null;
            if (context != null)
            {
                context.UiShell.Changed -= OnUiShellChanged;
            }
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            }
            visibleUntil = 0f;
        }

        private void Update()
        {
            if (visibleUntil > 0f && Time.unscaledTime >= visibleUntil)
            {
                visibleUntil = 0f;
                SetVisible(false);
            }
        }

        private void ShowSubtitle(NarrativeSubtitleRequested requested)
        {
            ShowSubtitle(requested.SpeakerReference, requested.TextReference, requested.Duration);
        }

        private void ShowAtmosphereSubtitle(AtmosphereSubtitleCueRequested requested)
        {
            ShowSubtitle(requested.SpeakerReference, requested.TextReference, requested.Duration);
        }

        private void ShowSubtitle(
            LocalizedTextReference speaker,
            LocalizedTextReference text,
            float duration)
        {
            activeSpeaker = speaker;
            activeText = text;
            if (context?.RuntimeSettings?.Current.SubtitlesEnabled != true ||
                context.UiShell.Current.CurrentScreen != UiShellScreen.Hidden)
            {
                return;
            }

            if (speakerText != null)
            {
                speakerText.text = LocalizationTextResolver.Resolve(speaker);
            }

            if (subtitleText != null)
            {
                subtitleText.text = LocalizationTextResolver.Resolve(text);
            }

            visibleUntil = Time.unscaledTime + Mathf.Max(0.5f, duration);
            SetVisible(true);
        }

        private void OnLocaleChanged(Locale _)
        {
            if (visibleUntil > Time.unscaledTime)
            {
                if (speakerText != null)
                {
                    speakerText.text = LocalizationTextResolver.Resolve(activeSpeaker);
                }

                if (subtitleText != null)
                {
                    subtitleText.text = LocalizationTextResolver.Resolve(activeText);
                }
            }
        }

        private void OnUiShellChanged(UiShellStateChanged _)
        {
            if (context.UiShell.Current.CurrentScreen != UiShellScreen.Hidden)
            {
                visibleUntil = 0f;
                SetVisible(false);
            }
        }

        private void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible)
            {
                root.SetActive(visible);
            }
        }
    }
}
