using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;
using Setus.HorrorFramework.Interaction.Interactables;
using Setus.HorrorFramework.Inventory.Runtime;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.Player.State;
using Setus.HorrorFramework.SaveProgression.Checkpoints;
using Setus.HorrorFramework.SaveProgression.Migration;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.Serialization;
using Setus.HorrorFramework.SaveProgression.StableIds;
using Setus.HorrorFramework.SceneFlow.Transitions;
using UnityEngine;

namespace Setus.HorrorFramework.Tests.EditMode.SaveProgression
{
    public sealed class PersistentSaveSlotStoreTests
    {
        private string temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                "SetusHorrorFrameworkTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public void SaveSurvivesStoreRecreationAndMetadataSelectsCheckpointScene()
        {
            var slotId = new SaveSlotId("slot-one");
            var snapshot = CreateSnapshot(slotId);
            var firstStore = new PersistentSaveSlotStore(temporaryDirectory);
            firstStore.Save(snapshot);

            var recreatedStore = new PersistentSaveSlotStore(temporaryDirectory);

            Assert.That(recreatedStore.TryGetMetadata(slotId, out var metadata), Is.True);
            Assert.That(metadata.SceneName, Is.EqualTo("Gameplay"));
            Assert.That(metadata.CheckpointId, Is.EqualTo("checkpoint-a"));
            Assert.That(metadata.SpawnId, Is.EqualTo("spawn-a"));
            Assert.That(recreatedStore.TryLoad(slotId, out var loaded), Is.True);
            Assert.That(loaded.StableStates.Count, Is.EqualTo(1));
            Assert.That(loaded.StableStates[0].StateTypeKey, Is.EqualTo(SaveStateTypeKeys.InteractionObjectV1));
            Assert.That(((InteractionObjectState)loaded.StableStates[0].State).IsOpen, Is.True);
            Assert.That(loaded.Checkpoint.PlayerPose.HasValue, Is.True);
            Assert.That(loaded.Checkpoint.PlayerPose.Value.CameraPitch, Is.EqualTo(-12f));
        }

        [Test]
        public void SceneScopeAndVisitedSceneLedgerSurviveStoreRecreation()
        {
            var slotId = new SaveSlotId("multi-scene-ledger");
            var snapshot = new SaveGameSnapshot(
                SaveSchemaVersion.Current,
                slotId,
                new CheckpointModel("checkpoint-b", "SceneB", "spawn-b"),
                Array.Empty<SaveStateRecord>(),
                new[]
                {
                    new StableSaveStateRecord(
                        "door.a",
                        "interaction.state",
                        SaveStateTypeKeys.InteractionObjectV1,
                        new InteractionObjectState(isOpen: true),
                        false,
                        SaveRestorePolicy.ResumeDeterministicPhase,
                        "SceneA"),
                    new StableSaveStateRecord(
                        "drawer.b",
                        "interaction.state",
                        SaveStateTypeKeys.InteractionObjectV1,
                        new InteractionObjectState(isOpen: false),
                        false,
                        SaveRestorePolicy.ResumeDeterministicPhase,
                        "SceneB")
                },
                visitedSceneIds: new[] { "SceneB", "SceneA" });
            var store = new PersistentSaveSlotStore(temporaryDirectory);

            store.Save(snapshot);
            var recreated = new PersistentSaveSlotStore(temporaryDirectory);

            Assert.That(recreated.TryLoad(slotId, out var loaded), Is.True);
            Assert.That(loaded.VisitedSceneIds, Is.EquivalentTo(new[] { "SceneA", "SceneB" }));
            Assert.That(
                loaded.StableStates.Single(state => state.StableId == "door.a").SceneId,
                Is.EqualTo("SceneA"));
            Assert.That(
                loaded.StableStates.Single(state => state.StableId == "drawer.b").SceneId,
                Is.EqualTo("SceneB"));
        }

