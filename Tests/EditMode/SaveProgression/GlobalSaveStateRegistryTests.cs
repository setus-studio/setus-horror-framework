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

namespace Setus.HorrorFramework.Tests.EditMode.SaveProgression
{
    public sealed class GlobalSaveStateRegistryTests
    {
        [Test]
        public void ProductionDeclarationsClassifyCurrentGlobalStates()
        {
            var declarations = HorrorGlobalSaveStateRegistry.CreateDefault();

            AssertDeclaration(
                declarations,
                HorrorGlobalSaveStateRegistry.InventoryStateKey,
                SaveStateTypeKeys.InventoryRuntimeV1,
                GlobalSaveStateRequirement.Required,
                GlobalSaveStateOwnerScope.Context);
            AssertDeclaration(
                declarations,
                HorrorGlobalSaveStateRegistry.PlayerPoseStateKey,
                SaveStateTypeKeys.PlayerPoseV1,
                GlobalSaveStateRequirement.Required,
                GlobalSaveStateOwnerScope.Scene);
            AssertDeclaration(
                declarations,
                HorrorGlobalSaveStateRegistry.RuntimeSettingsStateKey,
                SaveStateTypeKeys.RuntimeSettingsV2,
                GlobalSaveStateRequirement.Optional,
                GlobalSaveStateOwnerScope.Context);
        }

        [Test]
        public void RequiredInventoryRecordPresentPassesSlotValidation()
        {
            var registry = CreateRegistryWith(
                Required(HorrorGlobalSaveStateRegistry.InventoryStateKey, SaveStateTypeKeys.InventoryRuntimeV1));
            registry.RegisterGlobal(new InventoryRuntimeModel());
            var report = registry.ValidateSlotRestoreCompatibility(
                new[] { InventoryRecord() },
                Array.Empty<StableSaveStateRecord>(),
                null);

            Assert.That(report.HasErrors, Is.False);
        }

        [Test]
        public void RequiredInventoryRecordMissingFailsSlotValidation()
        {
            var registry = CreateRegistryWith(
                Required(HorrorGlobalSaveStateRegistry.InventoryStateKey, SaveStateTypeKeys.InventoryRuntimeV1));
            registry.RegisterGlobal(new InventoryRuntimeModel());
            var report = registry.ValidateSlotRestoreCompatibility(
                Array.Empty<SaveStateRecord>(),
                Array.Empty<StableSaveStateRecord>(),
                null);

            AssertError(report, "missing-required-global-record");
        }

        [Test]
        public void RequiredPlayerPosePresentPassesWithoutLiveSceneOwner()
        {
            var registry = CreateRegistryWith(
                Required(
                    HorrorGlobalSaveStateRegistry.PlayerPoseStateKey,
                    SaveStateTypeKeys.PlayerPoseV1,
                    GlobalSaveStateOwnerScope.Scene));
            var report = registry.ValidateSlotRestoreCompatibility(
                new[] { PlayerPoseRecord() },
                Array.Empty<StableSaveStateRecord>(),
                null);

            Assert.That(report.HasErrors, Is.False);
        }

        [Test]
        public void RequiredPlayerPoseMissingFailsSlotValidation()
        {
            var registry = CreateRegistryWith(
                Required(
                    HorrorGlobalSaveStateRegistry.PlayerPoseStateKey,
                    SaveStateTypeKeys.PlayerPoseV1,
                    GlobalSaveStateOwnerScope.Scene));
            var report = registry.ValidateSlotRestoreCompatibility(
                Array.Empty<SaveStateRecord>(),
                Array.Empty<StableSaveStateRecord>(),
                null);

            AssertError(report, "missing-required-global-record");
        }

