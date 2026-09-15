using System;
using System.Collections.Generic;
using System.Linq;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Inventory.Runtime;
using Setus.HorrorFramework.Narrative.Scenario;

namespace Setus.HorrorFramework.Narrative.Objectives
{
    public sealed class ObjectiveRuntimeModel : RuntimeStateOwnerBase<ObjectiveRuntimeState>, IRestoreStateValidator
    {
        private readonly IGameplayEventBus events;
        private readonly Dictionary<string, ObjectiveDefinition> definitionsById =
            new Dictionary<string, ObjectiveDefinition>(StringComparer.Ordinal);
        private readonly HashSet<string> completedObjectiveIds =
            new HashSet<string>(StringComparer.Ordinal);

        private string scenarioId = string.Empty;
        private string configuredScenarioId = string.Empty;
        private string activeObjectiveId = string.Empty;

        public ObjectiveRuntimeModel(IGameplayEventBus events = null)
            : base("objective.state")
        {
            this.events = events;
            events?.Subscribe<GameplaySignalRaised>(OnGameplaySignalRaised);
            events?.Subscribe<InteractionTriggerEntered>(OnInteractionTriggerEntered);
            events?.Subscribe<NarrativeStoryBeatConsumed>(OnStoryBeatConsumed);
            events?.Subscribe<NarrativePhoneMessageRead>(OnPhoneMessageRead);
            events?.Subscribe<NarrativePhoneMessageReplied>(OnPhoneMessageReplied);
            events?.Subscribe<NarrativeNoteRead>(OnNoteRead);
            events?.Subscribe<NarrativeRouteUnlocked>(OnRouteUnlocked);
            events?.Subscribe<InventoryChanged>(OnInventoryChanged);
        }

        public string ActiveObjectiveId => activeObjectiveId;

        public bool IsComplete(string objectiveId)
        {
            return !string.IsNullOrWhiteSpace(objectiveId) && completedObjectiveIds.Contains(objectiveId);
        }

        public bool TryGetActiveDefinition(out ObjectiveDefinition definition)
        {
            return definitionsById.TryGetValue(activeObjectiveId, out definition);
        }

        public void ConfigureScenario(
            string configuredScenarioId,
            IEnumerable<ObjectiveDefinition> definitions,
            string initialObjectiveId)
        {
            var definitionArray = (definitions ?? Enumerable.Empty<ObjectiveDefinition>()).ToArray();
            ObjectiveGraphValidator.ValidateOrThrow(configuredScenarioId, definitionArray, initialObjectiveId);

            if (!string.IsNullOrWhiteSpace(scenarioId) &&
                !string.Equals(scenarioId, configuredScenarioId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Cannot configure scenario '{configuredScenarioId}' while '{scenarioId}' is active.");
            }

            definitionsById.Clear();
            foreach (var definition in definitionArray)
            {
                definitionsById.Add(definition.ObjectiveId, definition);
            }

            this.configuredScenarioId = configuredScenarioId;

            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                scenarioId = configuredScenarioId;
                activeObjectiveId = initialObjectiveId;
                PublishChanged();
            }
        }

        public void Reset()
        {
            scenarioId = string.Empty;
            activeObjectiveId = string.Empty;
            completedObjectiveIds.Clear();
            PublishChanged();
        }

        public override ObjectiveRuntimeState CaptureState()
        {
            return new ObjectiveRuntimeState(
                scenarioId,
                activeObjectiveId,
                completedObjectiveIds.OrderBy(id => id, StringComparer.Ordinal));
        }

        public override void RestoreState(ObjectiveRuntimeState state)
        {
            scenarioId = state?.ScenarioId ?? string.Empty;
            activeObjectiveId = state?.ActiveObjectiveId ?? string.Empty;
            completedObjectiveIds.Clear();

            if (state != null)
            {
                for (var i = 0; i < state.CompletedObjectiveIds.Count; i++)
                {
                    completedObjectiveIds.Add(state.CompletedObjectiveIds[i]);
                }
            }

            PublishChanged();
        }

