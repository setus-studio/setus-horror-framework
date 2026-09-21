using System;
using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Editor.Builders;
using Setus.HorrorFramework.Interaction.Interactables;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;
using Setus.HorrorFramework.SceneFlow.Spawn;
using Setus.HorrorFramework.UI.Settings;
using Setus.HorrorFramework.UI.Settings.Display;
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
        public void GeneratedRoomWallsOverlapFloorToPreventContactLightLeaks()
        {
            var scene = SceneManager.GetActiveScene();
            FirstPersonHorrorTemplateBuilder.EnsureGameplayTemplateContent(scene, manifest);

            var template = scene.GetRootGameObjects()
                .Single(root => root.name == FirstPersonHorrorTemplateBuilder.TemplateRootName);
            var floor = template.transform.Find("Room/Floor").GetComponent<Renderer>();
            Assert.IsNotNull(floor);

            foreach (var wallName in new[] { "LeftWall", "RightWall", "BackWall", "FrontWall" })
            {
                var wall = template.transform.Find($"Room/{wallName}").GetComponent<Renderer>();
                Assert.IsNotNull(wall, wallName);
                Assert.That(
                    wall.bounds.min.y,
                    Is.LessThanOrEqualTo(floor.bounds.max.y - 0.04f),
                    $"{wallName} must overlap the floor enough to prevent a shadow/light seam.");
            }
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

        [Test]
        public void TabbedSettingsTemplateRemainsIdempotentAndKeepsPrefabGuid()
        {
            const string testRoot = "Assets/Game/__SetusTabbedSettingsLayoutTests";
            const string testPrefab = testRoot + "/SetusUiShell.prefab";
            var sourcePrefab = SceneFlowUiShellTemplateBuilder.UiShellPrefabPath;
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefab),
                "Generate the canonical M12 sample before running this template test.");
            if (AssetDatabase.IsValidFolder(testRoot))
            {
                AssetDatabase.DeleteAsset(testRoot);
            }

            AssetDatabase.CreateFolder("Assets/Game", "__SetusTabbedSettingsLayoutTests");
            try
            {
                Assert.IsTrue(AssetDatabase.CopyAsset(sourcePrefab, testPrefab));
                var guid = AssetDatabase.AssetPathToGUID(testPrefab);

                TabbedSettingsLayoutBuilder.ApplyToPrefabAtPath(testPrefab);
                TabbedSettingsLayoutBuilder.ApplyToPrefabAtPath(testPrefab);

                Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(testPrefab));
                var root = PrefabUtility.LoadPrefabContents(testPrefab);
                try
                {
                    var panel = root.transform.Find("SettingsPanel");
                    Assert.IsNotNull(panel);
                    Assert.AreEqual(1, panel.GetComponents<TabbedSettingsPresenter>().Length);
                    Assert.AreEqual(1, panel.Cast<Transform>().Count(child => child.name == "GameSettingsLayout"));
                    var content = panel.Find("GameSettingsLayout/Body/TabContent");
                    Assert.IsNotNull(content);
                    var audioTab = panel.Find("GameSettingsLayout/Body/TabNavigation/AudioTab")
                        ?.GetComponent<UnityEngine.UI.Button>();
                    Assert.IsNotNull(audioTab);
                    Assert.AreEqual(1, audioTab.onClick.GetPersistentEventCount());
                    Assert.IsInstanceOf<TabbedSettingsPresenter>(audioTab.onClick.GetPersistentTarget(0));
                    Assert.AreEqual("ShowAudio", audioTab.onClick.GetPersistentMethodName(0));
                    var pageScrolls = content.Cast<Transform>()
                        .Select(child => child.GetComponent<UnityEngine.UI.ScrollRect>())
                        .Where(scroll => scroll != null)
                        .ToArray();
                    Assert.AreEqual(5, pageScrolls.Length);
                    foreach (var scroll in pageScrolls)
                    {
                        Assert.AreEqual(UnityEngine.UI.ScrollRect.MovementType.Clamped, scroll.movementType);
                        Assert.IsFalse(scroll.inertia);
                    }
                    var settings = panel.GetComponent<AccessibilitySettingsPanel>();
                    Assert.IsNotNull(settings);
                    Assert.AreEqual(9, content.GetComponentsInChildren<UnityEngine.UI.Text>(true)
                        .Count(label => label.name == "ValueText"));
                    foreach (var pair in new[]
                    {
                        ("AudioPage", "ResetAudioDefaults"),
                        ("ControlsPage", "ResetMovementDefaults"),
                        ("AccessibilityPage", "ResetAccessibilityDefaults")
                    })
                    {
                        var reset = content.Find(pair.Item1 + "/Viewport/Content/" + pair.Item2)
                            ?.GetComponent<UnityEngine.UI.Button>();
                        Assert.IsNotNull(reset);
                        Assert.AreEqual(1, reset.onClick.GetPersistentEventCount());
                        Assert.AreSame(settings, reset.onClick.GetPersistentTarget(0));
                        Assert.AreEqual(pair.Item2, reset.onClick.GetPersistentMethodName(0));
                    }
                    var rebind = panel.GetComponent<InputRebindPresenter>();
                    Assert.IsNotNull(rebind);
                    Assert.IsNotNull(panel.Find("RebindModal/Dialog/Actions/Cancel"));
                    Assert.AreEqual(1, panel.Cast<Transform>().Count(child => child.name == "RebindModal"));
                    Assert.IsFalse(panel.Find("RebindModal").gameObject.activeSelf);
                    var controls = content.Find("ControlsPage/Viewport/Content");
                    Assert.IsNotNull(controls.Find("Interact/BindingText"));
                    Assert.IsNotNull(controls.Find("Pause/BindingText"));
                    Assert.AreEqual(9, controls.GetComponentsInChildren<UnityEngine.UI.Text>(true)
                        .Count(label => label.name == "BindingText"));
                    var resetBindings = controls.Find("ResetBindings")
                        .GetComponent<UnityEngine.UI.Button>();
                    Assert.AreEqual(1, resetBindings.onClick.GetPersistentEventCount());
                    Assert.AreSame(rebind, resetBindings.onClick.GetPersistentTarget(0));
                    Assert.AreEqual(nameof(InputRebindPresenter.RequestResetBindings),
                        resetBindings.onClick.GetPersistentMethodName(0));
                    var displayTab = panel.Find("GameSettingsLayout/Body/TabNavigation/DisplayTab")
                        ?.GetComponent<UnityEngine.UI.Button>();
                    Assert.IsNotNull(displayTab);
                    Assert.AreEqual(1, displayTab.onClick.GetPersistentEventCount());
                    Assert.AreEqual(nameof(TabbedSettingsPresenter.ShowDisplay),
                        displayTab.onClick.GetPersistentMethodName(0));
                    Assert.IsNotNull(content.Find("DisplayPage/Viewport/Content/ModeRow/Dropdown"));
                    Assert.IsNotNull(content.Find("DisplayPage/Viewport/Content/ResolutionRow/Dropdown"));
                    Assert.IsNotNull(content.Find("DisplayPage/Viewport/Content/AntiAliasingRow/Dropdown"));
                    var resolutionDropdown = content.Find(
                        "DisplayPage/Viewport/Content/ResolutionRow/Dropdown")
                        .GetComponent<UnityEngine.UI.Dropdown>();
                    Assert.AreNotEqual(Color.white,
                        resolutionDropdown.template.GetComponent<UnityEngine.UI.Image>().color);
                    Assert.AreEqual(
                        new Color(0.91f, 0.92f, 0.88f, 1f),
                        resolutionDropdown.itemText.color);
                    Assert.AreEqual(
                        new Color(0.16f, 0.41f, 0.4f, 1f),
                        resolutionDropdown.template.Find("Viewport/Content/Item")
                            .GetComponent<UnityEngine.UI.Toggle>().colors.highlightedColor);
                    Assert.That(
                        resolutionDropdown.template.Find("Viewport/Content/Item")
                            .GetComponent<RectTransform>().sizeDelta.y,
                        Is.GreaterThanOrEqualTo(40f));
                    Assert.AreEqual(
                        VerticalWrapMode.Overflow,
                        resolutionDropdown.itemText.verticalOverflow);
                    var vSync = content.Find("DisplayPage/Viewport/Content/VSyncRow/Toggle")
                        ?.GetComponent<UnityEngine.UI.Toggle>();
                    Assert.IsNotNull(vSync);
                    Assert.IsTrue(vSync.interactable);
                    var vSyncHitArea = vSync.GetComponent<UnityEngine.UI.Image>();
                    Assert.IsNotNull(vSyncHitArea);
                    Assert.IsTrue(vSyncHitArea.raycastTarget);
                    Assert.AreNotEqual(
                        ((UnityEngine.UI.Image)vSync.targetGraphic).color,
                        ((UnityEngine.UI.Image)vSync.graphic).color);
                    Assert.IsNotNull(content.Find("DisplayPage/Viewport/Content/ApplyDisplay"));
                    Assert.IsNotNull(panel.Find("DisplayConfirmModal/Dialog/Actions/Keep"));
                    Assert.IsNotNull(panel.GetComponent<DisplaySettingsPresenter>());
                    Assert.IsNotNull(root.GetComponent<DisplaySettingsRuntimeApplier>());
                    Assert.AreEqual(1, panel.Cast<Transform>()
                        .Count(child => child.name == "DisplayConfirmModal"));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(testRoot);
            }
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
