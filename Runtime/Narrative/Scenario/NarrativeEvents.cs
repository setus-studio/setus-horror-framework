using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Localization;

namespace Setus.HorrorFramework.Narrative.Scenario
{
    public readonly struct NarrativeStateChanged : IGameplayEvent
    {
        public NarrativeStateChanged(NarrativeRuntimeState state)
        {
            State = state;
        }

        public NarrativeRuntimeState State { get; }
    }

    public readonly struct NarrativeStoryBeatConsumed : IGameplayEvent
    {
        public NarrativeStoryBeatConsumed(string storyBeatId)
        {
            StoryBeatId = storyBeatId;
        }

        public string StoryBeatId { get; }
    }

    public readonly struct NarrativePhoneMessageDelivered : IGameplayEvent
    {
        public NarrativePhoneMessageDelivered(string messageId)
        {
            MessageId = messageId;
        }

        public string MessageId { get; }
    }

    public readonly struct NarrativePhoneMessageRead : IGameplayEvent
    {
        public NarrativePhoneMessageRead(string messageId)
        {
            MessageId = messageId;
        }

        public string MessageId { get; }
    }

    public readonly struct NarrativePhoneMessageReplied : IGameplayEvent
    {
        public NarrativePhoneMessageReplied(string messageId)
        {
            MessageId = messageId;
        }

        public string MessageId { get; }
    }

    public readonly struct NarrativeNoteRead : IGameplayEvent
    {
        public NarrativeNoteRead(string noteId)
        {
            NoteId = noteId;
        }

        public string NoteId { get; }
    }

    public readonly struct NarrativeRouteUnlocked : IGameplayEvent
    {
        public NarrativeRouteUnlocked(string routeId)
        {
            RouteId = routeId;
        }

        public string RouteId { get; }
    }

    public readonly struct NarrativeSubtitleRequested : IGameplayEvent
    {
        public NarrativeSubtitleRequested(string speaker, string text, float duration)
            : this(
                new LocalizedTextReference(string.Empty, speaker),
                new LocalizedTextReference(string.Empty, text),
                duration)
        {
        }

        public NarrativeSubtitleRequested(
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
}
