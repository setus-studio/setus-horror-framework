using UnityEngine;
using Setus.HorrorFramework.Localization;

namespace Setus.HorrorFramework.Atmosphere.Scares
{
    [CreateAssetMenu(fileName = "ScareDefinition", menuName = "Setus/Horror Framework/Atmosphere/Scare Definition")]
    public sealed class ScareDefinition : ScriptableObject
    {
        [SerializeField] private string scareId;
        [SerializeField, Range(0f, 1f)] private float tensionIntensity = 0.7f;
        [SerializeField, Min(0.1f)] private float presentationDuration = 1.5f;
        [SerializeField] private string powerStateId;
        [SerializeField] private string fallbackSpeaker = "Unknown";
        [SerializeField] private string fallbackSpeakerLocalizationKey;
        [SerializeField, TextArea] private string fallbackCueText;
        [SerializeField] private string fallbackCueLocalizationKey;
        [SerializeField, Min(0.5f)] private float fallbackCueDuration = 2.5f;

        public string ScareId => scareId;
        public float TensionIntensity => tensionIntensity;
        public float PresentationDuration => presentationDuration;
        public string PowerStateId => powerStateId;
        public string FallbackSpeaker => fallbackSpeaker;
        public string FallbackCueText => fallbackCueText;
        public float FallbackCueDuration => fallbackCueDuration;
        public LocalizedTextReference LocalizedFallbackSpeaker =>
            new LocalizedTextReference(fallbackSpeakerLocalizationKey, fallbackSpeaker);
        public LocalizedTextReference LocalizedFallbackCue =>
            new LocalizedTextReference(fallbackCueLocalizationKey, fallbackCueText);
    }
}
