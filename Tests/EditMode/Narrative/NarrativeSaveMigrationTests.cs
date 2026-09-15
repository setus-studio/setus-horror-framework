using System;
using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.SaveProgression.Checkpoints;
using Setus.HorrorFramework.SaveProgression.Migration;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.Serialization;

namespace Setus.HorrorFramework.Tests.EditMode.Narrative
{
    public sealed class NarrativeSaveMigrationTests
    {
        [Test]
        public void SchemaOneMigrationAddsRequiredNarrativeAndObjectiveRecords()
        {
            var pipeline = new SaveMigrationPipeline();
            HorrorSaveMigrations.RegisterDefaults(pipeline);
            var legacy = new SaveGameSnapshot(
                1,
                new SaveSlotId("legacy-m6"),
                null,
                Array.Empty<SaveStateRecord>(),
                Array.Empty<StableSaveStateRecord>());

            var migrated = pipeline.MigrateToCurrent(legacy);

            Assert.That(migrated.SchemaVersion, Is.EqualTo(SaveSchemaVersion.Current));
            var objective = migrated.GlobalStates.Single(state =>
                state.StateKey == HorrorGlobalSaveStateRegistry.ObjectiveStateKey);
            var narrative = migrated.GlobalStates.Single(state =>
                state.StateKey == HorrorGlobalSaveStateRegistry.NarrativeStateKey);
            Assert.That(objective.StateTypeKey, Is.EqualTo(SaveStateTypeKeys.ObjectiveRuntimeV1));
            Assert.That(objective.State, Is.TypeOf<ObjectiveRuntimeState>());
            Assert.That(narrative.StateTypeKey, Is.EqualTo(SaveStateTypeKeys.NarrativeRuntimeV1));
            Assert.That(narrative.State, Is.TypeOf<NarrativeRuntimeState>());
        }

        [Test]
        public void ProductionRegistryDeclaresM6GlobalStatesFromSchemaTwo()
        {
            var declarations = HorrorGlobalSaveStateRegistry.CreateDefault();

            Assert.That(
                declarations.TryGet(HorrorGlobalSaveStateRegistry.ObjectiveStateKey, out var objective),
                Is.True);
            Assert.That(objective.IsRequiredForSchema(1), Is.False);
            Assert.That(objective.IsRequiredForSchema(2), Is.True);
            Assert.That(objective.StateTypeKey, Is.EqualTo(SaveStateTypeKeys.ObjectiveRuntimeV1));

            Assert.That(
                declarations.TryGet(HorrorGlobalSaveStateRegistry.NarrativeStateKey, out var narrative),
                Is.True);
            Assert.That(narrative.IsRequiredForSchema(1), Is.False);
            Assert.That(narrative.IsRequiredForSchema(2), Is.True);
            Assert.That(narrative.StateTypeKey, Is.EqualTo(SaveStateTypeKeys.NarrativeRuntimeV1));
        }

        [Test]
        public void SaveOwnerRestoresObjectiveAndNarrativeStateTogether()
        {
            var declarations = new GlobalSaveStateRegistry();
            declarations.Register(new GlobalSaveStateDeclaration(
                HorrorGlobalSaveStateRegistry.ObjectiveStateKey,
                SaveStateTypeKeys.ObjectiveRuntimeV1,
                GlobalSaveStateRequirement.Required,
                2,
                GlobalSaveStateOwnerScope.Context,
                GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));
            declarations.Register(new GlobalSaveStateDeclaration(
                HorrorGlobalSaveStateRegistry.NarrativeStateKey,
                SaveStateTypeKeys.NarrativeRuntimeV1,
                GlobalSaveStateRequirement.Required,
                2,
                GlobalSaveStateOwnerScope.Context,
                GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));

            var sourceEvents = new GameplayEventBus();
            var sourceObjectives = new ObjectiveRuntimeModel(sourceEvents);
            var sourceNarrative = new NarrativeRuntimeModel(sourceEvents);
            var sourceRegistry = new SaveableRegistry(HorrorSaveStateTypeRegistry.CreateDefault(), declarations);
            sourceRegistry.RegisterGlobal(sourceObjectives);
            sourceRegistry.RegisterGlobal(sourceNarrative);
            var sourceOwner = new SaveGameOwner(sourceRegistry, new SaveMigrationPipeline(), sourceEvents, null);

            sourceNarrative.InitializeScenario("standalone.scenario");
            sourceNarrative.TryReadNote("note.maintenance");
            sourceNarrative.TryUnlockRoute("route.hallway");
            sourceObjectives.RestoreState(new ObjectiveRuntimeState(
                "standalone.scenario",
                "objective.route",
                new[] { "objective.note" }));
            var snapshot = sourceOwner.Capture(
                new SaveSlotId("m6-round-trip"),
                checkpoint: new CheckpointModel("m6-objective-change", "Gameplay", "debug"));

            var targetEvents = new GameplayEventBus();
            var targetObjectives = new ObjectiveRuntimeModel(targetEvents);
            var targetNarrative = new NarrativeRuntimeModel(targetEvents);
            var targetRegistry = new SaveableRegistry(HorrorSaveStateTypeRegistry.CreateDefault(), declarations);
            targetRegistry.RegisterGlobal(targetObjectives);
            targetRegistry.RegisterGlobal(targetNarrative);
            var targetOwner = new SaveGameOwner(targetRegistry, new SaveMigrationPipeline(), targetEvents, null);

            targetOwner.Restore(snapshot);

            Assert.That(targetObjectives.ActiveObjectiveId, Is.EqualTo("objective.route"));
            Assert.That(targetObjectives.IsComplete("objective.note"), Is.True);
            Assert.That(targetNarrative.HasReadNote("note.maintenance"), Is.True);
            Assert.That(targetNarrative.IsRouteUnlocked("route.hallway"), Is.True);
            Assert.That(snapshot.Checkpoint.CheckpointId, Is.EqualTo("m6-objective-change"));
        }
    }
}
