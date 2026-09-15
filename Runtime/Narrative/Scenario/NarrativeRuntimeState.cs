using System;
using System.Collections.Generic;
using UnityEngine;

namespace Setus.HorrorFramework.Narrative.Scenario
{
    [Serializable]
    public sealed class NarrativeRuntimeState
    {
        [SerializeField] private string scenarioId;
        [SerializeField] private string[] consumedStoryBeatIds;
        [SerializeField] private PhoneMessageProgress[] phoneMessages;
        [SerializeField] private string latestDeliveredMessageId;
        [SerializeField] private string[] readNoteIds;
        [SerializeField] private string[] unlockedRouteIds;
        [SerializeField] private NarrativeBranchFlag[] branchFlags;

        public NarrativeRuntimeState(
            string scenarioId = null,
            IEnumerable<string> consumedStoryBeatIds = null,
            IEnumerable<PhoneMessageProgress> phoneMessages = null,
            IEnumerable<string> readNoteIds = null,
            IEnumerable<string> unlockedRouteIds = null,
            IEnumerable<NarrativeBranchFlag> branchFlags = null,
            string latestDeliveredMessageId = null)
        {
            this.scenarioId = scenarioId ?? string.Empty;
            this.consumedStoryBeatIds = ToArray(consumedStoryBeatIds);
            this.phoneMessages = phoneMessages == null
                ? Array.Empty<PhoneMessageProgress>()
                : new List<PhoneMessageProgress>(phoneMessages).ToArray();
            this.latestDeliveredMessageId = latestDeliveredMessageId;
            this.readNoteIds = ToArray(readNoteIds);
            this.unlockedRouteIds = ToArray(unlockedRouteIds);
            this.branchFlags = branchFlags == null
                ? Array.Empty<NarrativeBranchFlag>()
                : new List<NarrativeBranchFlag>(branchFlags).ToArray();
        }

        public string ScenarioId => scenarioId;
        public IReadOnlyList<string> ConsumedStoryBeatIds => consumedStoryBeatIds ?? Array.Empty<string>();
        public IReadOnlyList<PhoneMessageProgress> PhoneMessages => phoneMessages ?? Array.Empty<PhoneMessageProgress>();
        public string LatestDeliveredMessageId => latestDeliveredMessageId ?? string.Empty;
        public bool HasExplicitLatestDeliveredMessage => latestDeliveredMessageId != null;
        public IReadOnlyList<string> ReadNoteIds => readNoteIds ?? Array.Empty<string>();
        public IReadOnlyList<string> UnlockedRouteIds => unlockedRouteIds ?? Array.Empty<string>();
        public IReadOnlyList<NarrativeBranchFlag> BranchFlags => branchFlags ?? Array.Empty<NarrativeBranchFlag>();

        private static string[] ToArray(IEnumerable<string> values)
        {
            return values == null ? Array.Empty<string>() : new List<string>(values).ToArray();
        }
    }

    [Serializable]
    public sealed class PhoneMessageProgress
    {
        [SerializeField] private string messageId;
        [SerializeField] private bool delivered;
        [SerializeField] private bool read;
        [SerializeField] private bool replied;

        public PhoneMessageProgress(string messageId, bool delivered, bool read, bool replied)
        {
            this.messageId = messageId ?? string.Empty;
            this.delivered = delivered;
            this.read = read;
            this.replied = replied;
        }

        public string MessageId => messageId;
        public bool Delivered => delivered;
        public bool Read => read;
        public bool Replied => replied;
    }

    [Serializable]
    public sealed class NarrativeBranchFlag
    {
        [SerializeField] private string flagId;
        [SerializeField] private bool value;

        public NarrativeBranchFlag(string flagId, bool value)
        {
            this.flagId = flagId ?? string.Empty;
            this.value = value;
        }

        public string FlagId => flagId;
        public bool Value => value;
    }
}