        public RestoreStateValidationResult ValidateRestoreState(object state)
        {
            if (!(state is ObjectiveRuntimeState objectiveState))
            {
                return RestoreStateValidationResult.Invalid($"Expected {nameof(ObjectiveRuntimeState)}.");
            }

            if (string.IsNullOrWhiteSpace(objectiveState.ScenarioId) &&
                !string.IsNullOrWhiteSpace(objectiveState.ActiveObjectiveId))
            {
                return RestoreStateValidationResult.Invalid("An active objective requires a scenario ID.");
            }

            if (!string.IsNullOrWhiteSpace(configuredScenarioId) &&
                !string.IsNullOrWhiteSpace(objectiveState.ScenarioId) &&
                !string.Equals(objectiveState.ScenarioId, configuredScenarioId, StringComparison.Ordinal))
            {
                return RestoreStateValidationResult.Invalid(
                    $"Objective state belongs to scenario '{objectiveState.ScenarioId}', not '{configuredScenarioId}'.");
            }

            var uniqueCompleted = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < objectiveState.CompletedObjectiveIds.Count; i++)
            {
                var objectiveId = objectiveState.CompletedObjectiveIds[i];
                if (string.IsNullOrWhiteSpace(objectiveId) || !uniqueCompleted.Add(objectiveId))
                {
                    return RestoreStateValidationResult.Invalid("Completed objective IDs must be unique and non-empty.");
                }
            }

            if (!string.IsNullOrWhiteSpace(objectiveState.ActiveObjectiveId) &&
                uniqueCompleted.Contains(objectiveState.ActiveObjectiveId))
            {
                return RestoreStateValidationResult.Invalid("The active objective cannot already be completed.");
            }

            if (definitionsById.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(objectiveState.ActiveObjectiveId) &&
                    !definitionsById.ContainsKey(objectiveState.ActiveObjectiveId))
                {
                    return RestoreStateValidationResult.Invalid(
                        $"Active objective '{objectiveState.ActiveObjectiveId}' is not defined by the loaded scenario.");
                }

                foreach (var objectiveId in uniqueCompleted)
                {
                    if (!definitionsById.ContainsKey(objectiveId))
                    {
                        return RestoreStateValidationResult.Invalid(
                            $"Completed objective '{objectiveId}' is not defined by the loaded scenario.");
                    }
                }
            }

            return RestoreStateValidationResult.Success;
        }

        private void TryAdvance(ObjectiveGateKind gateKind, string gateId)
        {
            if (string.IsNullOrWhiteSpace(activeObjectiveId) ||
                !definitionsById.TryGetValue(activeObjectiveId, out var definition) ||
                definition.CompletionGate != gateKind ||
                !string.Equals(definition.CompletionGateId, gateId, StringComparison.Ordinal))
            {
                return;
            }

            var completedId = activeObjectiveId;
            if (!completedObjectiveIds.Add(completedId))
            {
                return;
            }

            activeObjectiveId = definition.NextObjectiveId ?? string.Empty;
            events?.Publish(new ObjectiveCompleted(scenarioId, completedId));
            PublishChanged();
        }

        private void OnGameplaySignalRaised(GameplaySignalRaised raised)
        {
            TryAdvance(ObjectiveGateKind.GameplaySignal, raised.SignalId);
        }

        private void OnInteractionTriggerEntered(InteractionTriggerEntered entered)
        {
            TryAdvance(ObjectiveGateKind.InteractionTrigger, entered.TriggerId);
        }

        private void OnStoryBeatConsumed(NarrativeStoryBeatConsumed consumed)
        {
            TryAdvance(ObjectiveGateKind.StoryBeat, consumed.StoryBeatId);
        }

        private void OnPhoneMessageRead(NarrativePhoneMessageRead read)
        {
            TryAdvance(ObjectiveGateKind.PhoneRead, read.MessageId);
        }

        private void OnPhoneMessageReplied(NarrativePhoneMessageReplied replied)
        {
            TryAdvance(ObjectiveGateKind.PhoneReply, replied.MessageId);
        }

        private void OnNoteRead(NarrativeNoteRead read)
        {
            TryAdvance(ObjectiveGateKind.NoteRead, read.NoteId);
        }

        private void OnRouteUnlocked(NarrativeRouteUnlocked unlocked)
        {
            TryAdvance(ObjectiveGateKind.RouteUnlocked, unlocked.RouteId);
        }

        private void OnInventoryChanged(InventoryChanged changed)
        {
            if (definitionsById.TryGetValue(activeObjectiveId, out var definition) &&
                definition.CompletionGate == ObjectiveGateKind.InventoryItem &&
                changed.State != null)
            {
                for (var i = 0; i < changed.State.ItemIds.Count; i++)
                {
                    if (string.Equals(changed.State.ItemIds[i], definition.CompletionGateId, StringComparison.Ordinal))
                    {
                        TryAdvance(ObjectiveGateKind.InventoryItem, definition.CompletionGateId);
                        return;
                    }
                }
            }
        }

        private void PublishChanged()
        {
            events?.Publish(new ObjectiveStateChanged(CaptureState()));
        }
    }
}
