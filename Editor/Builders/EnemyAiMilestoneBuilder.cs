using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Setus.HorrorFramework.AI.Debugging;
using Setus.HorrorFramework.AI.Feedback;
using Setus.HorrorFramework.AI.Memory;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.AI.Tuning;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Editor.Builders
{
    public static class EnemyAiMilestoneBuilder
    {
        private const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
        private const string ManifestPath = "Assets/Game/Settings/GameplayStableIdManifest.asset";
        private const string ProfilePath = "Assets/Game/Content/AIProfiles/M8_StalkerTuning.asset";
        private const string FixtureRootName = "M8EnemyAiFixtures";
        private const string StalkerStableId = "m8.stalker";

        [MenuItem("Setus/Horror Framework/AI/Apply M8 Enemy AI Foundation")]
        public static void ApplyM8EnemyAiFoundationFromMenu()
        {
            var profile = CreateOrLoad<StalkerAiTuningProfile>(ProfilePath);
            var scene = OpenScene(GameplayScenePath);
            UpdateGameplayScene(scene, profile);
            UpdateStableIdManifest();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Applied M8 enemy AI foundation artifacts.");
        }

        private static void UpdateGameplayScene(Scene scene, StalkerAiTuningProfile profile)
        {
            var root = GameObject.Find(FixtureRootName) ?? new GameObject(FixtureRootName);
            EnsureM7ScareTriggerRequiresPlayer();
            var navigation = GetOrCreateChild(root.transform, "M8_Navigation").gameObject;
            var surface = EnsureComponent<NavMeshSurface>(navigation);
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            var routeRoot = GetOrCreateChild(root.transform, "M8_PatrolRoute").gameObject;
            var route = EnsureComponent<StalkerPatrolRoute>(routeRoot);
            var pointA = GetOrCreateChild(routeRoot.transform, "Point_A");
            pointA.position = new Vector3(-1.3f, 0f, 13f);
            var pointB = GetOrCreateChild(routeRoot.transform, "Point_B");
            pointB.position = new Vector3(1.3f, 0f, 18f);
            ConfigureObjectArray(route, "waypoints", pointA, pointB);

            var stalker = GetOrCreateChild(root.transform, "M8_Stalker").gameObject;
            stalker.transform.position = pointA.position;
            var capsule = EnsureComponent<CapsuleCollider>(stalker);
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);
            EnsureVisualCapsule(stalker.transform);
            var agent = EnsureComponent<NavMeshAgent>(stalker);
            agent.radius = 0.28f;
            agent.height = 1.8f;
            agent.stoppingDistance = 0.2f;
            SetStableId(stalker, StalkerStableId);

            var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonPlayerController>(FindObjectsInactive.Include);
            var controller = EnsureComponent<StalkerAiController>(stalker);
            ConfigureReference(controller, "tuningProfile", profile);
            ConfigureReference(controller, "patrolRoute", route);
            ConfigureReference(controller, "target", player != null ? player.transform : null);
            ConfigureReference(controller, "eye", stalker.transform);
            ConfigureReference(controller, "stableId", stalker.GetComponent<StableId>());
            ConfigureReference(controller, "agent", agent);
            ConfigureLayerMask(controller, "targetMask", ~0);
            ConfigureLayerMask(controller, "occluderMask", ~0);
            ValidateControllerAuthoring(controller, profile, stalker.GetComponent<StableId>(), agent);
            EnsureComponent<StalkerAiSaveAdapter>(stalker);
            EnsureComponent<StalkerAiFeedbackHook>(stalker);
            EnsureComponent<StalkerAiDebugView>(stalker);

            surface.BuildNavMesh();
            if (NavMesh.SamplePosition(pointA.position, out var hit, 2f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        private static void UpdateStableIdManifest()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<StableIdManifest>(ManifestPath);
            if (manifest == null)
            {
                throw new InvalidOperationException("M8 requires the Gameplay Stable ID Manifest.");
            }

            var entries = manifest.Entries
                .Where(entry => entry != null && !string.Equals(entry.StableId, StalkerStableId, StringComparison.Ordinal))
                .ToList();
            // Existing M7 slots predate this scene-owned enemy. Optional permits that legacy state
            // to start at authored patrol defaults; every M8 capture writes its state thereafter.
            entries.Add(new StableIdManifestEntry(
                StalkerStableId,
                StableIdManifestEntryKind.Optional,
                SaveRestorePolicy.ResumeDeterministicPhase,
                "M8 stalker. Chasing restores as search at the captured last known position.",
                Path.GetFileNameWithoutExtension(GameplayScenePath)));
            manifest.ReplaceEntries(entries.OrderBy(entry => entry.StableId, StringComparer.Ordinal));
            EditorUtility.SetDirty(manifest);
        }

        private static void EnsureM7ScareTriggerRequiresPlayer()
        {
            var m7Trigger = GameObject.Find("M7_ScareTrigger");
            if (m7Trigger == null)
            {
                return;
            }

            var trigger = m7Trigger.GetComponent<InteractionTriggerZone>();
            if (trigger != null)
            {
                ConfigureBool(trigger, "requirePlayerController", true);
            }
        }

        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets");
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Scene OpenScene(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("M8 requires the authored Gameplay scene.", path);
            }

            return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            var children = parent.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].name == name)
                {
                    return children[i];
                }
            }

            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var existing = target.GetComponent<T>();
            return existing != null ? existing : target.AddComponent<T>();
        }

        private static void EnsureVisualCapsule(Transform stalker)
        {
            var visual = GetOrCreateChild(stalker, "Visual");
            if (visual.GetComponent<MeshFilter>() == null)
            {
                UnityEngine.Object.DestroyImmediate(visual.gameObject);
                var primitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                primitive.transform.SetParent(stalker, false);
                primitive.name = "Visual";
                var collider = primitive.GetComponent<Collider>();
                if (collider != null)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }

                visual = primitive.transform;
            }

            visual.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        }

        private static void SetStableId(GameObject target, string id)
        {
            EnsureComponent<StableId>(target).Assign(id);
        }

        private static void ConfigureReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            RequireProperty(serialized, propertyName, target).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureObjectArray(UnityEngine.Object target, string propertyName, params UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = RequireProperty(serialized, propertyName, target);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureLayerMask(UnityEngine.Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            RequireProperty(serialized, propertyName, target).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureBool(UnityEngine.Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            RequireProperty(serialized, propertyName, target).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateControllerAuthoring(
            StalkerAiController controller,
            StalkerAiTuningProfile profile,
            StableId id,
            NavMeshAgent agent)
        {
            var serialized = new SerializedObject(controller);
            if (RequireProperty(serialized, "tuningProfile", controller).objectReferenceValue != profile ||
                RequireProperty(serialized, "stableId", controller).objectReferenceValue != id ||
                RequireProperty(serialized, "agent", controller).objectReferenceValue != agent)
            {
                throw new InvalidOperationException(
                    "M8 builder could not assign mandatory StalkerAiController references. " +
                    "Gameplay scene was not saved.");
            }
        }

        private static SerializedProperty RequireProperty(SerializedObject serialized, string propertyName, UnityEngine.Object target)
        {
            return serialized.FindProperty(propertyName) ?? throw new InvalidOperationException(
                $"Serialized property '{propertyName}' was not found on {target.GetType().Name}.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var slash = path.LastIndexOf('/');
            var parent = slash > 0 ? path.Substring(0, slash) : "Assets";
            var folder = slash > 0 ? path.Substring(slash + 1) : path;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
