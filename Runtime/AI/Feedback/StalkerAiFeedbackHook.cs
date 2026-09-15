using System;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.AI.States;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Localization;
using UnityEngine;
using Setus.HorrorFramework.UI.Settings;

namespace Setus.HorrorFramework.AI.Feedback
{
    [DisallowMultipleComponent]
    public sealed class StalkerAiFeedbackHook : MonoBehaviour
    {
        [SerializeField] private StalkerAiController stalker;
        [SerializeField] private AudioSource cueSource;
        private IDisposable feedbackSubscription;
        private RuntimeSettingsModel settings;
        private float authoredVolume = 1f;

        private void Awake()
        {
            if (stalker == null)
            {
                stalker = GetComponent<StalkerAiController>();
            }

            authoredVolume = cueSource != null ? cueSource.volume : 1f;
        }

        private void OnEnable()
        {
            var context = HorrorGameContext.Active;
            if (context != null)
            {
                feedbackSubscription = context.Events.Subscribe<StalkerAiHighStakesFeedbackRequested>(OnHighStakesFeedback);
                settings = context.RuntimeSettings;
                settings.Changed += ApplyVolume;
                ApplyVolume(settings.Current);
            }
        }

        private void OnDisable()
        {
            feedbackSubscription?.Dispose();
            feedbackSubscription = null;
            if (settings != null)
            {
                settings.Changed -= ApplyVolume;
                settings = null;
            }
        }

        private void OnHighStakesFeedback(StalkerAiHighStakesFeedbackRequested requested)
        {
            if (stalker == null || !string.Equals(requested.StableId, stalker.StableId, StringComparison.Ordinal))
            {
                return;
            }

            if (cueSource != null && cueSource.clip != null)
            {
                cueSource.Play();
            }

            var profile = stalker.TuningProfile;
            HorrorGameContext.Active?.Events.Publish(new AtmosphereSubtitleCueRequested(
                new LocalizedTextReference(FrameworkTextKeys.UnknownSpeaker, "Unknown"),
                requested.SubtitleReference,
                profile != null ? profile.ChaseSubtitleDuration : 2f));
        }

        private void ApplyVolume(RuntimeSettingsState state)
        {
            if (cueSource != null)
            {
                cueSource.volume = authoredVolume * state.MasterVolume * state.SfxVolume;
            }
        }
    }
}
