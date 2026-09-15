using System;
using System.Collections.Generic;
using System.Linq;

namespace Setus.HorrorFramework.Narrative.Objectives
{
    public readonly struct ObjectiveGraphValidationIssue
    {
        public ObjectiveGraphValidationIssue(string objectiveId, string fieldName, string reason)
        {
            ObjectiveId = objectiveId ?? string.Empty;
            FieldName = fieldName ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string ObjectiveId { get; }
        public string FieldName { get; }
        public string Reason { get; }
        public bool IsValid => string.IsNullOrEmpty(Reason);

        public string ToDiagnostic(string scenarioId)
        {
            return $"Objective graph validation failed for scenario '{scenarioId}': " +
                   $"objective '{ObjectiveId}', field '{FieldName}': {Reason}";
        }
    }

    public static class ObjectiveGraphValidator
    {
        public static ObjectiveGraphValidationIssue Validate(
            string scenarioId,
            IEnumerable<ObjectiveDefinition> definitions,
            string initialObjectiveId,
            IEnumerable<string> knownPhoneMessageIds = null)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                return Invalid("<scenario>", "ScenarioId", "must not be empty.");
            }

            var definitionArray = (definitions ?? Enumerable.Empty<ObjectiveDefinition>()).ToArray();
            var definitionsById = new Dictionary<string, ObjectiveDefinition>(StringComparer.Ordinal);
            for (var index = 0; index < definitionArray.Length; index++)
            {
                var definition = definitionArray[index];
                var diagnosticId = definition != null && !string.IsNullOrWhiteSpace(definition.ObjectiveId)
                    ? definition.ObjectiveId
                    : $"<index:{index}>";
                if (definition == null)
                {
                    return Invalid(diagnosticId, "Definition", "must not be null.");
                }

                if (string.IsNullOrWhiteSpace(definition.ObjectiveId))
                {
                    return Invalid(diagnosticId, "ObjectiveId", "must not be empty.");
                }

                if (definitionsById.ContainsKey(definition.ObjectiveId))
                {
                    return Invalid(definition.ObjectiveId, "ObjectiveId", "must be unique within the scenario.");
                }

                definitionsById.Add(definition.ObjectiveId, definition);
            }

            if (string.IsNullOrWhiteSpace(initialObjectiveId))
            {
                return Invalid("<scenario>", "InitialObjectiveId", "must reference an objective in the graph.");
            }

            if (!definitionsById.ContainsKey(initialObjectiveId))
            {
                return Invalid(
                    initialObjectiveId,
                    "InitialObjectiveId",
                    "does not reference an objective in the graph.");
            }

            HashSet<string> phoneMessageIds = null;
            if (knownPhoneMessageIds != null)
            {
                phoneMessageIds = new HashSet<string>(
                    knownPhoneMessageIds.Where(id => !string.IsNullOrWhiteSpace(id)),
                    StringComparer.Ordinal);
            }

            for (var index = 0; index < definitionArray.Length; index++)
            {
                var definition = definitionArray[index];
                if (!Enum.IsDefined(typeof(ObjectiveGateKind), definition.CompletionGate))
                {
                    return Invalid(
                        definition.ObjectiveId,
                        "CompletionGate",
                        $"uses unsupported gate value '{(int)definition.CompletionGate}'.");
                }

                if (RequiresGateId(definition.CompletionGate) &&
                    string.IsNullOrWhiteSpace(definition.CompletionGateId))
                {
                    return Invalid(
                        definition.ObjectiveId,
                        "CompletionGateId",
                        $"is required for gate '{definition.CompletionGate}'.");
                }

                if ((definition.CompletionGate == ObjectiveGateKind.PhoneRead ||
                     definition.CompletionGate == ObjectiveGateKind.PhoneReply) &&
                    phoneMessageIds != null &&
                    !phoneMessageIds.Contains(definition.CompletionGateId))
                {
                    return Invalid(
                        definition.ObjectiveId,
                        "CompletionGateId",
                        $"references unknown phone message '{definition.CompletionGateId}'.");
                }

                if (string.IsNullOrWhiteSpace(definition.NextObjectiveId))
                {
                    continue;
                }

                if (string.Equals(definition.ObjectiveId, definition.NextObjectiveId, StringComparison.Ordinal))
                {
                    return Invalid(
                        definition.ObjectiveId,
                        "NextObjectiveId",
                        "must not reference the same objective.");
                }

                if (!definitionsById.ContainsKey(definition.NextObjectiveId))
                {
                    return Invalid(
                        definition.ObjectiveId,
                        "NextObjectiveId",
                        $"references missing objective '{definition.NextObjectiveId}'.");
                }
            }

            return default;
        }

        public static void ValidateOrThrow(
            string scenarioId,
            IEnumerable<ObjectiveDefinition> definitions,
            string initialObjectiveId,
            IEnumerable<string> knownPhoneMessageIds = null)
        {
            var issue = Validate(scenarioId, definitions, initialObjectiveId, knownPhoneMessageIds);
            if (!issue.IsValid)
            {
                throw new InvalidOperationException(issue.ToDiagnostic(scenarioId));
            }
        }

        private static bool RequiresGateId(ObjectiveGateKind gateKind)
        {
            switch (gateKind)
            {
                case ObjectiveGateKind.GameplaySignal:
                case ObjectiveGateKind.InteractionTrigger:
                case ObjectiveGateKind.StoryBeat:
                case ObjectiveGateKind.PhoneRead:
                case ObjectiveGateKind.PhoneReply:
                case ObjectiveGateKind.NoteRead:
                case ObjectiveGateKind.RouteUnlocked:
                case ObjectiveGateKind.InventoryItem:
                    return true;
                default:
                    return false;
            }
        }

        private static ObjectiveGraphValidationIssue Invalid(string objectiveId, string fieldName, string reason)
        {
            return new ObjectiveGraphValidationIssue(objectiveId, fieldName, reason);
        }
    }
}
