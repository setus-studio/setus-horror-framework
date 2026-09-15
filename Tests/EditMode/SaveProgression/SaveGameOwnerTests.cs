using System;
using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Inventory.Runtime;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.Player.State;
using Setus.HorrorFramework.SaveProgression.Checkpoints;
using Setus.HorrorFramework.SaveProgression.Migration;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.Serialization;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Setus.HorrorFramework.Tests.EditMode.SaveProgression
{
    public sealed class SaveGameOwnerTests
    {
        [SetUp]
        public void SetUp()
        {
            HorrorGameContext.ShutdownActive();
        }

        [TearDown]
        public void TearDown()
        {
            HorrorGameContext.ShutdownActive();
        }

        [Test]
        public void CaptureFailsWhenRequiredIdIsMissing()
        {
            var registry = new SaveableRegistry();
            var owner = CreateOwner(registry);
            var manifest = CreateManifest(new StableIdManifestEntry("door.front", StableIdManifestEntryKind.Required));

            var exception = Assert.Throws<SaveValidationException>(() =>
                owner.Capture(new SaveSlotId("slot-1"), manifest));

            Assert.IsTrue(exception.Report.HasErrors);
            Assert.AreEqual("door.front", exception.Report.Issues.Single().StableId);
        }

        [Test]
        public void MissingOptionalIdReportsWarningWithoutBlockingCapture()
        {
            var registry = new SaveableRegistry();
            var owner = CreateOwner(registry);
            var manifest = CreateManifest(new StableIdManifestEntry("note.optional", StableIdManifestEntryKind.Optional));

            var result = owner.CaptureWithReport(new SaveSlotId("slot-1"), manifest);

            Assert.IsNotNull(result.Snapshot);
            Assert.AreEqual(0, result.Snapshot.StableStates.Count);
            Assert.IsTrue(result.Report.HasWarnings);
            Assert.AreEqual("note.optional", result.Report.Issues.Single().StableId);
        }

        [Test]
        public void DuplicateStableIdsBlockCapture()
        {
            var registry = new SaveableRegistry();
            registry.RegisterStable(new TestSaveable("door.front", 1));
            registry.RegisterStable(new TestSaveable("door.front", 2));
            var owner = CreateOwner(registry);

            var exception = Assert.Throws<SaveValidationException>(() =>
                owner.Capture(new SaveSlotId("slot-1")));

            Assert.IsTrue(exception.Report.HasErrors);
            Assert.AreEqual("door.front", exception.Report.Issues.Single().StableId);
        }

        [Test]
        public void LoadingSameSaveTwiceProducesSameState()
        {
            var registry = new SaveableRegistry();
            var saveable = new TestSaveable("door.front", 7);
            registry.RegisterStable(saveable);
            var owner = CreateOwner(registry);

            var snapshot = owner.Capture(new SaveSlotId("slot-1"));

            saveable.Value = 0;
            owner.Restore(snapshot);
            Assert.AreEqual(7, saveable.Value);

            saveable.Value = 1;
            owner.Restore(snapshot);
            Assert.AreEqual(7, saveable.Value);
        }

        [Test]
        public void SaveAfterRestoreKeepsDisabledButRequiredSaveable()
        {
            var registry = new SaveableRegistry();
            var saveable = new TestSaveable("door.front", 5) { SimulatedDisabled = true };
            registry.RegisterStable(saveable);
            var owner = CreateOwner(registry);
            var manifest = CreateManifest(new StableIdManifestEntry("door.front", StableIdManifestEntryKind.Required));

            var firstSnapshot = owner.Capture(new SaveSlotId("slot-1"), manifest);
            saveable.Value = 0;

            owner.Restore(firstSnapshot, manifest);
            var secondSnapshot = owner.Capture(new SaveSlotId("slot-1"), manifest);

            Assert.AreEqual(5, saveable.Value);
            Assert.AreEqual("door.front", secondSnapshot.StableStates.Single().StableId);
        }

        [Test]
        public void OptionalSavedStateMissingOnRestoreWarnsWithoutCorruptingRegisteredState()
        {
            var registry = new SaveableRegistry();
            var required = new TestSaveable("door.front", 3);
            registry.RegisterStable(required);
            var owner = CreateOwner(registry);
            var manifest = CreateManifest(
                new StableIdManifestEntry("door.front", StableIdManifestEntryKind.Required),
                new StableIdManifestEntry("note.optional", StableIdManifestEntryKind.Optional));
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[]
                {
                    new StableSaveStateRecord(
                        "door.front",
                        "door.state",
                        SaveStateTypeKeys.Int32V1,
                        11,
                        false,
                        SaveRestorePolicy.ResumeDeterministicPhase),
                    new StableSaveStateRecord(
                        "note.optional",
                        "note.state",
                        SaveStateTypeKeys.Int32V1,
                        22,
                        false,
                        SaveRestorePolicy.ResumeDeterministicPhase)
                });

            var report = owner.Restore(snapshot, manifest);

            Assert.AreEqual(11, required.Value);
            Assert.IsTrue(report.HasWarnings);
            Assert.IsFalse(report.HasErrors);
        }

        [Test]
        public void RestorePublishesRestoreCompleteEvent()
        {
            var context = HorrorGameContext.Ensure();
            context.Saveables.RegisterGlobal(new TestPlayerPoseOwner());
            var saveable = new TestSaveable("door.front", 7);
            context.Saveables.RegisterStable(saveable);
            var snapshot = context.Saves.Capture(new SaveSlotId("slot-1"));
            var received = false;
            context.Events.Subscribe<SaveRestoreCompleted>(_ => received = true);

            context.Saves.Restore(snapshot);

            Assert.IsTrue(received);
        }

        [Test]
        public void MigrationRuleAdvancesOlderSchemaBeforeRestore()
        {
            var registry = new SaveableRegistry();
            var saveable = new TestSaveable("door.front", 1);
            registry.RegisterStable(saveable);
            var migrations = new SaveMigrationPipeline();
            migrations.Register(new VersionZeroMigration());
            var owner = new SaveGameOwner(registry, migrations, new GameplayEventBus(), null);
            var oldSnapshot = new SaveGameSnapshot(
                0,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[]
                {
                    new StableSaveStateRecord(
                        "door.front",
                        "door.state",
                        SaveStateTypeKeys.Int32V1,
                        9,
                        false,
                        SaveRestorePolicy.ResumeDeterministicPhase)
                });

            owner.Restore(oldSnapshot);

            Assert.AreEqual(9, saveable.Value);
        }

        [Test]
        public void MissingRequiredSnapshotRecordFailsBeforeAnyLiveStateMutates()
        {
            var registry = new SaveableRegistry();
            var saveable = new TestSaveable("door.front", 4);
            registry.RegisterStable(saveable);
            var owner = CreateOwner(registry);
            var manifest = CreateManifest(new StableIdManifestEntry("door.front", StableIdManifestEntryKind.Required));
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                Array.Empty<StableSaveStateRecord>());

            var exception = Assert.Throws<SaveValidationException>(() => owner.Restore(snapshot, manifest));

            Assert.IsTrue(HasError(exception.Report, "missing-required-record"));
            Assert.AreEqual(4, saveable.Value);
        }

        [Test]
        public void UnsupportedSnapshotSchemaFailsWithStructuredReportBeforeMutation()
        {
            var registry = new SaveableRegistry();
            var saveable = new TestSaveable("door.front", 4);
            registry.RegisterStable(saveable);
            var owner = CreateOwner(registry);
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current + 1,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[] { CreateStableRecord("door.front", "door.state", 9) });

            var exception = Assert.Throws<SaveValidationException>(() => owner.Restore(snapshot));

            Assert.IsTrue(HasError(exception.Report, "snapshot-schema-invalid"));
            Assert.AreEqual(4, saveable.Value);
        }

        [Test]
        public void DuplicateStableSnapshotRecordFailsBeforeAnyLiveStateMutates()
        {
            var registry = new SaveableRegistry();
            var saveable = new TestSaveable("door.front", 4);
            registry.RegisterStable(saveable);
            var owner = CreateOwner(registry);
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[]
                {
                    CreateStableRecord("door.front", "door.state", 7),
                    CreateStableRecord("door.front", "door.state", 9)
                });

            var exception = Assert.Throws<SaveValidationException>(() => owner.Restore(snapshot));

            Assert.IsTrue(HasError(exception.Report, "duplicate-stable-record"));
            Assert.AreEqual(4, saveable.Value);
        }

        [Test]
        public void WrongStateKeyFailsBeforeAnyLiveStateMutates()
        {
            var registry = new SaveableRegistry();
            var saveable = new TestSaveable("door.front", 4);
            registry.RegisterStable(saveable);
            var owner = CreateOwner(registry);
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[] { CreateStableRecord("door.front", "wrong.key", 9) });

            var exception = Assert.Throws<SaveValidationException>(() => owner.Restore(snapshot));

            Assert.IsTrue(HasError(exception.Report, "state-key-mismatch"));
            Assert.AreEqual(4, saveable.Value);
        }

        [Test]
        public void WrongStateTypeAndCorruptPayloadFailBeforeAnyLiveStateMutates()
        {
            var registry = new SaveableRegistry();
            var saveable = new TestSaveable("door.front", 4);
            var corruptSaveable = new TestSaveable("door.corrupt", 5);
            registry.RegisterStable(saveable);
            registry.RegisterStable(corruptSaveable);
            var owner = CreateOwner(registry);
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[]
                {
                    new StableSaveStateRecord(
                        "door.front",
                        "door.state",
                        SaveStateTypeKeys.StringV1,
                        "invalid",
                        false,
                        SaveRestorePolicy.ResumeDeterministicPhase),
                    new StableSaveStateRecord(
                        "door.corrupt",
                        "door.state",
                        SaveStateTypeKeys.Int32V1,
                        null,
                        false,
                        SaveRestorePolicy.ResumeDeterministicPhase)
                });

            var exception = Assert.Throws<SaveValidationException>(() => owner.Restore(snapshot));

            Assert.IsTrue(HasError(exception.Report, "state-type-mismatch"));
            Assert.IsTrue(HasError(exception.Report, "state-payload-invalid"));
            Assert.AreEqual(4, saveable.Value);
            Assert.AreEqual(5, corruptSaveable.Value);
        }

        [Test]
        public void RequiredLiveOwnerMissingFailsBeforeAnyLiveStateMutates()
        {
            var registry = new SaveableRegistry();
            var owner = CreateOwner(registry);
            var manifest = CreateManifest(new StableIdManifestEntry("door.front", StableIdManifestEntryKind.Required));
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[] { CreateStableRecord("door.front", "door.state", 9) });

            var exception = Assert.Throws<SaveValidationException>(() => owner.Restore(snapshot, manifest));

            Assert.IsTrue(HasError(exception.Report, StableIdValidationIssueType.MissingRequiredId.ToString()));
        }

        [Test]
        public void OptionalSnapshotRecordMissingDoesNotBlockRestore()
        {
            var registry = new SaveableRegistry();
            var saveable = new TestSaveable("door.front", 4);
            registry.RegisterStable(saveable);
            var owner = CreateOwner(registry);
            var manifest = CreateManifest(
                new StableIdManifestEntry("door.front", StableIdManifestEntryKind.Required),
                new StableIdManifestEntry("note.optional", StableIdManifestEntryKind.Optional));
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[] { CreateStableRecord("door.front", "door.state", 9) });

            var report = owner.Restore(snapshot, manifest);

            Assert.IsFalse(report.HasErrors);
            Assert.AreEqual(9, saveable.Value);
        }

        [Test]
        public void PreflightFailureDoesNotApplyValidGlobalState()
        {
            var registry = CreateRegistryWithGlobalDeclaration(
                "inventory.state",
                SaveStateTypeKeys.Int32V1);
            var global = new TestGlobalOwner("inventory.state", 3);
            var saveable = new TestSaveable("door.front", 4);
            registry.RegisterGlobal(global);
            registry.RegisterStable(saveable);
            var owner = CreateOwner(registry);
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                new[] { new SaveStateRecord("inventory.state", SaveStateTypeKeys.Int32V1, 8) },
                new[] { CreateStableRecord("door.front", "wrong.key", 9) });

            Assert.Throws<SaveValidationException>(() => owner.Restore(snapshot));

            Assert.AreEqual(3, global.Value);
            Assert.AreEqual(4, saveable.Value);
        }

        [Test]
        public void StructurallyAndSemanticallyValidStateRestores()
        {
            var registry = new SaveableRegistry();
            var saveable = new SemanticTestSaveable(
                "door.front",
                4,
                value => value >= 0
                    ? RestoreStateValidationResult.Success
                    : RestoreStateValidationResult.Invalid("Value must not be negative."));
            registry.RegisterStable(saveable);
            var owner = CreateOwner(registry);
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[] { CreateStableRecord("door.front", "door.state", 9) });

            owner.Restore(snapshot);

            Assert.AreEqual(9, saveable.Value);
            Assert.AreEqual(1, saveable.RestoreCount);
        }

        [Test]
        public void CorrectTypeWithInvalidSemanticValueFailsBeforeMutation()
        {
            var registry = new SaveableRegistry();
            var saveable = new SemanticTestSaveable(
                "door.front",
                4,
                value => value >= 0
                    ? RestoreStateValidationResult.Success
                    : RestoreStateValidationResult.Invalid("Value must not be negative."));
            registry.RegisterStable(saveable);
            var owner = CreateOwner(registry);
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[] { CreateStableRecord("door.front", "door.state", -1) });

            var exception = Assert.Throws<SaveValidationException>(() => owner.Restore(snapshot));

            Assert.IsTrue(HasError(exception.Report, "state-semantic-invalid"));
            Assert.AreEqual(4, saveable.Value);
            Assert.AreEqual(0, saveable.RestoreCount);
        }

        [Test]
        public void InvalidHeldInventoryReferenceFailsWithoutMutationOrGameplayEvent()
        {
            var events = new GameplayEventBus();
            var inventory = new InventoryRuntimeModel(events);
            inventory.AddItem("key.present", true);
            var changedEventCount = 0;
            events.Subscribe<InventoryChanged>(_ => changedEventCount++);
            var registry = CreateRegistryWithGlobalDeclaration(
                "inventory.state",
                SaveStateTypeKeys.InventoryRuntimeV1);
            registry.RegisterGlobal(inventory);
            var owner = new SaveGameOwner(registry, new SaveMigrationPipeline(), events, null);
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                new[]
                {
                    new SaveStateRecord(
                        "inventory.state",
                        SaveStateTypeKeys.InventoryRuntimeV1,
                        new InventoryState(new[] { "key.present" }, "key.missing"))
                },
                Array.Empty<StableSaveStateRecord>());

            var exception = Assert.Throws<SaveValidationException>(() => owner.Restore(snapshot));

            Assert.IsTrue(HasError(exception.Report, "state-semantic-invalid"));
            Assert.IsTrue(inventory.HasItem("key.present"));
            Assert.AreEqual("key.present", inventory.Current.HeldItemId);
            Assert.AreEqual(0, changedEventCount);
        }

        [Test]
        public void SemanticValidatorExceptionFailsSafelyBeforeMutation()
        {
            var registry = new SaveableRegistry();
            var saveable = new SemanticTestSaveable(
                "door.front",
                4,
                _ => throw new InvalidOperationException("Simulated validator failure."));
            registry.RegisterStable(saveable);
            var owner = CreateOwner(registry);
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[] { CreateStableRecord("door.front", "door.state", 9) });

            var exception = Assert.Throws<SaveValidationException>(() => owner.Restore(snapshot));

            Assert.IsTrue(HasError(exception.Report, "state-semantic-validation-failed"));
            Assert.AreEqual(4, saveable.Value);
            Assert.AreEqual(0, saveable.RestoreCount);
        }

        [Test]
        public void OwnerWithoutSemanticCapabilityUsesStructuralRestorePath()
        {
            var registry = CreateRegistryWithGlobalDeclaration(
                "plain.state",
                SaveStateTypeKeys.Int32V1);
            var global = new TestGlobalOwner("plain.state", 3);
            registry.RegisterGlobal(global);
            var owner = CreateOwner(registry);
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                new[] { new SaveStateRecord("plain.state", SaveStateTypeKeys.Int32V1, 8) },
                Array.Empty<StableSaveStateRecord>());

            owner.Restore(snapshot);

            Assert.AreEqual(8, global.Value);
        }

        [Test]
        public void LastSemanticFailurePreventsMutationForEveryPlannedOwner()
        {
            var events = new GameplayEventBus();
            var restoreCompletedCount = 0;
            events.Subscribe<SaveRestoreCompleted>(_ => restoreCompletedCount++);
            var registry = new SaveableRegistry();
            var first = new SemanticTestSaveable(
                "door.first",
                1,
                _ => RestoreStateValidationResult.Success);
            var last = new SemanticTestSaveable(
                "door.last",
                2,
                _ => RestoreStateValidationResult.Invalid("Final owner rejected state."));
            registry.RegisterStable(first);
            registry.RegisterStable(last);
            var owner = new SaveGameOwner(registry, new SaveMigrationPipeline(), events, null);
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[]
                {
                    CreateStableRecord("door.first", "door.state", 10),
                    CreateStableRecord("door.last", "door.state", 20)
                });

            Assert.Throws<SaveValidationException>(() => owner.Restore(snapshot));

            Assert.AreEqual(1, first.Value);
            Assert.AreEqual(2, last.Value);
            Assert.AreEqual(0, first.RestoreCount);
            Assert.AreEqual(0, last.RestoreCount);
            Assert.AreEqual(0, restoreCompletedCount);
        }

        [Test]
        public void ValidSemanticStateStillParticipatesInApplyRollback()
        {
            var registry = new SaveableRegistry();
            var first = new SemanticTestSaveable(
                "door.first",
                1,
                _ => RestoreStateValidationResult.Success);
            var second = new TestSaveable("door.second", 2) { ThrowOnNextRestore = true };
            registry.RegisterStable(first);
            registry.RegisterStable(second);
            var owner = CreateOwner(registry);
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[]
                {
                    CreateStableRecord("door.first", "door.state", 10),
                    CreateStableRecord("door.second", "door.state", 20)
                });

            var exception = Assert.Throws<SaveValidationException>(() => owner.Restore(snapshot));

            Assert.IsTrue(HasError(exception.Report, "restore-apply-failed"));
            Assert.AreEqual(1, first.Value);
            Assert.AreEqual(2, second.Value);
            Assert.AreEqual(2, first.RestoreCount);
        }

        [Test]
        public void ApplyFailureRollsBackPreviouslyAppliedState()
        {
            var registry = new SaveableRegistry();
            var first = new TestSaveable("door.first", 1);
            var second = new TestSaveable("door.second", 2) { ThrowOnNextRestore = true };
            registry.RegisterStable(first);
            registry.RegisterStable(second);
            var owner = CreateOwner(registry);
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                new SaveSlotId("slot-1"),
                null,
                Array.Empty<SaveStateRecord>(),
                new[]
                {
                    CreateStableRecord("door.first", "door.state", 10),
                    CreateStableRecord("door.second", "door.state", 20)
                });

            var exception = Assert.Throws<SaveValidationException>(() => owner.Restore(snapshot));

            Assert.IsTrue(HasError(exception.Report, "restore-apply-failed"));
            Assert.AreEqual(1, first.Value);
            Assert.AreEqual(2, second.Value);
        }

        [Test]
        public void SceneLedgerPreservesSceneAThroughSceneBSaveRelaunchAndReturn()
        {
            var manifest = CreateManifest(
                new StableIdManifestEntry(
                    "door.scene-a",
                    StableIdManifestEntryKind.Required,
                    sceneId: "SceneA"),
                new StableIdManifestEntry(
                    "drawer.scene-b",
                    StableIdManifestEntryKind.Required,
                    sceneId: "SceneB"));
            try
            {
                var firstRegistry = new SaveableRegistry();
                var firstOwner = CreateOwner(firstRegistry);
                var sceneA = new TestSaveable("door.scene-a", 11, "SceneA");
                var sceneARegistration = firstRegistry.RegisterStable(sceneA);

                var retainReport = firstOwner.RetainSceneState(manifest, "SceneA");
                Assert.That(retainReport.HasErrors, Is.False);
                sceneARegistration.Dispose();

                var sceneB = new TestSaveable("drawer.scene-b", 22, "SceneB");
                firstRegistry.RegisterStable(sceneB);
                var snapshot = firstOwner.Capture(
                    new SaveSlotId("multi-scene"),
                    manifest,
                    new CheckpointModel("checkpoint-b", "SceneB", "spawn-b"));

                Assert.That(snapshot.StableStates.Count, Is.EqualTo(2));
                Assert.That(snapshot.VisitedSceneIds, Is.EquivalentTo(new[] { "SceneA", "SceneB" }));
                Assert.That(
                    snapshot.StableStates.Single(state => state.StableId == "door.scene-a").SceneId,
                    Is.EqualTo("SceneA"));

                var recreatedRegistry = new SaveableRegistry();
                var recreatedOwner = CreateOwner(recreatedRegistry);
                var restoredB = new TestSaveable("drawer.scene-b", 0, "SceneB");
                var sceneBRegistration = recreatedRegistry.RegisterStable(restoredB);

                Assert.That(recreatedOwner.Restore(snapshot, manifest).HasErrors, Is.False);
                Assert.That(restoredB.Value, Is.EqualTo(22));

                restoredB.Value = 0;
                Assert.That(recreatedOwner.Restore(snapshot, manifest).HasErrors, Is.False);
                Assert.That(restoredB.Value, Is.EqualTo(22));

                var secondSnapshot = recreatedOwner.Capture(
                    new SaveSlotId("multi-scene-second"),
                    manifest,
                    new CheckpointModel("checkpoint-b", "SceneB", "spawn-b"));
                Assert.That(secondSnapshot.VisitedSceneIds, Is.EqualTo(snapshot.VisitedSceneIds));
                Assert.That(
                    secondSnapshot.StableStates.Select(StateIdentity),
                    Is.EqualTo(snapshot.StableStates.Select(StateIdentity)));

                sceneBRegistration.Dispose();
                var restoredA = new TestSaveable("door.scene-a", 0, "SceneA");
                recreatedRegistry.RegisterStable(restoredA);
                Assert.That(recreatedOwner.RestoreRetainedScene(manifest, "SceneA").HasErrors, Is.False);
                Assert.That(restoredA.Value, Is.EqualTo(11));
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void RequiredOwnerIsValidatedOnlyWhenItsSceneLoads()
        {
            var manifest = CreateManifest(
                new StableIdManifestEntry(
                    "door.scene-a",
                    StableIdManifestEntryKind.Required,
                    sceneId: "SceneA"),
                new StableIdManifestEntry(
                    "drawer.scene-b",
                    StableIdManifestEntryKind.Required,
                    sceneId: "SceneB"));
            try
            {
                var registry = new SaveableRegistry();
                registry.RegisterStable(new TestSaveable("drawer.scene-b", 2, "SceneB"));
                var owner = CreateOwner(registry);
                var snapshot = new SaveGameSnapshot(
                    SaveSchemaVersion.Current,
                    new SaveSlotId("scene-applicability"),
                    new CheckpointModel("checkpoint-b", "SceneB", "spawn-b"),
                    Array.Empty<SaveStateRecord>(),
                    new[]
                    {
                        CreateStableRecord("door.scene-a", "door.state", 1, "SceneA"),
                        CreateStableRecord("drawer.scene-b", "door.state", 2, "SceneB")
                    },
                    visitedSceneIds: new[] { "SceneA", "SceneB" });

                Assert.That(owner.Restore(snapshot, manifest).HasErrors, Is.False);

                var sceneAReport = owner.RestoreRetainedScene(manifest, "SceneA");
                Assert.That(sceneAReport.HasErrors, Is.True);
                Assert.That(
                    sceneAReport.Issues.Any(issue => issue.StableId == "door.scene-a"),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void CaptureRejectsOwnerWhoseManifestEntryBelongsToAnotherScene()
        {
            var manifest = CreateManifest(new StableIdManifestEntry(
                "door.shared",
                StableIdManifestEntryKind.Required,
                sceneId: "SceneB"));
            try
            {
                var registry = new SaveableRegistry();
                registry.RegisterStable(new TestSaveable("door.shared", 1, "SceneA"));
                var owner = CreateOwner(registry);

                var exception = Assert.Throws<SaveValidationException>(() => owner.Capture(
                    new SaveSlotId("wrong-scene-owner"),
                    manifest,
                    new CheckpointModel("checkpoint-a", "SceneA", "spawn-a")));

                Assert.That(HasError(exception.Report, "stable-owner-scene-mismatch"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void CaptureRejectsDuplicateStableIdAcrossLoadedScenes()
        {
            var manifest = CreateManifest(new StableIdManifestEntry(
                "door.duplicate",
                StableIdManifestEntryKind.Required));
            try
            {
                var registry = new SaveableRegistry();
                registry.RegisterStable(new TestSaveable("door.duplicate", 1, "SceneA"));
                registry.RegisterStable(new TestSaveable("door.duplicate", 2, "SceneB"));
                var owner = CreateOwner(registry);

                var exception = Assert.Throws<SaveValidationException>(() => owner.Capture(
                    new SaveSlotId("additive-duplicate"),
                    manifest,
                    new CheckpointModel("checkpoint-a", "SceneA", "spawn-a")));

                Assert.That(
                    exception.Report.Issues.Any(issue =>
                        issue.Code == StableIdValidationIssueType.DuplicateId.ToString()),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void CaptureMergesOwnersFromAllLoadedScenesWithRetainedState()
        {
            var manifest = CreateManifest(
                new StableIdManifestEntry(
                    "door.scene-a",
                    StableIdManifestEntryKind.Required,
                    sceneId: "SceneA"),
                new StableIdManifestEntry(
                    "drawer.scene-b",
                    StableIdManifestEntryKind.Required,
                    sceneId: "SceneB"));
            try
            {
                var registry = new SaveableRegistry();
                registry.RegisterStable(new TestSaveable("door.scene-a", 10, "SceneA"));
                registry.RegisterStable(new TestSaveable("drawer.scene-b", 20, "SceneB"));
                var owner = CreateOwner(registry);

                var snapshot = owner.Capture(
                    new SaveSlotId("all-loaded-scenes"),
                    manifest,
                    new CheckpointModel("checkpoint-b", "SceneB", "spawn-b"));

                Assert.That(snapshot.StableStates.Count, Is.EqualTo(2));
                Assert.That(snapshot.VisitedSceneIds, Is.EquivalentTo(new[] { "SceneA", "SceneB" }));
                Assert.That(
                    snapshot.StableStates.Single(state => state.StableId == "door.scene-a").State,
                    Is.EqualTo(10));
                Assert.That(
                    snapshot.StableStates.Single(state => state.StableId == "drawer.scene-b").State,
                    Is.EqualTo(20));
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void RetainedSceneRestoreOnlyMutatesOwnersFromRequestedScene()
        {
            var manifest = CreateManifest(
                new StableIdManifestEntry(
                    "door.scene-a",
                    StableIdManifestEntryKind.Required,
                    sceneId: "SceneA"),
                new StableIdManifestEntry(
                    "drawer.scene-b",
                    StableIdManifestEntryKind.Required,
                    sceneId: "SceneB"));
            try
            {
                var registry = new SaveableRegistry();
                var sceneA = new TestSaveable("door.scene-a", 0, "SceneA");
                var sceneB = new TestSaveable("drawer.scene-b", 0, "SceneB");
                registry.RegisterStable(sceneA);
                registry.RegisterStable(sceneB);
                var owner = CreateOwner(registry);
                var snapshot = new SaveGameSnapshot(
                    SaveSchemaVersion.Current,
                    new SaveSlotId("additive-scene-scope"),
                    new CheckpointModel("checkpoint-a", "SceneA", "spawn-a"),
                    Array.Empty<SaveStateRecord>(),
                    new[]
                    {
                        CreateStableRecord("door.scene-a", "door.state", 10, "SceneA"),
                        CreateStableRecord("drawer.scene-b", "door.state", 20, "SceneB")
                    },
                    visitedSceneIds: new[] { "SceneA", "SceneB" });

                Assert.That(owner.Restore(snapshot, manifest).HasErrors, Is.False);
                Assert.That(sceneA.Value, Is.EqualTo(10));
                Assert.That(sceneB.Value, Is.EqualTo(0));

                Assert.That(owner.RestoreRetainedScene(manifest, "SceneB").HasErrors, Is.False);
                Assert.That(sceneA.Value, Is.EqualTo(10));
                Assert.That(sceneB.Value, Is.EqualTo(20));
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void FailedRestoreDoesNotReplaceRetainedSceneLedger()
        {
            var manifest = CreateManifest(
                new StableIdManifestEntry(
                    "door.scene-a",
                    StableIdManifestEntryKind.Required,
                    sceneId: "SceneA"),
                new StableIdManifestEntry(
                    "door.scene-b",
                    StableIdManifestEntryKind.Required,
                    sceneId: "SceneB"),
                new StableIdManifestEntry(
                    "zz.scene-b.failure",
                    StableIdManifestEntryKind.Required,
                    sceneId: "SceneB"));
            try
            {
                var registry = new SaveableRegistry();
                var owner = CreateOwner(registry);
                var originalA = new TestSaveable("door.scene-a", 7, "SceneA");
                var registrationA = registry.RegisterStable(originalA);
                Assert.That(owner.RetainSceneState(manifest, "SceneA").HasErrors, Is.False);
                registrationA.Dispose();

                var sceneB = new TestSaveable("door.scene-b", 3, "SceneB");
                var failingSceneB = new TestSaveable("zz.scene-b.failure", 4, "SceneB")
                {
                    ThrowOnNextRestore = true
                };
                registry.RegisterStable(sceneB);
                registry.RegisterStable(failingSceneB);
                var failingSnapshot = new SaveGameSnapshot(
                    SaveSchemaVersion.Current,
                    new SaveSlotId("ledger-rollback"),
                    new CheckpointModel("checkpoint-b", "SceneB", "spawn-b"),
                    Array.Empty<SaveStateRecord>(),
                    new[]
                    {
                        CreateStableRecord("door.scene-b", "door.state", 30, "SceneB"),
                        CreateStableRecord("zz.scene-b.failure", "door.state", 40, "SceneB")
                    },
                    visitedSceneIds: new[] { "SceneB" });

                Assert.Throws<SaveValidationException>(() => owner.Restore(failingSnapshot, manifest));
                Assert.That(owner.RetainedStableStates.Count, Is.EqualTo(1));
                Assert.That(owner.RetainedStableStates.Single().StableId, Is.EqualTo("door.scene-a"));
                Assert.That(owner.VisitedSceneIds, Is.EquivalentTo(new[] { "SceneA" }));
                Assert.That(sceneB.Value, Is.EqualTo(3));
                Assert.That(failingSceneB.Value, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }

        private static StableSaveStateRecord CreateStableRecord(
            string stableId,
            string stateKey,
            int value,
            string sceneId = null)
        {
            return new StableSaveStateRecord(
                stableId,
                stateKey,
                SaveStateTypeKeys.Int32V1,
                value,
                false,
                SaveRestorePolicy.ResumeDeterministicPhase,
                sceneId);
        }

        private static bool HasError(SaveRestoreReport report, string code)
        {
            return report.Issues.Any(issue =>
                issue.Severity == SaveRestoreIssueSeverity.Error && issue.Code == code);
        }

        private static string StateIdentity(StableSaveStateRecord state)
        {
            return $"{state.SceneId}|{state.StableId}|{state.StateKey}|{state.State}";
        }

        private static SaveGameOwner CreateOwner(SaveableRegistry registry)
        {
            return new SaveGameOwner(registry, new SaveMigrationPipeline(), new GameplayEventBus(), null);
        }

        private static SaveableRegistry CreateRegistryWithGlobalDeclaration(
            string stateKey,
            string stateTypeKey)
        {
            var declarations = new GlobalSaveStateRegistry();
            declarations.Register(new GlobalSaveStateDeclaration(
                stateKey,
                stateTypeKey,
                GlobalSaveStateRequirement.Required,
                SaveSchemaVersion.Current,
                GlobalSaveStateOwnerScope.Context,
                GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));
            return new SaveableRegistry(null, declarations);
        }

        private static StableIdManifest CreateManifest(params StableIdManifestEntry[] entries)
        {
            var manifest = ScriptableObject.CreateInstance<StableIdManifest>();
            manifest.ReplaceEntries(entries);
            return manifest;
        }

        private sealed class TestSaveable :
            RuntimeStateOwnerBase<int>,
            ISaveableStateOwner,
            ISceneScopedSaveableStateOwner
        {
            public TestSaveable(string stableId, int value, string sceneId = null)
                : base("door.state")
            {
                StableId = stableId;
                Value = value;
                SceneId = sceneId ?? string.Empty;
            }

            public string StableId { get; }
            public string SceneId { get; }
            public int Value { get; set; }
            public bool SimulatedDisabled { get; set; }
            public bool HasActiveState { get; set; }
            public SaveRestorePolicy ActiveRestorePolicy { get; set; } = SaveRestorePolicy.ResumeDeterministicPhase;
            public bool ThrowOnNextRestore { get; set; }

            public override int CaptureState()
            {
                return Value;
            }

            public override void RestoreState(int state)
            {
                Value = state;
                if (ThrowOnNextRestore)
                {
                    ThrowOnNextRestore = false;
                    throw new InvalidOperationException("Simulated restore apply failure.");
                }
            }
        }

        private sealed class TestGlobalOwner : RuntimeStateOwnerBase<int>
        {
            public TestGlobalOwner(string stateKey, int value)
                : base(stateKey)
            {
                Value = value;
            }

            public int Value { get; private set; }

            public override int CaptureState()
            {
                return Value;
            }

            public override void RestoreState(int state)
            {
                Value = state;
            }
        }

        private sealed class TestPlayerPoseOwner : RuntimeStateOwnerBase<PlayerPose>
        {
            private PlayerPose pose = new PlayerPose(
                Vector3.zero,
                Quaternion.identity,
                0f,
                PlayerControlState.Normal);

            public TestPlayerPoseOwner()
                : base(HorrorGlobalSaveStateRegistry.PlayerPoseStateKey)
            {
            }

            public override PlayerPose CaptureState()
            {
                return pose;
            }

            public override void RestoreState(PlayerPose state)
            {
                pose = state;
            }
        }

        private sealed class SemanticTestSaveable :
            RuntimeStateOwnerBase<int>,
            ISaveableStateOwner,
            IRestoreStateValidator
        {
            private readonly Func<int, RestoreStateValidationResult> validator;

            public SemanticTestSaveable(
                string stableId,
                int value,
                Func<int, RestoreStateValidationResult> validator)
                : base("door.state")
            {
                StableId = stableId;
                Value = value;
                this.validator = validator;
            }

            public string StableId { get; }
            public int Value { get; private set; }
            public int RestoreCount { get; private set; }
            public bool HasActiveState => false;
            public SaveRestorePolicy ActiveRestorePolicy => SaveRestorePolicy.ResumeDeterministicPhase;

            public override int CaptureState()
            {
                return Value;
            }

            public override void RestoreState(int state)
            {
                RestoreCount++;
                Value = state;
            }

            public RestoreStateValidationResult ValidateRestoreState(object state)
            {
                if (!(state is int value))
                {
                    return RestoreStateValidationResult.Invalid("Expected Int32 state.");
                }

                return validator(value);
            }
        }

        private sealed class VersionZeroMigration : ISaveMigrationRule
        {
            public int SourceVersion => 0;
            public int TargetVersion => SaveSchemaVersion.Current;

            public SaveGameSnapshot Migrate(SaveGameSnapshot snapshot)
            {
                return new SaveGameSnapshot(
                    TargetVersion,
                    snapshot.SlotId,
                    snapshot.Checkpoint,
                    snapshot.GlobalStates,
                    snapshot.StableStates,
                    snapshot.SavedAtUtcTicks);
            }
        }
    }
}
