using System;
using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Editor.Builders;
using Setus.HorrorFramework.Interaction.Interactables;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;
using Setus.HorrorFramework.SceneFlow.Spawn;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Tests.EditMode.Samples
{
    public sealed class FirstPersonHorrorTemplateBuilderTests
    {
        private StableIdManifest manifest;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            manifest = ScriptableObject.CreateInstance<StableIdManifest>();
        }

        [TearDown]
        public void TearDown()
        {
            if (manifest != null)
            {
                UnityEngine.Object.DestroyImmediate(manifest);
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void GameplayTemplateContentIsIdempotentAndDeclaresPersistentContainer()
        {
            var scene = SceneManager.GetActiveScene();

            FirstPersonHorrorTemplateBuilder.EnsureGameplayTemplateContent(scene, manifest);
            FirstPersonHorrorTemplateBuilder.EnsureGameplayTemplateContent(scene, manifest);

            var roots = scene.GetRootGameObjects();
            Assert.AreEqual(
                1,
                roots.Count(root => root.name == FirstPersonHorrorTemplateBuilder.TemplateRootName));

            var template = roots.Single(root => root.name == FirstPersonHorrorTemplateBuilder.TemplateRootName);
            var containers = template.GetComponentsInChildren<DrawerInteractable>(true)
                .Where(drawer => drawer.GetComponent<StableId>()?.Id ==
                                 FirstPersonHorrorTemplateBuilder.LockedContainerStableId)
                .ToArray();
            Assert.AreEqual(1, containers.Length);

            var serialized = new SerializedObject(containers[0]);
            Assert.IsTrue(serialized.FindProperty("isLocked").boolValue);
            Assert.AreEqual(
                "m5.debug.key",
                serialized.FindProperty("lockRequirement").FindPropertyRelative("requiredItemId").stringValue);

            var spawn = template.GetComponentsInChildren<PlayerSpawnPoint>(true);
            Assert.AreEqual(1, spawn.Length);
            Assert.AreEqual("player-start", spawn[0].SpawnId);

            Assert.AreEqual(
                1,
                manifest.Entries.Count(entry =>
                    entry.StableId == FirstPersonHorrorTemplateBuilder.LockedContainerStableId));
            Assert.IsTrue(manifest.TryGetEntry(
                FirstPersonHorrorTemplateBuilder.LockedContainerStableId,
                out var containerEntry));
            Assert.AreEqual(StableIdManifestEntryKind.Required, containerEntry.Kind);
            Assert.AreEqual(scene.name, containerEntry.SceneId);
        }

        [Test]
        public void PackageDeclaresFirstPersonHorrorTemplateSample()
        {
            var package = JsonUtility.FromJson<PackageManifest>(System.IO.File.ReadAllText(
                "Packages/com.setus.horror-framework/package.json"));

            Assert.IsNotNull(package.samples);
            Assert.IsTrue(package.samples.Any(sample =>
                sample.path == "Samples~/FirstPersonHorrorTemplate" &&
                !string.IsNullOrWhiteSpace(sample.displayName)));
        }

        [Test]
        public void GameplayTemplateReusesExistingPlayerStartWithoutCreatingDuplicate()
        {
            var scene = SceneManager.GetActiveScene();
            var existing = new GameObject("ExistingPlayerStart");
            var spawn = existing.AddComponent<PlayerSpawnPoint>();
            var serialized = new SerializedObject(spawn);
            serialized.FindProperty("spawnId").stringValue = "player-start";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            FirstPersonHorrorTemplateBuilder.EnsureGameplayTemplateContent(scene, manifest);

            var matchingSpawns = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PlayerSpawnPoint>(true))
                .Where(candidate => candidate.SpawnId == "player-start")
                .ToArray();
            Assert.AreEqual(1, matchingSpawns.Length);
            Assert.AreSame(spawn, matchingSpawns[0]);
            Assert.IsNull(GameObject.Find("M12_PlayerStart"));
        }

        [Test]
        public void InterruptedPartialTemplateIsRepairedWithoutDuplicateOwnedState()
        {
            var scene = SceneManager.GetActiveScene();
            CreatePartialTemplateRoot(scene);
            CreatePartialTemplateRoot(scene);
            manifest.ReplaceEntries(new[]
            {
                new StableIdManifestEntry(
                    FirstPersonHorrorTemplateBuilder.LockedContainerStableId,
                    StableIdManifestEntryKind.Required,
                    SaveRestorePolicy.ResumeDeterministicPhase,
                    "Interrupted entry A."),
                new StableIdManifestEntry(
                    FirstPersonHorrorTemplateBuilder.LockedContainerStableId,
                    StableIdManifestEntryKind.Required,
                    SaveRestorePolicy.ResumeDeterministicPhase,
                    "Interrupted entry B.")
            });

            FirstPersonHorrorTemplateBuilder.EnsureGameplayTemplateContent(scene, manifest);
            FirstPersonHorrorTemplateBuilder.EnsureGameplayTemplateContent(scene, manifest);

            var roots = scene.GetRootGameObjects();
            Assert.AreEqual(
                1,
                roots.Count(root => root.name == FirstPersonHorrorTemplateBuilder.TemplateRootName));
            Assert.AreEqual(
                1,
                roots.SelectMany(root => root.GetComponentsInChildren<PlayerSpawnPoint>(true))
                    .Count(spawn => spawn.SpawnId == "player-start"));
            Assert.AreEqual(
                1,
                roots.SelectMany(root => root.GetComponentsInChildren<StableId>(true))
                    .Count(stableId => stableId.Id == FirstPersonHorrorTemplateBuilder.LockedContainerStableId));
            Assert.AreEqual(
                1,
                manifest.Entries.Count(entry =>
                    entry.StableId == FirstPersonHorrorTemplateBuilder.LockedContainerStableId));
        }

        [Test]
        public void GeneratedCanonicalSamplePassesM12Validation()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(
                FirstPersonHorrorTemplateBuilder.GameplayScenePath);
            Assert.IsNotNull(sceneAsset, "Generate the canonical M12 sample before running this smoke test.");

            var scene = EditorSceneManager.OpenScene(
                FirstPersonHorrorTemplateBuilder.GameplayScenePath,
                OpenSceneMode.Single);
            var canonicalManifest = AssetDatabase.LoadAssetAtPath<StableIdManifest>(
                FirstPersonHorrorTemplateBuilder.ManifestPath);
            Assert.IsNotNull(canonicalManifest, "The canonical M12 manifest is missing.");
            var result = FirstPersonHorrorTemplateBuilder.Validate(scene, canonicalManifest);

            Assert.IsTrue(result.IsValid, result.ToDiagnostic());
        }

        private static void CreatePartialTemplateRoot(Scene scene)
        {
            var root = new GameObject(FirstPersonHorrorTemplateBuilder.TemplateRootName);
            SceneManager.MoveGameObjectToScene(root, scene);

            var spawnObject = new GameObject("M12_PlayerStart");
            spawnObject.transform.SetParent(root.transform, false);
            var spawn = spawnObject.AddComponent<PlayerSpawnPoint>();
            var serializedSpawn = new SerializedObject(spawn);
            serializedSpawn.FindProperty("spawnId").stringValue = "player-start";
            serializedSpawn.ApplyModifiedPropertiesWithoutUndo();

            var container = new GameObject("M12_LockedContainer");
            container.transform.SetParent(root.transform, false);
            container.AddComponent<StableId>().Assign(
                FirstPersonHorrorTemplateBuilder.LockedContainerStableId);
        }

        [Serializable]
        private sealed class PackageManifest
        {
            public PackageSample[] samples;
        }

        [Serializable]
        private sealed class PackageSample
        {
            public string displayName;
            public string path;
        }
    }
}
