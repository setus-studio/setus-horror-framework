using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Setus.HorrorFramework.AI.Debugging;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Editor.ContentPipeline;
using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.Interaction.Interactables;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Narrative.Notes;
using Setus.HorrorFramework.Narrative.Phone;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;
using Setus.HorrorFramework.SceneFlow.Loading;
using Setus.HorrorFramework.SceneFlow.Spawn;
using Setus.HorrorFramework.UI.Menus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Editor.Builders
{
    public static class FirstPersonHorrorTemplateBuilder
    {
        public const string BootScenePath = "Assets/Game/Scenes/Boot.unity";
        public const string MainMenuScenePath = "Assets/Game/Scenes/MainMenu.unity";
        public const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
        public const string ManifestPath = "Assets/Game/Settings/GameplayStableIdManifest.asset";
        public const string TemplateRootName = "M12FirstPersonHorrorTemplate";
        public const string LockedContainerStableId = "m12.container.locked";
        public const string CheckpointId = "m12.room.checkpoint";

        private const string DebugKeyItemId = "m5.debug.key";
        private const string SpawnId = "player-start";

        private static readonly ManifestDeclaration[] CanonicalManifestDeclarations =
        {
            new ManifestDeclaration("m5.door", StableIdManifestEntryKind.Required, "M12 sample door."),
            new ManifestDeclaration("m5.drawer", StableIdManifestEntryKind.Required, "M12 sample drawer."),
            new ManifestDeclaration("m5.keypickup", StableIdManifestEntryKind.Required, "M12 sample key pickup."),
            new ManifestDeclaration("m5.lockeddoor", StableIdManifestEntryKind.Required, "M12 sample locked door."),
            new ManifestDeclaration("m5.inspectable", StableIdManifestEntryKind.Required, "M12 sample inspectable."),
            new ManifestDeclaration("m5.trigger", StableIdManifestEntryKind.Required, "M12 sample interaction trigger."),
            new ManifestDeclaration(LockedContainerStableId, StableIdManifestEntryKind.Required, "M12 sample locked container.")
        };

        [MenuItem("Setus/Horror Framework/Samples/Build First Person Horror Template")]
        public static void BuildFromMenu()
        {
            var result = Build();
            if (!result.IsValid)
            {
                throw new InvalidOperationException(result.ToDiagnostic());
            }

            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayScenePath);
            Selection.activeObject = sceneAsset;
            EditorGUIUtility.PingObject(sceneAsset);
            Debug.Log(
                "M12 First Person Horror Template is ready. Open Assets/Game/Scenes/Boot.unity " +
                "and enter Play Mode to run the full sample.");
        }

        public static FirstPersonHorrorTemplateValidationResult Build()
        {
            StandaloneGameContentTemplate.CreateAt(StandaloneGameContentTemplate.DefaultGameRoot);
            PlayerFoundationTemplateBuilder.BuildDefaultAssets(false);
            TabbedSettingsLayoutBuilder.EnsurePauseActionAtPath(PlayerFoundationTemplateBuilder.InputActionsPath);
            SceneFlowUiShellTemplateBuilder.BuildDefaultUiShell(false);

            var manifest = CreateOrLoadManifest();
            AssignManifestToGameConfig(manifest);
            AssetDatabase.SaveAssets();

            MilestoneHardeningBuilder.ApplyM0ToM4Hardening();
            InteractionMilestoneBuilder.ApplyM5InteractionFoundation();

            var gameplay = OpenGameplayScene();
            manifest = LoadManifestOrThrow();
            EnsureGameplayTemplateContent(gameplay, manifest);
            SaveGameplayScene(gameplay);

            NarrativeMilestoneBuilder.ApplyM6NarrativeFoundation();
            AtmosphereMilestoneBuilder.ApplyM7AtmosphereFoundation();
            EnemyAiMilestoneBuilder.ApplyM8EnemyAiFoundationFromMenu();
            AccessibilityMilestoneBuilder.ApplyFromMenu();
            GraphicsSettingsMilestoneBuilder.Apply();

            gameplay = OpenGameplayScene();
            manifest = LoadManifestOrThrow();
            EnsureGameplayTemplateContent(gameplay, manifest);
            RequirePlayerForScenarioTriggers(gameplay);
            SaveGameplayScene(gameplay);

            ConfigureSampleUiShell();
            ConfigureDebugDefaults();
            EnsureBuildSettings();
            AssignManifestToGameConfig(manifest);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            gameplay = OpenGameplayScene();
            manifest = LoadManifestOrThrow();
            return Validate(gameplay, manifest);
        }

        public static void EnsureGameplayTemplateContent(Scene scene, StableIdManifest manifest)
        {
            if (!scene.IsValid())
            {
                throw new ArgumentException("A valid Gameplay scene is required.", nameof(scene));
            }

            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            var root = EnsureSingleTemplateRoot(scene);
            if (root.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(root, scene);
            }
            EnsureRoom(root.transform);
            EnsurePlayerSpawn(scene, root.transform);
            EnsureLockedContainer(root.transform);
            EnsureCanonicalManifestEntries(manifest, scene.name);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static FirstPersonHorrorTemplateValidationResult Validate(Scene scene, StableIdManifest manifest)
        {
            var framework = FrameworkSceneValidator.ValidateScene(scene, manifest);
            var issues = ValidateSampleContract(scene);

            return new FirstPersonHorrorTemplateValidationResult(framework, issues);
        }

        internal static IReadOnlyList<string> ValidateSampleContract(Scene scene)
        {
            var issues = new List<string>();

            var components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null)
                .ToArray();

            RequireComponent<DoorInteractable>(components, "door", issues);
            RequireComponents<DrawerInteractable>(components, 2, "drawer and locked container", issues);
            RequireComponent<PickupInteractable>(components, "pickup", issues);
            RequireComponent<NarrativeNoteInteractable>(components, "inspectable note", issues);
            RequireComponent<NarrativeScenarioAdapter>(components, "objective scenario", issues);
            RequireComponent<PhoneMessageInteractable>(components, "phone message", issues);
            RequireComponent<ScareScenarioTrigger>(components, "scare trigger", issues);
            RequireComponent<StalkerAiController>(components, "AI encounter", issues);
            RequireComponent<StalkerPatrolRoute>(components, "AI patrol route", issues);
            RequireComponent<StalkerAiDebugView>(components, "toggleable AI debug overlay", issues);

            var scenario = components.OfType<NarrativeScenarioAdapter>()
                .Select(adapter => new SerializedObject(adapter).FindProperty("scenario")
                    ?.objectReferenceValue as NarrativeScenarioDefinition)
                .FirstOrDefault(definition => definition != null);
            if (scenario == null ||
                (scenario.Objectives?.Length ?? 0) < 2 ||
                (scenario.PhoneMessages?.Length ?? 0) < 1)
            {
                issues.Add("M12 scenario must contain an objective chain and at least one phone message.");
            }

            var spawn = components.OfType<PlayerSpawnPoint>()
                .Where(candidate => string.Equals(candidate.SpawnId, SpawnId, StringComparison.Ordinal))
                .ToArray();
            if (spawn.Length != 1)
            {
                issues.Add($"Expected exactly one PlayerSpawnPoint with spawnId '{SpawnId}', found {spawn.Length}.");
            }

            var lockedContainer = components.OfType<DrawerInteractable>()
                .FirstOrDefault(candidate => candidate.GetComponent<StableId>()?.Id == LockedContainerStableId);
            if (lockedContainer == null)
            {
                issues.Add($"Locked container StableId '{LockedContainerStableId}' is missing.");
            }
            else
            {
                var serialized = new SerializedObject(lockedContainer);
                var isLocked = serialized.FindProperty("isLocked")?.boolValue == true;
                var requiredItemId = serialized.FindProperty("lockRequirement")
                    ?.FindPropertyRelative("requiredItemId")?.stringValue;
                if (!isLocked || !string.Equals(requiredItemId, DebugKeyItemId, StringComparison.Ordinal))
                {
                    issues.Add(
                        $"Locked container '{lockedContainer.name}' must start locked and require '{DebugKeyItemId}'.");
                }
            }

            ValidateProjectAssets(issues);
            return issues;
        }

        private static void EnsureRoom(Transform parent)
        {
            var environment = GetOrCreateChild(parent, "Room");
            EnsureCube(environment, "Floor", new Vector3(0f, -0.1f, 8f), new Vector3(10f, 0.2f, 30f));
            const float wallCenterY = 1.45f;
            EnsureCube(environment, "LeftWall", new Vector3(-5f, wallCenterY, 8f), new Vector3(0.2f, 3f, 30f));
            EnsureCube(environment, "RightWall", new Vector3(5f, wallCenterY, 8f), new Vector3(0.2f, 3f, 30f));
            EnsureCube(environment, "BackWall", new Vector3(0f, wallCenterY, -7f), new Vector3(10f, 3f, 0.2f));
            EnsureCube(environment, "FrontWall", new Vector3(0f, wallCenterY, 23f), new Vector3(10f, 3f, 0.2f));

            var lightObject = GetOrCreateChild(environment, "RoomLight").gameObject;
            lightObject.transform.position = new Vector3(0f, 2.6f, 4f);
            var light = EnsureComponent<UnityEngine.Light>(lightObject);
            light.type = LightType.Point;
            light.range = 18f;
            light.intensity = 2.2f;
            light.color = new Color(1f, 0.84f, 0.68f);
        }

        private static void EnsurePlayerSpawn(Scene scene, Transform parent)
        {
            var authoredSpawns = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PlayerSpawnPoint>(true))
                .Where(spawn =>
                    !spawn.transform.IsChildOf(parent) &&
                    string.Equals(spawn.SpawnId, SpawnId, StringComparison.Ordinal))
                .ToArray();
            var managedSpawns = FindDirectChildren(parent, "M12_PlayerStart");
            if (authoredSpawns.Length > 0)
            {
                for (var i = 0; i < managedSpawns.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(managedSpawns[i].gameObject);
                }

                return;
            }

            var managedSpawn = managedSpawns.FirstOrDefault();
            for (var i = 1; i < managedSpawns.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(managedSpawns[i].gameObject);
            }

            var spawnObject = managedSpawn != null
                ? managedSpawn.gameObject
                : GetOrCreateChild(parent, "M12_PlayerStart").gameObject;
            spawnObject.transform.position = new Vector3(0f, 0.4f, -4f);
            spawnObject.transform.rotation = Quaternion.identity;
            var spawn = EnsureComponent<PlayerSpawnPoint>(spawnObject);
            ConfigureString(spawn, "spawnId", SpawnId);
        }

        private static void EnsureLockedContainer(Transform parent)
        {
            var container = EnsureCube(
                parent,
                "M12_LockedContainer",
                new Vector3(3.4f, 0.55f, 4.5f),
                new Vector3(0.9f, 0.5f, 0.65f));
            EnsureComponent<StableId>(container).Assign(LockedContainerStableId);
            var drawer = EnsureComponent<DrawerInteractable>(container);
            ConfigureObjectReference(drawer, "movingRoot", container.transform);
            ConfigureBool(drawer, "isLocked", true);
            ConfigureNestedString(drawer, "lockRequirement", "requiredItemId", DebugKeyItemId);
            ConfigureNestedBool(drawer, "lockRequirement", "consumeItemOnUnlock", false);
        }

        private static void EnsureCanonicalManifestEntries(StableIdManifest manifest, string sceneId)
        {
            var managedIds = new HashSet<string>(
                CanonicalManifestDeclarations.Select(declaration => declaration.StableId),
                StringComparer.Ordinal);
            var entries = manifest.Entries
                .Where(entry => entry != null && !managedIds.Contains(entry.StableId))
                .ToList();

            for (var i = 0; i < CanonicalManifestDeclarations.Length; i++)
            {
                var declaration = CanonicalManifestDeclarations[i];
                entries.Add(new StableIdManifestEntry(
                    declaration.StableId,
                    declaration.Kind,
                    SaveRestorePolicy.ResumeDeterministicPhase,
                    declaration.Note,
                    sceneId));
            }

            manifest.ReplaceEntries(entries.OrderBy(entry => entry.StableId, StringComparer.Ordinal));
            EditorUtility.SetDirty(manifest);
        }

        private static StableIdManifest CreateOrLoadManifest()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<StableIdManifest>(ManifestPath);
            if (manifest != null)
            {
                return manifest;
            }

            EnsureParentFolder(ManifestPath);
            manifest = ScriptableObject.CreateInstance<StableIdManifest>();
            AssetDatabase.CreateAsset(manifest, ManifestPath);
            return manifest;
        }

        private static StableIdManifest LoadManifestOrThrow()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<StableIdManifest>(ManifestPath);
            if (manifest == null)
            {
                throw new InvalidOperationException(
                    $"M12 requires the canonical StableIdManifest at '{ManifestPath}'.");
            }

            return manifest;
        }

        private static void AssignManifestToGameConfig(StableIdManifest manifest)
        {
            var config = AssetDatabase.LoadAssetAtPath<StandaloneGameConfig>(
                SceneFlowUiShellTemplateBuilder.ConfigPath);
            if (config == null)
            {
                throw new InvalidOperationException("M12 requires the canonical StandaloneGameConfig.");
            }

            ConfigureObjectReference(config, "stableIdManifest", manifest);
            ConfigureString(config, "defaultCheckpointId", CheckpointId);
            ConfigureString(config, "defaultSpawnId", SpawnId);
            EditorUtility.SetDirty(config);
        }

        private static void ConfigureSampleUiShell()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("M12 requires the canonical UI shell prefab.");
            }

            var root = PrefabUtility.LoadPrefabContents(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            try
            {
                var panel = root.GetComponent<DebugSaveLoadPanel>();
                if (panel == null)
                {
                    throw new InvalidOperationException("M12 requires DebugSaveLoadPanel on the canonical UI shell.");
                }

                ConfigureString(panel, "checkpointId", CheckpointId);
                PrefabUtility.SaveAsPrefabAsset(root, SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureDebugDefaults()
        {
            var config = AssetDatabase.LoadAssetAtPath<HorrorFrameworkConfig>(
                "Assets/Game/Settings/HorrorFrameworkConfig.asset");
            if (config == null)
            {
                throw new InvalidOperationException("M12 requires HorrorFrameworkConfig.");
            }

            ConfigureBool(config, "debugEnabledByDefault", false);
            ConfigureBool(config, "debugHotkeyEnabled", true);
            EditorUtility.SetDirty(config);
        }

        private static void RequirePlayerForScenarioTriggers(Scene scene)
        {
            var triggerNames = new HashSet<string>(
                new[] { "M5_TriggerZone", "M6_StoryBeatTrigger", "M7_ScareTrigger" },
                StringComparer.Ordinal);
            var triggers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<InteractionTriggerZone>(true));
            foreach (var trigger in triggers)
            {
                if (triggerNames.Contains(trigger.name))
                {
                    ConfigureBool(trigger, "requirePlayerController", true);
                }
            }
        }

        private static void EnsureBuildSettings()
        {
            var required = new[] { BootScenePath, MainMenuScenePath, GameplayScenePath };
            var result = required.Select(path => new EditorBuildSettingsScene(path, true)).ToList();
            var seen = new HashSet<string>(required, StringComparer.Ordinal);
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene != null && !string.IsNullOrWhiteSpace(scene.path) && seen.Add(scene.path))
                {
                    result.Add(scene);
                }
            }

            EditorBuildSettings.scenes = result.ToArray();
        }

        private static void ValidateProjectAssets(ICollection<string> issues)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerFoundationTemplateBuilder.PlayerPrefabPath) == null)
            {
                issues.Add($"Canonical player prefab is missing at '{PlayerFoundationTemplateBuilder.PlayerPrefabPath}'.");
            }

            var shell = AssetDatabase.LoadAssetAtPath<GameObject>(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            if (shell == null)
            {
                issues.Add($"Canonical UI shell is missing at '{SceneFlowUiShellTemplateBuilder.UiShellPrefabPath}'.");
            }
            else if (shell.GetComponent<DebugSaveLoadPanel>() == null ||
                     shell.GetComponent<UiShellCommandAdapter>() == null)
            {
                issues.Add("Canonical UI shell must expose menu, pause, save/load, and Continue commands.");
            }
            else
            {
                var savePanel = shell.GetComponent<DebugSaveLoadPanel>();
                var checkpoint = new SerializedObject(savePanel).FindProperty("checkpointId")?.stringValue;
                if (!string.Equals(checkpoint, CheckpointId, StringComparison.Ordinal))
                {
                    issues.Add($"DebugSaveLoadPanel must capture checkpoint '{CheckpointId}'.");
                }
            }

            var gameConfig = AssetDatabase.LoadAssetAtPath<StandaloneGameConfig>(
                SceneFlowUiShellTemplateBuilder.ConfigPath);
            if (gameConfig?.StableIdManifest == null)
            {
                issues.Add("StandaloneGameConfig must reference the Gameplay StableIdManifest.");
            }
            else if (!string.Equals(gameConfig.DefaultCheckpointId, CheckpointId, StringComparison.Ordinal) ||
                     !string.Equals(gameConfig.DefaultSpawnId, SpawnId, StringComparison.Ordinal))
            {
                issues.Add(
                    $"StandaloneGameConfig must use checkpoint '{CheckpointId}' and spawn '{SpawnId}'.");
            }

            var buildPaths = new HashSet<string>(
                EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path),
                StringComparer.Ordinal);
            foreach (var path in new[] { BootScenePath, MainMenuScenePath, GameplayScenePath })
            {
                if (!File.Exists(path) || !buildPaths.Contains(path))
                {
                    issues.Add($"Required sample scene '{path}' must exist and be enabled in Build Settings.");
                }
            }

            var frameworkConfig = AssetDatabase.LoadAssetAtPath<HorrorFrameworkConfig>(
                "Assets/Game/Settings/HorrorFrameworkConfig.asset");
            if (frameworkConfig == null || !frameworkConfig.DebugHotkeyEnabled || frameworkConfig.DebugEnabledByDefault)
            {
                issues.Add("Debug overlay must be hotkey-toggleable and disabled by default.");
            }
        }

        private static void RequireComponent<T>(IReadOnlyList<MonoBehaviour> components, string label, ICollection<string> issues)
            where T : MonoBehaviour
        {
            if (!components.OfType<T>().Any())
            {
                issues.Add($"M12 sample is missing its {label} ({typeof(T).Name}).");
            }
        }

        private static void RequireComponents<T>(
            IReadOnlyList<MonoBehaviour> components,
            int minimum,
            string label,
            ICollection<string> issues)
            where T : MonoBehaviour
        {
            var count = components.OfType<T>().Count();
            if (count < minimum)
            {
                issues.Add($"M12 sample requires {minimum} {label} components; found {count}.");
            }
        }

        private static Scene OpenGameplayScene()
        {
            if (!File.Exists(GameplayScenePath))
            {
                throw new FileNotFoundException("M12 Gameplay scene was not generated.", GameplayScenePath);
            }

            return EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        private static void SaveGameplayScene(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, GameplayScenePath))
            {
                throw new InvalidOperationException($"Failed to save '{GameplayScenePath}'.");
            }
        }

        private static GameObject EnsureSingleTemplateRoot(Scene scene)
        {
            var roots = scene.GetRootGameObjects()
                .Where(root => string.Equals(root.name, TemplateRootName, StringComparison.Ordinal))
                .ToArray();
            var root = roots.FirstOrDefault() ?? new GameObject(TemplateRootName);
            for (var i = 1; i < roots.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(roots[i]);
            }

            return root;
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            var matches = FindDirectChildren(parent, name);
            var existing = matches.FirstOrDefault();
            for (var i = 1; i < matches.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(matches[i].gameObject);
            }

            if (existing != null)
            {
                return existing;
            }

            var created = new GameObject(name).transform;
            created.SetParent(parent, false);
            return created;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform[] FindDirectChildren(Transform parent, string name)
        {
            var matches = new List<Transform>();
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    matches.Add(child);
                }
            }

            return matches.ToArray();
        }

        private static GameObject EnsureCube(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var child = GetOrCreateChild(parent, name);
            var gameObject = child.gameObject;
            if (gameObject.GetComponent<MeshFilter>() == null)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                gameObject.name = name;
                gameObject.transform.SetParent(parent, false);
            }

            gameObject.transform.position = position;
            gameObject.transform.rotation = Quaternion.identity;
            gameObject.transform.localScale = scale;
            return gameObject;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void ConfigureObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName) ??
                           throw new InvalidOperationException($"{target.GetType().Name}.{propertyName} was not found.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureString(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName) ??
                           throw new InvalidOperationException($"{target.GetType().Name}.{propertyName} was not found.");
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBool(UnityEngine.Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName) ??
                           throw new InvalidOperationException($"{target.GetType().Name}.{propertyName} was not found.");
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureNestedString(
            UnityEngine.Object target,
            string propertyName,
            string childPropertyName,
            string value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)?.FindPropertyRelative(childPropertyName) ??
                           throw new InvalidOperationException(
                               $"{target.GetType().Name}.{propertyName}.{childPropertyName} was not found.");
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureNestedBool(
            UnityEngine.Object target,
            string propertyName,
            string childPropertyName,
            bool value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)?.FindPropertyRelative(childPropertyName) ??
                           throw new InvalidOperationException(
                               $"{target.GetType().Name}.{propertyName}.{childPropertyName} was not found.");
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureParentFolder(string assetPath)
        {
            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/') ?? "Assets";
            EnsureParentFolder(parent + "/placeholder.asset");
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private readonly struct ManifestDeclaration
        {
            public ManifestDeclaration(string stableId, StableIdManifestEntryKind kind, string note)
            {
                StableId = stableId;
                Kind = kind;
                Note = note;
            }

            public string StableId { get; }
            public StableIdManifestEntryKind Kind { get; }
            public string Note { get; }
        }
    }

    public sealed class FirstPersonHorrorTemplateValidationResult
    {
        public FirstPersonHorrorTemplateValidationResult(
            FrameworkSceneValidationResult frameworkValidation,
            IEnumerable<string> issues)
        {
            FrameworkValidation = frameworkValidation ??
                                  throw new ArgumentNullException(nameof(frameworkValidation));
            Issues = (issues ?? Enumerable.Empty<string>()).ToArray();
        }

        public FrameworkSceneValidationResult FrameworkValidation { get; }
        public IReadOnlyList<string> Issues { get; }
        public bool IsValid => FrameworkValidation.IsValid && Issues.Count == 0;

        public string ToDiagnostic()
        {
            var diagnostics = FrameworkValidation.Issues
                .Where(issue => issue.IsError)
                .Select(issue => issue.ToString())
                .Concat(Issues)
                .ToArray();
            return IsValid
                ? "M12 First Person Horror Template validation passed."
                : "M12 First Person Horror Template validation failed:\n" +
                  string.Join("\n", diagnostics.Select(issue => "- " + issue));
        }
    }
}