        [Test]
        public void CorruptSaveIsRejectedWithoutThrowing()
        {
            var slotId = new SaveSlotId("corrupt-slot");
            var store = new PersistentSaveSlotStore(temporaryDirectory);
            Directory.CreateDirectory(temporaryDirectory);
            File.WriteAllText(store.GetSlotFilePath(slotId), "{ this is not valid save data");

            Assert.That(store.TryGetMetadata(slotId, out _), Is.False);
            Assert.That(store.TryLoad(slotId, out _), Is.False);
            Assert.That(store.HasSlot(slotId), Is.False);
        }

        [Test]
        public void CurrentSavePersistsStableTypeKeyWithoutClrIdentity()
        {
            var slotId = new SaveSlotId("stable-key-slot");
            var store = new PersistentSaveSlotStore(temporaryDirectory);

            store.Save(CreateSnapshot(slotId));

            var json = File.ReadAllText(store.GetSlotFilePath(slotId));
            Assert.That(json, Does.Contain("\"formatVersion\": 2"));
            Assert.That(json, Does.Contain("\"stateTypeKey\": \"interaction.object.v1\""));
            Assert.That(json, Does.Not.Contain("AssemblyQualifiedName"));
            Assert.That(json, Does.Not.Contain(typeof(InteractionObjectState).FullName));
        }

        [Test]
        public void UnknownStableTypeKeyIsRejectedSafely()
        {
            var slotId = new SaveSlotId("unknown-key-slot");
            var store = new PersistentSaveSlotStore(temporaryDirectory);
            WriteTestDocument(store, slotId, CreateState("unknown.state.v1", null, "12"));

            Assert.That(store.TryLoad(slotId, out _), Is.False);
        }