        [Test]
        public void RequiredSceneOwnerIsDeferredAtSlotLevelAndRequiredDuringRestore()
        {
            var registry = CreateRegistryWith(
                Required(
                    HorrorGlobalSaveStateRegistry.PlayerPoseStateKey,
                    SaveStateTypeKeys.PlayerPoseV1,
                    GlobalSaveStateOwnerScope.Scene));
            var snapshot = CreateSnapshot(new[] { PlayerPoseRecord() });
            var saveOwner = CreateSaveOwner(registry);

            var slotReport = saveOwner.PreflightSlotRestore(snapshot);
            var restoreException = Assert.Throws<SaveValidationException>(
                () => saveOwner.Restore(snapshot));

            Assert.That(slotReport.IsValid, Is.True);
            AssertError(restoreException.Report, "missing-required-global-owner");
        }

        [Test]
        public void OptionalGlobalRecordMissingPreservesCurrentState()
        {
            var declaration = Optional("optional.state", SaveStateTypeKeys.Int32V1);
            var registry = CreateRegistryWith(declaration);
            var owner = new TestGlobalOwner("optional.state", 7);
            registry.RegisterGlobal(owner);
            var saveOwner = CreateSaveOwner(registry);
            var snapshot = CreateSnapshot(Array.Empty<SaveStateRecord>());

            var report = saveOwner.Restore(snapshot);

            Assert.That(report.HasErrors, Is.False);
            Assert.That(owner.Value, Is.EqualTo(7));
            Assert.That(owner.RestoreCount, Is.Zero);
        }

        [Test]
        public void UnknownGlobalRecordIsRejected()
        {
            var registry = CreateRegistryWith(
                Optional("known.state", SaveStateTypeKeys.Int32V1));
            var report = registry.ValidateSlotRestoreCompatibility(
                new[] { new SaveStateRecord("unknown.state", SaveStateTypeKeys.Int32V1, 3) },
                Array.Empty<StableSaveStateRecord>(),
                null);

            AssertError(report, "unknown-global-state-key");
        }

        [Test]
        public void DuplicateGlobalRecordIsRejected()
        {
            var registry = CreateRegistryWith(
                Optional("known.state", SaveStateTypeKeys.Int32V1));
            var records = new[]
            {
                new SaveStateRecord("known.state", SaveStateTypeKeys.Int32V1, 3),
                new SaveStateRecord("known.state", SaveStateTypeKeys.Int32V1, 4)
            };

            var report = registry.ValidateSlotRestoreCompatibility(
                records,
                Array.Empty<StableSaveStateRecord>(),
                null);

            AssertError(report, "duplicate-global-record");
        }

        [Test]
        public void RequiredDeclarationAppliesOnlyFromIntroductionSchema()
        {
            var declaration = Required(
                "objective.state",
                SaveStateTypeKeys.Int32V1,
                introducedInSchemaVersion: 2);
            var registry = CreateRegistryWith(declaration);

            var legacyReport = registry.ValidateSlotRestoreCompatibility(
                Array.Empty<SaveStateRecord>(),
                Array.Empty<StableSaveStateRecord>(),
                null,
                schemaVersion: 1);
            var currentReport = registry.ValidateSlotRestoreCompatibility(
                Array.Empty<SaveStateRecord>(),
                Array.Empty<StableSaveStateRecord>(),
                null,
                schemaVersion: 2);

            Assert.That(legacyReport.HasErrors, Is.False);
            AssertError(currentReport, "missing-required-global-record");
        }

