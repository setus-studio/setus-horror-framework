using System;
using System.Collections.Generic;
using System.Linq;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Narrative.Phone;

namespace Setus.HorrorFramework.Narrative.Scenario
{
    public sealed class NarrativeRuntimeModel : RuntimeStateOwnerBase<NarrativeRuntimeState>, IRestoreStateValidator
    {
        private readonly IGameplayEventBus events;
        private readonly HashSet<string> consumedStoryBeatIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, PhoneMessageProgress> phoneMessagesById =
            new Dictionary<string, PhoneMessageProgress>(StringComparer.Ordinal);
        private readonly Dictionary<string, PhoneMessageDefinition> phoneDefinitionsById =
            new Dictionary<string, PhoneMessageDefinition>(StringComparer.Ordinal);
        private readonly HashSet<string> readNoteIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> unlockedRouteIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> branchFlagsById = new Dictionary<string, bool>(StringComparer.Ordinal);

        private string scenarioId = string.Empty;
        private string configuredScenarioId = string.Empty;
        private string latestDeliveredMessageId = string.Empty;
        private bool hasExplicitLatestDeliveryOrder = true;

        public NarrativeRuntimeModel(IGameplayEventBus events = null)
            : base("narrative.state")
        {
            this.events = events;
        }

        public bool HasConsumedStoryBeat(string storyBeatId) => consumedStoryBeatIds.Contains(storyBeatId ?? string.Empty);
        public bool HasReadNote(string noteId) => readNoteIds.Contains(noteId ?? string.Empty);
        public bool IsRouteUnlocked(string routeId) => unlockedRouteIds.Contains(routeId ?? string.Empty);

        public bool TryGetPhoneProgress(string messageId, out PhoneMessageProgress progress)
        {
            return phoneMessagesById.TryGetValue(messageId ?? string.Empty, out progress);
        }

        public bool TryGetPhoneDefinition(string messageId, out PhoneMessageDefinition definition)
        {
            return phoneDefinitionsById.TryGetValue(messageId ?? string.Empty, out definition);
        }

        public bool TryGetLatestDeliveredPhoneMessageId(out string messageId)
        {
            messageId = latestDeliveredMessageId;
            return !string.IsNullOrWhiteSpace(messageId) &&
                   phoneMessagesById.TryGetValue(messageId, out var progress) &&
                   progress.Delivered;
        }

        public void ConfigurePhoneMessages(IEnumerable<PhoneMessageDefinition> definitions)
        {
            phoneDefinitionsById.Clear();
            foreach (var definition in definitions ?? Enumerable.Empty<PhoneMessageDefinition>())
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.MessageId))
                {
                    throw new InvalidOperationException("Phone message definitions require non-empty message IDs.");
                }

                if (phoneDefinitionsById.ContainsKey(definition.MessageId))
                {
                    throw new InvalidOperationException(
                        $"Scenario contains duplicate phone message ID '{definition.MessageId}'.");
                }

