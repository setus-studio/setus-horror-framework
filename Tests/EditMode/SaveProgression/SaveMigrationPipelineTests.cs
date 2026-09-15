using System;
using System.Collections.Generic;
using NUnit.Framework;
using Setus.HorrorFramework.SaveProgression.Migration;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;

namespace Setus.HorrorFramework.Tests.EditMode.SaveProgression
{
    public sealed class SaveMigrationPipelineTests
    {
        [Test]
        public void ValidLinearChainMigratesFromVersionOneToVersionThree()
        {
            var execution = new List<string>();
            var pipeline = new SaveMigrationPipeline();
            pipeline.Register(new TestMigrationRule(1, 2, execution));
            pipeline.Register(new TestMigrationRule(2, 3, execution));

            var result = pipeline.MigrateToVersion(CreateSnapshot(1), 3);

            Assert.That(result.SchemaVersion, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] { "1->2", "2->3" }, execution);
        }

        [Test]
        public void DuplicateSourceVersionIsRejectedDuringRegistration()
        {
            var pipeline = new SaveMigrationPipeline();
            pipeline.Register(new TestMigrationRule(1, 2));

            Assert.That(
                () => pipeline.Register(new TestMigrationRule(1, 3)),
                Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void RuleReturningVersionOtherThanDeclaredTargetIsRejected()
        {
            var pipeline = new SaveMigrationPipeline();
            pipeline.Register(new TestMigrationRule(1, 2, resultVersion: 3));
            pipeline.Register(new TestMigrationRule(2, 3));

            Assert.That(
                () => pipeline.MigrateToVersion(CreateSnapshot(1), 3),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("declared target"));
        }

        [Test]
        public void SameSourceAndTargetIsRejectedDuringRegistration()
        {
            Assert.That(
                () => new SaveMigrationPipeline().Register(new TestMigrationRule(1, 1)),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void BackwardMigrationIsRejectedDuringRegistration()
        {
            Assert.That(
                () => new SaveMigrationPipeline().Register(new TestMigrationRule(2, 1)),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void MissingMigrationEdgeFailsClearly()
        {
            var pipeline = new SaveMigrationPipeline();
            pipeline.Register(new TestMigrationRule(1, 2));

            Assert.That(
                () => pipeline.MigrateToVersion(CreateSnapshot(1), 3),
                Throws.TypeOf<UnsupportedSaveVersionException>());
        }

        [Test]
        public void RegistrationOrderDoesNotChangeLinearMigrationResult()
        {
            var firstExecution = new List<string>();
            var firstPipeline = new SaveMigrationPipeline();
            firstPipeline.Register(new TestMigrationRule(1, 2, firstExecution));
            firstPipeline.Register(new TestMigrationRule(2, 3, firstExecution));

            var secondExecution = new List<string>();
            var secondPipeline = new SaveMigrationPipeline();
            secondPipeline.Register(new TestMigrationRule(2, 3, secondExecution));
            secondPipeline.Register(new TestMigrationRule(1, 2, secondExecution));

            var firstResult = firstPipeline.MigrateToVersion(CreateSnapshot(1), 3);
            var secondResult = secondPipeline.MigrateToVersion(CreateSnapshot(1), 3);

            Assert.That(firstResult.SchemaVersion, Is.EqualTo(secondResult.SchemaVersion));
            CollectionAssert.AreEqual(new[] { "1->2", "2->3" }, firstExecution);
            CollectionAssert.AreEqual(firstExecution, secondExecution);
        }

        [Test]
        public void VersionZeroRuleStillMigratesToCurrentSchema()
        {
            var pipeline = new SaveMigrationPipeline();
            pipeline.Register(new TestMigrationRule(0, SaveSchemaVersion.Current));

            var result = pipeline.MigrateToCurrent(CreateSnapshot(0));

            Assert.That(result.SchemaVersion, Is.EqualTo(SaveSchemaVersion.Current));
        }

        [Test]
        public void SchemaFourMigrationScopesLegacyStableStateToCheckpointScene()
        {
            var pipeline = new SaveMigrationPipeline();
            HorrorSaveMigrations.RegisterDefaults(pipeline);
            var snapshot = new SaveGameSnapshot(
                4,
                new SaveSlotId("schema-four-scene-scope"),
                new Setus.HorrorFramework.SaveProgression.Checkpoints.CheckpointModel(
                    "checkpoint-a",
                    "SceneA",
                    "spawn-a"),
                Array.Empty<SaveStateRecord>(),
                new[]
                {
                    new StableSaveStateRecord(
                        "door.a",
                        "door.state",
                        Setus.HorrorFramework.SaveProgression.Serialization.SaveStateTypeKeys.Int32V1,
                        1,
                        false,
                        SaveRestorePolicy.ResumeDeterministicPhase)
                });

            var migrated = pipeline.MigrateToCurrent(snapshot);

            Assert.That(migrated.SchemaVersion, Is.EqualTo(SaveSchemaVersion.Current));
            Assert.That(migrated.StableStates[0].SceneId, Is.EqualTo("SceneA"));
            Assert.That(migrated.VisitedSceneIds, Is.EquivalentTo(new[] { "SceneA" }));
        }

        private static SaveGameSnapshot CreateSnapshot(int schemaVersion)
        {
            return new SaveGameSnapshot(
                schemaVersion,
                new SaveSlotId("migration-pipeline-test"),
                null,
                Array.Empty<SaveStateRecord>(),
                Array.Empty<StableSaveStateRecord>());
        }

        private sealed class TestMigrationRule : ISaveMigrationRule
        {
            private readonly ICollection<string> execution;
            private readonly int? resultVersion;

            public TestMigrationRule(
                int sourceVersion,
                int targetVersion,
                ICollection<string> execution = null,
                int? resultVersion = null)
            {
                SourceVersion = sourceVersion;
                TargetVersion = targetVersion;
                this.execution = execution;
                this.resultVersion = resultVersion;
            }

            public int SourceVersion { get; }
            public int TargetVersion { get; }

            public SaveGameSnapshot Migrate(SaveGameSnapshot snapshot)
            {
                execution?.Add($"{SourceVersion}->{TargetVersion}");
                return new SaveGameSnapshot(
                    resultVersion ?? TargetVersion,
                    snapshot.SlotId,
                    snapshot.Checkpoint,
                    snapshot.GlobalStates,
                    snapshot.StableStates,
                    snapshot.SavedAtUtcTicks);
            }
        }
    }
}