        [Test]
        public void DuplicateStableTypeKeyRegistrationIsRejected()
        {
            var registry = new SaveStateTypeRegistry();
            registry.Register(CreateIntCodec("test.number.v1"));

            Assert.That(
                () => registry.Register(new SaveStateCodec<string>("test.number.v1", value => value, value => value)),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void LegacyIdentityMapsToStableKeyBeforeSchemaMigration()
        {
            const string stableTypeKey = "test.counter.v1";
            const string legacyIdentity = "Legacy.Game.CounterState, Legacy.Game";
            var slotId = new SaveSlotId("legacy-migration-slot");
            var stateTypes = CreateTestRegistry(stableTypeKey, legacyIdentity);
            var store = new PersistentSaveSlotStore(temporaryDirectory, stateTypes);
            WriteTestDocument(
                store,
                slotId,
                CreateState(null, legacyIdentity, "17"),
                SaveFileFormatVersion.LegacyClrTypeIdentity,
                schemaVersion: 0,
                stableId: "legacy.owner");

            Assert.That(store.TryLoad(slotId, out var legacySnapshot), Is.True);
            Assert.That(legacySnapshot.StableStates[0].StateTypeKey, Is.EqualTo(stableTypeKey));

            var saveables = new SaveableRegistry(stateTypes);
            var saveable = new TestSaveable("legacy.owner", 3);
            saveables.RegisterStable(saveable);
            var migrations = new SaveMigrationPipeline();
            migrations.Register(new VersionZeroMigration());
            var owner = new SaveGameOwner(saveables, migrations, new GameplayEventBus(), null);

            var report = owner.Restore(legacySnapshot);

            Assert.That(report.HasErrors, Is.False);
            Assert.That(saveable.Value, Is.EqualTo(17));
        }

        [Test]
        public void LegacyFormatOneProductionIdentityLoadsThroughExplicitAlias()
        {
            const string legacyIdentity =
                "Setus.HorrorFramework.Interaction.Interactables.InteractionObjectState, " +
                "Setus.HorrorFramework.Runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
            var slotId = new SaveSlotId("legacy-production-slot");
            var store = new PersistentSaveSlotStore(temporaryDirectory);
            WriteTestDocument(
                store,
                slotId,
                CreateState(
                    null,
                    legacyIdentity,
                    "{\"isOpen\":true,\"isLocked\":false,\"isConsumed\":false," +
                    "\"isInspected\":false,\"isEnabled\":true}",
                    "json"),
                SaveFileFormatVersion.LegacyClrTypeIdentity);

            Assert.That(store.TryLoad(slotId, out var snapshot), Is.True);
            Assert.That(snapshot.StableStates[0].StateTypeKey, Is.EqualTo(SaveStateTypeKeys.InteractionObjectV1));
            Assert.That(((InteractionObjectState)snapshot.StableStates[0].State).IsOpen, Is.True);
        }

        [Test]
        public void InvalidLegacyIdentityIsRejectedWithoutThrowing()
        {
            var slotId = new SaveSlotId("invalid-legacy-slot");
            var store = new PersistentSaveSlotStore(temporaryDirectory);
            WriteTestDocument(
                store,
                slotId,
                CreateState(null, "Missing.Legacy.State, Missing.Assembly", "12"),
                SaveFileFormatVersion.LegacyClrTypeIdentity);

            Assert.That(store.TryLoad(slotId, out _), Is.False);
        }

        [Test]
        public void RegistryRecreationResolvesStableKeysDeterministically()
        {
            var first = HorrorSaveStateTypeRegistry.CreateDefault();
            var second = HorrorSaveStateTypeRegistry.CreateDefault();

            CollectionAssert.AreEqual(first.RegisteredTypeKeys, second.RegisteredTypeKeys);
            Assert.That(first.Resolve(SaveStateTypeKeys.PlayerPoseV1).StateType, Is.EqualTo(typeof(PlayerPose)));
            Assert.That(second.Resolve(typeof(PlayerPose)).TypeKey, Is.EqualTo(SaveStateTypeKeys.PlayerPoseV1));
        }

        [Test]
        public void CorruptStableTypeKeyCannotMutateLiveState()
        {
            var slotId = new SaveSlotId("corrupt-key-slot");
            var store = new PersistentSaveSlotStore(temporaryDirectory);
            var saveable = new TestSaveable("m5.door", 4);
            WriteTestDocument(store, slotId, CreateState("corrupt.key", null, "99"));

            Assert.That(store.TryLoad(slotId, out _), Is.False);
            Assert.That(saveable.Value, Is.EqualTo(4));
        }

        [Test]
        public void MissingManifestBlocksProductionCapture()
        {
            var coordinator = CreateCoordinator(new InMemorySaveSlotStore());

            Assert.That(
                () => coordinator.CaptureAndSave(new SaveSlotId("slot-one"), CreateSnapshot(new SaveSlotId("slot-one")).Checkpoint),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void ContinueIsUnavailableWhenNoValidSlotExists()
        {
            var slotId = SingleSaveSlotContract.SlotId;
            var coordinator = CreateCoordinator(new InMemorySaveSlotStore());
            coordinator.Configure(CreateManifest());

            Assert.That(coordinator.HasValidSlot(slotId), Is.False);
            Assert.That(
                coordinator.TryPrepareContinue(slotId, out _, out var failureReason),
                Is.False);
            Assert.That(failureReason, Does.Contain("does not exist"));
            Assert.That(coordinator.CurrentUserFailure.HasValue, Is.True);
            Assert.That(coordinator.CurrentUserFailure.Value.Kind, Is.EqualTo(SaveLoadUserFailureKind.NoSave));
            Assert.That(
                coordinator.CurrentUserFailure.Value.Message.EntryKey,
                Is.EqualTo(FrameworkTextKeys.SaveErrorNoSave));
            Assert.That(
                coordinator.CurrentUserFailure.Value.Message.FallbackText,
                Is.EqualTo("No saved game is available."));
        }

        [Test]
        public void ValidSlotPassesNonMutatingContinueValidation()
        {
            var slotId = new SaveSlotId("valid-slot");
            var store = new InMemorySaveSlotStore();
            store.Save(CreateSnapshot(slotId));
            var coordinator = CreateCoordinator(store);
            coordinator.Configure(CreateManifest());

            var result = coordinator.ValidateSlot(slotId);

            Assert.That(result.IsValid, Is.True, result.Diagnostic);
            Assert.That(result.Metadata.SceneName, Is.EqualTo("Gameplay"));
            Assert.That(result.Snapshot, Is.Not.Null);
        }

        [Test]
        public void CorruptJsonDisablesContinueBeforeSceneTransition()
        {
            var slotId = SingleSaveSlotContract.SlotId;
            var store = new PersistentSaveSlotStore(temporaryDirectory);
            Directory.CreateDirectory(temporaryDirectory);
            File.WriteAllText(store.GetSlotFilePath(slotId), "{ not-json");
            var coordinator = CreateCoordinator(store);
            coordinator.Configure(CreateManifest());

            var result = coordinator.ValidateSlot(slotId);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureStage, Is.EqualTo(SaveSlotValidationStage.FileFormat));
            Assert.That(coordinator.TryPrepareContinue(slotId, out _, out _), Is.False);
            Assert.That(
                coordinator.CurrentUserFailure.Value.Kind,
                Is.EqualTo(SaveLoadUserFailureKind.CorruptSave));
            Assert.That(
                coordinator.CurrentUserFailure.Value.Message.EntryKey,
                Is.EqualTo(FrameworkTextKeys.SaveErrorCorrupt));
        }

        [Test]
        public void MetadataValidButPayloadCorruptDisablesContinue()
        {
            var slotId = new SaveSlotId("payload-corrupt-slot");
            var store = new PersistentSaveSlotStore(temporaryDirectory);
            WriteTestDocument(
                store,
                slotId,
                CreateState(SaveStateTypeKeys.InteractionObjectV1, null, "{ not-json", "json"));
            var coordinator = CreateCoordinator(store);
            coordinator.Configure(CreateManifest());

            var result = coordinator.ValidateSlot(slotId);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureStage, Is.EqualTo(SaveSlotValidationStage.PayloadDecode));
        }

        [Test]
        public void UnsupportedSchemaDisablesContinue()
        {
            var slotId = new SaveSlotId("unsupported-schema-slot");
            var store = new InMemorySaveSlotStore();
            store.Save(CreateSnapshot(slotId, SaveSchemaVersion.Current + 1));
            var coordinator = CreateCoordinator(store);
            coordinator.Configure(CreateManifest());

            var result = coordinator.ValidateSlot(slotId);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureStage, Is.EqualTo(SaveSlotValidationStage.SchemaMigration));
            Assert.That(coordinator.TryPrepareContinue(slotId, out _, out _), Is.False);
            Assert.That(
                coordinator.CurrentUserFailure.Value.Kind,
                Is.EqualTo(SaveLoadUserFailureKind.IncompatibleSave));
            Assert.That(
                coordinator.CurrentUserFailure.Value.Message.EntryKey,
                Is.EqualTo(FrameworkTextKeys.SaveErrorIncompatible));
        }

        [Test]
        public void MigratableLegacySlotEnablesContinue()
        {
            const string stableTypeKey = "test.counter.v1";
            const string legacyIdentity = "Legacy.Game.CounterState, Legacy.Game";
            const string stableId = "legacy.owner";
            var slotId = new SaveSlotId("legacy-continue-slot");
            var stateTypes = CreateTestRegistry(stableTypeKey, legacyIdentity);
            var store = new PersistentSaveSlotStore(temporaryDirectory, stateTypes);
            WriteTestDocument(
                store,
                slotId,
                CreateState(null, legacyIdentity, "17"),
                SaveFileFormatVersion.LegacyClrTypeIdentity,
                schemaVersion: 0,
                stableId: stableId);
            var migrations = new SaveMigrationPipeline();
            migrations.Register(new VersionZeroMigration());
            var coordinator = CreateCoordinator(store, new SaveableRegistry(stateTypes), migrations);
            coordinator.Configure(CreateManifest(stableId));

            var result = coordinator.ValidateSlot(slotId);

            Assert.That(result.IsValid, Is.True, result.Diagnostic);
            Assert.That(result.Snapshot.SchemaVersion, Is.EqualTo(SaveSchemaVersion.Current));
            Assert.That(result.Snapshot.StableStates[0].StateTypeKey, Is.EqualTo(stableTypeKey));
        }

        [Test]
        public void UnknownStableTypeKeyDisablesContinue()
        {
            var slotId = new SaveSlotId("unknown-continue-key-slot");
            var store = new PersistentSaveSlotStore(temporaryDirectory);
            WriteTestDocument(store, slotId, CreateState("unknown.state.v1", null, "12"));
            var coordinator = CreateCoordinator(store);
            coordinator.Configure(CreateManifest());

            var result = coordinator.ValidateSlot(slotId);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureStage, Is.EqualTo(SaveSlotValidationStage.TypeKeyResolution));
        }