                phoneDefinitionsById.Add(definition.MessageId, definition);
            }
        }

        public bool TryGetBranchFlag(string flagId, out bool value)
        {
            return branchFlagsById.TryGetValue(flagId ?? string.Empty, out value);
        }

        public void InitializeScenario(string configuredScenarioId)
        {
            if (string.IsNullOrWhiteSpace(configuredScenarioId))
            {
                throw new ArgumentException("Scenario ID must not be empty.", nameof(configuredScenarioId));
            }

            if (!string.IsNullOrWhiteSpace(this.configuredScenarioId) &&
                !string.Equals(this.configuredScenarioId, configuredScenarioId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Cannot configure scenario '{configuredScenarioId}' while '{this.configuredScenarioId}' is loaded.");
            }

            this.configuredScenarioId = configuredScenarioId;
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                scenarioId = configuredScenarioId;
                PublishChanged();
                return;
            }

            if (!string.Equals(scenarioId, configuredScenarioId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Cannot initialize scenario '{configuredScenarioId}' while '{scenarioId}' is active.");
            }
        }

        public bool TryConsumeStoryBeat(string storyBeatId)
        {
            if (string.IsNullOrWhiteSpace(storyBeatId) || !consumedStoryBeatIds.Add(storyBeatId))
            {
                return false;
            }

            events?.Publish(new NarrativeStoryBeatConsumed(storyBeatId));
            PublishChanged();
            return true;
        }

        public bool TryDeliverPhoneMessage(string messageId)
        {
            if (string.IsNullOrWhiteSpace(messageId) || phoneMessagesById.ContainsKey(messageId))
            {
                return false;
            }

            phoneMessagesById.Add(messageId, new PhoneMessageProgress(messageId, true, false, false));
            latestDeliveredMessageId = messageId;
            hasExplicitLatestDeliveryOrder = true;
            events?.Publish(new NarrativePhoneMessageDelivered(messageId));
            PublishChanged();
            return true;
        }

        public bool TryReadPhoneMessage(string messageId)
        {
            if (!phoneMessagesById.TryGetValue(messageId ?? string.Empty, out var progress) || progress.Read)
            {
                return false;
            }

            phoneMessagesById[messageId] = new PhoneMessageProgress(messageId, true, true, progress.Replied);
            events?.Publish(new NarrativePhoneMessageRead(messageId));
            PublishChanged();
            return true;
        }

        public bool TryReplyToPhoneMessage(string messageId)
        {
            if (!phoneMessagesById.TryGetValue(messageId ?? string.Empty, out var progress) ||
                !progress.Read || progress.Replied)
            {
                return false;
            }

            phoneMessagesById[messageId] = new PhoneMessageProgress(messageId, true, true, true);
            events?.Publish(new NarrativePhoneMessageReplied(messageId));
            PublishChanged();
            return true;
        }

        public bool TryReadNote(string noteId)
        {
            if (string.IsNullOrWhiteSpace(noteId) || !readNoteIds.Add(noteId))
            {
                return false;
            }

            events?.Publish(new NarrativeNoteRead(noteId));
            PublishChanged();
            return true;
        }

        public bool TryUnlockRoute(string routeId)
        {
            if (string.IsNullOrWhiteSpace(routeId) || !unlockedRouteIds.Add(routeId))
            {
                return false;
            }

            events?.Publish(new NarrativeRouteUnlocked(routeId));
            PublishChanged();
            return true;
        }

        public bool SetBranchFlag(string flagId, bool value)
        {
            if (string.IsNullOrWhiteSpace(flagId) ||
                (branchFlagsById.TryGetValue(flagId, out var existing) && existing == value))
            {
                return false;
            }

            branchFlagsById[flagId] = value;
            PublishChanged();
            return true;
        }

        public void Reset()
        {
            scenarioId = string.Empty;
            consumedStoryBeatIds.Clear();
            phoneMessagesById.Clear();
            latestDeliveredMessageId = string.Empty;
            hasExplicitLatestDeliveryOrder = true;
            readNoteIds.Clear();
            unlockedRouteIds.Clear();
            branchFlagsById.Clear();
            PublishChanged();
        }

        public override NarrativeRuntimeState CaptureState()
        {
            return new NarrativeRuntimeState(
                scenarioId,
                consumedStoryBeatIds.OrderBy(id => id, StringComparer.Ordinal),
                phoneMessagesById.Values.OrderBy(progress => progress.MessageId, StringComparer.Ordinal),
                readNoteIds.OrderBy(id => id, StringComparer.Ordinal),
                unlockedRouteIds.OrderBy(id => id, StringComparer.Ordinal),
                branchFlagsById
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new NarrativeBranchFlag(pair.Key, pair.Value)),
                hasExplicitLatestDeliveryOrder ? latestDeliveredMessageId : null);
        }

        public override void RestoreState(NarrativeRuntimeState state)
        {
            scenarioId = state?.ScenarioId ?? string.Empty;
            consumedStoryBeatIds.Clear();
            phoneMessagesById.Clear();
            latestDeliveredMessageId = string.Empty;
            hasExplicitLatestDeliveryOrder = true;
            readNoteIds.Clear();
            unlockedRouteIds.Clear();
            branchFlagsById.Clear();

            if (state != null)
            {
                AddAll(consumedStoryBeatIds, state.ConsumedStoryBeatIds);
                for (var i = 0; i < state.PhoneMessages.Count; i++)
                {
                    var progress = state.PhoneMessages[i];
                    phoneMessagesById[progress.MessageId] = progress;
                }

                RestoreLatestDeliveredMessageId(state);

                AddAll(readNoteIds, state.ReadNoteIds);
                AddAll(unlockedRouteIds, state.UnlockedRouteIds);
                for (var i = 0; i < state.BranchFlags.Count; i++)
                {
                    var flag = state.BranchFlags[i];
                    branchFlagsById[flag.FlagId] = flag.Value;
                }
            }

            PublishChanged();
        }

        public RestoreStateValidationResult ValidateRestoreState(object state)
        {
            if (!(state is NarrativeRuntimeState narrativeState))
            {
                return RestoreStateValidationResult.Invalid($"Expected {nameof(NarrativeRuntimeState)}.");
            }

            if (!string.IsNullOrWhiteSpace(configuredScenarioId) &&
                !string.IsNullOrWhiteSpace(narrativeState.ScenarioId) &&
                !string.Equals(narrativeState.ScenarioId, configuredScenarioId, StringComparison.Ordinal))
            {
                return RestoreStateValidationResult.Invalid(
                    $"Narrative state belongs to scenario '{narrativeState.ScenarioId}', not '{configuredScenarioId}'.");
            }

            if (!ValidateUniqueIdentifiers(narrativeState.ConsumedStoryBeatIds) ||
                !ValidateUniqueIdentifiers(narrativeState.ReadNoteIds) ||
                !ValidateUniqueIdentifiers(narrativeState.UnlockedRouteIds))
            {
                return RestoreStateValidationResult.Invalid("Narrative identifiers must be unique and non-empty.");
            }

            var phoneIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < narrativeState.PhoneMessages.Count; i++)
            {
                var progress = narrativeState.PhoneMessages[i];
                if (progress == null || string.IsNullOrWhiteSpace(progress.MessageId) ||
                    !phoneIds.Add(progress.MessageId) || !progress.Delivered ||
                    (progress.Replied && !progress.Read))
                {
                    return RestoreStateValidationResult.Invalid(
                        "Phone progress must be unique, delivered, and read before a reply.");
                }

                if (phoneDefinitionsById.Count > 0 && !phoneDefinitionsById.ContainsKey(progress.MessageId))
                {
                    return RestoreStateValidationResult.Invalid(
                        $"Phone progress references unknown message '{progress.MessageId}'.");
                }
            }

            if (narrativeState.HasExplicitLatestDeliveredMessage)
            {
                if (phoneIds.Count > 0 && string.IsNullOrWhiteSpace(narrativeState.LatestDeliveredMessageId))
                {
                    return RestoreStateValidationResult.Invalid(
                        "A narrative state with delivered messages requires a latest delivered message ID.");
                }

                if (!string.IsNullOrWhiteSpace(narrativeState.LatestDeliveredMessageId) &&
                    !phoneIds.Contains(narrativeState.LatestDeliveredMessageId))
                {
                    return RestoreStateValidationResult.Invalid(
                        $"Latest delivered message '{narrativeState.LatestDeliveredMessageId}' is not delivered.");
                }
            }

            var flagIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < narrativeState.BranchFlags.Count; i++)
            {
                var flag = narrativeState.BranchFlags[i];
                if (flag == null || string.IsNullOrWhiteSpace(flag.FlagId) || !flagIds.Add(flag.FlagId))
                {
                    return RestoreStateValidationResult.Invalid("Narrative branch flag IDs must be unique and non-empty.");
                }
            }

            return RestoreStateValidationResult.Success;
        }

        private static void AddAll(ISet<string> destination, IReadOnlyList<string> values)
        {
            for (var i = 0; i < values.Count; i++)
            {
                destination.Add(values[i]);
            }
        }

        private static bool ValidateUniqueIdentifiers(IReadOnlyList<string> values)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < values.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(values[i]) || !ids.Add(values[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void RestoreLatestDeliveredMessageId(NarrativeRuntimeState state)
        {
            if (state.HasExplicitLatestDeliveredMessage)
            {
                latestDeliveredMessageId = state.LatestDeliveredMessageId;
                hasExplicitLatestDeliveryOrder = true;
                return;
            }

            // Legacy payloads did not persist delivery order. A single message is unambiguous;
            // with multiple messages, leave selection empty rather than inventing an incorrect order.
            if (state.PhoneMessages.Count == 1)
            {
                latestDeliveredMessageId = state.PhoneMessages[0].MessageId;
                hasExplicitLatestDeliveryOrder = true;
                return;
            }

            latestDeliveredMessageId = string.Empty;
            hasExplicitLatestDeliveryOrder = state.PhoneMessages.Count == 0;
        }

        private void PublishChanged()
        {
            events?.Publish(new NarrativeStateChanged(CaptureState()));
        }
    }
}
