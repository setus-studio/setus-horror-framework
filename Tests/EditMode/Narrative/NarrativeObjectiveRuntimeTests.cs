using System;
using System.Reflection;
using NUnit.Framework;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Narrative.Scenario;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Setus.HorrorFramework.Tests.EditMode.Narrative
{
    public sealed class NarrativeObjectiveRuntimeTests
    {
        [Test]
        public void MatchingStoryBeatAdvancesObjectiveOnlyOnce()
        {
            var events = new GameplayEventBus();
            var narrative = new NarrativeRuntimeModel(events);
            var objectives = new ObjectiveRuntimeModel(events);
            var first = CreateObjective(
                "objective.arrive",
                ObjectiveGateKind.StoryBeat,
                "story.arrive",
                "objective.read-note");
            var second = CreateObjective(
                "objective.read-note",
                ObjectiveGateKind.NoteRead,
                "note.maintenance",
                string.Empty);

            try
            {
                narrative.InitializeScenario("standalone.scenario");
                objectives.ConfigureScenario("standalone.scenario", new[] { first, second }, first.ObjectiveId);

                Assert.That(narrative.TryConsumeStoryBeat("story.arrive"), Is.True);
                Assert.That(objectives.IsComplete(first.ObjectiveId), Is.True);
                Assert.That(objectives.ActiveObjectiveId, Is.EqualTo(second.ObjectiveId));

                Assert.That(narrative.TryConsumeStoryBeat("story.arrive"), Is.False);
                Assert.That(objectives.CaptureState().CompletedObjectiveIds.Count, Is.EqualTo(1));
                Assert.That(objectives.ActiveObjectiveId, Is.EqualTo(second.ObjectiveId));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void NonMatchingGateDoesNotAdvanceObjective()
        {
            var events = new GameplayEventBus();
            var objectives = new ObjectiveRuntimeModel(events);
            var objective = CreateObjective(
                "objective.phone",
                ObjectiveGateKind.PhoneRead,
                "phone.return-call",
                string.Empty);

            try
            {
                objectives.ConfigureScenario("standalone.scenario", new[] { objective }, objective.ObjectiveId);
                events.Publish(new NarrativeNoteRead("phone.return-call"));

                Assert.That(objectives.ActiveObjectiveId, Is.EqualTo(objective.ObjectiveId));
                Assert.That(objectives.IsComplete(objective.ObjectiveId), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(objective);
            }
        }

        [Test]
        public void PhoneNoteRouteAndBranchStateRoundTrip()
        {
            var source = new NarrativeRuntimeModel();
            source.InitializeScenario("standalone.scenario");
            source.TryConsumeStoryBeat("story.arrive");
            source.TryDeliverPhoneMessage("phone.return-call");
            source.TryReadPhoneMessage("phone.return-call");
            source.TryReplyToPhoneMessage("phone.return-call");
            source.TryReadNote("note.maintenance");
            source.TryUnlockRoute("route.hallway");
            source.SetBranchFlag("branch.arrived", true);

            var restored = new NarrativeRuntimeModel();
            restored.RestoreState(source.CaptureState());

            Assert.That(restored.HasConsumedStoryBeat("story.arrive"), Is.True);
            Assert.That(restored.TryGetPhoneProgress("phone.return-call", out var phone), Is.True);
            Assert.That(phone.Delivered && phone.Read && phone.Replied, Is.True);
            Assert.That(restored.TryGetLatestDeliveredPhoneMessageId(out var latestMessageId), Is.True);
            Assert.That(latestMessageId, Is.EqualTo("phone.return-call"));
            Assert.That(restored.HasReadNote("note.maintenance"), Is.True);
            Assert.That(restored.IsRouteUnlocked("route.hallway"), Is.True);
            Assert.That(restored.TryGetBranchFlag("branch.arrived", out var branchValue), Is.True);
            Assert.That(branchValue, Is.True);
        }

        [Test]
        public void LatestDeliveredMessageUsesDeliveryOrderInsteadOfSerializationOrder()
        {
            var narrative = new NarrativeRuntimeModel();

            Assert.That(narrative.TryDeliverPhoneMessage("message-z"), Is.True);
            Assert.That(narrative.TryDeliverPhoneMessage("message-a"), Is.True);

            var captured = narrative.CaptureState();
            Assert.That(captured.PhoneMessages[0].MessageId, Is.EqualTo("message-a"));
            Assert.That(captured.PhoneMessages[1].MessageId, Is.EqualTo("message-z"));
            Assert.That(captured.LatestDeliveredMessageId, Is.EqualTo("message-a"));
            Assert.That(JsonUtility.ToJson(captured), Does.Contain("\"latestDeliveredMessageId\":\"message-a\""));
            Assert.That(narrative.TryGetLatestDeliveredPhoneMessageId(out var latest), Is.True);
            Assert.That(latest, Is.EqualTo("message-a"));
        }

        [Test]
        public void LatestDeliveredMessageUpdatesForEitherDeliveryOrder()
        {
            var narrative = new NarrativeRuntimeModel();

            Assert.That(narrative.TryDeliverPhoneMessage("message-a"), Is.True);
            Assert.That(narrative.TryDeliverPhoneMessage("message-z"), Is.True);

            Assert.That(narrative.TryGetLatestDeliveredPhoneMessageId(out var latest), Is.True);
            Assert.That(latest, Is.EqualTo("message-z"));
        }

        [Test]
        public void SaveRestorePreservesLatestDeliveredMessage()
        {
            var source = new NarrativeRuntimeModel();
            source.TryDeliverPhoneMessage("message-z");
            source.TryDeliverPhoneMessage("message-a");

            var restored = new NarrativeRuntimeModel();
            restored.RestoreState(source.CaptureState());

            Assert.That(restored.TryGetLatestDeliveredPhoneMessageId(out var latest), Is.True);
            Assert.That(latest, Is.EqualTo("message-a"));
        }

        [Test]
        public void ReadingOrReplyingDoesNotChangeLatestDelivery()
        {
            var narrative = new NarrativeRuntimeModel();
            narrative.TryDeliverPhoneMessage("message-a");
            narrative.TryDeliverPhoneMessage("message-z");
            narrative.TryReadPhoneMessage("message-a");

            Assert.That(narrative.TryGetLatestDeliveredPhoneMessageId(out var afterRead), Is.True);
            Assert.That(afterRead, Is.EqualTo("message-z"));

            narrative.TryReadPhoneMessage("message-z");
            narrative.TryReplyToPhoneMessage("message-z");

            Assert.That(narrative.TryGetLatestDeliveredPhoneMessageId(out var afterReply), Is.True);
            Assert.That(afterReply, Is.EqualTo("message-z"));
        }

        [Test]
        public void ExplicitLatestMessageMustReferToDeliveredProgress()
        {
            var model = new NarrativeRuntimeModel();
            var state = new NarrativeRuntimeState(
                "standalone.scenario",
                phoneMessages: new[] { new PhoneMessageProgress("message-a", true, false, false) },
                latestDeliveredMessageId: "message-z");

            var result = model.ValidateRestoreState(state);

            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ExplicitLatestMessageCannotBeEmptyWhenMessagesAreDelivered()
        {
            var model = new NarrativeRuntimeModel();
            var state = new NarrativeRuntimeState(
                "standalone.scenario",
                phoneMessages: new[] { new PhoneMessageProgress("message-a", true, false, false) },
                latestDeliveredMessageId: string.Empty);

            var result = model.ValidateRestoreState(state);

            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void LegacySingleMessageStateInfersLatestMessage()
        {
            var model = new NarrativeRuntimeModel();
            var legacy = new NarrativeRuntimeState(
                "standalone.scenario",
                phoneMessages: new[] { new PhoneMessageProgress("message-a", true, false, false) });

            Assert.That(legacy.HasExplicitLatestDeliveredMessage, Is.False);
            Assert.That(model.ValidateRestoreState(legacy).IsValid, Is.True);

            model.RestoreState(legacy);

            Assert.That(model.TryGetLatestDeliveredPhoneMessageId(out var latest), Is.True);
            Assert.That(latest, Is.EqualTo("message-a"));
        }

        [Test]
        public void LegacyMultipleMessageStateDoesNotInventLatestDeliveryOrder()
        {
            var model = new NarrativeRuntimeModel();
            var legacy = JsonUtility.FromJson<NarrativeRuntimeState>(
                "{\"scenarioId\":\"standalone.scenario\",\"phoneMessages\":[{\"messageId\":\"message-a\",\"delivered\":true,\"read\":false,\"replied\":false},{\"messageId\":\"message-z\",\"delivered\":true,\"read\":false,\"replied\":false}]}");

            Assert.That(legacy, Is.Not.Null);
            Assert.That(legacy.HasExplicitLatestDeliveredMessage, Is.False);
            Assert.That(model.ValidateRestoreState(legacy).IsValid, Is.True);

            model.RestoreState(legacy);

            Assert.That(model.TryGetLatestDeliveredPhoneMessageId(out _), Is.False);
            Assert.That(model.CaptureState().HasExplicitLatestDeliveredMessage, Is.False);
        }

        [Test]
        public void InvalidNarrativePhoneProgressFailsSemanticValidation()
        {
            var model = new NarrativeRuntimeModel();
            var state = new NarrativeRuntimeState(
                "standalone.scenario",
                phoneMessages: new[] { new PhoneMessageProgress("phone.return-call", true, false, true) });

            var result = model.ValidateRestoreState(state);

            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void ValidLinearGraphPassesValidation()
        {
            var first = CreateObjective("objective.first", ObjectiveGateKind.StoryBeat, "story.first", "objective.last");
            var last = CreateObjective("objective.last", ObjectiveGateKind.NoteRead, "note.last", string.Empty);

            try
            {
                var issue = ObjectiveGraphValidator.Validate(
                    "scenario.valid",
                    new[] { first, last },
                    first.ObjectiveId);

                Assert.That(issue.IsValid, Is.True, issue.ToDiagnostic("scenario.valid"));
            }
            finally
            {
                DestroyObjectives(first, last);
            }
        }

        [Test]
        public void DuplicateObjectiveIdFailsValidation()
        {
            var first = CreateObjective("objective.duplicate", ObjectiveGateKind.StoryBeat, "story.first", string.Empty);
            var duplicate = CreateObjective("objective.duplicate", ObjectiveGateKind.NoteRead, "note.second", string.Empty);

            try
            {
                var issue = ObjectiveGraphValidator.Validate(
                    "scenario.duplicate",
                    new[] { first, duplicate },
                    first.ObjectiveId);

                Assert.That(issue.IsValid, Is.False);
                Assert.That(issue.ObjectiveId, Is.EqualTo("objective.duplicate"));
                Assert.That(issue.FieldName, Is.EqualTo("ObjectiveId"));
            }
            finally
            {
                DestroyObjectives(first, duplicate);
            }
        }

        [Test]
        public void MissingInitialObjectiveFailsValidation()
        {
            var objective = CreateObjective("objective.start", ObjectiveGateKind.StoryBeat, "story.start", string.Empty);

            try
            {
                var issue = ObjectiveGraphValidator.Validate(
                    "scenario.initial",
                    new[] { objective },
                    "objective.missing");

                Assert.That(issue.IsValid, Is.False);
                Assert.That(issue.ObjectiveId, Is.EqualTo("objective.missing"));
                Assert.That(issue.FieldName, Is.EqualTo("InitialObjectiveId"));
            }
            finally
            {
                DestroyObjectives(objective);
            }
        }

        [Test]
        public void MissingNextObjectiveFailsValidationWithOffendingObjectiveDiagnostic()
        {
            var objective = CreateObjective(
                "objective.start",
                ObjectiveGateKind.StoryBeat,
                "story.start",
                "objective.missing");

            try
            {
                var issue = ObjectiveGraphValidator.Validate(
                    "scenario.next",
                    new[] { objective },
                    objective.ObjectiveId);

                Assert.That(issue.IsValid, Is.False);
                Assert.That(issue.ObjectiveId, Is.EqualTo("objective.start"));
                Assert.That(issue.FieldName, Is.EqualTo("NextObjectiveId"));
                Assert.That(issue.Reason, Does.Contain("objective.missing"));
            }
            finally
            {
                DestroyObjectives(objective);
            }
        }

        [Test]
        public void RuntimeConfigurationRejectsInvalidGraphBeforeReplacingCurrentDefinitions()
        {
            var model = new ObjectiveRuntimeModel();
            var valid = CreateObjective("objective.valid", ObjectiveGateKind.StoryBeat, "story.valid", string.Empty);
            var invalid = CreateObjective(
                "objective.invalid",
                ObjectiveGateKind.StoryBeat,
                "story.invalid",
                "objective.missing");

            try
            {
                model.ConfigureScenario("scenario.runtime", new[] { valid }, valid.ObjectiveId);

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    model.ConfigureScenario("scenario.runtime", new[] { invalid }, invalid.ObjectiveId));

                Assert.That(exception.Message, Does.Contain("NextObjectiveId"));
                Assert.That(model.ActiveObjectiveId, Is.EqualTo(valid.ObjectiveId));
                Assert.That(model.TryGetActiveDefinition(out var active), Is.True);
                Assert.That(active.ObjectiveId, Is.EqualTo(valid.ObjectiveId));
            }
            finally
            {
                DestroyObjectives(valid, invalid);
            }
        }

        [Test]
        public void TerminalObjectiveWithEmptyNextObjectivePassesValidation()
        {
            var objective = CreateObjective("objective.terminal", ObjectiveGateKind.RouteUnlocked, "route.exit", string.Empty);

            try
            {
                var issue = ObjectiveGraphValidator.Validate(
                    "scenario.terminal",
                    new[] { objective },
                    objective.ObjectiveId);

                Assert.That(issue.IsValid, Is.True, issue.ToDiagnostic("scenario.terminal"));
            }
            finally
            {
                DestroyObjectives(objective);
            }
        }

        [Test]
        public void EveryCurrentGateKindRequiresAGateId()
        {
            foreach (ObjectiveGateKind gateKind in Enum.GetValues(typeof(ObjectiveGateKind)))
            {
                var objective = CreateObjective("objective.gate", gateKind, string.Empty, string.Empty);
                try
                {
                    var issue = ObjectiveGraphValidator.Validate(
                        "scenario.gate",
                        new[] { objective },
                        objective.ObjectiveId);

                    Assert.That(issue.IsValid, Is.False, $"Gate '{gateKind}' accepted an empty gate ID.");
                    Assert.That(issue.FieldName, Is.EqualTo("CompletionGateId"));
                }
                finally
                {
                    DestroyObjectives(objective);
                }
            }
        }

        [Test]
        public void UnsupportedGateKindFailsValidation()
        {
            var objective = CreateObjective("objective.gate", (ObjectiveGateKind)999, "unexpected", string.Empty);

            try
            {
                var issue = ObjectiveGraphValidator.Validate(
                    "scenario.unsupported-gate",
                    new[] { objective },
                    objective.ObjectiveId);

                Assert.That(issue.IsValid, Is.False);
                Assert.That(issue.FieldName, Is.EqualTo("CompletionGate"));
            }
            finally
            {
                DestroyObjectives(objective);
            }
        }

        [Test]
        public void SelfReferencingObjectiveFailsValidation()
        {
            var objective = CreateObjective(
                "objective.loop",
                ObjectiveGateKind.GameplaySignal,
                "signal.loop",
                "objective.loop");

            try
            {
                var issue = ObjectiveGraphValidator.Validate(
                    "scenario.self-reference",
                    new[] { objective },
                    objective.ObjectiveId);

                Assert.That(issue.IsValid, Is.False);
                Assert.That(issue.FieldName, Is.EqualTo("NextObjectiveId"));
            }
            finally
            {
                DestroyObjectives(objective);
            }
        }

        [Test]
        public void ScenarioPhoneGateMustReferenceAuthoredPhoneMessage()
        {
            var objective = CreateObjective("objective.phone", ObjectiveGateKind.PhoneRead, "phone.missing", string.Empty);

            try
            {
                var issue = ObjectiveGraphValidator.Validate(
                    "scenario.phone-reference",
                    new[] { objective },
                    objective.ObjectiveId,
                    new[] { "phone.authored" });

                Assert.That(issue.IsValid, Is.False);
                Assert.That(issue.ObjectiveId, Is.EqualTo("objective.phone"));
                Assert.That(issue.FieldName, Is.EqualTo("CompletionGateId"));
            }
            finally
            {
                DestroyObjectives(objective);
            }
        }

        [Test]
        public void ExistingM6ScenarioPassesObjectiveGraphValidation()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<NarrativeScenarioDefinition>(
                "Assets/Game/Content/Scenarios/M6_DebugScenario.asset");

            Assert.That(scenario, Is.Not.Null);
            var issue = scenario.ValidateObjectiveGraph();
            Assert.That(issue.IsValid, Is.True, issue.ToDiagnostic(scenario.ScenarioId));
        }

        private static ObjectiveDefinition CreateObjective(
            string objectiveId,
            ObjectiveGateKind gateKind,
            string gateId,
            string nextObjectiveId)
        {
            var definition = ScriptableObject.CreateInstance<ObjectiveDefinition>();
            SetField(definition, "objectiveId", objectiveId);
            SetField(definition, "completionGate", gateKind);
            SetField(definition, "completionGateId", gateId);
            SetField(definition, "nextObjectiveId", nextObjectiveId);
            return definition;
        }

        private static void SetField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }

        private static void DestroyObjectives(params ObjectiveDefinition[] definitions)
        {
            for (var i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null)
                {
                    Object.DestroyImmediate(definitions[i]);
                }
            }
        }
    }
}
