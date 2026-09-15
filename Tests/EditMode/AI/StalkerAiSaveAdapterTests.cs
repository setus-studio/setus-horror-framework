using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Setus.HorrorFramework.AI.Memory;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.AI.Perception;
using Setus.HorrorFramework.AI.States;
using Setus.HorrorFramework.AI.Tuning;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.SaveProgression.Migration;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Setus.HorrorFramework.Tests.EditMode.AI
{
    public sealed class StalkerAiSaveAdapterTests
    {
        private const string StalkerStableId = "m8.test.stalker-save";

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
        public void InitializedControllerRegistersCapturesAndRestores()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var gameObject = CreateInactiveStalker(StalkerStableId, profile, out var controller, out var adapter);
            try
            {
                Initialize(controller, adapter);

                var context = HorrorGameContext.Active;
                var captured = (StalkerAiRuntimeState)adapter.CaptureState();
                adapter.RestoreState(captured);
                var savedRecord = context.Saveables.CaptureStableStates().Single();

                Assert.That(controller.IsInitialized, Is.True);
                Assert.That(context.Saveables.StableOwners.Count, Is.EqualTo(1));
                Assert.That(savedRecord.StableId, Is.EqualTo(StalkerStableId));
                Assert.That(savedRecord.State, Is.TypeOf<StalkerAiRuntimeState>());
                Assert.That(adapter.ValidateRestoreState(captured).IsValid, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void LaterApplyFailureRollsBackExactChaseWithoutRestoreCompleted()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var gameObject = CreateInactiveStalker(StalkerStableId, profile, out var controller, out var adapter);
            var manifest = ScriptableObject.CreateInstance<StableIdManifest>();
            try
            {
                Initialize(controller, adapter);
                var registry = new SaveableRegistry();
                registry.RegisterStable(adapter);
                var failing = new FailingAiOwner(controller);
                registry.RegisterStable(failing);
                manifest.ReplaceEntries(new[]
                {
                    new StableIdManifestEntry(StalkerStableId, StableIdManifestEntryKind.Required,
                        SaveRestorePolicy.ResumeDeterministicPhase),
                    new StableIdManifestEntry(failing.StableId, StableIdManifestEntryKind.Required)
                });
                var events = new GameplayEventBus();
                var completed = 0;
                events.Subscribe<SaveRestoreCompleted>(_ => completed++);
                var saves = new SaveGameOwner(registry, new SaveMigrationPipeline(), events, null);
                var snapshot = saves.Capture(new SaveSlotId("m8-rollback"), manifest);
                var model = (StalkerAiRuntimeModel)typeof(StalkerAiController)
                    .GetField("model", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                var sight = new StalkerPerceptionInput(
                    new StalkerSightResult(StalkerSightReason.TargetConfirmed, new Vector3(4f, 0f, 7f), null), false);
                model.Tick(0.1f, sight);
                model.Tick(profile.ChaseAnticipationDuration, sight);
                model.Tick(0.1f, new StalkerPerceptionInput(default, false));
                var before = controller.CaptureState();
                Assert.That(before.State, Is.EqualTo(StalkerAiState.Chasing));

                var failure = Assert.Throws<SaveValidationException>(() => saves.Restore(snapshot, manifest));

                Assert.That(failure.Report.Issues.Any(issue => issue.Code == "restore-apply-failed"), Is.True);
                Assert.That(failure.Report.Issues.Any(issue => issue.Code == "restore-rollback-failed"), Is.False);
                Assert.That(failing.SawMutatedAi, Is.True);
                Assert.That(JsonUtility.ToJson(controller.CaptureState()), Is.EqualTo(JsonUtility.ToJson(before)));
                Assert.That(completed, Is.Zero);
                controller.RestoreState(before);
                Assert.That(controller.State, Is.EqualTo(StalkerAiState.Searching));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(manifest);
                Object.DestroyImmediate(profile);
            }
        }

        private sealed class FailingAiOwner : ISaveableStateOwner
        {
            private readonly StalkerAiController controller;
            private bool failed;
            public FailingAiOwner(StalkerAiController controller) { this.controller = controller; }
            public bool SawMutatedAi { get; private set; }
            public string StableId => "z.m8.failure";
            public string StateKey => StalkerAiRuntimeModel.StateKeyValue;
            public Type StateType => typeof(StalkerAiRuntimeState);
            public bool HasActiveState => false;
            public SaveRestorePolicy ActiveRestorePolicy => SaveRestorePolicy.ResumeDeterministicPhase;
            public object CaptureState() => new StalkerAiRuntimeState(StalkerAiState.Patrol, 0f, Vector3.zero, 0, 0f, 0f);
            public void RestoreState(object state)
            {
                if (failed) return;
                failed = true;
                SawMutatedAi = controller.State == StalkerAiState.Patrol;
                throw new InvalidOperationException("Forced later owner failure.");
            }
        }

        [Test]
        public void MissingProfileDoesNotRegisterSaveOwner()
        {
            var gameObject = CreateInactiveStalker(StalkerStableId, null, out var controller, out var adapter);
            try
            {
                LogAssert.Expect(
                    LogType.Error,
                    "Stalker AI 'M8 Test Stalker' requires an assigned StalkerAiTuningProfile. " +
                    "Run Setus > Horror Framework > AI > Apply M8 Enemy AI Foundation, then verify the controller reference.");

                Initialize(controller, adapter);

                Assert.That(controller.IsInitialized, Is.False);
                Assert.That(HorrorGameContext.Ensure().Saveables.StableOwners, Is.Empty);
                Assert.Throws<InvalidOperationException>(() => adapter.CaptureState());
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void InvalidStableIdDoesNotRegisterSaveOwner()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var gameObject = CreateInactiveStalker(string.Empty, profile, out var controller, out var adapter);
            try
            {
                LogAssert.Expect(
                    LogType.Error,
                    "Stalker AI 'M8 Test Stalker' requires an authored, non-empty StableId.");

                Initialize(controller, adapter);

                Assert.That(controller.IsInitialized, Is.False);
                Assert.That(HorrorGameContext.Ensure().Saveables.StableOwners, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void UninitializedRequiredOwnerCannotCreateFallbackSaveRecord()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var manifest = ScriptableObject.CreateInstance<StableIdManifest>();
            var gameObject = CreateInactiveStalker(StalkerStableId, profile, out _, out var adapter);
            try
            {
                InvokeLifecycle(adapter, "Awake");
                InvokeLifecycle(adapter, "OnEnable");
                manifest.ReplaceEntries(new[]
                {
                    new StableIdManifestEntry(StalkerStableId, StableIdManifestEntryKind.Required)
                });

                var context = HorrorGameContext.Ensure();
                var exception = Assert.Throws<SaveValidationException>(
                    () => context.Saves.Capture(new SaveSlotId("m8-uninitialized"), manifest));

                Assert.That(context.Saveables.StableOwners, Is.Empty);
                Assert.That(context.Saveables.CaptureStableStates(), Is.Empty);
                Assert.Throws<InvalidOperationException>(() => adapter.CaptureState());
                Assert.That(
                    exception.Report.Issues.Any(issue =>
                        issue.Code == "MissingRequiredId" && issue.StableId == StalkerStableId),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(manifest);
            }
        }

        private static GameObject CreateInactiveStalker(
            string id,
            StalkerAiTuningProfile profile,
            out StalkerAiController controller,
            out StalkerAiSaveAdapter adapter)
        {
            var gameObject = new GameObject("M8 Test Stalker");
            gameObject.SetActive(false);
            var agent = gameObject.AddComponent<NavMeshAgent>();
            var stableId = gameObject.AddComponent<StableId>();
            stableId.Assign(id);
            controller = gameObject.AddComponent<StalkerAiController>();
            adapter = gameObject.AddComponent<StalkerAiSaveAdapter>();

            SetField(controller, "tuningProfile", profile);
            SetField(controller, "stableId", stableId);
            SetField(controller, "agent", agent);
            SetField(adapter, "stalker", controller);
            SetField(adapter, "stableId", stableId);
            return gameObject;
        }

        private static void Initialize(StalkerAiController controller, StalkerAiSaveAdapter adapter)
        {
            InvokeLifecycle(controller, "Awake");
            InvokeLifecycle(adapter, "Awake");
            InvokeLifecycle(adapter, "OnEnable");
            InvokeLifecycle(adapter, "Start");
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing test field: {fieldName}");
            field.SetValue(target, value);
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing lifecycle method: {methodName}");
            method.Invoke(target, null);
        }
    }
}