        [Test]
        public void SemanticInvalidGlobalStateDisablesContinueWithoutMutatingOwner()
        {
            var slotId = new SaveSlotId("semantic-invalid-slot");
            var store = new InMemorySaveSlotStore();
            store.Save(CreateSnapshot(
                slotId,
                SaveSchemaVersion.Current,
                new[]
                {
                    new SaveStateRecord(
                        "inventory.state",
                        SaveStateTypeKeys.InventoryRuntimeV1,
                        new InventoryState(new[] { "key-a" }, "missing-key"))
                }));
            var registry = CreateRegistryWithGlobalDeclaration(
                "inventory.state",
                SaveStateTypeKeys.InventoryRuntimeV1);
            var inventory = new InventoryRuntimeModel();
            inventory.AddItem("existing-item", holdItem: true);
            registry.RegisterGlobal(inventory);
            var coordinator = CreateCoordinator(store, registry);
            coordinator.Configure(CreateManifest());

            var result = coordinator.ValidateSlot(slotId);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureStage, Is.EqualTo(SaveSlotValidationStage.SemanticPreflight));
            Assert.That(inventory.HasItem("existing-item"), Is.True);
            Assert.That(inventory.HasItem("key-a"), Is.False);
        }

        [Test]
        public void SaveRewriteInvalidatesCachedContinueValidation()
        {
            var slotId = new SaveSlotId("cache-refresh-slot");
            var store = new InMemorySaveSlotStore();
            var coordinator = CreateCoordinator(store);
            coordinator.Configure(CreateEmptyManifest());

            Assert.That(coordinator.HasValidSlot(slotId), Is.False);

            coordinator.CaptureAndSave(slotId, CreateSnapshot(slotId).Checkpoint);

            Assert.That(coordinator.HasValidSlot(slotId), Is.True);
        }

