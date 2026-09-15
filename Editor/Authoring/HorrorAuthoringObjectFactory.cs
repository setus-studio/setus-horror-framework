using System;
using System.IO;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Interaction.Interactables;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Editor.Authoring
{
    public static class HorrorAuthoringObjectFactory
    {
        public static GameObject CreateInspectable(
            Transform parent,
            string objectName,
            string stableId,
            Vector3 position,
            bool registerUndo)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = objectName;
            target.transform.SetParent(parent, true);
            target.transform.position = position;
            target.transform.localScale = new Vector3(0.6f, 0.4f, 0.1f);
            RegisterCreatedObject(target, "Create Horror Interactable", registerUndo);
            AddComponent<InspectableInteractable>(target, registerUndo);
            AddComponent<StableId>(target, registerUndo).Assign(RequireId(stableId, nameof(stableId)));
            return target;
        }

        public static GameObject CreateScareTrigger(
            Transform parent,
            string objectName,
            string triggerId,
            string scareId,
            Vector3 position,
            bool registerUndo,
            Scene targetScene = default)
        {
            var target = new GameObject(objectName);
            if (targetScene.IsValid() && targetScene.isLoaded && target.scene != targetScene)
            {
                SceneManager.MoveGameObjectToScene(target, targetScene);
            }

            target.transform.SetParent(parent, true);
            target.transform.position = position;
            RegisterCreatedObject(target, "Create Horror Scare Trigger", registerUndo);

            var collider = AddComponent<BoxCollider>(target, registerUndo);
            RecordObject(collider, "Configure Horror Scare Trigger", registerUndo);
            collider.isTrigger = true;
            collider.size = new Vector3(2f, 2f, 0.5f);
            var rigidbody = AddComponent<Rigidbody>(target, registerUndo);
            RecordObject(rigidbody, "Configure Horror Scare Trigger", registerUndo);
            rigidbody.isKinematic = true;
            var stableId = AddComponent<StableId>(target, registerUndo);
            RecordObject(stableId, "Configure Horror Scare Trigger", registerUndo);
            stableId.Assign(RequireId(triggerId, nameof(triggerId)));
            AddComponent<InteractionTriggerZone>(target, registerUndo);
            var trigger = AddComponent<ScareScenarioTrigger>(target, registerUndo);
            RecordObject(trigger, "Configure Horror Scare Trigger", registerUndo);
            SetString(trigger, "triggerId", triggerId);
            SetString(trigger, "scareId", RequireId(scareId, nameof(scareId)));
            return target;
        }

        public static StalkerPatrolRoute CreatePatrolRoute(
            Transform parent,
            string objectName,
            Vector3 origin,
            bool registerUndo)
        {
            var root = new GameObject(objectName);
            root.transform.SetParent(parent, true);
            root.transform.position = origin;
            RegisterCreatedObject(root, "Create Horror AI Patrol Route", registerUndo);
            var route = AddComponent<StalkerPatrolRoute>(root, registerUndo);
            var points = new Transform[3];
            var offsets = new[] { Vector3.zero, Vector3.forward * 3f, new Vector3(3f, 0f, 3f) };
            for (var i = 0; i < points.Length; i++)
            {
                var waypoint = new GameObject($"Point_{i + 1}");
                waypoint.transform.SetParent(root.transform, false);
                waypoint.transform.localPosition = offsets[i];
                RegisterCreatedObject(waypoint, "Create Horror AI Patrol Waypoint", registerUndo);
                points[i] = waypoint.transform;
            }

            SetObjectArray(route, "waypoints", points);
            return route;
        }

        public static NarrativeScenarioDefinition CreateScenarioSkeleton(
            string scenarioAssetPath,
            string objectiveAssetPath,
            string scenarioId,
            string objectiveId,
            ObjectiveGateKind gateKind,
            string gateId)
        {
            RequireGameAssetPath(scenarioAssetPath);
            RequireGameAssetPath(objectiveAssetPath);
            EnsureFolder(Path.GetDirectoryName(scenarioAssetPath)?.Replace('\\', '/') ?? "Assets/Game");
            EnsureFolder(Path.GetDirectoryName(objectiveAssetPath)?.Replace('\\', '/') ?? "Assets/Game");

            var objective = LoadOrCreate<ObjectiveDefinition>(objectiveAssetPath);
            SetString(objective, "objectiveId", RequireId(objectiveId, nameof(objectiveId)));
            SetString(objective, "title", "New objective");
            SetString(objective, "description", "Describe the player-facing goal.");
            SetEnum(objective, "completionGate", (int)gateKind);
            SetString(objective, "completionGateId", RequireId(gateId, nameof(gateId)));
            SetString(objective, "nextObjectiveId", string.Empty);
            EditorUtility.SetDirty(objective);
            AssetDatabase.SaveAssetIfDirty(objective);

            var scenario = LoadOrCreate<NarrativeScenarioDefinition>(scenarioAssetPath);
            SetString(scenario, "scenarioId", RequireId(scenarioId, nameof(scenarioId)));
            SetString(scenario, "initialObjectiveId", objective.ObjectiveId);
            SetObjectArray(scenario, "objectives", objective);
            SetObjectArray(scenario, "phoneMessages", Array.Empty<UnityEngine.Object>());
            scenario.ValidateObjectiveGraphOrThrow();
            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssetIfDirty(scenario);
            return scenario;
        }

        public static ScareDefinition CreateScareDefinition(string path, string scareId)
        {
            RequireGameAssetPath(path);
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets/Game");
            var definition = LoadOrCreate<ScareDefinition>(path);
            SetString(definition, "scareId", RequireId(scareId, nameof(scareId)));
            SetString(definition, "fallbackSpeaker", "Unknown");
            SetString(definition, "fallbackCueText", "Something moved nearby.");
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            return definition;
        }

        public static void SetObjectArray(UnityEngine.Object target, string propertyName, params UnityEngine.Object[] values)
        {
            SetObjectArray(target, propertyName, false, null, values);
        }

        public static void SetObjectArrayWithUndo(
            UnityEngine.Object target,
            string propertyName,
            string undoName,
            params UnityEngine.Object[] values)
        {
            SetObjectArray(target, propertyName, true, undoName, values);
        }

        private static void SetObjectArray(
            UnityEngine.Object target,
            string propertyName,
            bool registerUndo,
            string undoName,
            UnityEngine.Object[] values)
        {
            RecordObject(target, undoName, registerUndo);
            var serialized = new SerializedObject(target);
            var property = RequireProperty(serialized, propertyName, target);
            property.arraySize = values?.Length ?? 0;
            for (var i = 0; i < property.arraySize; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        public static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            RequireProperty(serialized, propertyName, target).stringValue = value ?? string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(UnityEngine.Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            RequireProperty(serialized, propertyName, target).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T AddComponent<T>(GameObject target, bool registerUndo) where T : Component
        {
            var existing = target.GetComponent<T>();
            if (existing != null)
            {
                return existing;
            }

            return registerUndo ? Undo.AddComponent<T>(target) : target.AddComponent<T>();
        }

        private static void RegisterCreatedObject(GameObject target, string label, bool registerUndo)
        {
            if (registerUndo)
            {
                Undo.RegisterCreatedObjectUndo(target, label);
            }
        }

        private static void RecordObject(UnityEngine.Object target, string label, bool registerUndo)
        {
            if (registerUndo && target != null)
            {
                Undo.RecordObject(target, string.IsNullOrWhiteSpace(label) ? "Configure Horror Authoring Object" : label);
            }
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static SerializedProperty RequireProperty(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object target)
        {
            return serialized.FindProperty(propertyName) ?? throw new InvalidOperationException(
                $"Serialized property '{propertyName}' was not found on {target.GetType().Name}.");
        }

        private static string RequireId(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Authoring ID must not be empty.", parameterName);
            }

            return value.Trim();
        }

        private static void RequireGameAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/Game/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Game-specific authored assets must be stored under Assets/Game.", nameof(path));
            }
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
