using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Atmosphere.Tension;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.SaveProgression.Migration;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.Serialization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Setus.HorrorFramework.Tests.EditMode.Atmosphere
{
    public sealed class ScareRuntimeModelTests
    {
        [Test]
        public void TriggeredScareBecomesConsumedInCaptureAndCannotReplayAfterRestore()
        {
            var definition = CreateDefinition("scare.hallway", 0.75f);
            try
            {
                var events = new GameplayEventBus();
                var tension = new TensionRuntimeModel(events);
                var source = new ScareRuntimeModel(events, tension);
                source.Configure(new[] { definition });

                Assert.That(source.TryTrigger(definition.ScareId), Is.True);
                Assert.That(source.TryGetState(definition.ScareId, out var playing), Is.True);
                Assert.That(playing, Is.EqualTo(ScareLifecycleState.Playing));
                Assert.That(tension.CurrentIntensity, Is.EqualTo(0.75f).Within(0.001f));

                var captured = source.CaptureState();
                Assert.That(captured.Scares[0].State, Is.EqualTo(ScareLifecycleState.Consumed));
                Assert.That(tension.CaptureState().Sources, Is.Empty);

                var restoredTension = new TensionRuntimeModel(events);
                var restored = new ScareRuntimeModel(events, restoredTension);
                restored.Configure(new[] { definition });
                restored.RestoreState(captured);

                Assert.That(restored.TryGetState(definition.ScareId, out var consumed), Is.True);
                Assert.That(consumed, Is.EqualTo(ScareLifecycleState.Consumed));
                Assert.That(restored.TryTrigger(definition.ScareId), Is.False);
                Assert.That(restoredTension.CurrentIntensity, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void InvalidOrDuplicateScareStateFailsSemanticValidation()
        {
            var definition = CreateDefinition("scare.hallway", 0.5f);
            try
            {
                var model = new ScareRuntimeModel(new GameplayEventBus(), new TensionRuntimeModel());
                model.Configure(new[] { definition });

                var duplicate = new ScareRuntimeState(new[]
                {
                    new ScareStateRecord(definition.ScareId, ScareLifecycleState.Consumed),
                    new ScareStateRecord(definition.ScareId, ScareLifecycleState.Armed)
                });
                var invalid = new ScareRuntimeState(new[]
                {
                    new ScareStateRecord(definition.ScareId, (ScareLifecycleState)999)
                });

                Assert.That(model.ValidateRestoreState(duplicate).IsValid, Is.False);
                Assert.That(model.ValidateRestoreState(invalid).IsValid, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ConfigureSceneBAndReturnToSceneAPreservesConsumedAndArmedHistory()
        {
            var scareA = CreateDefinition("scare.scene-a", 0.5f);
            var scareB = CreateDefinition("scare.scene-b", 0.6f);
            try
            {
                var model = new ScareRuntimeModel(new GameplayEventBus(), new TensionRuntimeModel());
                model.Configure(new[] { scareA });
                Assert.That(model.TryTrigger(scareA.ScareId), Is.True);
                Assert.That(model.Complete(scareA.ScareId), Is.True);

                model.Configure(new[] { scareB });
                Assert.That(model.TryGetState(scareA.ScareId, out var consumed), Is.True);
                Assert.That(consumed, Is.EqualTo(ScareLifecycleState.Consumed));
                Assert.That(model.TryTrigger(scareA.ScareId), Is.False);

                model.Configure(new[] { scareA });
                Assert.That(model.TryGetState(scareA.ScareId, out consumed), Is.True);
                Assert.That(consumed, Is.EqualTo(ScareLifecycleState.Consumed));

                var armed = new ScareRuntimeModel(new GameplayEventBus(), new TensionRuntimeModel());
                armed.Configure(new[] { scareA });
                armed.Configure(new[] { scareB });
                armed.Configure(new[] { scareA });
                Assert.That(armed.TryGetState(scareA.ScareId, out var armedState), Is.True);
                Assert.That(armedState, Is.EqualTo(ScareLifecycleState.Armed));
                Assert.That(armed.TryTrigger(scareA.ScareId), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(scareA);
                Object.DestroyImmediate(scareB);
            }
        }

        [Test]
        public void SaveRestoreAcrossSceneCatalogsPreservesUnregisteredScareHistory()
        {
            var scareA = CreateDefinition("scare.restore-a", 0.5f);
            var scareB = CreateDefinition("scare.restore-b", 0.6f);
            try
            {
                var source = new ScareRuntimeModel(new GameplayEventBus(), new TensionRuntimeModel());
                source.Configure(new[] { scareA });
                Assert.That(source.TryTrigger(scareA.ScareId), Is.True);
                Assert.That(source.Complete(scareA.ScareId), Is.True);
                source.Configure(new[] { scareB });
                var captured = source.CaptureState();

                var restored = new ScareRuntimeModel(new GameplayEventBus(), new TensionRuntimeModel());
                restored.Configure(new[] { scareB });
                Assert.That(restored.ValidateRestoreState(captured).IsValid, Is.True);
                restored.RestoreState(captured);
                Assert.That(restored.TryTrigger(scareA.ScareId), Is.False);

                restored.Configure(new[] { scareA });
                Assert.That(restored.TryGetState(scareA.ScareId, out var state), Is.True);
                Assert.That(state, Is.EqualTo(ScareLifecycleState.Consumed));
                Assert.That(restored.TryTrigger(scareA.ScareId), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(scareA);
                Object.DestroyImmediate(scareB);
            }
        }

        [Test]
        public void SceneRegistrationsReferenceCountAndRejectDifferentAssetsWithTheSameId()
        {
            var shared = CreateDefinition("scare.shared", 0.5f);
            var incompatible = CreateDefinition("scare.shared", 0.8f);
            var model = new ScareRuntimeModel(new GameplayEventBus(), new TensionRuntimeModel());
            var first = model.RegisterDefinitions(new[] { shared });
            var second = model.RegisterDefinitions(new[] { shared });
            try
            {
                Assert.Throws<System.InvalidOperationException>(() =>
                    model.RegisterDefinitions(new[] { incompatible }));

                first.Dispose();
                Assert.That(model.TryTrigger(shared.ScareId), Is.True);
                second.Dispose();

                Assert.That(model.TryGetState(shared.ScareId, out var state), Is.True);
                Assert.That(state, Is.EqualTo(ScareLifecycleState.Consumed));
                Assert.That(model.TryTrigger(shared.ScareId), Is.False);

                using (model.RegisterDefinitions(new[] { shared }))
                {
                    Assert.That(model.TryTrigger(shared.ScareId), Is.False);
                }
            }
            finally
            {
                first.Dispose();
                second.Dispose();
                Object.DestroyImmediate(shared);
                Object.DestroyImmediate(incompatible);
            }
        }

        [Test]
        public void UnregisteredOrUnknownScareCannotTrigger()
        {
            var definition = CreateDefinition("scare.unregistered", 0.5f);
            var model = new ScareRuntimeModel(new GameplayEventBus(), new TensionRuntimeModel());
            var registration = model.RegisterDefinitions(new[] { definition });
            try
            {
                registration.Dispose();

                Assert.That(model.TryGetState(definition.ScareId, out var armed), Is.True);
                Assert.That(armed, Is.EqualTo(ScareLifecycleState.Armed));
                Assert.That(model.TryTrigger(definition.ScareId), Is.False);
                Assert.That(model.TryTrigger("scare.never-registered"), Is.False);

                using (model.RegisterDefinitions(new[] { definition }))
                {
                    Assert.That(model.TryTrigger(definition.ScareId), Is.True);
                }
            }
            finally
            {
                registration.Dispose();
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void SchemaTwoMigrationAddsRequiredAtmosphereRecords()
        {
            var pipeline = new SaveMigrationPipeline();
            HorrorSaveMigrations.RegisterDefaults(pipeline);
            var schemaTwo = new SaveGameSnapshot(
                2,
                new SaveSlotId("m7-migration"),
                null,
                System.Array.Empty<SaveStateRecord>(),
                System.Array.Empty<StableSaveStateRecord>());

            var migrated = pipeline.MigrateToCurrent(schemaTwo);

            Assert.That(migrated.SchemaVersion, Is.EqualTo(SaveSchemaVersion.Current));
            var scare = migrated.GlobalStates.Single(state =>
                state.StateKey == HorrorGlobalSaveStateRegistry.AtmosphereScareStateKey);
            var tension = migrated.GlobalStates.Single(state =>
                state.StateKey == HorrorGlobalSaveStateRegistry.AtmosphereTensionStateKey);
            Assert.That(scare.StateTypeKey, Is.EqualTo(SaveStateTypeKeys.AtmosphereScareRuntimeV1));
            Assert.That(scare.State, Is.TypeOf<ScareRuntimeState>());
            Assert.That(tension.StateTypeKey, Is.EqualTo(SaveStateTypeKeys.AtmosphereTensionRuntimeV1));
            Assert.That(tension.State, Is.TypeOf<TensionRuntimeState>());
        }

        [Test]
        public void AtmosphereStateCodecsPersistAndRestoreTheirRecords()
        {
            var registry = HorrorSaveStateTypeRegistry.CreateDefault();
            var scareCodec = registry.Resolve(SaveStateTypeKeys.AtmosphereScareRuntimeV1);
            var tensionCodec = registry.Resolve(SaveStateTypeKeys.AtmosphereTensionRuntimeV1);

            var scarePayload = scareCodec.Serialize(new ScareRuntimeState(new[]
            {
                new ScareStateRecord("scare.hallway", ScareLifecycleState.Consumed)
            }));
            var tensionPayload = tensionCodec.Serialize(new TensionRuntimeState(new[]
            {
                new TensionSourceState("ambient.hallway", 0.6f)
            }));

            var restoredScares = (ScareRuntimeState)scareCodec.Deserialize(scarePayload);
            var restoredTension = (TensionRuntimeState)tensionCodec.Deserialize(tensionPayload);

            StringAssert.Contains("\"scares\"", scarePayload);
            StringAssert.Contains("\"sources\"", tensionPayload);
            Assert.That(restoredScares.Scares.Single().ScareId, Is.EqualTo("scare.hallway"));
            Assert.That(restoredScares.Scares.Single().State, Is.EqualTo(ScareLifecycleState.Consumed));
            Assert.That(restoredTension.Sources.Single().SourceId, Is.EqualTo("ambient.hallway"));
            Assert.That(restoredTension.Sources.Single().Intensity, Is.EqualTo(0.6f).Within(0.001f));
        }

        [Test]
        public void InterruptConsumesActiveScareOnceClearsTensionAndPreventsRetrigger()
        {
            var definition = CreateDefinition("scare.interrupted", 0.65f);
            try
            {
                var events = new GameplayEventBus();
                var tension = new TensionRuntimeModel(events);
                var model = new ScareRuntimeModel(events, tension);
                model.Configure(new[] { definition });
                var consumedEvents = 0;
                using (events.Subscribe<ScareStateChanged>(changed =>
                       {
                           if (changed.ScareId == definition.ScareId &&
                               changed.State == ScareLifecycleState.Consumed)
                           {
                               consumedEvents++;
                           }
                       }))
                {
                    Assert.That(model.TryTrigger(definition.ScareId), Is.True);
                    Assert.That(model.Interrupt(definition.ScareId), Is.True);
                    Assert.That(model.Interrupt(definition.ScareId), Is.False);
                }

                Assert.That(model.TryGetState(definition.ScareId, out var state), Is.True);
                Assert.That(state, Is.EqualTo(ScareLifecycleState.Consumed));
                Assert.That(model.CaptureState().Scares.Single().State, Is.EqualTo(ScareLifecycleState.Consumed));
                Assert.That(tension.CurrentIntensity, Is.Zero);
                Assert.That(consumedEvents, Is.EqualTo(1));
                Assert.That(model.TryTrigger(definition.ScareId), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void RestoreClearsOnlyScareOwnedTransientTensionSource()
        {
            var definition = CreateDefinition("scare.restore-ownership", 0.9f);
            try
            {
                var events = new GameplayEventBus();
                var tension = new TensionRuntimeModel(events);
                var model = new ScareRuntimeModel(events, tension);
                model.Configure(new[] { definition });
                tension.SetSourceIntensity("objective:pressure", 0.35f);
                tension.SetSourceIntensity("debug:manual", 0.6f);

                Assert.That(model.TryTrigger(definition.ScareId), Is.True);
                Assert.That(tension.CurrentIntensity, Is.EqualTo(0.9f).Within(0.001f));

                model.RestoreState(new ScareRuntimeState(new[]
                {
                    new ScareStateRecord(definition.ScareId, ScareLifecycleState.Consumed)
                }));

                var restoredTension = tension.CaptureState();
                Assert.That(restoredTension.Sources.Count, Is.EqualTo(2));
                Assert.That(
                    restoredTension.Sources.Single(source => source.SourceId == "objective:pressure").Intensity,
                    Is.EqualTo(0.35f).Within(0.001f));
                Assert.That(
                    restoredTension.Sources.Single(source => source.SourceId == "debug:manual").Intensity,
                    Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(tension.CurrentIntensity, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(model.TryGetState(definition.ScareId, out var state), Is.True);
                Assert.That(state, Is.EqualTo(ScareLifecycleState.Consumed));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void FailedRestoreRollbackPreservesPlayingScareAndTransientTensionExactly()
        {
            const string failingStateKey = "zz.test.restore-failure";
            var definition = CreateDefinition("scare.rollback", 0.9f);
            try
            {
                var events = new GameplayEventBus();
                var tension = new TensionRuntimeModel(events);
                var scares = new ScareRuntimeModel(events, tension);
                var failingOwner = new FailingGlobalOwner(failingStateKey, 1);
                scares.Configure(new[] { definition });
                tension.SetSourceIntensity("objective:pressure", 0.35f);
                tension.SetSourceIntensity("debug:manual", 0.6f);
                Assert.That(scares.TryTrigger(definition.ScareId), Is.True);
                Assert.That(scares.TryGetState(definition.ScareId, out var playing), Is.True);
                Assert.That(playing, Is.EqualTo(ScareLifecycleState.Playing));
                Assert.That(tension.CurrentIntensity, Is.EqualTo(0.9f).Within(0.001f));

                var declarations = new GlobalSaveStateRegistry();
                declarations.Register(new GlobalSaveStateDeclaration(
                    HorrorGlobalSaveStateRegistry.AtmosphereScareStateKey,
                    SaveStateTypeKeys.AtmosphereScareRuntimeV1,
                    GlobalSaveStateRequirement.Required,
                    SaveSchemaVersion.Current,
                    GlobalSaveStateOwnerScope.Context,
                    GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));
                declarations.Register(new GlobalSaveStateDeclaration(
                    HorrorGlobalSaveStateRegistry.AtmosphereTensionStateKey,
                    SaveStateTypeKeys.AtmosphereTensionRuntimeV1,
                    GlobalSaveStateRequirement.Required,
                    SaveSchemaVersion.Current,
                    GlobalSaveStateOwnerScope.Context,
                    GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));
                declarations.Register(new GlobalSaveStateDeclaration(
                    failingStateKey,
                    SaveStateTypeKeys.Int32V1,
                    GlobalSaveStateRequirement.Required,
                    SaveSchemaVersion.Current,
                    GlobalSaveStateOwnerScope.Context,
                    GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));

                var registry = new SaveableRegistry(null, declarations);
                registry.RegisterGlobal(scares);
                registry.RegisterGlobal(tension);
                registry.RegisterGlobal(failingOwner);
                var saveOwner = new SaveGameOwner(registry, new SaveMigrationPipeline(), events, null);
                var snapshot = new SaveGameSnapshot(
                    SaveSchemaVersion.Current,
                    new SaveSlotId("m7-f5-rollback"),
                    null,
                    new[]
                    {
                        new SaveStateRecord(
                            HorrorGlobalSaveStateRegistry.AtmosphereScareStateKey,
                            SaveStateTypeKeys.AtmosphereScareRuntimeV1,
                            new ScareRuntimeState(new[]
                            {
                                new ScareStateRecord(definition.ScareId, ScareLifecycleState.Consumed)
                            })),
                        new SaveStateRecord(
                            HorrorGlobalSaveStateRegistry.AtmosphereTensionStateKey,
                            SaveStateTypeKeys.AtmosphereTensionRuntimeV1,
                            new TensionRuntimeState(new[]
                            {
                                new TensionSourceState("objective:pressure", 0.8f),
                                new TensionSourceState("narrative:pressure", 0.45f)
                            })),
                        new SaveStateRecord(failingStateKey, SaveStateTypeKeys.Int32V1, 99)
                    },
                    System.Array.Empty<StableSaveStateRecord>());

                var consumedEvents = 0;
                var armedEvents = 0;
                var restoredEvents = 0;
                var tensionEvents = 0;
                var restoreCompletedEvents = 0;
                using (events.Subscribe<ScareStateChanged>(changed =>
                       {
                           if (changed.State == ScareLifecycleState.Consumed)
                           {
                               consumedEvents++;
                           }
                           else if (changed.State == ScareLifecycleState.Armed)
                           {
                               armedEvents++;
                           }
                           else if (changed.State == ScareLifecycleState.Restored)
                           {
                               restoredEvents++;
                           }
                       }))
                using (events.Subscribe<TensionChanged>(_ => tensionEvents++))
                using (events.Subscribe<SaveRestoreCompleted>(_ => restoreCompletedEvents++))
                {
                    var exception = Assert.Throws<SaveValidationException>(() => saveOwner.Restore(snapshot));
                    Assert.That(
                        exception.Report.Issues.Any(issue => issue.Code == "restore-apply-failed"),
                        Is.True);
                }

                var rolledBackTension = tension.CaptureState();
                Assert.That(rolledBackTension.Sources.Count, Is.EqualTo(2));
                Assert.That(
                    rolledBackTension.Sources.Single(source => source.SourceId == "objective:pressure").Intensity,
                    Is.EqualTo(0.35f).Within(0.001f));
                Assert.That(
                    rolledBackTension.Sources.Single(source => source.SourceId == "debug:manual").Intensity,
                    Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(scares.TryGetState(definition.ScareId, out var rolledBackScare), Is.True);
                Assert.That(rolledBackScare, Is.EqualTo(ScareLifecycleState.Playing));
                Assert.That(tension.CurrentIntensity, Is.EqualTo(0.9f).Within(0.001f));
                Assert.That(tension.ClearSource("scare:" + definition.ScareId), Is.True);
                Assert.That(tension.CurrentIntensity, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(failingOwner.Value, Is.EqualTo(1));
                Assert.That(consumedEvents, Is.EqualTo(1));
                Assert.That(armedEvents, Is.Zero);
                Assert.That(restoredEvents, Is.EqualTo(1));
                Assert.That(tensionEvents, Is.EqualTo(2));
                Assert.That(restoreCompletedEvents, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        private static ScareDefinition CreateDefinition(string scareId, float tension)
        {
            var definition = ScriptableObject.CreateInstance<ScareDefinition>();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("scareId").stringValue = scareId;
            serialized.FindProperty("tensionIntensity").floatValue = tension;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private sealed class FailingGlobalOwner : RuntimeStateOwnerBase<int>
        {
            public FailingGlobalOwner(string stateKey, int value)
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
                if (state == 99)
                {
                    throw new System.InvalidOperationException("Simulated restore apply failure.");
                }
            }
        }
    }
}
