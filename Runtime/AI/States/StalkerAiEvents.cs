using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Localization;
using UnityEngine;

namespace Setus.HorrorFramework.AI.States
{
    public readonly struct StalkerAiStateChanged : IGameplayEvent
    {
        public StalkerAiStateChanged(
            string stableId,
            StalkerAiState previousState,
            StalkerAiState state,
            StalkerAiTransitionReason reason,
            Vector3 lastKnownPosition,
            bool restored)
        {
            StableId = stableId;
            PreviousState = previousState;
            State = state;
            Reason = reason;
            LastKnownPosition = lastKnownPosition;
            Restored = restored;
        }

        public string StableId { get; }
        public StalkerAiState PreviousState { get; }
        public StalkerAiState State { get; }
        public StalkerAiTransitionReason Reason { get; }
        public Vector3 LastKnownPosition { get; }
        public bool Restored { get; }
    }

    public readonly struct StalkerAiHighStakesFeedbackRequested : IGameplayEvent
    {
        public StalkerAiHighStakesFeedbackRequested(string stableId, string subtitleText)
            : this(stableId, new LocalizedTextReference(string.Empty, subtitleText))
        {
        }

        public StalkerAiHighStakesFeedbackRequested(
            string stableId,
            LocalizedTextReference subtitle)
        {
            StableId = stableId;
            SubtitleReference = subtitle;
        }

        public string StableId { get; }
        public LocalizedTextReference SubtitleReference { get; }
        public string SubtitleText => SubtitleReference.FallbackText;
    }
}
