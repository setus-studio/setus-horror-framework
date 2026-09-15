using System;
using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.AI.Memory;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.AI.Tuning;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Tests.EditMode.Authoring
{
    public sealed class FrameworkSceneValidatorTests
    {
        private Scene scene;
        private StalkerAiTuningProfile profile;
        private StableIdManifest manifest;
        private ScareDefinition transientScareDefinition;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            manifest = ScriptableObject.CreateInstance<StableIdManifest>();
        }

        [TearDown]
        public void TearDown()
        {
            if (profile != null)
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }

            if (manifest != null)
            {
                UnityEngine.Object.DestroyImmediate(manifest);
            }

            if (transientScareDefinition != null)
            {
                UnityEngine.Object.DestroyImmediate(transientScareDefinition);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void DuplicateStableIdIsRejected()
        {
            CreateStableObject("First", "duplicate.id");
            CreateStableObject("Second", "duplicate.id");

            var result = FrameworkSceneValidator.ValidateScene(scene, null);

            Assert.IsTrue(result.Issues.Any(issue => issue.Code == "stable-id.DuplicateId"));
            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void ActiveSceneStableIdValidationIgnoresDuplicateFromAdditiveScene()
        {
            const string testRoot = "Assets/Game/__SetusHorrorFrameworkTests/M9StableIdValidator";
            const string activeScenePath = testRoot + "/ActiveScene.unity";
            AuthoringTestAssetCleanup.EnsureFolder(testRoot);

            try
            {
                CreateStableObject("ActiveSceneObject", "scene-scoped.id");
                Assert.IsTrue(EditorSceneManager.SaveScene(scene, activeScenePath));
                var additiveScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
                SceneManager.SetActiveScene(additiveScene);
                var additiveObject = new GameObject("AdditiveSceneObject");
                additiveObject.AddComponent<StableId>().Assign("scene-scoped.id");
                SceneManager.SetActiveScene(scene);

                var result = StableIdManifestSceneValidator.ValidateActiveScene(null);

                Assert.AreEqual(additiveScene, additiveObject.scene);
                Assert.IsFalse(result.HasErrors);
                Assert.IsFalse(result.Duplicates.Any());
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AuthoringTestAssetCleanup.Delete(testRoot);
            }
        }

        [Test]
        public void ActiveSceneStableIdValidationRejectsDuplicateWithinActiveScene()
        {
            CreateStableObject("FirstActiveObject", "active-duplicate.id");
            CreateStableObject("SecondActiveObject", "active-duplicate.id");

            var result = StableIdManifestSceneValidator.ValidateActiveScene(null);

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Duplicates.Any(issue => issue.StableId == "active-duplicate.id"));
        }

        [Test]
        public void MissingRequiredManifestIdIsRejected()
        {
            manifest.ReplaceEntries(new[]
            {
                new StableIdManifestEntry(
                    "required.missing",
                    StableIdManifestEntryKind.Required,
                    SaveRestorePolicy.ResumeDeterministicPhase)
            });

            var result = FrameworkSceneValidator.ValidateScene(scene, manifest);

            Assert.IsTrue(result.Issues.Any(issue => issue.Code == "stable-id.MissingRequiredId"));
            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void RequiredManifestIdFromAnotherSceneIsNotRequiredInActiveScene()
        {
            manifest.ReplaceEntries(new[]
            {
                new StableIdManifestEntry(
                    "required.in.other-scene",
                    StableIdManifestEntryKind.Required,
                    SaveRestorePolicy.ResumeDeterministicPhase,
                    sceneId: "OtherScene")
            });

            var result = FrameworkSceneValidator.ValidateScene(scene, manifest);

            Assert.IsFalse(result.Issues.Any(issue => issue.Code == "stable-id.MissingRequiredId"));
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void CanonicalM8AllLayersMasksAreAccepted()
        {
            var controller = CreateValidController("ai.canonical-masks", includeSaveAdapter: true);
            var authoredTarget = CreateTarget("CanonicalTarget", 8);
            SetObject(controller, "target", authoredTarget.transform);
            SetInt(controller, "targetMask", ~0);
            SetInt(controller, "occluderMask", ~0);

            var result = FrameworkSceneValidator.ValidateScene(scene, manifest);

            Assert.IsFalse(result.Issues.Any(issue => issue.Code.StartsWith("ai.target-mask", StringComparison.Ordinal)));
            Assert.IsFalse(result.Issues.Any(issue => issue.Code.StartsWith("ai.occluder-mask", StringComparison.Ordinal)));
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void OverlappingAiLayerMasksAreAcceptedWhenTargetLayerIsIncluded()
        {
            var controller = CreateValidController("ai.valid-overlap", includeSaveAdapter: true);
            var authoredTarget = CreateTarget("OverlappingMaskTarget", 8);
            SetObject(controller, "target", authoredTarget.transform);
            SetInt(controller, "targetMask", 1 << 8);
            SetInt(controller, "occluderMask", (1 << 8) | (1 << 9));

            var result = FrameworkSceneValidator.ValidateScene(scene, manifest);

            Assert.IsFalse(result.Issues.Any(issue => issue.Code.Contains("mask")));
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void TargetMaskExcludingAssignedTargetColliderIsRejectedWithActionableDiagnostic()
        {
            var controller = CreateValidController("ai.excluded-target", includeSaveAdapter: true);
            var authoredTarget = CreateTarget("ExcludedTarget", 8);
            SetObject(controller, "target", authoredTarget.transform);
            SetInt(controller, "targetMask", 1 << 9);
            SetInt(controller, "occluderMask", 1 << 8);

            var result = FrameworkSceneValidator.ValidateScene(scene, manifest);
            var issue = result.Issues.Single(candidate =>
                candidate.Code == "ai.target-mask.excludes-authored-target");

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("targetMask", issue.Message);
            StringAssert.Contains("ExcludedTarget", issue.Message);
            StringAssert.Contains("Sight can never confirm", issue.Message);
        }

        [Test]
        public void EmptyAiMasksAreRejectedWithFieldSpecificDiagnostics()
        {
            var controller = CreateValidController("ai.empty-masks", includeSaveAdapter: true);
            SetInt(controller, "targetMask", 0);
            SetInt(controller, "occluderMask", 0);

            var result = FrameworkSceneValidator.ValidateScene(scene, manifest);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Issues.Any(issue =>
                issue.Code == "ai.target-mask.empty" && issue.Message.Contains("targetMask")));
            Assert.IsTrue(result.Issues.Any(issue =>
                issue.Code == "ai.occluder-mask.empty" && issue.Message.Contains("occluderMask")));
        }

        [Test]
        public void MissingAiSaveAdapterIsRejected()
        {
            CreateValidController("ai.no-save-adapter", includeSaveAdapter: false);

            var result = FrameworkSceneValidator.ValidateScene(scene, manifest);

            Assert.IsTrue(result.Issues.Any(issue => issue.Code == "ai.save-adapter.missing"));
            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void MissingAiPatrolRouteIsRejectedWithActionableDiagnostic()
        {
            var controller = CreateValidController("ai.no-route", includeSaveAdapter: true);
            SetObject(controller, "patrolRoute", null);

            var result = FrameworkSceneValidator.ValidateScene(scene, manifest);
            var issue = result.Issues.Single(candidate => candidate.Code == "ai.patrol-route.missing");

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("Stalker", issue.Message);
            StringAssert.Contains("patrolRoute", issue.Message);
            StringAssert.Contains("Patrol", issue.Message);
        }

        [Test]
        public void ValidAiPatrolRoutePassesRouteValidation()
        {
            CreateValidController("ai.valid-route", includeSaveAdapter: true);

            var result = FrameworkSceneValidator.ValidateScene(scene, manifest);

            Assert.IsFalse(result.Issues.Any(issue => issue.Code.StartsWith("ai.patrol-route", StringComparison.Ordinal)));
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void EmptyScareTriggerIdIsRejectedWithActionableDiagnostic()
        {
            var trigger = CreateValidScareTrigger("", "m9.scare-trigger.empty", "m9.scare.valid");

            var result = FrameworkSceneValidator.ValidateScene(scene, manifest);
            var issue = result.Issues.Single(candidate => candidate.Code == "atmosphere.trigger-id.empty");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(trigger, issue.Owner);
            StringAssert.Contains("ScareTrigger", issue.Message);
            StringAssert.Contains("triggerId", issue.Message);
            StringAssert.Contains("InteractionTriggerZone StableId", issue.Message);
        }

        [Test]
        public void UnknownScareTriggerIdIsRejectedWithActionableDiagnostic()
        {
            var trigger = CreateValidScareTrigger(
                "m9.trigger.unknown",
                "m9.trigger.actual",
                "m9.scare.valid");

            var result = FrameworkSceneValidator.ValidateScene(scene, manifest);
            var issue = result.Issues.Single(candidate => candidate.Code == "atmosphere.trigger-id.unresolved");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(trigger, issue.Owner);
            StringAssert.Contains("triggerId", issue.Message);
            StringAssert.Contains("m9.trigger.unknown", issue.Message);
            StringAssert.Contains(scene.name, issue.Message);
        }

        [Test]
        public void ValidScareTriggerIdMatchingTriggerSourcePasses()
        {
            CreateValidScareTrigger("m9.trigger.valid", "m9.trigger.valid", "m9.scare.valid");

            var result = FrameworkSceneValidator.ValidateScene(scene, manifest);

            Assert.IsFalse(result.Issues.Any(issue => issue.Code.StartsWith("atmosphere.", StringComparison.Ordinal)));
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void ExistingCanonicalGameplayFixturePassesUnifiedValidation()
        {
            const string gameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
            const string gameplayManifestPath = "Assets/Game/Settings/GameplayStableIdManifest.asset";
            var gameplayScene = EditorSceneManager.OpenScene(gameplayScenePath, OpenSceneMode.Additive);

            try
            {
                var gameplayManifest = AssetDatabase.LoadAssetAtPath<StableIdManifest>(gameplayManifestPath);
                var result = FrameworkSceneValidator.ValidateScene(gameplayScene, gameplayManifest);

                Assert.IsTrue(result.IsValid,
                    result.Issues.FirstOrDefault(issue => issue.IsError)?.ToString() ?? string.Empty);
            }
            finally
            {
                EditorSceneManager.CloseScene(gameplayScene, true);
            }
        }

        private void CreateStableObject(string name, string id)
        {
            var target = new GameObject(name);
            target.AddComponent<StableId>().Assign(id);
        }

        private static GameObject CreateTarget(string name, int layer)
        {
            var target = new GameObject(name)
            {
                layer = layer
            };
            target.AddComponent<BoxCollider>();
            return target;
        }

        private StalkerAiController CreateValidController(string stableId, bool includeSaveAdapter)
        {
            var target = new GameObject("Stalker");
            target.SetActive(false);
            var id = target.AddComponent<StableId>();
            id.Assign(stableId);
            var controller = target.AddComponent<StalkerAiController>();
            SetObject(controller, "tuningProfile", profile);
            SetObject(controller, "stableId", id);
            SetObject(controller, "patrolRoute", CreateValidPatrolRoute());
            SetInt(controller, "targetMask", 1 << 8);
            SetInt(controller, "occluderMask", 1 << 9);
            if (includeSaveAdapter)
            {
                target.AddComponent<StalkerAiSaveAdapter>();
            }

            manifest.ReplaceEntries(new[]
            {
                new StableIdManifestEntry(
                    stableId,
                    StableIdManifestEntryKind.Required,
                    SaveRestorePolicy.ResumeDeterministicPhase)
            });
            return controller;
        }

        private static StalkerPatrolRoute CreateValidPatrolRoute()
        {
            var routeObject = new GameObject("PatrolRoute");
            var waypoint = new GameObject("Waypoint").transform;
            waypoint.SetParent(routeObject.transform, false);
            var route = routeObject.AddComponent<StalkerPatrolRoute>();
            SetObjectArray(route, "waypoints", waypoint);
            return route;
        }

        private ScareScenarioTrigger CreateValidScareTrigger(
            string triggerId,
            string sourceStableId,
            string scareId)
        {
            transientScareDefinition = ScriptableObject.CreateInstance<ScareDefinition>();
            SetString(transientScareDefinition, "scareId", scareId);

            var catalogObject = new GameObject("ScareCatalog");
            catalogObject.SetActive(false);
            var adapter = catalogObject.AddComponent<ScareScenarioAdapter>();
            SetObjectArray(adapter, "scares", transientScareDefinition);

            var sourceObject = new GameObject("ScareTrigger");
            sourceObject.SetActive(false);
            var collider = sourceObject.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            sourceObject.AddComponent<StableId>().Assign(sourceStableId);
            sourceObject.AddComponent<InteractionTriggerZone>();
            var trigger = sourceObject.AddComponent<ScareScenarioTrigger>();
            SetString(trigger, "triggerId", triggerId);
            SetString(trigger, "scareId", scareId);

            manifest.ReplaceEntries(new[]
            {
                new StableIdManifestEntry(
                    sourceStableId,
                    StableIdManifestEntryKind.Required,
                    SaveRestorePolicy.ResumeDeterministicPhase)
            });
            return trigger;
        }

        private static void SetObject(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(UnityEngine.Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(UnityEngine.Object target, string propertyName, params UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