        [Test]
        public void CanonicalSingleSlotSaveDoesNotOverwriteAnotherStoredSlot()
        {
            var canonicalSlot = SingleSaveSlotContract.SlotId;
            var otherSlot = new SaveSlotId("other-slot");
            var store = new InMemorySaveSlotStore();
            var otherSnapshot = CreateSnapshot(otherSlot);
            store.Save(otherSnapshot);
            var coordinator = CreateCoordinator(store);
            coordinator.Configure(CreateEmptyManifest());

            coordinator.CaptureAndSave(
                canonicalSlot,
                new CheckpointModel("primary-checkpoint", "Gameplay", "spawn-a"));

            Assert.That(store.TryLoad(canonicalSlot, out var canonicalSnapshot), Is.True);
            Assert.That(canonicalSnapshot.SlotId, Is.EqualTo(canonicalSlot));
            Assert.That(canonicalSnapshot.Checkpoint.CheckpointId, Is.EqualTo("primary-checkpoint"));
            Assert.That(store.TryLoad(otherSlot, out var retainedOtherSnapshot), Is.True);
            Assert.That(retainedOtherSnapshot, Is.SameAs(otherSnapshot));
        }

        [Test]
        public void RuntimeOwnerTypeMismatchDisablesContinueBeforeRestore()
        {
            var slotId = new SaveSlotId("owner-mismatch-slot");
            var store = new InMemorySaveSlotStore();
            store.Save(CreateSnapshot(
                slotId,
                SaveSchemaVersion.Current,
                new[]
                {
                    new SaveStateRecord(
                        "inventory.state",
                        SaveStateTypeKeys.StringV1,
                        "not-an-inventory-state")
                }));
            var registry = CreateRegistryWithGlobalDeclaration(
                "inventory.state",
                SaveStateTypeKeys.InventoryRuntimeV1);
            registry.RegisterGlobal(new InventoryRuntimeModel());
            var coordinator = CreateCoordinator(store, registry);
            coordinator.Configure(CreateManifest());

            var result = coordinator.ValidateSlot(slotId);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureStage, Is.EqualTo(SaveSlotValidationStage.StructuralPreflight));
        }

        [Test]
        public void ContinuePreparationUsesCanonicalSingleSlotWhenAnotherSlotExists()
        {
            var slotId = SingleSaveSlotContract.SlotId;
            var store = new InMemorySaveSlotStore();
            store.Save(CreateSnapshot(new SaveSlotId("other-slot")));
            store.Save(CreateSnapshot(slotId));
            var coordinator = CreateCoordinator(store);
            coordinator.Configure(CreateManifest());

            Assert.That(
                coordinator.TryPrepareContinue(new SaveSlotId("missing-slot"), out _, out _),
                Is.False);
            Assert.That(coordinator.CurrentUserFailure.HasValue, Is.True);

            Assert.That(coordinator.TryPrepareContinue(slotId, out var request, out var failureReason), Is.True, failureReason);
            Assert.That(coordinator.CurrentUserFailure.HasValue, Is.False);
            Assert.That(request.SceneName, Is.EqualTo("Gameplay"));
            Assert.That(request.CheckpointId, Is.EqualTo("checkpoint-a"));
            Assert.That(request.SpawnId, Is.EqualTo("spawn-a"));
        }

        [Test]
        public void PreparedContinueRestoresOnlyAfterTheMatchingSceneRequestCompletes()
        {
            var registry = new SaveableRegistry();
            var saveable = new TestSaveable("m5.door", 7);
            registry.RegisterStable(saveable);
            var owner = new SaveGameOwner(
                registry,
                new SaveMigrationPipeline(),
                new GameplayEventBus(),
                null);
            var store = new InMemorySaveSlotStore();
            var coordinator = new SaveLoadCoordinator(owner, store);
            coordinator.Configure(CreateManifest());
            var slotId = new SaveSlotId("slot-one");

            coordinator.CaptureAndSave(slotId, new CheckpointModel("checkpoint-a", "Gameplay", "spawn-a"));
            saveable.Value = 0;
            Assert.That(coordinator.TryPrepareContinue(slotId, out var request, out var failureReason), Is.True, failureReason);
            Assert.That(coordinator.TryRestorePrepared(new SceneTransitionRequest("MainMenu"), out _, out _), Is.True);
            Assert.That(saveable.Value, Is.EqualTo(0));

            Assert.That(coordinator.TryRestorePrepared(request, out var report, out var restoreFailure), Is.True, restoreFailure);
            Assert.That(report.HasErrors, Is.False);
            Assert.That(saveable.Value, Is.EqualTo(7));
        }

        [Test]
        public void RestoreApplyFailurePublishesSafeUserMessageAndRetainsTechnicalDiagnostic()
        {
            var registry = new SaveableRegistry();
            var saveable = new TestSaveable("m5.door", 7);
            registry.RegisterStable(saveable);
            var owner = new SaveGameOwner(
                registry,
                new SaveMigrationPipeline(),
                new GameplayEventBus(),
                null);
            var store = new InMemorySaveSlotStore();
            var logger = new RecordingHorrorLogger();
            var coordinator = new SaveLoadCoordinator(owner, store, logger);
            coordinator.Configure(CreateManifest());
            var slotId = new SaveSlotId("restore-failure-slot");

            coordinator.CaptureAndSave(
                slotId,
                new CheckpointModel("checkpoint-a", "Gameplay", "spawn-a"));
            saveable.Value = 0;
            Assert.That(coordinator.TryPrepareContinue(slotId, out var request, out _), Is.True);
            saveable.ThrowOnNextRestore = true;

            Assert.That(
                coordinator.TryRestorePrepared(request, out _, out var technicalDiagnostic),
                Is.False);
            Assert.That(technicalDiagnostic, Does.Contain("Simulated restore apply failure"));
            Assert.That(coordinator.CurrentUserFailure.HasValue, Is.True);
            Assert.That(
                coordinator.CurrentUserFailure.Value.Kind,
                Is.EqualTo(SaveLoadUserFailureKind.RestoreFailed));
            Assert.That(
                coordinator.CurrentUserFailure.Value.Message.EntryKey,
                Is.EqualTo(FrameworkTextKeys.SaveErrorRestoreFailed));
            Assert.That(
                coordinator.CurrentUserFailure.Value.Message.FallbackText,
                Does.Not.Contain("Simulated"));
            Assert.That(
                logger.Errors.Any(message => message.Contains("Simulated restore apply failure")),
                Is.True);
        }

        private static SaveLoadCoordinator CreateCoordinator(
            ISaveSlotStore store,
            SaveableRegistry registry = null,
            SaveMigrationPipeline migrations = null)
        {
            var owner = new SaveGameOwner(
                registry ?? new SaveableRegistry(),
                migrations ?? new SaveMigrationPipeline(),
                new GameplayEventBus(),
                null);
            return new SaveLoadCoordinator(owner, store);
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

        private static StableIdManifest CreateManifest(string requiredStableId = "m5.door")
        {
            var manifest = UnityEngine.ScriptableObject.CreateInstance<StableIdManifest>();
            manifest.ReplaceEntries(new[]
            {
                new StableIdManifestEntry(requiredStableId, StableIdManifestEntryKind.Required)
            });
            return manifest;
        }

        private static StableIdManifest CreateEmptyManifest()
        {
            return UnityEngine.ScriptableObject.CreateInstance<StableIdManifest>();
        }

        private static SaveGameSnapshot CreateSnapshot(
            SaveSlotId slotId,
            int schemaVersion = SaveSchemaVersion.Current,
            SaveStateRecord[] globalStates = null)
        {
            return new SaveGameSnapshot(
                schemaVersion,
                slotId,
                new CheckpointModel(
                    "checkpoint-a",
                    "Gameplay",
                    "spawn-a",
                    new PlayerPose(
                        new Vector3(3f, 1f, -2f),
                        Quaternion.Euler(0f, 40f, 0f),
                        -12f,
                        PlayerControlState.Normal)),
                globalStates ?? Array.Empty<SaveStateRecord>(),
                new[]
                {
                    new StableSaveStateRecord(
                        "m5.door",
                        "interaction.state",
                        SaveStateTypeKeys.InteractionObjectV1,
                        new InteractionObjectState(isOpen: true),
                        false,
                        SaveRestorePolicy.ResumeDeterministicPhase)
                });
        }

        private static SaveStateCodec<int> CreateIntCodec(string typeKey)
        {
            return new SaveStateCodec<int>(
                typeKey,
                value => value.ToString(CultureInfo.InvariantCulture),
                value => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture));
        }

        private static SaveStateTypeRegistry CreateTestRegistry(string typeKey, string legacyIdentity)
        {
            var registry = new SaveStateTypeRegistry();
            registry.Register(CreateIntCodec(typeKey), legacyIdentity);
            return registry;
        }

        private static TestPersistedState CreateState(
            string stateTypeKey,
            string legacyTypeIdentity,
            string value,
            string kind = "Int32")
        {
            return new TestPersistedState
            {
                stateKey = "test.state",
                stateTypeKey = stateTypeKey,
                stateTypeName = legacyTypeIdentity,
                payload = new TestPersistedPayload { kind = kind, value = value }
            };
        }

        private void WriteTestDocument(
            PersistentSaveSlotStore store,
            SaveSlotId slotId,
            TestPersistedState state,
            int formatVersion = SaveFileFormatVersion.Current,
            int schemaVersion = SaveSchemaVersion.Current,
            string stableId = "m5.door")
        {
            Directory.CreateDirectory(temporaryDirectory);
            var document = new TestSaveDocument
            {
                formatVersion = formatVersion,
                slotId = slotId.Value,
                schemaVersion = schemaVersion,
                savedAtUtcTicks = DateTime.UtcNow.Ticks,
                checkpoint = new TestPersistedCheckpoint
                {
                    checkpointId = "checkpoint-a",
                    sceneName = "Gameplay",
                    spawnId = "spawn-a"
                },
                globalStates = Array.Empty<TestPersistedState>(),
                stableStates = new[]
                {
                    new TestPersistedStableState
                    {
                        stableId = stableId,
                        stateKey = state.stateKey,
                        stateTypeKey = state.stateTypeKey,
                        stateTypeName = state.stateTypeName,
                        payload = state.payload,
                        activeRestorePolicy = SaveRestorePolicy.ResumeDeterministicPhase
                    }
                }
            };
            File.WriteAllText(store.GetSlotFilePath(slotId), JsonUtility.ToJson(document, true));
        }

        [Serializable]
        private sealed class TestSaveDocument
        {
            public int formatVersion;
            public string slotId;
            public int schemaVersion;
            public long savedAtUtcTicks;
            public TestPersistedCheckpoint checkpoint;
            public TestPersistedState[] globalStates;
            public TestPersistedStableState[] stableStates;
        }

        [Serializable]
        private sealed class TestPersistedCheckpoint
        {
            public string checkpointId;
            public string sceneName;
            public string spawnId;
        }

        [Serializable]
        private class TestPersistedState
        {
            public string stateKey;
            public string stateTypeKey;
            public string stateTypeName;
            public TestPersistedPayload payload;
        }

        [Serializable]
        private sealed class TestPersistedStableState : TestPersistedState
        {
            public string stableId;
            public bool hadActiveState;
            public SaveRestorePolicy activeRestorePolicy;
        }

        [Serializable]
        private sealed class TestPersistedPayload
        {
            public string kind;
            public string value;
        }

        private sealed class TestSaveable : RuntimeStateOwnerBase<int>, ISaveableStateOwner
        {
            public TestSaveable(string stableId, int value)
                : base("test.state")
            {
                StableId = stableId;
                Value = value;
            }

            public string StableId { get; }
            public int Value { get; set; }
            public bool ThrowOnNextRestore { get; set; }
            public bool HasActiveState => false;
            public SaveRestorePolicy ActiveRestorePolicy => SaveRestorePolicy.ResumeDeterministicPhase;

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

        private sealed class RecordingHorrorLogger : IHorrorLogger
        {
            public System.Collections.Generic.List<string> Errors { get; } =
                new System.Collections.Generic.List<string>();

            public bool IsEnabled(HorrorLogCategory category) => true;
            public void Log(HorrorLogCategory category, string message) { }
            public void Warning(HorrorLogCategory category, string message) { }
            public void Error(HorrorLogCategory category, string message) => Errors.Add(message);
        }
    }
}