        [Test]
        public void ContinueValidationRejectsMissingRequiredInventoryRecord()
        {
            var registry = CreateRegistryWith(
                Required(HorrorGlobalSaveStateRegistry.InventoryStateKey, SaveStateTypeKeys.InventoryRuntimeV1));
            registry.RegisterGlobal(new InventoryRuntimeModel());
            var store = new InMemorySaveSlotStore();
            var slotId = new SaveSlotId("missing-required-global");
            store.Save(CreateSnapshot(Array.Empty<SaveStateRecord>(), slotId));
            var coordinator = new SaveLoadCoordinator(CreateSaveOwner(registry), store);
            var manifest = ScriptableObject.CreateInstance<StableIdManifest>();

            try
            {
                coordinator.Configure(manifest);

                var result = coordinator.ValidateSlot(slotId);

                Assert.That(result.IsValid, Is.False);
                Assert.That(result.FailureStage, Is.EqualTo(SaveSlotValidationStage.StructuralPreflight));
                Assert.That(result.ErrorCode, Is.EqualTo("missing-required-global-record"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void ContinueValidationCacheRefreshesWhenGlobalDeclarationChanges()
        {
            var declarations = new GlobalSaveStateRegistry();
            var registry = new SaveableRegistry(null, declarations);
            var store = new InMemorySaveSlotStore();
            var slotId = new SaveSlotId("declaration-cache-refresh");
            store.Save(CreateSnapshot(Array.Empty<SaveStateRecord>(), slotId));
            var coordinator = new SaveLoadCoordinator(CreateSaveOwner(registry), store);
            var manifest = ScriptableObject.CreateInstance<StableIdManifest>();

            try
            {
                coordinator.Configure(manifest);
                Assert.That(coordinator.HasValidSlot(slotId), Is.True);

                declarations.Register(Required("objective.state", SaveStateTypeKeys.Int32V1));

                Assert.That(coordinator.HasValidSlot(slotId), Is.False);
                Assert.That(
                    coordinator.ValidateSlot(slotId).ErrorCode,
                    Is.EqualTo("missing-required-global-record"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void RestoreRejectsMissingRequiredRecordBeforeAnyOwnerMutates()
        {
            var declarations = new GlobalSaveStateRegistry();
            declarations.Register(Required("first.state", SaveStateTypeKeys.Int32V1));
            declarations.Register(Required("second.state", SaveStateTypeKeys.Int32V1));
            var registry = new SaveableRegistry(null, declarations);
            var first = new TestGlobalOwner("first.state", 1);
            var second = new TestGlobalOwner("second.state", 2);
            registry.RegisterGlobal(first);
            registry.RegisterGlobal(second);
            var snapshot = CreateSnapshot(new[]
            {
                new SaveStateRecord("first.state", SaveStateTypeKeys.Int32V1, 10)
            });

            var exception = Assert.Throws<SaveValidationException>(
                () => CreateSaveOwner(registry).Restore(snapshot));

            AssertError(exception.Report, "missing-required-global-record");
            Assert.That(first.Value, Is.EqualTo(1));
            Assert.That(second.Value, Is.EqualTo(2));
            Assert.That(first.RestoreCount, Is.Zero);
            Assert.That(second.RestoreCount, Is.Zero);
        }

        [Test]
        public void CaptureRejectsMissingRequiredGlobalOwner()
        {
            var registry = CreateRegistryWith(
                Required(HorrorGlobalSaveStateRegistry.InventoryStateKey, SaveStateTypeKeys.InventoryRuntimeV1));

            var exception = Assert.Throws<SaveValidationException>(
                () => CreateSaveOwner(registry).CaptureWithReport(new SaveSlotId("capture-missing-owner")));

            AssertError(exception.Report, "missing-required-global-owner");
        }

        [Test]
        public void CaptureRejectsRequiredOwnerThatCannotProduceARecordWithoutPersistingSlot()
        {
            var registry = CreateRegistryWith(Required("broken.state", SaveStateTypeKeys.Int32V1));
            registry.RegisterGlobal(new NullCaptureOwner("broken.state"));
            var slotId = new SaveSlotId("capture-null-state");
            var store = new InMemorySaveSlotStore();
            var coordinator = new SaveLoadCoordinator(CreateSaveOwner(registry), store);
            var manifest = ScriptableObject.CreateInstance<StableIdManifest>();

            try
            {
                coordinator.Configure(manifest);

                Assert.That(
                    () => coordinator.CaptureAndSave(
                        slotId,
                        new CheckpointModel("checkpoint", "Gameplay", "spawn")),
                    Throws.TypeOf<InvalidOperationException>()
                        .With.Message.Contains("did not capture a valid"));
                Assert.That(store.HasSlot(slotId), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void RequiredSemanticInvalidStateIsRejectedBeforeMutation()
        {
            var registry = CreateRegistryWith(
                Required(HorrorGlobalSaveStateRegistry.InventoryStateKey, SaveStateTypeKeys.InventoryRuntimeV1));
            var inventory = new InventoryRuntimeModel();
            inventory.AddItem("existing.key", true);
            registry.RegisterGlobal(inventory);
            var invalidRecord = new SaveStateRecord(
                HorrorGlobalSaveStateRegistry.InventoryStateKey,
                SaveStateTypeKeys.InventoryRuntimeV1,
                new InventoryState(new[] { "saved.key" }, "missing.key"));

            var exception = Assert.Throws<SaveValidationException>(
                () => CreateSaveOwner(registry).Restore(CreateSnapshot(new[] { invalidRecord })));

            AssertError(exception.Report, "state-semantic-invalid");
            Assert.That(inventory.HasItem("existing.key"), Is.True);
            Assert.That(inventory.HasItem("saved.key"), Is.False);
        }

        [Test]
        public void MigrationMustProvideNewlyRequiredRecordBeforeCurrentPreflight()
        {
            var registry = CreateRegistryWith(
                Required(HorrorGlobalSaveStateRegistry.InventoryStateKey, SaveStateTypeKeys.InventoryRuntimeV1));
            registry.RegisterGlobal(new InventoryRuntimeModel());
            var migrations = new SaveMigrationPipeline();
            migrations.Register(new AddInventoryStateMigration());
            var saveOwner = new SaveGameOwner(registry, migrations, new GameplayEventBus(), null);
            var legacySnapshot = CreateSnapshot(
                Array.Empty<SaveStateRecord>(),
                new SaveSlotId("legacy-global-state"),
                schemaVersion: 0);

            var result = saveOwner.PreflightSlotRestore(legacySnapshot);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Snapshot.SchemaVersion, Is.EqualTo(SaveSchemaVersion.Current));
            Assert.That(
                result.Snapshot.GlobalStates.Single().StateKey,
                Is.EqualTo(HorrorGlobalSaveStateRegistry.InventoryStateKey));
        }

        [Test]
        public void DuplicateGlobalDeclarationIsRejected()
        {
            var declarations = new GlobalSaveStateRegistry();
            declarations.Register(Optional("duplicate.state", SaveStateTypeKeys.Int32V1));

            Assert.That(
                () => declarations.Register(Optional("duplicate.state", SaveStateTypeKeys.Int32V1)),
                Throws.TypeOf<InvalidOperationException>());
        }

        private static SaveableRegistry CreateRegistryWith(params GlobalSaveStateDeclaration[] declarations)
        {
            var globalStates = new GlobalSaveStateRegistry();
            for (var i = 0; i < declarations.Length; i++)
            {
                globalStates.Register(declarations[i]);
            }

            return new SaveableRegistry(null, globalStates);
        }

        private static GlobalSaveStateDeclaration Required(
            string stateKey,
            string stateTypeKey,
            GlobalSaveStateOwnerScope ownerScope = GlobalSaveStateOwnerScope.Context,
            int introducedInSchemaVersion = 1)
        {
            return new GlobalSaveStateDeclaration(
                stateKey,
                stateTypeKey,
                GlobalSaveStateRequirement.Required,
                introducedInSchemaVersion,
                ownerScope,
                GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord);
        }

        private static GlobalSaveStateDeclaration Optional(string stateKey, string stateTypeKey)
        {
            return new GlobalSaveStateDeclaration(
                stateKey,
                stateTypeKey,
                GlobalSaveStateRequirement.Optional,
                introducedInSchemaVersion: 1,
                GlobalSaveStateOwnerScope.Context,
                GlobalSaveStateIntroductionPolicy.NoBackfillRequired);
        }

        private static SaveStateRecord InventoryRecord()
        {
            return new SaveStateRecord(
                HorrorGlobalSaveStateRegistry.InventoryStateKey,
                SaveStateTypeKeys.InventoryRuntimeV1,
                new InventoryState(Array.Empty<string>(), null));
        }

        private static SaveStateRecord PlayerPoseRecord()
        {
            return new SaveStateRecord(
                HorrorGlobalSaveStateRegistry.PlayerPoseStateKey,
                SaveStateTypeKeys.PlayerPoseV1,
                new PlayerPose(Vector3.zero, Quaternion.identity, 0f, PlayerControlState.Normal));
        }

        private static SaveGameOwner CreateSaveOwner(SaveableRegistry registry)
        {
            return new SaveGameOwner(registry, new SaveMigrationPipeline(), new GameplayEventBus(), null);
        }

        private static SaveGameSnapshot CreateSnapshot(
            SaveStateRecord[] globalStates,
            SaveSlotId? slotId = null,
            int schemaVersion = SaveSchemaVersion.Current)
        {
            return new SaveGameSnapshot(
                schemaVersion,
                slotId ?? new SaveSlotId("global-state-test"),
                new CheckpointModel("checkpoint", "Gameplay", "spawn"),
                globalStates,
                Array.Empty<StableSaveStateRecord>());
        }

        private static void AssertDeclaration(
            GlobalSaveStateRegistry registry,
            string stateKey,
            string stateTypeKey,
            GlobalSaveStateRequirement requirement,
            GlobalSaveStateOwnerScope ownerScope)
        {
            Assert.That(registry.TryGet(stateKey, out var declaration), Is.True);
            Assert.That(declaration.StateTypeKey, Is.EqualTo(stateTypeKey));
            Assert.That(declaration.Requirement, Is.EqualTo(requirement));
            Assert.That(declaration.OwnerScope, Is.EqualTo(ownerScope));
            Assert.That(declaration.IntroducedInSchemaVersion, Is.EqualTo(1));
            Assert.That(
                declaration.IntroductionPolicy,
                Is.EqualTo(requirement == GlobalSaveStateRequirement.Required
                    ? GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord
                    : GlobalSaveStateIntroductionPolicy.NoBackfillRequired));
        }

        private static void AssertError(SaveRestoreReport report, string code)
        {
            Assert.That(
                report.Issues.Any(issue =>
                    issue.Severity == SaveRestoreIssueSeverity.Error && issue.Code == code),
                Is.True,
                $"Expected restore error '{code}'.");
        }

        private sealed class TestGlobalOwner : RuntimeStateOwnerBase<int>
        {
            public TestGlobalOwner(string stateKey, int value)
                : base(stateKey)
            {
                Value = value;
            }

            public int Value { get; private set; }
            public int RestoreCount { get; private set; }

            public override int CaptureState()
            {
                return Value;
            }

            public override void RestoreState(int state)
            {
                Value = state;
                RestoreCount++;
            }
        }

        private sealed class NullCaptureOwner : IRuntimeStateOwner
        {
            public NullCaptureOwner(string stateKey)
            {
                StateKey = stateKey;
            }

            public string StateKey { get; }
            public Type StateType => typeof(int);

            public object CaptureState()
            {
                return null;
            }

            public void RestoreState(object state)
            {
            }
        }

        private sealed class AddInventoryStateMigration : ISaveMigrationRule
        {
            public int SourceVersion => 0;
            public int TargetVersion => SaveSchemaVersion.Current;

            public SaveGameSnapshot Migrate(SaveGameSnapshot snapshot)
            {
                return new SaveGameSnapshot(
                    TargetVersion,
                    snapshot.SlotId,
                    snapshot.Checkpoint,
                    new[] { InventoryRecord() },
                    snapshot.StableStates,
                    snapshot.SavedAtUtcTicks);
            }
        }
    }
}
