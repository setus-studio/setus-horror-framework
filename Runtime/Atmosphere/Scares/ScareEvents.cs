using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Localization;

namespace Setus.HorrorFramework.Atmosphere.Scares
{
    public readonly struct ScareStateChanged : IGameplayEvent
    {
        public ScareStateChanged(string scareId, ScareLifecycleState state, bool restored)
        {
            ScareId = scareId;
            State = state;
            Restored = restored;
        }

        public string ScareId { get; }
        public ScareLifecycleState State { get; }
        public bool Restored { get; }
    }

    public readonly struct TensionChanged : IGameplayEvent
    {
        public TensionChanged(float intensity, string sourceId)
        {
            Intensity = intensity;
            SourceId = sourceId;
        }

        public float Intensity { get; }
        public string SourceId { get; }
    }

    public readonly struct AtmosphereSubtitleCueRequested : IGameplayEvent
    {
        public AtmosphereSubtitleCueRequested(string speaker, string text, float duration)
            : this(
                new LocalizedTextReference(string.Empty, speaker),
                new LocalizedTextReference(string.Empty, text),
                duration)
        {
        }

        public AtmosphereSubtitleCueRequested(
            LocalizedTextReference speaker,
            LocalizedTextReference text,
            float duration)
        {
            SpeakerReference = speaker;
            TextReference = text;
            Duration = duration;
        }

        public LocalizedTextReference SpeakerReference { get; }
        public LocalizedTextReference TextReference { get; }
        public string Speaker => SpeakerReference.FallbackText;
        public string Text => TextReference.FallbackText;
        public float Duration { get; }
    }

    public readonly struct ScarePowerStateChanged : IGameplayEvent
    {
        public ScarePowerStateChanged(string powerStateId, bool isPowered)
        {
            PowerStateId = powerStateId;
            IsPowered = isPowered;
        }

        public string PowerStateId { get; }
        public bool IsPowered { get; }
    }
}
